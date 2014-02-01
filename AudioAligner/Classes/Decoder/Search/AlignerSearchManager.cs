using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AudioAligner.Classes.PhraseSpotter;
using AudioAligner.Classes.Util;
using com.sun.org.apache.regexp.@internal;
using edu.cmu.sphinx.decoder.search;
using edu.cmu.sphinx.frontend;
using edu.cmu.sphinx.linguist;
using edu.cmu.sphinx.result;
using edu.cmu.sphinx.util;
using edu.cmu.sphinx.util.props;
using log4net;
using edu.cmu.sphinx.decoder.pruner;
using edu.cmu.sphinx.decoder.scorer;
using Word = edu.cmu.sphinx.linguist.dictionary.Word;

namespace AudioAligner.Classes.Decoder.Search
{
    class AlignerSearchManager : TokenSearchManager
    {
        /**
	     * The property that defines the name of the linguist to be used by this
	     * search manager.
	     */
	    //@S4Component(type = Linguist.class)
	    public const String PROP_LINGUIST = "linguist";

	    /**
	     * The property that defines the name of the linguist to be used by this
	     * search manager.
	     */
	    //@S4Component(type = Pruner.class)
	    public const String PROP_PRUNER = "pruner";

	    /**
	     * The property that defines the name of the scorer to be used by this
	     * search manager.
	     */
	    //@S4Component(type = AcousticScorer.class)
	    public const String PROP_SCORER = "scorer";

	    /**
	     * The property that defines the name of the logmath to be used by this
	     * search manager.
	     */
	    //@S4Component(type = LogMath.class)
	    public const String PROP_LOG_MATH = "logMath";

	    /**
	     * The property that defines the name of the active list factory to be used
	     * by this search manager.
	     */
	    //@S4Component(type = ActiveListFactory.class)
	    public const String PROP_ACTIVE_LIST_FACTORY = "activeListFactory";

	    /**
	     * The property that when set to <code>true</code> will cause the recognizer
	     * to count up all the tokens in the active list after every frame.
	     */
	    //@S4bool(defaultValue = false)
	    public const String PROP_SHOW_TOKEN_COUNT = "showTokenCount";

	    /**
	     * The property that sets the minimum score relative to the maximum score in
	     * the word list for pruning. Words with a score less than relativeBeamWidth
	     * * maximumScore will be pruned from the list
	     */
	    //@S4Double(defaultValue = 0.00000000000000000000000001)
	    public const String PROP_RELATIVE_WORD_BEAM_WIDTH = "relativeWordBeamWidth";

	    /**
	     * The property that controls whether or not relative beam pruning will be
	     * performed on the entry into a state.
	     */
	    //@S4bool(defaultValue = false)
	    public const String PROP_WANT_ENTRY_PRUNING = "wantEntryPruning";

	    /**
	     * The property that controls the number of frames processed for every time
	     * the decode growth step is skipped. Setting this property to zero disables
	     * grow skipping. Setting this number to a small integer will increase the
	     * speed of the decoder but will also decrease its accuracy. The higher the
	     * number, the less often the grow code is skipped.
	     */
	    //@S4Integer(defaultValue = 0)
	    public const String PROP_GROW_SKIP_INTERVAL = "growSkipInterval";

        protected edu.cmu.sphinx.linguist.Linguist linguist; // Provides grammar/language info
	    private Pruner pruner; // used to prune the active list
	    private AcousticScorer scorer; // used to score the active list
	    protected int currentFrameNumber; // the current frame number
	    protected ActiveList activeList; // the list of active tokens
	    protected List<Token> resultList; // the current set of results
	    protected LogMath logMath;

	    private ILog logger;
	    private String name;

	    private List<String> phraseWordList;
	    private bool phraseDetected;
	    protected List<PhraseSpotterResult> spotterResult;
	    protected SortedSet<float> spotterTimes = new SortedSet<float>();
	    private float sampleRate = 1.0f;
	    private float timeThreshold = 0.04f;
	    private int logCounter = 1;
	    //private Runtime runtime = Runtime.getRuntime();

	    // ------------------------------------
	    // monitoring data
	    // ------------------------------------

	    private Timer scoreTimer; // TODO move these timers out
	    private Timer pruneTimer;
	    private Timer growTimer;
	    private StatisticsVariable totalTokensScored;
	    private StatisticsVariable tokensPerSecond;
	    private StatisticsVariable curTokensScored;
	    private StatisticsVariable tokensCreated;
	    private StatisticsVariable viterbiPruned;
	    private StatisticsVariable beamPruned;

