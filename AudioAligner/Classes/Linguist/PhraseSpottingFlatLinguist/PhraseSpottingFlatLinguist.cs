using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AudioAligner.Classes.Util;
using com.sun.org.apache.regexp.@internal;
using edu.cmu.sphinx.linguist;
using edu.cmu.sphinx.linguist.acoustic;
using edu.cmu.sphinx.linguist.dictionary;
using edu.cmu.sphinx.linguist.flat;
using edu.cmu.sphinx.linguist.language.grammar;
using edu.cmu.sphinx.util;
using edu.cmu.sphinx.util.props;
using Grammar = edu.cmu.sphinx.linguist.language.grammar.Grammar;
using Word = edu.cmu.sphinx.linguist.dictionary.Word;

namespace AudioAligner.Classes.Linguist.PhraseSpottingFlatLinguist
{
    /**
     * A simple form of the linguist.
     * <p/>
     * The flat linguist takes a Grammar graph (as returned by the underlying,
     * configurable grammar), and generates a search graph for this grammar.
     * <p/>
     * It makes the following simplifying assumptions:
     * <p/>
     * <ul>
     * <li>Zero or one word per grammar node
     * <li>No fan-in allowed ever
     * <li>No composites (yet)
     * <li>Only Unit, HMMState, and pronunciation states (and the initial/final
     * grammar state are in the graph (no word, alternative or grammar states
     * attached).
     * <li>Only valid transitions (matching contexts) are allowed
     * <li>No tree organization of units
     * <li>Branching grammar states are allowed
     * </ul>
     * <p/>
     * <p/>
     * Note that all probabilities are maintained in the log math domain
     */
    class PhraseSpottingFlatLinguist : edu.cmu.sphinx.linguist.Linguist, Configurable
    {
        /**
	     * The property used to define the grammar to use when building the search
	     * graph
	     */
	    //@S4Component(type = Grammar.class)
	    public const string PROP_GRAMMAR = "grammar";

	    /**
	     * The property used to define the unit manager to use when building the
	     * search graph
	     */
	    //@S4Component(type = UnitManager.class)
	    public const string PROP_UNIT_MANAGER = "unitManager";

	    /**
	     * The property used to define the acoustic model to use when building the
	     * search graph
	     */
	    //@S4Component(type = AcousticModel.class)
	    public const string PROP_ACOUSTIC_MODEL = "acousticModel";

	    /**
	     * The property that defines the name of the logmath to be used by this
	     * search manager.
	     */
	    //@S4Component(type = LogMath.class)
	    public const string PROP_LOG_MATH = "logMath";

	    /**
	     * The property used to determine whether or not the gstates are dumped.
	     */
	    //@S4Boolean(defaultValue = false)
	    public const string PROP_DUMP_GSTATES = "dumpGstates";

	    /**
	     * The property that specifies whether to add a branch for detecting
	     * out-of-grammar utterances.
	     */
	    //@S4Boolean(defaultValue = false)
	    public const string PROP_ADD_OUT_OF_GRAMMAR_BRANCH = "addOutOfGrammarBranch";

	    /**
	     * The property for the probability of entering the out-of-grammar branch.
	     */
	    //@S4Double(defaultValue = 1.0)
	    public const string PROP_OUT_OF_GRAMMAR_PROBABILITY = "outOfGrammarProbability";

	    /**
	     * The property for the acoustic model used for the CI phone loop.
	     */
	    //@S4Component(type = AcousticModel.class)
	    public const string PROP_PHONE_LOOP_ACOUSTIC_MODEL = "phoneLoopAcousticModel";

	    /**
	     * The property for the probability of inserting a CI phone in the
	     * out-of-grammar ci phone loop
	     */
	    //@S4Double(defaultValue = 1.0)
	    public const string PROP_PHONE_INSERTION_PROBABILITY = "phoneInsertionProbability";

	    /**
	     * Property to control whether compilation progress is displayed on standard
	     * output. If this property is true, a 'dot' is displayed for every 1000
	     * search states added to the search space
	     */
	    //@S4Boolean(defaultValue = false)
	    public const string PROP_SHOW_COMPILATION_PROGRESS = "showCompilationProgress";

	    /**
	     * Property that controls whether word probabilities are spread across all
	     * pronunciations.
	     */
	    //@S4Boolean(defaultValue = false)
	    public const string PROP_SPREAD_WORD_PROBABILITIES_ACROSS_PRONUNCIATIONS = "spreadWordProbabilitiesAcrossPronunciations";

	    protected readonly float logOne = LogMath.getLogOne();

	    // note: some fields are protected to allow to override
	    // FlatLinguist.compileGrammar()

	    // ----------------------------------
	    // Subcomponents that are configured
	    // by the property sheet
	    // -----------------------------------
	    protected edu.cmu.sphinx.linguist.language.grammar.Grammar grammar;
	    private AcousticModel acousticModel;
	    private UnitManager unitManager;
	    protected LogMath logMath;

	    // ------------------------------------
	    // Fields that define the OOV-behavior
	    // ------------------------------------
	    protected AcousticModel phoneLoopAcousticModel;
	    protected bool addOutOfGrammarBranch;
	    protected float logOutOfGrammarBranchProbability;
	    protected float logPhoneInsertionProbability;

	    // ------------------------------------
	    // Data that is configured by the
	    // property sheet
	    // ------------------------------------
	    private float logWordInsertionProbability;
	    private float logSilenceInsertionProbability;
	    private float logFillerInsertionProbability;
	    private float logUnitInsertionProbability;
	    private bool showCompilationProgress = true;
	    private bool spreadWordProbabilitiesAcrossPronunciations;
	    private bool dumpGStates;
	    private float languageWeight;

	    // -----------------------------------
	    // Data for monitoring performance
	    // ------------------------------------
	    protected StatisticsVariable totalStates;
	    protected StatisticsVariable totalArcs;
	    protected StatisticsVariable actualArcs;
	    private /*transient*/ int totalStateCounter;
	    private const bool tracing = false;

	    // ------------------------------------
	    // Data used for building and maintaining
	    // the search graph
	    // -------------------------------------
	    private /*transient*/ List<SentenceHMMState> stateSet;
	    private string name;
	    protected Dictionary<GrammarNode, GState> nodeStateMap;
	    protected Cache<SentenceHMMStateArc> arcPool;
	    protected GrammarNode initialGrammarState;

	    protected SearchGraph searchGraph;

	    /**
	     * Returns the search graph
	     * 
	     * @return the search graph
	     */
	    //@Override
	    public SearchGraph getSearchGraph() {
		    return searchGraph;
	    }

	    public PhraseSpottingFlatLinguist(AcousticModel acousticModel, LogMath logMath,
			    edu.cmu.sphinx.linguist.language.grammar.Grammar grammar, UnitManager unitManager,
			    double wordInsertionProbability,
			    double silenceInsertionProbability,
			    double fillerInsertionProbability, double unitInsertionProbability,
			    float languageWeight, bool dumpGStates,
			    bool showCompilationProgress,
			    bool spreadWordProbabilitiesAcrossPronunciations,
			    bool addOutOfGrammarBranch,
			    double outOfGrammarBranchProbability,
			    double phoneInsertionProbability,
			    AcousticModel phoneLoopAcousticModel) {

		    this.acousticModel = acousticModel;
		    this.logMath = logMath;
		    this.grammar = grammar;
		    this.unitManager = unitManager;

		    this.logWordInsertionProbability = logMath
				    .linearToLog(wordInsertionProbability);
		    this.logSilenceInsertionProbability = logMath
				    .linearToLog(silenceInsertionProbability);
		    this.logFillerInsertionProbability = logMath
				    .linearToLog(fillerInsertionProbability);
		    this.logUnitInsertionProbability = logMath
				    .linearToLog(unitInsertionProbability);
		    this.languageWeight = languageWeight;

		    this.dumpGStates = dumpGStates;
		    this.showCompilationProgress = showCompilationProgress;
		    this.spreadWordProbabilitiesAcrossPronunciations = spreadWordProbabilitiesAcrossPronunciations;

		    this.addOutOfGrammarBranch = addOutOfGrammarBranch;

		    if (addOutOfGrammarBranch) {
			    this.logOutOfGrammarBranchProbability = logMath
					    .linearToLog(outOfGrammarBranchProbability);
			    this.logPhoneInsertionProbability = logMath
					    .linearToLog(phoneInsertionProbability);
			    this.phoneLoopAcousticModel = phoneLoopAcousticModel;
		    }

		    this.name = null;
	    }

