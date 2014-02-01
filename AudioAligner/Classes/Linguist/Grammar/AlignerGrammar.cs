using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AudioAligner.Classes.Util;
using com.sun.corba.se.impl.encoding;
using edu.cmu.sphinx.linguist.language.grammar;
using edu.cmu.sphinx.util;
using ikvm.extensions;
using edu.cmu.sphinx.util.props;
using Dictionary = edu.cmu.sphinx.linguist.dictionary.Dictionary;

namespace AudioAligner.Classes.Linguist.Grammar
{
    class AlignerGrammar : edu.cmu.sphinx.linguist.language.grammar.Grammar
    {
        //@S4Component(type = LogMath.class)
	    public const string PROP_LOG_MATH = "logMath";
	    private LogMath logMath;
	    private int start;

	    private bool modelRepetitions = false;

	    private bool modelDeletions = false;
	    private bool modelBackwardJumps = false;

	    private double selfLoopProbability = 0.0;
	    private double backwardTransitionProbability = 0.0;
	    private double forwardJumpProbability = 0.0;
	    private int numAllowedWordJumps;

	    protected GrammarNode finalNode;
	    private readonly List<string> tokens = new List<string>();

	    public AlignerGrammar(string text, LogMath logMath, bool showGrammar, bool optimizeGrammar, bool addSilenceWords, 
            bool addFillerWords, Dictionary dictionary) : base(showGrammar, optimizeGrammar, addSilenceWords, addFillerWords, dictionary){
		    this.logMath = logMath;
		    setText(text);
	    }

	    public AlignerGrammar() {

	    }

	    /*
	     * Reads Text and converts it into a list of tokens
	     */
	    public void setText(string text) {
		    string word;
		    try {
			    ExtendedStreamTokenizer tok = new ExtendedStreamTokenizer(
					    new java.io.StringReader(text), true);

			    tokens.Clear();
			    while (!tok.isEOF()) {
				    while ((word = tok.getString()) != null) {
					    word = word.toLowerCase();
					    tokens.Add(word);
				    }
			    }
		    } catch (Exception e) {
			    e.printStackTrace();
		    }
	    }
	
	    public void allowDeletions(bool modelDeletions){
		    this.modelDeletions = modelDeletions;
	    }
	
	    public void allowRepetions(bool modelRepetitions){
		    this.modelRepetitions = modelRepetitions; 
	    }
	
	    public void allowBackwardJumps(bool modelBackwardJumps){
		    this.modelBackwardJumps = modelBackwardJumps;
	    }

	    //@Override
	    public override void newProperties(PropertySheet ps){
		    base.newProperties(ps);
		    logMath = (LogMath) ps.getComponent(PROP_LOG_MATH);
	    }

	    public void setSelfLoopProbability(double prob) {
		    selfLoopProbability = prob;
	    }

	    public void setBackWardTransitionProbability(double prob) {
		    backwardTransitionProbability = prob;
	    }

	    public void setForwardJumpProbability(double prob) {

		    forwardJumpProbability = prob;
	    }

	    public void setNumAllowedGrammarJumps(int n) {
		    if (n >= 0) {
			    numAllowedWordJumps = n;
		    }
	    }

	    //@Override
        protected override GrammarNode createGrammar(){

		    //logger.info("Creating Grammar");
		    initialNode = createGrammarNode(Constants.SILENCE_SPELLING);
		    finalNode = createGrammarNode(Constants.SILENCE_SPELLING);
		    finalNode.setFinalNode(true);
		    GrammarNode branchNode = createGrammarNode(false);

		    List<GrammarNode> wordGrammarNodes = new List<GrammarNode>();
		    int end = tokens.Count;

		    //logger.info("Creating Grammar nodes");
		    foreach (var word in tokens.Take(end)) {
			    GrammarNode wordNode = createGrammarNode(word.toLowerCase());
			    wordGrammarNodes.Add(wordNode);
		    }
		    //logger.info("Done creating grammar node");
		
		    // now connect all the GrammarNodes together
		    initialNode.add(branchNode, LogMath.getLogOne());
		
		    createBaseGrammar(wordGrammarNodes, branchNode, finalNode);
		
		    if (modelDeletions) {
			    addForwardJumps(wordGrammarNodes, branchNode, finalNode);
		    }
		    if (modelBackwardJumps) {
			    addBackwardJumps(wordGrammarNodes, branchNode, finalNode);
		    }
		    if (modelRepetitions) {
			    addSelfLoops(wordGrammarNodes);
		    }
		    //logger.info("Done making Grammar");
		    //initialNode.dumpDot("./graph.dot");
		    return initialNode;
	    }