	    // ------------------------------------
	    // Working data
	    // ------------------------------------

	    protected bool showTokenCountP;
	    private bool wantEntryPruning;
	    private Dictionary<SearchState, Token> bestTokenMap;
	    private float logRelativeWordBeamWidth;
	    private int totalHmms;
	    private double startTime;
	    private float threshold;
	    private float wordThreshold;
	    private int growSkipInterval;
	    private ActiveListFactory activeListFactory;
	    protected bool streamEnd;

	    public AlignerSearchManager() {

	    }

	    /**
	     * 
	     * @param logMath
	     * @param linguist
	     * @param pruner
	     * @param scorer
	     * @param activeListFactory
	     * @param showTokenCount
	     * @param relativeWordBeamWidth
	     * @param growSkipInterval
	     * @param wantEntryPruning
	     */
        public AlignerSearchManager(LogMath logMath, edu.cmu.sphinx.linguist.Linguist linguist,
			    Pruner pruner, AcousticScorer scorer,
			    ActiveListFactory activeListFactory, bool showTokenCount,
			    double relativeWordBeamWidth, int growSkipInterval,
			    bool wantEntryPruning) {
		    this.name = getClass().getName();
		    this.logger = LogManager.GetLogger(name);
		    this.logMath = logMath;
		    this.linguist = linguist;
		    this.pruner = pruner;
		    this.scorer = scorer;
		    this.activeListFactory = activeListFactory;
		    this.showTokenCountP = showTokenCount;
		    this.growSkipInterval = growSkipInterval;
		    this.wantEntryPruning = wantEntryPruning;
		    this.logRelativeWordBeamWidth = logMath
				    .linearToLog(relativeWordBeamWidth);
		    this.keepAllTokens = false;
		    this.phraseWordList = new List<String>();
	    }

	    //@Override
	    public override void newProperties(PropertySheet ps){
		    base.newProperties(ps);

		    //logger = ps.getLogger();
		    name = ps.getInstanceName();

		    logMath = (LogMath) ps.getComponent(PROP_LOG_MATH);

            linguist = (edu.cmu.sphinx.linguist.Linguist)ps.getComponent(PROP_LINGUIST);
		    pruner = (Pruner) ps.getComponent(PROP_PRUNER);
		    scorer = (AcousticScorer) ps.getComponent(PROP_SCORER);
		    activeListFactory = (ActiveListFactory) ps
				    .getComponent(PROP_ACTIVE_LIST_FACTORY);
		    showTokenCountP = JavaToCs.ConvertBool(ps.getBoolean(PROP_SHOW_TOKEN_COUNT));

		    double relativeWordBeamWidth = ps.getDouble(PROP_RELATIVE_WORD_BEAM_WIDTH);
		    growSkipInterval = ps.getInt(PROP_GROW_SKIP_INTERVAL);
		    wantEntryPruning = JavaToCs.ConvertBool(ps.getBoolean(PROP_WANT_ENTRY_PRUNING));
		    logRelativeWordBeamWidth = logMath.linearToLog(relativeWordBeamWidth);

		    this.keepAllTokens = true;
	    }

	    /**
	     * Called at the start of recognition. Gets the search manager ready to
	     * recognize
	     */
	    //@Override
	    public override void startRecognition() {
		    logger.Info("starting recognition");
		
		    //System.out.println("Relative Beam Width: " + logRelativeWordBeamWidth);
		    linguist.startRecognition();
		    pruner.startRecognition();
		    scorer.startRecognition();
		    localStart();
		    if (startTime == 0.0) {
			    //startTime = System.currentTimeMillis();
		        startTime = DateTime.Now.TimeOfDay.TotalMilliseconds;
		    }
	    }