	    public PhraseSpottingFlatLinguist() {

	    }

	    /*
	     * (non-Javadoc)
	     * 
	     * @see
	     * edu.cmu.sphinx.util.props.Configurable#newProperties(edu.cmu.sphinx.util
	     * .props.PropertySheet)
	     */
	    //@Override
	    public void newProperties(PropertySheet ps) {
		    // hookup to all of the components
		    setupAcousticModel(ps);
		    logMath = (LogMath) ps.getComponent(PROP_LOG_MATH);
		    grammar = (edu.cmu.sphinx.linguist.language.grammar.Grammar) ps.getComponent(PROP_GRAMMAR);
		    unitManager = (UnitManager) ps.getComponent(PROP_UNIT_MANAGER);

		    // get the rest of the configuration data
		    logWordInsertionProbability = logMath.linearToLog(ps
                    .getDouble(edu.cmu.sphinx.linguist.Linguist.PROP_WORD_INSERTION_PROBABILITY));
		    logSilenceInsertionProbability = logMath.linearToLog(ps
                    .getDouble(edu.cmu.sphinx.linguist.Linguist.PROP_SILENCE_INSERTION_PROBABILITY));
		    logFillerInsertionProbability = logMath.linearToLog(ps
                    .getDouble(edu.cmu.sphinx.linguist.Linguist.PROP_FILLER_INSERTION_PROBABILITY));
		    logUnitInsertionProbability = logMath.linearToLog(ps
                    .getDouble(edu.cmu.sphinx.linguist.Linguist.PROP_UNIT_INSERTION_PROBABILITY));
            languageWeight = ps.getFloat(edu.cmu.sphinx.linguist.Linguist.PROP_LANGUAGE_WEIGHT);
		    dumpGStates = JavaToCs.ConvertBool(ps.getBoolean(PROP_DUMP_GSTATES));
		    showCompilationProgress = JavaToCs.ConvertBool(ps.getBoolean(PROP_SHOW_COMPILATION_PROGRESS));
		    spreadWordProbabilitiesAcrossPronunciations = JavaToCs.ConvertBool(ps.getBoolean(PROP_SPREAD_WORD_PROBABILITIES_ACROSS_PRONUNCIATIONS));

		    addOutOfGrammarBranch = JavaToCs.ConvertBool(ps.getBoolean(PROP_ADD_OUT_OF_GRAMMAR_BRANCH));

		    if (addOutOfGrammarBranch) {
			    logOutOfGrammarBranchProbability = logMath.linearToLog(ps
					    .getDouble(PROP_OUT_OF_GRAMMAR_PROBABILITY));
			    logPhoneInsertionProbability = logMath.linearToLog(ps
					    .getDouble(PROP_PHONE_INSERTION_PROBABILITY));
			    phoneLoopAcousticModel = (AcousticModel) ps
					    .getComponent(PROP_PHONE_LOOP_ACOUSTIC_MODEL);
		    }

		    name = ps.getInstanceName();
	    }

	    /**
	     * Sets up the acoustic model.
	     * 
	     * @param ps
	     *            the PropertySheet from which to obtain the acoustic model
	     * @throws edu.cmu.sphinx.util.props.PropertyException
	     */
	    protected void setupAcousticModel(PropertySheet ps) {
		    acousticModel = (AcousticModel) ps.getComponent(PROP_ACOUSTIC_MODEL);
	    }

	    /*
	     * (non-Javadoc)
	     * 
	     * @see edu.cmu.sphinx.util.props.Configurable#getName()
	     */
	    public string getName() {
		    return name;
	    }

	    /*
	     * (non-Javadoc)
	     * 
	     * @see edu.cmu.sphinx.linguist.Linguist#allocate()
	     */
	    //@Override
	    public void allocate(){
		    allocateAcousticModel();
		    grammar.allocate();
		    totalStates = StatisticsVariable.getStatisticsVariable(getName(),
				    "totalStates");
		    totalArcs = StatisticsVariable.getStatisticsVariable(getName(),
				    "totalArcs");
		    actualArcs = StatisticsVariable.getStatisticsVariable(getName(),
				    "actualArcs");
		    stateSet = compileGrammar();
		    totalStates.value = stateSet.Count();
	    }

	    /**
	     * Allocates the acoustic model.
	     * 
	     * @throws java.io.IOException
	     */
	    protected void allocateAcousticModel(){
		    acousticModel.allocate();
		    if (addOutOfGrammarBranch) {
			    phoneLoopAcousticModel.allocate();
		    }
	    }

	    /*
	     * (non-Javadoc)
	     * 
	     * @see edu.cmu.sphinx.linguist.Linguist#deallocate()
	     */
	    //@Override
	    public void deallocate() {
		    if (acousticModel != null) {
			    acousticModel.deallocate();
		    }
		    grammar.deallocate();
	    }

	    /**
	     * Called before a recognition
	     */
	    //@Override
	    public void startRecognition() {
		    if (grammarHasChanged()) {
			    try {
				    stateSet = compileGrammar();
			    } catch (Exception e) {
				    // TODO Auto-generated catch block
				    Console.WriteLine(e);
			    }
			    totalStates.value = stateSet.Count();
		    }
	    }

	    /**
	     * Called after a recognition
	     */
	    //@Override
	    public void stopRecognition() {
	    }

	    /**
	     * Returns the LogMath used.
	     * 
	     * @return the logMath used
	     */
	    public LogMath getLogMath() {
		    return logMath;
	    }

	    /**
	     * Returns the log silence insertion probability.
	     * 
	     * @return the log silence insertion probability.
	     */
	    public float getLogSilenceInsertionProbability() {
		    return logSilenceInsertionProbability;
	    }