	    private void addSelfLoops(List<GrammarNode> wordGrammarNodes) {
		    /*ListIterator<GrammarNode> iter = wordGrammarNodes.listIterator();
		    while (iter.hasNext()) {
			    GrammarNode currNode = iter.next();
			    currNode.add(currNode, logMath.linearToLog(selfLoopProbability));
		    }*/
	        foreach (var wordGrammarNode in wordGrammarNodes)
	        {
	            wordGrammarNode.add(wordGrammarNode, logMath.linearToLog(selfLoopProbability));
	        }
	    }

	    private void addBackwardJumps(List<GrammarNode> wordGrammarNodes, GrammarNode branchNode, GrammarNode finalNode2) {
		    GrammarNode currNode;
		    for (int i = 0; i < wordGrammarNodes.Count; i++) {
			    currNode = wordGrammarNodes[i];
			    for (int j = Math.Max(i - numAllowedWordJumps - 1, 0); j < i - 1; j++) {
				    GrammarNode jumpToNode = wordGrammarNodes[j];
				    currNode.add(
						    jumpToNode,
						    logMath.linearToLog(backwardTransitionProbability));
			    }
		    }
	    }

	    private void addForwardJumps(List<GrammarNode> wordGrammarNodes,
			    GrammarNode branchNode, GrammarNode finalNode) {
		    GrammarNode currNode = branchNode;
		    for (int i = -1; i < wordGrammarNodes.Count; i++) {
			    if (i > -1) {
				    currNode = wordGrammarNodes[i];
			    }
			    for (int j = i + 2; j < Math.Min(wordGrammarNodes.Count, i + numAllowedWordJumps + 1); j++) {
				    GrammarNode jumpNode = wordGrammarNodes[j];
				    currNode.add(
						    jumpNode,
						    logMath.linearToLog(forwardJumpProbability));
			    }
		    }
		    for (int i = wordGrammarNodes.Count - numAllowedWordJumps - 1; i < wordGrammarNodes.Count - 1; i++) {
			    int j = wordGrammarNodes.Count;
			    currNode = wordGrammarNodes[i];
			    currNode.add(finalNode, logMath.linearToLog((float)forwardJumpProbability * Math.Pow(Math.E, j - i)));
		    }

	    }

	    private void createBaseGrammar(List<GrammarNode> wordGrammarNodes,
			    GrammarNode branchNode, GrammarNode finalNode) {
            branchNode.add(wordGrammarNodes[0], LogMath.getLogOne());
		    for (var i = 0; i < wordGrammarNodes.Count - 1; i++)
	        {
	            var currNode = wordGrammarNodes[i];
	            var nextNode = wordGrammarNodes[i + 1];
                currNode.add(nextNode, LogMath.getLogOne());
	        }
	        var lastNode = wordGrammarNodes[wordGrammarNodes.Count - 1];
            lastNode.add(finalNode, LogMath.getLogOne());
            /*GrammarNode currNode = branchNode;
                * ListIterator<GrammarNode> iter = wordGrammarNodes.listIterator();
            while (iter.hasNext()) {
                GrammarNode nextNode = iter.next();
                currNode.add(nextNode, logMath.getLogOne());
                currNode = nextNode;
            }
            currNode.add(finalNode, logMath.getLogOne());*/
		}

    }
}