	    /**
	     * Performs the recognition for the given number of frames.
	     * 
	     * @param nFrames
	     *            the number of frames to recognize
	     * @return the current result or null if there is no Result (due to the lack
	     *         of frames to recognize)
	     */
	    //@Override
	    public override Result recognize(int nFrames) {
		    bool done = false;
		    Result result = null;
		    streamEnd = false;

		    for (int i = 0; i < nFrames && !done; i++) {
			    done = recognize();
		    }

		    // generate a new temporary result if the current token is based on a
		    // final search state
		    // remark: the first check for not null is necessary in cases that the
		    // search space does not contain scoreable tokens.
		    if (activeList.getBestToken() != null) {
			    // to make the current result as correct as possible we undo the
			    // last search graph expansion here
			    ActiveList fixedList = undoLastGrowStep();

			    if (!streamEnd)
			    {
			        var jResultList = CsToJava.ConvertToJList(resultList);
				    // now create the result using the fixed active-list
				    result = new Result(fixedList, jResultList, currentFrameNumber,
						    done, logMath);
			    }
		    }

		    if (showTokenCountP) {
			    showTokenCount();
		    }

		    return result;
	    }

	    /**
	     * Because the growBranches() is called although no data is left after the
	     * last speech frame, the ordering of the active-list might depend on the
	     * transition probabilities and (penalty-scores) only. Therefore we need to
	     * undo the last grow-step up to final states or the last emitting state in
	     * order to fix the list.
	     * 
	     * @return newly created list
	     */
	    protected ActiveList undoLastGrowStep() {
		    ActiveList fixedList = activeList.newInstance();

	        var tokens = JavaToCs.GetTokenCollection(activeList);
		    foreach (Token token in tokens){
			    Token curToken = token.getPredecessor();

			    // remove the final states that are not the real final ones because
			    // they're just hide prior final tokens:
			    while (curToken.getPredecessor() != null
					    && ((curToken.isFinal()
							    && curToken.getPredecessor() != null && !curToken
							    .getPredecessor().isFinal())
							    || (curToken.isEmitting() && curToken.getData() == null) // the
					    // so
					    // long
					    // not
					    // scored
					    // tokens
					    || (!curToken.isFinal() && !curToken.isEmitting()))) {
				    curToken = curToken.getPredecessor();
			    }

			    fixedList.add(curToken);
		    }

		    return fixedList;
	    }

	    /** Terminates a recognition */
	    //@Override
	    public override void stopRecognition() {
		    localStop();
		    scorer.stopRecognition();
		    pruner.stopRecognition();
		    linguist.stopRecognition();

		    logger.Info("recognition stopped");
	    }

	    /**
	     * Performs recognition for one frame. Returns true if recognition has been
	     * completed.
	     * 
	     * @return <code>true</code> if recognition is completed.
	     */
	    protected bool recognize() {
		    bool more = scoreTokens(); // score emitting tokens
		    phraseDetected = false;
		    if (more) {
			    pruneBranches(); // eliminate poor branches
			    if (phraseDetected) {
				    logger.Info("Active List Pruned: number of active Token: "
						    + activeList.size());
			    }
			    logger.Info("Pruning Done: Number of Active tokens: "
					    + activeList.size());
			    currentFrameNumber++;
			    if (growSkipInterval == 0
					    || (currentFrameNumber % growSkipInterval) != 0) {
				    phraseDetected = false;
				    logger.Info("---------- Grow Branches Step : Start ---------");
				    growBranches(); // extend remaining branches
				    logger.Info("---------- Grow Branches Step : Over ----------");
			    }
			    logCounter++;
		    }
		    return !more;
	    }

	    /**
	     * Gets the initial grammar node from the linguist and creates a
	     * GrammarNodeToken
	     */
	    protected void localStart() {
		    currentFrameNumber = 0;
		    curTokensScored.value = 0;
		    ActiveList newActiveList = activeListFactory.newInstance();
		    SearchState state = linguist.getSearchGraph().getInitialState();
		    newActiveList.add(new Token(state, currentFrameNumber));
		    activeList = newActiveList;

		    growBranches();
	    }

	    /** Local cleanup for this search manager */
	    protected void localStop() {
	    }

	    /**
	     * Goes through the active list of tokens and expands each token, finding
	     * the set of successor tokens until all the successor tokens are emitting
	     * tokens.
	     */
	    protected void growBranches() {
		    int mapSize = activeList.size() * 10;
		    if (mapSize == 0) {
			    mapSize = 1;
		    }
		    growTimer.start();
		    bestTokenMap = new Dictionary<SearchState, Token>(mapSize);
		    ActiveList oldActiveList = activeList;
		    resultList = new List<Token>();
		    activeList = activeListFactory.newInstance();
		    threshold = oldActiveList.getBeamThreshold();
		    wordThreshold = oldActiveList.getBestScore() + logRelativeWordBeamWidth;

	        var tokens = JavaToCs.GetTokenCollection(oldActiveList);
		    foreach (Token token in tokens){
			    collectSuccessorTokens(token);
		    }
		    growTimer.stop();
		    if (logger.IsInfoEnabled) {
			    int hmms = activeList.size();
			    totalHmms += hmms;
			    logger.Info("Frame: " + currentFrameNumber + " Hmms: " + hmms
					    + "  total " + totalHmms);
		    }
	    }