	    /**
	     * Compiles the grammar into a sentence HMM. A GrammarJob is created for the
	     * initial grammar node and added to the GrammarJob queue. While there are
	     * jobs left on the grammar job queue, a job is removed from the queue and
	     * the associated grammar node is expanded and attached to the tails.
	     * GrammarJobs for the successors are added to the grammar job queue.
	     */
	    /**
	     * Compiles the grammar into a sentence HMM. A GrammarJob is created for the
	     * initial grammar node and added to the GrammarJob queue. While there are
	     * jobs left on the grammar job queue, a job is removed from the queue and
	     * the associated grammar node is expanded and attached to the tails.
	     * GrammarJobs for the successors are added to the grammar job queue.
	     * 
	     * @throws IOException
	     */
	    protected List<SentenceHMMState> compileGrammar(){
		    initialGrammarState = grammar.getInitialNode();

		    nodeStateMap = new Dictionary<GrammarNode, GState>();
		    arcPool = new Cache<SentenceHMMStateArc>();

		    List<GState> gstateList = new List<GState>();
		    TimerPool.getTimer(this, "Compile").start();

		    // for each non-empty grammar node in the grammar
		    // break it into two nodes, one empty node representing the end
		    // of word state
	        var nodes = JavaToCs.SetToCollection(grammar.getGrammarNodes());
		    foreach (GrammarNode grammarNode in /*grammar.getGrammarNodes()*/nodes) {
			    if (!grammarNode.isEmpty()) {
				    GrammarNode branchNode = new GrammarNode(0, new Word[0][]);
				    GrammarArc[] successors = grammarNode.getSuccessors();
				    foreach (GrammarArc arc in successors) {
					    branchNode.add(arc.getGrammarNode(), arc.getProbability());
				    }

			    }
		    }

		    // get the nodes from the grammar and create states
		    // for them. Add the non-empty gstates to the gstate list.
            TimerPool.getTimer(this, "Create States").start();
            var nodes2 = JavaToCs.SetToCollection(grammar.getGrammarNodes());
		    foreach (GrammarNode grammarNode in /*grammar.getGrammarNodes()*/nodes2) {
			    GState gstate = createGState(grammarNode);
			    gstateList.Add(gstate);
		    }
		    TimerPool.getTimer(this, "Create States").stop();
		    addStartingPath();

		    // ensures an initial path to the start state
		    // Prep all the gstates, by gathering all of the contexts up
		    // this allows each gstate to know about its surrounding
		    // contexts
		    TimerPool.getTimer(this, "Collect Contexts").start();
		    foreach (GState gstate in gstateList)
			    gstate.collectContexts();
		    TimerPool.getTimer(this, "Collect Contexts").stop();
		    // now all gstates know all about their contexts, we can
		    // expand them fully
		    TimerPool.getTimer(this, "Expand States").start();
		    foreach (GState gstate in gstateList) {
			    gstate.expand();
		    }
		    TimerPool.getTimer(this, "Expand States").stop();

		    // now that all states are expanded fully, we can connect all
		    // the states up
		    TimerPool.getTimer(this, "Connect Nodes").start();
	        foreach (GState gstate in gstateList)
	        {
	            gstate.connect();
	        }
		    TimerPool.getTimer(this, "Connect Nodes").stop();

		    SentenceHMMState initialState = findStartingState()[0];
		    int count = 0;
		    // add an out-of-grammar branch between each word transition if
		    // configured to do so
		    if (addOutOfGrammarBranch) {
			    GState gstate = nodeStateMap[initialGrammarState];
			    foreach (SentenceHMMState state in gstate.getEntryPoints()) {
				    PhoneLoopCI phoneLoop = new PhoneLoopCI(phoneLoopAcousticModel,
						    logPhoneInsertionProbability, state);
				    SentenceHMMState firstBranchState = (SentenceHMMState) phoneLoop
						    .getSearchGraph().getInitialState();
				    state.connect(getArc(firstBranchState, logOne,
						    logOutOfGrammarBranchProbability));
			    }
		    }
		    nodeStateMap = null;
		    arcPool = null;
		    searchGraph = new FlatSearchGraph(initialState);
		    TimerPool.getTimer(this, "Compile").stop();

		    // Now that we are all done, dump out some interesting
		    // information about the process
		    if (dumpGStates)
		    {
		        var nodes3 = JavaToCs.SetToCollection(grammar.getGrammarNodes());
			    foreach (GrammarNode grammarNode in /*grammar.getGrammarNodes()*/nodes3) {
				    GState gstate = getGState(grammarNode);
				    gstate.dumpInfo();
			    }
		    }
		    return SentenceHMMState.collectStates(initialState);
	    }

	    /**
	     * Returns a new GState for the given GrammarNode.
	     * 
	     * @return a new GState for the given GrammarNode
	     */
	    protected GState createGState(GrammarNode grammarNode) {
		    return new GState(grammarNode);
	    }

	    /**
	     * Ensures that there is a starting path by adding an empty left context to
	     * the starting gstate
	     */
	    // TODO: Currently the FlatLinguist requires that the initial
	    // grammar node returned by the Grammar contains a "sil" word
	    protected void addStartingPath() {
		    addStartingPath(grammar.getInitialNode());
	    }

	    /**
	     * Start the search at the indicated node
	     * 
	     */
	    protected void addStartingPath(GrammarNode initialNode) {
		    // guarantees a starting path into the initial node by
		    // adding an empty left context to the starting gstate
		    GrammarNode node = initialNode;
		    PhraseSpottingFlatLinguist.GState gstate = getGState(node);
		    gstate.addLeftContext(UnitContext.SILENCE);
	    }

	    /**
	     * Determines if the underlying grammar has changed since we last compiled
	     * the search graph
	     * 
	     * @return true if the grammar has changed
	     */
	    protected bool grammarHasChanged() {
		    return initialGrammarState == null
				    || initialGrammarState != grammar.getInitialNode();
	    }

	    /**
	     * Finds the starting state
	     * 
	     * @return the starting state
	     */
	    protected List<SentenceHMMState> findStartingState() {
		    GrammarNode node = grammar.getInitialNode();
		    GState gstate = getGState(node);
		    return gstate.getEntryPoints();
	    }

	    /**
	     * Gets a SentenceHMMStateArc. The arc is drawn from a pool of arcs.
	     * 
	     * @param nextState
	     *            the next state
	     * @param logLanguageProbability
	     *            the log language probability
	     * @param logInsertionProbability
	     *            the log insertion probability
	     */
	    protected SentenceHMMStateArc getArc(SentenceHMMState nextState,
			    float logLanguageProbability, float logInsertionProbability) {
		    SentenceHMMStateArc arc = new SentenceHMMStateArc(nextState,
				    logLanguageProbability * languageWeight,
				    logInsertionProbability);
		    SentenceHMMStateArc pooledArc = arcPool.cache(arc);
		    actualArcs.value = arcPool.getMisses();
		    totalArcs.value = arcPool.getHits() + arcPool.getMisses();
		    return pooledArc == null ? arc : pooledArc;
	    }

	    /**
	     * Given a grammar node, retrieve the grammar state
	     * 
	     * @param node
	     *            the grammar node
	     * @return the grammar state associated with the node
	     */
	    public GState getGState(GrammarNode node) {
		    return nodeStateMap[node];
	    }

	    /**
	     * The search graph that is produced by the flat linguist.
	     */
	    protected class FlatSearchGraph : SearchGraph {

		    /**
		     * An array of classes that represents the order in which the states
		     * will be returned.
		     */
		    private readonly SearchState initialState;

		    /**
		     * Constructs a flast search graph with the given initial state
		     * 
		     * @param initialState
		     *            the initial state
		     */
		    public FlatSearchGraph(SearchState initialState) {
			    this.initialState = initialState;
		    }

		    /*
		     * (non-Javadoc)
		     * 
		     * @see edu.cmu.sphinx.linguist.SearchGraph#getInitialState()
		     */
		    //@Override
		    public SearchState getInitialState() {
			    return initialState;
		    }

		    /*
		     * (non-Javadoc)
		     * 
		     * @see edu.cmu.sphinx.linguist.SearchGraph#getNumStateOrder()
		     */
		    //@Override
		    public int getNumStateOrder() {
			    return 7;
		    }
	    }

	    /**
	     * This is a nested class that is used to manage the construction of the
	     * states in a grammar node. There is one GState created for each grammar
	     * node. The GState is used to collect the entry and exit points for the
	     * grammar node and for connecting up the grammar nodes to each other.
	     */
	    public class GState {

		    private readonly Dictionary<ContextPair, List<SearchState>> entryPoints = new Dictionary<ContextPair, List<SearchState>>();
		    private readonly Dictionary<ContextPair, List<SearchState>> exitPoints = new Dictionary<ContextPair, List<SearchState>>();
		    private readonly Dictionary<string, SentenceHMMState> existingStates = new Dictionary<string, SentenceHMMState>();

		    private readonly GrammarNode node;

		    private readonly HashSet<UnitContext> rightContexts = new HashSet<UnitContext>();
		    private readonly HashSet<UnitContext> leftContexts = new HashSet<UnitContext>();
		    private HashSet<UnitContext> startingContexts;

		    private int exitConnections;

		    /**
		     * Creates a GState for a grammar node
		     * 
		     * @param node
		     *            the grammar node
		     */
		    public GState(GrammarNode node) {
			    this.node = node;
			    nodeStateMap.Add(node, this);
		    }

		    /**
		     * Retrieves the set of starting contexts for this node. The starting
		     * contexts are the set of Unit[] with a size equal to the maximum right
		     * context size.
		     * 
		     * @return the set of starting contexts across nodes.
		     */
		    private HashSet<UnitContext> getStartingContexts() {
			    if (startingContexts == null) {
				    startingContexts = new HashSet<UnitContext>();

				    // if this is an empty node, the starting context is
				    // the set of starting contexts for all successor
				    // nodes, otherwise, it is built up from each
				    // pronunciation of this word
				    if (node.isEmpty()) {
					    GrammarArc[] arcs = getSuccessors();
					    foreach (GrammarArc arc in arcs) {
						    GState gstate = getGState(arc.getGrammarNode());
						    startingContexts.UnionWith(gstate.getStartingContexts());
					    }
				    } else {

					    Word word = node.getWord();
					    Pronunciation[] prons = word.getPronunciations(null);
					    foreach (Pronunciation pron in prons) {
						    UnitContext startingContext = getStartingContext(pron);
						    startingContexts.Add(startingContext);
					    }
				    }
			    }
			    return startingContexts;
		    }

		    /**
		     * Retrieves the starting UnitContext for the given pronunciation
		     * 
		     * @param pronunciation
		     *            the pronunciation
		     * @return a UnitContext representing the starting context of the
		     *         pronunciation
		     */
		    private UnitContext getStartingContext(Pronunciation pronunciation) {
			    int maxSize = getRightContextSize();
			    Unit[] units = pronunciation.getUnits();
			    Unit[] context = units.Length > maxSize ? /*Arrays.copyOf(units, maxSize)*/units.Take(maxSize).ToArray() : units;
			    return UnitContext.get(context);
		    }

		    /**
		     * Retrieves the set of trailing contexts for this node. the trailing
		     * contexts are the set of Unit[] with a size equal to the maximum left
		     * context size that align with the end of the node
		     */
		    List<UnitContext> getEndingContexts() {
			    List<UnitContext> endingContexts = new List<UnitContext>();
			    if (!node.isEmpty()) {
				    int maxSize = getLeftContextSize();
				    Word word = node.getWord();
				    Pronunciation[] prons = word.getPronunciations(null);
				    foreach (Pronunciation pron in prons) {
					    Unit[] units = pron.getUnits();
					    int size = units.Length;
					    Unit[] context = size > maxSize ? units.Skip(size - maxSize).Take(maxSize).ToArray()/* Arrays.copyOfRange(units,
							    size - maxSize, size)*/ : units;
					    endingContexts.Add(UnitContext.get(context));
				    }
			    }
			    return endingContexts;
		    }

		    /**
		     * Visit all of the successor states, and gather their starting contexts
		     * into this gstates right context
		     */
		    private void pullRightContexts() {
			    GrammarArc[] arcs = getSuccessors();
			    foreach (GrammarArc arc in arcs) {
				    GState gstate = getGState(arc.getGrammarNode());
				    rightContexts.UnionWith(gstate.getStartingContexts());
			    }
		    }

		    /**
		     * Returns the set of succesor arcs for this grammar node. If a
		     * successor grammar node has no words we'll substitute the successors
		     * for that node (avoiding loops of course)
		     * 
		     * @return an array of successors for this GState
		     */
		    private GrammarArc[] getSuccessors() {
			    return node.getSuccessors();
		    }

		    /**
		     * Visit all of the successor states, and push our ending context into
		     * the successors left context
		     */
		    void pushLeftContexts() {
			    List<UnitContext> endingContext = getEndingContexts();
			    HashSet<GrammarNode> visitedSet = new HashSet<GrammarNode>();
			    pushLeftContexts(visitedSet, endingContext);
		    }

		    /**
		     * Pushes the given left context into the successor states. If a
		     * successor state is empty, continue to push into this empty states
		     * successors
		     * 
		     * 
		     * @param leftContext
		     *            the context to push
		     */
		    void pushLeftContexts(HashSet<GrammarNode> visitedSet,
				    List<UnitContext> leftContext) {
			    if (visitedSet.Contains(getNode())) {
				    return;
			    } else {
				    visitedSet.Add(getNode());
			    }
			    foreach (GrammarArc arc in getSuccessors()) {
				    GState gstate = getGState(arc.getGrammarNode());
				    gstate.addLeftContext(leftContext);

				    // if our successor state is empty, also push our
				    // ending context into the empty nodes successors
				    if (gstate.getNode().isEmpty()) {
					    gstate.pushLeftContexts(visitedSet, leftContext);
				    }
			    }
		    }

		    /**
		     * Add the given left contexts to the set of left contexts for this
		     * state
		     * 
		     * @param context
		     *            the set of contexts to add
		     */
		    public void addLeftContext(List<UnitContext> context) {
			    leftContexts.UnionWith(context);
		    }

		    /**
		     * Adds the given context to the set of left contexts for this state
		     * 
		     * @param context
		     *            the context to add
		     */
		    public void addLeftContext(UnitContext context) {
			    leftContexts.Add(context);
		    }

		    /**
		     * Returns the entry points for a given context pair
		     */
		    private List<SearchState> getEntryPoints(ContextPair contextPair) {
			    return entryPoints[contextPair];
		    }

		    /**
		     * Gets the context-free entry point to this state
		     * 
		     * @return the entry point to the state
		     */
		    // TODO: ideally we'll look for entry points with no left
		    // context, but those don't exist yet so we just take
		    // the first entry point with an SILENCE left context
		    // note that this assumes that the first node in a grammar has a
		    // word and that word is a SIL. Not always a valid assumption.
		    public List<SentenceHMMState> getEntryPoints() {
			    List<SentenceHMMState> entryPointsList = new List<SentenceHMMState>();
			    foreach (List<SearchState> list in entryPoints.Values) {
				    /*ListIterator<SearchState> iter = list.listIterator();
				    while (iter.hasNext()) {
					    entryPointsList.Add((SentenceHMMState) iter.next());
				    }*/
			        foreach (var searchState in list)
			        {
			            entryPointsList.Add((SentenceHMMState)searchState);
			        }
			    }
			    return entryPointsList;
		    }

		    /**
		     * Collects the right contexts for this node and pushes this nodes
		     * ending context into the next next set of nodes.
		     */
		    public void collectContexts() {
			    pullRightContexts();
			    pushLeftContexts();
		    }