	    /**
	     * Calculate the acoustic scores for the active list. The active list should
	     * contain only emitting tokens.
	     * 
	     * @return <code>true</code> if there are more frames to score, otherwise,
	     *         false
	     */
	    protected bool scoreTokens() {
		    bool hasMoreFrames = false;

		    scoreTimer.start();
		    Data data = scorer.calculateScores(activeList.getTokens());
		    scoreTimer.stop();

		    Token bestToken = null;
		    if (data is Token) {
			    bestToken = (Token) data;
		    } else if (data == null) {
			    streamEnd = true;
		    }

		    if (bestToken != null) {
			    hasMoreFrames = true;
			    activeList.setBestToken(bestToken);
		    }

		    // update statistics
		    curTokensScored.value += activeList.size();
		    totalTokensScored.value += activeList.size();
		    tokensPerSecond.value = totalTokensScored.value / getTotalTime();

		    // if (logger.isLoggable(Level.FINE)) {
		    // logger.fine(currentFrameNumber + " " + activeList.size()
		    // + " " + curTokensScored.value + " "
		    // + (int) tokensPerSecond.value);
		    // }

		    return hasMoreFrames;
	    }

	    /**
	     * Returns the total time since we start4ed
	     * 
	     * @return the total time (in seconds)
	     */
	    private double getTotalTime() {
		    //return (System.currentTimeMillis() - startTime) / 1000.0;
            // TODO: use stopwatch instead
		    return (DateTime.Now.TimeOfDay.TotalMilliseconds - startTime) / 1000.0;
	    }

	    /** Removes unpromising branches from the active list */
	    protected void pruneBranches() {
		    int startSize = activeList.size();
		    pruneTimer.start();
		    activeList = pruner.prune(activeList);
		    beamPruned.value += startSize - activeList.size();
		    pruneTimer.stop();
	    }

	    /**
	     * Gets the best token for this state
	     * 
	     * @param state
	     *            the state of interest
	     * @return the best token
	     */
	    protected Token getBestToken(SearchState state) {
		    Token best = bestTokenMap[state];
		    if (logger.IsInfoEnabled && best != null) {
			    logger.Info("BT " + best + " for state " + state);
		    }
		    return best;
	    }

	    /**
	     * Sets the best token for a given state
	     * 
	     * @param token
	     *            the best token
	     * @param state
	     *            the state
	     * @return the previous best token for the given state, or null if no
	     *         previous best token
	     */
	    protected Token setBestToken(Token token, SearchState state) {
		    bestTokenMap.Add(state, token);
	        return token;
	    }

	    public ActiveList getActiveList() {
		    return activeList;
	    }

	    /**
	     * Collects the next set of emitting tokens from a token and accumulates
	     * them in the active or result lists
	     * 
	     * @param token
	     *            the token to collect successors from
	     */
	    protected void collectSuccessorTokens(Token token) {

		    //System.out.println(logRelativeWordBeamWidth);
		    SearchState state = token.getSearchState();
		    // If this is a final state, add it to the final list
		    if (token.isFinal()) {
			    resultList.Add(token);
		    }

		    // if this is a non-emitting token and we've already
		    // visited the same state during this frame, then we
		    // are in a grammar loop, so we don't continue to expand.
		    // This check only works properly if we have kept all of the
		    // tokens (instead of skipping the non-word tokens).
		    // Note that certain linguists will never generate grammar loops
		    // (lextree linguist for example). For these cases, it is perfectly
		    // fine to disable this check by setting keepAllTokens to false

		    if (!token.isEmitting() && (keepAllTokens && isVisited(token))) {
			    return;
		    }

		    if (token.getScore() < threshold) {
			    return;
		    }

		    float penalty = 0.0f;

		    // Changes made here not only to check for wordThreshold but also
		    // Phrase Spotter's result
		    if (state is WordSearchState) {
			    FloatData data = (FloatData) token.getData();
			    Word word = token.getWord();
			    float phraseTime = (float) currentFrameNumber / 100;
			    if (spotterContains(word.getSpelling(), phraseTime)) {
				    penalty = 1.0f; // it's more of a reward
				    Console.WriteLine("spotted");
				    phraseDetected = true;
				    logger.Info("Token prioritized");
			    }
			    if (token.getScore() < wordThreshold) {
				    return;
			    }
		    }

		    // Idea is to award the favouring token very well
		    if (penalty != 0.0f) {
			    token.setScore(token.getScore() + 10000.0f);
			    setBestToken(token, state);
		    }

		    SearchStateArc[] arcs = state.getSuccessors();
		    // For each successor
		    // calculate the entry score for the token based upon the
		    // predecessor token score and the transition probabilities
		    // if the score is better than the best score encountered for
		    // the SearchState and frame then create a new token, add
		    // it to the lattice and the SearchState.
		    // If the token is an emitting token add it to the list,
		    // otherwise recursively collect the new tokens successors.
		    foreach (SearchStateArc arc in arcs) {
			    SearchState nextState = arc.getState();

			    // We're actually multiplying the variables, but since
			    // these come in log(), multiply gets converted to add
			    float logEntryScore = token.getScore() + arc.getProbability()
					    + penalty;

			    if (wantEntryPruning) { // false by default
				    if (logEntryScore < threshold) {
					    continue;
				    }
				    if (nextState is WordSearchState
						    && logEntryScore < wordThreshold) {
					    continue;
				    }
			    }
			    Token predecessor = getResultListPredecessor(token);
			    Token bestToken = getBestToken(nextState);
			    bool firstToken = bestToken == null;
			    if (firstToken || bestToken.getScore() <= logEntryScore) {
				    Token newToken = new Token(predecessor, nextState,
						    logEntryScore, arc.getInsertionProbability(),
						    arc.getLanguageProbability(), currentFrameNumber);
				    tokensCreated.value++;
				    setBestToken(newToken, nextState);
				    if (!newToken.isEmitting()) {
					    // if not emitting, check to see if we've already visited
					    // this state during this frame. Expand the token only if we
					    // haven't visited it already. This prevents the search
					    // from getting stuck in a loop of states with no
					    // intervening emitting nodes. This can happen with nasty
					    // jsgf grammars such as ((foo*)*)*
					    if (!isVisited(newToken)) {
						    collectSuccessorTokens(newToken);
					    }
				    } else {
					    if (firstToken) {
						    activeList.add(newToken);
					    } else {
						    activeList.replace(bestToken, newToken);
						    viterbiPruned.value++;
					    }
				    }
			    } else {
				    viterbiPruned.value++;
			    }
		    }
	    }

	    private bool spotterContains(String phraseFirstWord, float time) {
		
		    if(spotterResult != null) {
			    /*Iterator<PhraseSpotterResult> iter = spotterResult
					    .iterator();
			    while (iter.hasNext()) {
				    PhraseSpotterResult result = iter.next();
				    String firstWord = result.getPhraseFirstWord();
				    float timedData = result.getStartTime();
				    if (Math.Abs(timedData - time) < timeThreshold
						    && phraseFirstWord.compareToIgnoreCase(firstWord) == 0) {
					    return true;
				    }
			    }*/
		        foreach (var result in spotterResult)
		        {
		            string firstWord = result.getPhraseFirstWord();
		            float timedData = result.getStartTime();
                    if (Math.Abs(timedData - time) < timeThreshold
						    && phraseFirstWord.Equals(firstWord, StringComparison.InvariantCultureIgnoreCase)) {
					    return true;
				    }
		        }
		    }
		    return false;
	    }

	    /**
	     * Determines whether or not we've visited the state associated with this
	     * token since the previous frame.
	     * 
	     * @param t
	     *            the token to check
	     * @return true if we've visited the search state since the last frame
	     */
	    private bool isVisited(Token t) {
		    SearchState curState = t.getSearchState();

		    t = t.getPredecessor();

		    while (t != null && !t.isEmitting()) {
			    if (curState.Equals(t.getSearchState())) {
				    return true;
			    }
			    t = t.getPredecessor();
		    }
		    return false;
	    }