		    /**
		     * Expands each GState into the sentence HMM States
		     */
		    public void expand() {
			    // for each left context/starting context pair create a list
			    // of starting states.
			    foreach (UnitContext leftContext in leftContexts) {
				    foreach (UnitContext startingContext in getStartingContexts()) {
					    ContextPair contextPair = ContextPair.get(leftContext,
							    startingContext);
					    entryPoints.Add(contextPair, new List<SearchState>());
				    }
			    }

			    // if this is a final node don't expand it, just create a
			    // state and add it to all entry points
			    if (node.isFinalNode()) {
				    GrammarState gs = new GrammarState(node);
				    foreach (List<SearchState> epList in entryPoints.Values) {
					    epList.Add(gs);
				    }
			    } else if (!node.isEmpty()) {
				    // its a full fledged node with a word
				    // so expand it. Nodes without words don't need
				    // to be expanded.
				    foreach (var entry in entryPoints) {

					    ContextPair cp = entry.Key;
					    List<SearchState> epList = entry.Value;
					    SentenceHMMState bs = new BranchState(cp.getLeftContext()
							    .ToString(), cp.getRightContext().ToString(), node
							    .getID());
					    epList.Add(bs);
					    addExitPoint(cp, bs);
				    }
				    foreach (UnitContext leftContext in leftContexts) {
					    expandWord(leftContext);
				    }
			    } else {
				    // if the node is empty, populate the set of entry and exit
				    // points with a branch state. The branch state
				    // branches to the successor entry points for this
				    // state
				    // the exit point should consist of the set of
				    // incoming left contexts and outgoing right contexts
				    // the 'entryPoint' table already consists of such
				    // pairs so we can use that
				    foreach (var entry in entryPoints) {
					    ContextPair cp = entry.Key;
					    List<SearchState> epList = entry.Value;
					    SentenceHMMState bs = new BranchState(cp.getLeftContext()
							    .ToString(), cp.getRightContext().ToString(), node
							    .getID());
					    epList.Add(bs);
					    addExitPoint(cp, bs);
				    }
			    }
			    addEmptyEntryPoints();
		    }

		    /**
		     * Adds the set of empty entry points. The list of entry points are
		     * tagged with a context pair. The context pair represent the left
		     * context for the state and the starting context for the state, this
		     * allows states to be hooked up properly. However, we may be
		     * transitioning from states that have no right hand context (CI units
		     * such as SIL fall into this category). In this case we'd normally have
		     * no place to transition to since we add entry points for each starting
		     * context. To make sure that there are entry points for empty contexts
		     * if necessary, we go through the list of entry points and find all
		     * left contexts that have a right hand context size of zero. These
		     * entry points will need an entry point with an empty starting context.
		     * These entries are synthesized and added to the the list of entry
		     * points.
		     */
		    private void addEmptyEntryPoints() {
			    Dictionary<ContextPair, List<SearchState>> emptyEntryPoints = new Dictionary<ContextPair, List<SearchState>>();
			    foreach (var entry in entryPoints) {
				    ContextPair cp = entry.Key;
				    if (needsEmptyVersion(cp)) {
					    ContextPair emptyContextPair = ContextPair.get(cp
							    .getLeftContext(), UnitContext.EMPTY);
					    List<SearchState> epList = emptyEntryPoints[emptyContextPair];
					    if (epList == null) {
						    epList = new List<SearchState>();
						    emptyEntryPoints.Add(emptyContextPair, epList);
					    }
					    epList.AddRange(entry.Value);
				    }
			    }
			    foreach (var emptyEntryPoint in emptyEntryPoints)
	            {
		            entryPoints.Add(emptyEntryPoint.Key, emptyEntryPoint.Value); 
	            }
		    }

		    /**
		     * Determines if the context pair needs an empty version. A context pair
		     * needs an empty version if the left context has a max size of zero.
		     * 
		     * @param cp
		     *            the contex pair to check
		     * @return <code>true</code> if the pair needs an empt version
		     */
		    private bool needsEmptyVersion(ContextPair cp) {
			    UnitContext left = cp.getLeftContext();
			    Unit[] units = left.getUnits();
			    return units.Length > 0
					    && (getRightContextSize(units[0]) < getRightContextSize());

		    }

		    /**
		     * Returns the grammar node of the gstate
		     * 
		     * @return the grammar node
		     */
		    private GrammarNode getNode() {
			    return node;
		    }

		    /**
		     * Expand the the word given the left context
		     * 
		     * @param leftContext
		     *            the left context
		     */
		    private void expandWord(UnitContext leftContext) {
			    Word word = node.getWord();
			    T("  Expanding word " + word + " for lc " + leftContext);
			    Pronunciation[] pronunciations = word.getPronunciations(null);
			    for (int i = 0; i < pronunciations.Length; i++) {
				    expandPronunciation(leftContext, pronunciations[i], i);
			    }
		    }

		    /**
		     * Expand the pronunciation given the left context
		     * 
		     * @param leftContext
		     *            the left context
		     * @param pronunciation
		     *            the pronunciation to expand
		     * @param which
		     *            unique ID for this pronunciation
		     */
		    // Each GState maintains a list of entry points. This list of
		    // entry points is used when connecting up the end states of
		    // one GState to the beginning states in another GState. The
		    // entry points are tagged by a ContextPair which represents
		    // the left context upon entering the state (the left context
		    // of the initial units of the state), and the right context
		    // of the previous states (corresponding to the starting
		    // contexts for this state).
		    //
		    // When expanding a pronunciation, the following steps are
		    // taken:
		    // 1) Get the starting context for the pronunciation.
		    // This is the set of units that correspond to the start
		    // of the pronunciation.
		    //
		    // 2) Create a new PronunciationState for the
		    // pronunciation.
		    //
		    // 3) Add the PronunciationState to the entry point table
		    // (a hash table keyed by the ContextPair(LeftContext,
		    // StartingContext).
		    //
		    // 4) Generate the set of context dependent units, using
		    // the left and right context of the GState as necessary.
		    // Note that there will be fan out at the end of the
		    // pronunciation to allow for units with all of the
		    // various right contexts. The point where the fan-out
		    // occurs is the (length of the pronunciation - the max
		    // right context size).
		    //
		    // 5) Attach each cd unit to the tree
		    //
		    // 6) Expand each cd unit into the set of HMM states
		    //
		    // 7) Attach the optional and looping back silence cd
		    // unit
		    //
		    // 8) Collect the leaf states of the tree and add them to
		    // the exitStates list.
		    private void expandPronunciation(UnitContext leftContext,
				    Pronunciation pronunciation, int which) {
			    UnitContext startingContext = getStartingContext(pronunciation);
			    // Add the pronunciation state to the entry point list
			    // (based upon its left and right context)
			    String pname = "P(" + pronunciation.getWord() + '[' + leftContext
					    + ',' + startingContext + "])-G" + getNode().getID();
			    PronunciationState ps = new PronunciationState(pname,
					    pronunciation, which);
			    T("     Expanding " + ps.getPronunciation() + " for lc "
					    + leftContext);
			    ContextPair cp = ContextPair.get(leftContext, startingContext);
			    List<SearchState> epList = entryPoints[cp];
			    if (epList == null) {
				    throw new Exception("No EP list for context pair " + cp);
			    } else {
				    epList.Add(ps);
			    }
			    Unit[] units = pronunciation.getUnits();
			    int fanOutPoint = units.Length - getRightContextSize();
			    if (fanOutPoint < 0) {
				    fanOutPoint = 0;
			    }
			    SentenceHMMState tail = ps;
			    for (int i = 0; tail != null && i < fanOutPoint; i++) {
				    tail = attachUnit(ps, tail, units, i, leftContext,
						    UnitContext.EMPTY);
			    }
			    SentenceHMMState branchTail = tail;
			    foreach (UnitContext finalRightContext in rightContexts) {
				    tail = branchTail;
				    for (int i = fanOutPoint; tail != null && i < units.Length; i++) {
					    tail = attachUnit(ps, tail, units, i, leftContext,
							    finalRightContext);
				    }
			    }
		    }