	    /**
	     * Counts all the tokens in the active list (and displays them). This is an
	     * expensive operation.
	     */
	    protected void showTokenCount() {
		    if (logger.IsInfoEnabled) {
			    HashSet<Token> tokenSet = new HashSet<Token>();
		        var tokens = JavaToCs.GetTokenCollection(activeList);
			    foreach (Token token in tokens)
			    {
			        var tok = token;
				    while (tok != null) {
					    tokenSet.Add(tok);
					    tok = tok.getPredecessor();
				    }
			    }
			    logger.Info("Token Lattice size: " + tokenSet.Count);
			    tokenSet = new HashSet<Token>();
			    foreach (Token token in resultList)
			    {
			        var tok = token;
				    while (tok != null) {
					    tokenSet.Add(tok);
					    tok = tok.getPredecessor();
				    }
			    }
			    logger.Info("Result Lattice size: " + tokenSet.Count);
		    }
	    }

	    /**
	     * Returns the best token map.
	     * 
	     * @return the best token map
	     */
	    protected Dictionary<SearchState, Token> getBestTokenMap() {
		    return bestTokenMap;
	    }

	    /**
	     * Sets the best token Map.
	     * 
	     * @param bestTokenMap
	     *            the new best token Map
	     */
	    protected void setBestTokenMap(Dictionary<SearchState, Token> bestTokenMap) {
		    this.bestTokenMap = bestTokenMap;
	    }

	    /**
	     * Returns the result list.
	     * 
	     * @return the result list
	     */
	    public List<Token> getResultList() {
		    return resultList;
	    }

	    /**
	     * Returns the current frame number.
	     * 
	     * @return the current frame number
	     */
	    public int getCurrentFrameNumber() {
		    return currentFrameNumber;
	    }

	    /**
	     * Returns the Timer for growing.
	     * 
	     * @return the Timer for growing
	     */
	    public Timer getGrowTimer() {
		    return growTimer;
	    }

	    /**
	     * Returns the tokensCreated StatisticsVariable.
	     * 
	     * @return the tokensCreated StatisticsVariable.
	     */
	    public StatisticsVariable getTokensCreated() {
		    return tokensCreated;
	    }

	    /*
	     * (non-Javadoc) 
	     * @see edu.cmu.sphinx.decoder.search.SearchManager#allocate()
	     */
	    //@Override
	    public override void allocate() {
		    totalTokensScored = StatisticsVariable
				    .getStatisticsVariable("totalTokensScored");
		    tokensPerSecond = StatisticsVariable
				    .getStatisticsVariable("tokensScoredPerSecond");
		    curTokensScored = StatisticsVariable
				    .getStatisticsVariable("curTokensScored");
		    tokensCreated = StatisticsVariable
				    .getStatisticsVariable("tokensCreated");
		    viterbiPruned = StatisticsVariable
				    .getStatisticsVariable("viterbiPruned");
		    beamPruned = StatisticsVariable.getStatisticsVariable("beamPruned");

		    try {
			    linguist.allocate();
			    pruner.allocate();
			    scorer.allocate();
		    } catch (IOException e) {
                throw new SystemException(
					    "Allocation of search manager resources failed", e);
		    }

		    scoreTimer = TimerPool.getTimer(this, "Score");
		    pruneTimer = TimerPool.getTimer(this, "Prune");
		    growTimer = TimerPool.getTimer(this, "Grow");
	    }

	    /*
	     * (non-Javadoc)
	     * 
	     * @see edu.cmu.sphinx.decoder.search.SearchManager#deallocate()
	     */
	    //@Override
	    public override void deallocate() {
		    try {
			    scorer.deallocate();
			    pruner.deallocate();
			    linguist.deallocate();
		    } catch (IOException e) {
                throw new SystemException(
					    "Deallocation of search manager resources failed", e);
		    }
	    }

	    //@Override
        // TODO: try to override java toString()?
	    public override string toString() {
		    return name;
	    }

	    // Take Spotter's result and convert into a tangible type for pruning
	    public void setSpotterResult(List<PhraseSpotterResult> result) {
		    this.spotterResult = result;
		    processSpotterResult();
	    }

	    private void processSpotterResult() {
		    /*Iterator<PhraseSpotterResult> iter = spotterResult
				    .iterator();
		    while (iter.hasNext()) {
			    spotterTimes.add(iter.next().getStartTime());
		    }*/
	        foreach (var result in spotterResult)
	        {
	            spotterTimes.Add(result.getStartTime());
	        }
	    }
    }
}