		    /**
		     * Attaches the given unit to the given tail, expanding the unit if
		     * necessary. If an identical unit is already attached, then this path
		     * is folded into the existing path.
		     * 
		     * @param parent
		     *            the parent state
		     * @param tail
		     *            the place to attach the unit to
		     * @param units
		     *            the set of units
		     * @param which
		     *            the index into the set of units
		     * @param leftContext
		     *            the left context for the unit
		     * @param rightContext
		     *            the right context for the unit
		     * @return the tail of the added unit (or null if the path was folded
		     *         onto an already expanded path.
		     */
		    private SentenceHMMState attachUnit(PronunciationState parent,
				    SentenceHMMState tail, Unit[] units, int which,
				    UnitContext leftContext, UnitContext rightContext) {
			    Unit[] lc = getLC(leftContext, units, which);
			    Unit[] rc = getRC(units, which, rightContext);
			    UnitContext actualRightContext = UnitContext.get(rc);
			    LeftRightContext context = LeftRightContext.get(lc, rc);
			    Unit cdUnit = unitManager.getUnit(units[which].getName(),
					    units[which].isFiller(), context);
			    UnitState unitState = new ExtendedUnitState(parent, which, cdUnit);
			    float logInsertionProbability;
			    if (unitState.getUnit().isSilence()) {
				    logInsertionProbability = logSilenceInsertionProbability;
			    } else if (unitState.getUnit().isFiller()) {
				    logInsertionProbability = logFillerInsertionProbability;
			    } else if (unitState.getWhich() == 0) {
				    logInsertionProbability = logWordInsertionProbability;
			    } else {
				    logInsertionProbability = logUnitInsertionProbability;
			    }
			    // check to see if this state already exists, if so
			    // branch to it and we are done, otherwise, branch to
			    // the new state and expand it.
			    SentenceHMMState existingState = getExistingState(unitState);
			    if (existingState != null) {
				    attachState(tail, existingState, logOne,
						    logInsertionProbability);
				    // T(" Folding " + existingState);
				    return null;
			    } else {
				    attachState(tail, unitState, logOne, logInsertionProbability);
				    addStateToCache(unitState);
				    // T(" Attaching " + unitState);
				    tail = expandUnit(unitState);
				    // if we are attaching the last state of a word, then
				    // we add it to the exitPoints table. the exit points
				    // table is indexed by a ContextPair, consisting of
				    // the exiting left context and the right context.
				    if (unitState.isLast()) {
					    UnitContext nextLeftContext = generateNextLeftContext(
							    leftContext, units[which]);
					    ContextPair cp = ContextPair.get(nextLeftContext,
							    actualRightContext);
					    // T(" Adding to exitPoints " + cp);
					    addExitPoint(cp, tail);
				    }
				    return tail;
			    }
		    }

		    /**
		     * Adds an exit point to this gstate
		     * 
		     * @param cp
		     *            the context tag for the state
		     * @param state
		     *            the state associated with the tag
		     */
		    private void addExitPoint(ContextPair cp, SentenceHMMState state) {
			    List<SearchState> list = exitPoints[cp];
			    if (list == null) {
				    list = new List<SearchState>();
				    exitPoints.Add(cp, list);
			    }
			    list.Add(state);
		    }

		    /**
		     * Get the left context for a unit based upon the left context size, the
		     * entry left context and the current unit.
		     * 
		     * @param left
		     *            the entry left context
		     * @param units
		     *            the set of units
		     * @param index
		     *            the index of the current unit
		     */
		    private Unit[] getLC(UnitContext left, Unit[] units, int index) {
			    Unit[] leftUnits = left.getUnits();
			    int curSize = leftUnits.Length + index;
			    int actSize = Math.Min(curSize, getLeftContextSize(units[index]));
			    int leftIndex = index - actSize;

			    Unit[] lc = new Unit[actSize];
			    for (int i = 0; i < lc.Length; i++) {
				    int lcIndex = leftIndex + i;
				    if (lcIndex < 0) {
					    lc[i] = leftUnits[leftUnits.Length + lcIndex];
				    } else {
					    lc[i] = units[lcIndex];
				    }
			    }
			    return lc;
		    }

		    /**
		     * Get the right context for a unit based upon the right context size,
		     * the exit right context and the current unit.
		     * 
		     * @param units
		     *            the set of units
		     * @param index
		     *            the index of the current unit
		     * @param right
		     *            the exiting right context
		     */
		    private Unit[] getRC(Unit[] units, int index, UnitContext right) {
			    Unit[] rightUnits = right.getUnits();
			    int leftIndex = index + 1;
			    int curSize = units.Length - leftIndex + rightUnits.Length;
			    int actSize = Math.Min(curSize, getRightContextSize(units[index]));

			    Unit[] rc = new Unit[actSize];
			    for (int i = 0; i < rc.Length; i++) {
				    int rcIndex = leftIndex + i;
				    if (rcIndex < units.Length) {
					    rc[i] = units[rcIndex];
				    } else {
					    rc[i] = rightUnits[rcIndex - units.Length];
				    }
			    }
			    return rc;
		    }

		    /**
		     * Gets the maximum context size for the given unit
		     * 
		     * @param unit
		     *            the unit of interest
		     * @return the maximum left context size for the unit
		     */
		    private int getLeftContextSize(Unit unit) {
			    return unit.isFiller() ? 0 : getLeftContextSize();
		    }

		    /**
		     * Gets the maximum context size for the given unit
		     * 
		     * @param unit
		     *            the unit of interest
		     * @return the maximum right context size for the unit
		     */
		    private int getRightContextSize(Unit unit) {
			    return unit.isFiller() ? 0 : getRightContextSize();
		    }

		    /**
		     * Returns the size of the left context.
		     * 
		     * @return the size of the left context
		     */
		    protected int getLeftContextSize() {
			    return acousticModel.getLeftContextSize();
		    }

		    /**
		     * Returns the size of the right context.
		     * 
		     * @return the size of the right context
		     */
		    protected int getRightContextSize() {
			    return acousticModel.getRightContextSize();
		    }

		    /**
		     * Generates the next left context based upon a previous context and a
		     * unit
		     * 
		     * @param prevLeftContext
		     *            the previous left context
		     * @param unit
		     *            the current unit
		     */
		    UnitContext generateNextLeftContext(UnitContext prevLeftContext,
				    Unit unit) {
			    Unit[] prevUnits = prevLeftContext.getUnits();
			    int actSize = Math.Min(prevUnits.Length, getLeftContextSize());
			    if (actSize == 0)
				    return UnitContext.EMPTY;
		        Unit[] leftUnits = prevUnits.Skip(1).Take(actSize).ToArray();//Arrays.copyOfRange(prevUnits, 1, actSize + 1));
			    leftUnits[actSize - 1] = unit;
			    return UnitContext.get(leftUnits);
		    }

		    /**
		     * Expands the unit into a set of HMMStates. If the unit is a silence
		     * unit add an optional loopback to the tail.
		     * 
		     * @param unit
		     *            the unit to expand
		     * @return the head of the hmm tree
		     */
		    protected SentenceHMMState expandUnit(UnitState unit) {
			    SentenceHMMState tail = getHMMStates(unit);
			    // if the unit is a silence unit add a loop back from the
			    // tail silence unit
			    if (unit.getUnit().isSilence()) {
				    // add the loopback, but don't expand it // anymore
				    attachState(tail, unit, logOne, logSilenceInsertionProbability);
			    }
			    return tail;
		    }

		    /**
		     * Given a unit state, return the set of sentence hmm states associated
		     * with the unit
		     * 
		     * @param unitState
		     *            the unit state of intereset
		     * @return the hmm tree for the unit
		     */
		    private HMMStateState getHMMStates(UnitState unitState) {
			    HMMStateState hmmTree;
			    HMMStateState finalState;
			    Unit unit = unitState.getUnit();
			    HMMPosition position = unitState.getPosition();
			    HMM hmm = acousticModel.lookupNearestHMM(unit, position, false);
			    HMMState initialState = hmm.getInitialState();
			    hmmTree = new HMMStateState(unitState, initialState);
			    attachState(unitState, hmmTree, logOne, logOne);
			    addStateToCache(hmmTree);
			    finalState = expandHMMTree(unitState, hmmTree);
			    return finalState;
		    }

		    /**
		     * Expands the given hmm state tree
		     * 
		     * @param parent
		     *            the parent of the tree
		     * @param tree
		     *            the tree to expand
		     * @return the final state in the tree
		     */
		    private HMMStateState expandHMMTree(UnitState parent, HMMStateState tree) {
			    HMMStateState retState = tree;
			    foreach (HMMStateArc arc in tree.getHMMState().getSuccessors()) {
				    HMMStateState newState;
				    if (arc.getHMMState().isEmitting()) {
					    newState = new HMMStateState(parent, arc.getHMMState());
				    } else {
					    newState = new NonEmittingHMMState(parent, arc
							    .getHMMState());
				    }
				    SentenceHMMState existingState = getExistingState(newState);
				    float logProb = arc.getLogProbability();
				    if (existingState != null) {
					    attachState(tree, existingState, logOne, logProb);
				    } else {
					    attachState(tree, newState, logOne, logProb);
					    addStateToCache(newState);
					    retState = expandHMMTree(parent, newState);
				    }
			    }
			    return retState;
		    }

		    /**
		     * Connect up all of the GStates. Each state now has a table of exit
		     * points. These exit points represent tail states for the node. Each of
		     * these tail states is tagged with a ContextPair, that indicates what
		     * the left context is (the exiting context) and the right context (the
		     * entering context) for the transition. To connect up a state, the
		     * connect does the following: 1) Iterate through all of the grammar
		     * successors for this state 2) Get the 'entry points' for the successor
		     * that match the exit points. 3) Hook them up.
		     * <p/>
		     * Note that for a task with 1000 words this will involve checking on
		     * the order of 35,000,000 connections and making about 2,000,000
		     * connections
		     */
		    public void connect() {

			    foreach (GrammarArc arc in getSuccessors()) {
				    GState gstate = getGState(arc.getGrammarNode());
				    if (!gstate.getNode().isEmpty()
						    && gstate.getNode().getWord().getSpelling().Equals(
								    Dictionary.SENTENCE_START_SPELLING)) {
					    continue;
				    }
				    float probability = arc.getProbability();

				    // adjust the language probability by the number of
				    // pronunciations. If there are 3 ways to say the
				    // word, then each pronunciation gets 1/3 of the total
				    // probability.
				    if (spreadWordProbabilitiesAcrossPronunciations) {
					    int numPronunciations = gstate.getNode().getWord()
							    .getPronunciations(null).Length;
					    probability -= logMath.linearToLog(numPronunciations);
				    }
				    float fprob = probability; // final probability

				    foreach (var entry in exitPoints) {
					    List<SearchState> destEntryPoints = gstate
							    .getEntryPoints(entry.Key);
					    if (destEntryPoints != null) {
						    List<SearchState> srcExitPoints = entry.Value;
						    connect(srcExitPoints, destEntryPoints, fprob);
					    }
				    }
			    }
		    }

		    /**
		     * connect all the states in the source list to the states in the
		     * destination list
		     * 
		     * @param sourceList
		     *            the set of source states
		     * @param destList
		     *            the set of destination states.
		     */
		    private void connect(List<SearchState> sourceList,
				    List<SearchState> destList, float logLangProb) {
			    foreach (SearchState source in sourceList) {
				    SentenceHMMState sourceState = (SentenceHMMState) source;
				    foreach (SearchState dest in destList) {
					    SentenceHMMState destState = (SentenceHMMState) dest;
					    sourceState.connect(getArc(destState, logLangProb, logOne));
					    exitConnections++;
				    }
			    }
		    }

		    /**
		     * Attaches one SentenceHMMState as a child to another, the transition
		     * has the given probability
		     * 
		     * @param prevState
		     *            the parent state
		     * @param nextState
		     *            the child state
		     * @param logLanguageProbablity
		     *            the language probability of transition in the LogMath log
		     *            domain
		     * @param logInsertionProbablity
		     *            insertion probability of transition in the LogMath log
		     *            domain
		     */
		    protected void attachState(SentenceHMMState prevState,
				    SentenceHMMState nextState, float logLanguageProbablity,
				    float logInsertionProbablity) {
			    prevState.connect(getArc(nextState, logLanguageProbablity,
					    logInsertionProbablity));
			    if (showCompilationProgress && totalStateCounter++ % 1000 == 0) {
				    Console.WriteLine(".");
			    }
		    }

		    /**
		     * Returns all of the states maintained by this gstate
		     * 
		     * @return the set of all states
		     */
		    public List<SearchState> getStates() {
			    // since pstates are not placed in the cache we have to
			    // gather those states. All other states are found in the
			    // existingStates cache.
			    List<SearchState> allStates = new List<SearchState>(
					    existingStates.Values);
			    foreach (List<SearchState> list in entryPoints.Values) {
				    allStates.AddRange(list);
			    }
			    return allStates;
		    }

		    /**
		     * Checks to see if a state that matches the given state already exists
		     * 
		     * @param state
		     *            the state to check
		     * @return true if a state with an identical signature already exists.
		     */
		    private SentenceHMMState getExistingState(SentenceHMMState state) {
			    return existingStates[state.getSignature()];
		    }

		    /**
		     * Adds the given state to the cache of states
		     * 
		     * @param state
		     *            the state to add
		     */
		    private void addStateToCache(SentenceHMMState state) {
			    existingStates.Add(state.getSignature(), state);
		    }

		    /**
		     * Prints info about this GState
		     * 
		     * @throws IOException
		     */
		    public void dumpInfo(){
			    // BufferedWriter writer = new BufferedWriter(new
			    // FileWriter("./graph.dot"));
			    Console.WriteLine(" ==== " + this + " ========");
			    Console.Write("Node: " + node);
			    if (node.isEmpty()) {
				    Console.WriteLine("  (Empty)");
			    } else {
				    Console.Write(" " + node.getWord());
			    }
			    Console.Write(" ep: " + entryPoints.Count());
			    Console.Write(" exit: " + exitPoints.Count());
			    Console.Write(" cons: " + exitConnections);
			    Console.Write(" tot: " + getStates().Count());
			    Console.Write(" sc: " + getStartingContexts().Count());
			    Console.Write(" rc: " + leftContexts.Count());
			    Console.WriteLine(" lc: " + rightContexts.Count());
			    dumpDetails();
		    }

		    /**
		     * Dumps the details for a gstate
		     */
		    void dumpDetails() {
			    dumpCollection(" entryPoints", entryPoints.Keys.ToList());
			    dumpCollection(" entryPoints states", entryPoints.Values.ToList());
			    dumpCollection(" exitPoints", exitPoints.Keys.ToList());
			    dumpCollection(" exitPoints states", exitPoints.Values.ToList());
			    dumpNextNodes();
			    dumpExitPoints(exitPoints.Values.ToList());
			    dumpCollection(" startingContexts", getStartingContexts().ToList());
			    dumpCollection(" branchingInFrom", leftContexts.ToList());
			    dumpCollection(" branchingOutTo", rightContexts.ToList());
			    dumpCollection(" existingStates", existingStates.Keys.ToList());
		    }

		    /**
		     * Dumps out the names of the next set of grammar nodes
		     */
		    private void dumpNextNodes() {
			    Console.WriteLine("     Next Grammar Nodes: ");
			    foreach (GrammarArc arc in node.getSuccessors()) {
				    Console.WriteLine("          " + arc.getGrammarNode());
			    }
		    }

		    /**
		     * Dumps the exit points and their destination states
		     * 
		     * @param eps
		     *            the collection of exit points
		     */
		    private void dumpExitPoints(List<List<SearchState>> eps) {
			    foreach (List<SearchState> epList in eps) {
				    foreach (SearchState state in epList) {
					    Console.WriteLine("      Arcs from: " + state);
					    foreach (SearchStateArc arc in state.getSuccessors()) {
						    Console.WriteLine("          " + arc.getState());
					    }
				    }
			    }
		    }

		    /**
		     * Dumps the given collection
		     * 
		     * @param name
		     *            the name of the collection
		     * @param collection
		     *            the collection to dump
		     */
		    private void dumpCollection<T>(String name, List<T> collection) {
			    Console.WriteLine("     " + name);
			    foreach (Object obj in collection) {
				    Console.WriteLine("         " + obj);
			    }
		    }

		    /**
		     * Returns the string representation of the object
		     * 
		     * @return the string representation of the object
		     */
		    //@Override
		    public override String ToString() {
			    if (node.isEmpty()) {
				    return "GState " + node + "(empty)";
			    } else {
				    return "GState " + node + " word " + node.getWord();
			    }
		    }
	    }

	    /**
	     * Quick and dirty tracing. Traces the string if 'tracing' is true
	     * 
	     * @param s
	     *            the string to trace.
	     */
	    private void T(String s) {
		    if (tracing) {
			    Console.WriteLine(s);
		    }
	    }
    }

/**
     * A class that represents a set of units used as a context
     */
    public class UnitContext {

	    private static readonly Cache<UnitContext> unitContextCache = new Cache<UnitContext>();
	    private readonly Unit[] context;
	    private int hashCode = 12;
	    public readonly UnitContext EMPTY = new UnitContext(Unit.EMPTY_ARRAY);
	    public static readonly UnitContext SILENCE = new UnitContext(
			    new Unit[] { UnitManager.SILENCE });

        /*static {*/
            // TODO: activate code below
		    /*unitContextCache.cache(EMPTY);
		    unitContextCache.cache(SILENCE);*/
	    /*}*/

	    /**
	     * Creates a UnitContext for the given context. This constructor is not
	     * directly accessible, use the factory method instead.
	     * 
	     * @param context
	     *            the context to wrap with this UnitContext
	     */
	    private UnitContext(Unit[] context) {
		    this.context = context;
		    hashCode = 12;
		    for (int i = 0; i < context.Length; i++) {
			    hashCode += context[i].getName().GetHashCode() * ((i + 1) * 34);
		    }
	    }

	    /**
	     * Gets the unit context for the given units. There is a single unit context
	     * for each unit combination.
	     * 
	     * @param units
	     *            the units of interest
	     * @return the unit context.
	     */
	    public static UnitContext get(Unit[] units) {
		    UnitContext newUC = new UnitContext(units);
		    UnitContext cachedUC = (UnitContext) unitContextCache.cache(newUC);
		    return cachedUC == null ? newUC : cachedUC;
	    }

	    /**
	     * Retrieves the units for this context
	     * 
	     * @return the units associated with this context
	     */
	    public Unit[] getUnits() {
		    return context;
	    }

	    /**
	     * Determines if the given object is equal to this UnitContext
	     * 
	     * @param o
	     *            the object to compare to
	     * @return <code>true</code> if the objects are equal
	     */
	    //@Override
	    public override bool Equals(Object o) {
		    if (this == o) {
			    return true;
		    } else if (o is UnitContext) {
			    UnitContext other = (UnitContext) o;
			    if (this.context.Length != other.context.Length) {
				    return false;
			    } else {
				    for (int i = 0; i < this.context.Length; i++) {
					    if (this.context[i] != other.context[i]) {
						    return false;
					    }
				    }
				    return true;
			    }
		    } else {
			    return false;
		    }
	    }

	    /**
	     * Returns a hashcode for this object
	     * 
	     * @return the hashCode
	     */
	    //@Override
	    public override int GetHashCode() {
		    return hashCode;
	    }

	    /**
	     * Dumps information about the total number of UnitContext objects
	     */
	    public static void dumpInfo() {
		    Console.WriteLine("Total number of UnitContexts : "
				    + unitContextCache.getMisses() + " folded: "
				    + unitContextCache.getHits());
	    }

	    /**
	     * Returns a string representation of this object
	     * 
	     * @return a string representation
	     */
	    //@Override
	    public override String ToString() {
		    return LeftRightContext.getContextName(context);
	    }
    }

    /**
     * A context pair hold a left and starting context. It is used as a hash into
     * the set of starting points for a particular gstate
     */
    public class ContextPair {

        private static readonly Cache<ContextPair> contextPairCache = new Cache<ContextPair>();
        private readonly UnitContext left;
	    private readonly UnitContext right;
	    private readonly int hashCode;

	    /**
	     * Creates a UnitContext for the given context. This constructor is not
	     * directly accessible, use the factory method instead.
	     * 
	     * @param left
	     *            the left context
	     * @param right
	     *            the right context
	     */
	    private ContextPair(UnitContext left, UnitContext right) {
		    this.left = left;
		    this.right = right;
		    hashCode = 99 + left.GetHashCode() * 113 + right.GetHashCode();
	    }

	    /**
	     * Gets the ContextPair for the given set of contexts. This is a factory
	     * method. If the ContextPair already exists, return that one, otherwise,
	     * create it and store it so it can be reused.
	     * 
	     * @param left
	     *            the left context
	     * @param right
	     *            the right context
	     * @return the unit context.
	     */
	    public static ContextPair get(UnitContext left, UnitContext right) {
		    ContextPair newCP = new ContextPair(left, right);
		    ContextPair cachedCP = (ContextPair) contextPairCache.cache(newCP);
		    return cachedCP == null ? newCP : cachedCP;
	    }

	    /**
	     * Determines if the given object is equal to this UnitContext
	     * 
	     * @param o
	     *            the object to compare to
	     * @return <code>true</code> if the objects are equal return;
	     */
	    //@Override
	    public override bool Equals(Object o) {
		    if (this == o) {
			    return true;
		    } else if (o is ContextPair) {
			    ContextPair other = (ContextPair) o;
			    return this.left.Equals(other.left)
					    && this.right.Equals(other.right);
		    } else {
			    return false;
		    }
	    }

	    /**
	     * Returns a hashcode for this object
	     * 
	     * @return the hashCode
	     */
	    //@Override
	    public override int GetHashCode() {
		    return hashCode;
	    }

	    /**
	     * Returns a string representation of the object
	     */
	    //@Override
	    public override string ToString() {
		    return "CP left: " + left + " right: " + right;
	    }

	    /**
	     * Gets the left unit context
	     * 
	     * @return the left unit context
	     */
	    public UnitContext getLeftContext() {
		    return left;
	    }

	    /**
	     * Gets the right unit context
	     * 
	     * @return the right unit context
	     */
	    public UnitContext getRightContext() {
		    return right;
	    }
    }
}
