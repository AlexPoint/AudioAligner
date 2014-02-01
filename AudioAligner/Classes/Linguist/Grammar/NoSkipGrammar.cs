using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using edu.cmu.sphinx.decoder;
using edu.cmu.sphinx.linguist.language.grammar;
using edu.cmu.sphinx.util;
using edu.cmu.sphinx.util.props;
using java.util;
using Dictionary = edu.cmu.sphinx.linguist.dictionary.Dictionary;

namespace AudioAligner.Classes.Linguist.Grammar
{
    class NoSkipGrammar: edu.cmu.sphinx.linguist.language.grammar.Grammar, ResultListener
    {
        //@S4Component(type = LogMath.class)
        public const string PROP_LOG_MATH = "logMath";
    
        protected GrammarNode finalNode;
        private LogMath logMath;

        private readonly List<string> tokens = new List<string>();

        private int start;
	    public NoSkipGrammar() {
		
	    }
	    public NoSkipGrammar(string text, LogMath logMath, bool showGrammar, bool optimizeGrammar, 
                            bool addSilenceWords, bool addFillerWords, Dictionary dictionary) :
            base(showGrammar, optimizeGrammar, addSilenceWords, addFillerWords, dictionary){
            this.logMath = logMath;
            setText(text);
	    }
	
	    public void setText(String text) {
		    StringTokenizer st = new StringTokenizer(text);
		    while(st.hasMoreTokens()){
			    string token = st.nextToken();
			    token = token.ToLower();
			    if(token.CompareTo(" ")!= 0){
				    tokens.Add(token);
			    }			
		    }
	    }
	
	    /*
	     * (non-Javadoc)
	     * We want a very strict grammar structure like the following:
	     * InitialNode ----> KW1 ---> KW2 .... ---> KWn ---> FinalNode
	     *   â†‘________________________________________________|
	     */
	    protected override GrammarNode createGrammar()
	    {
	        string silence = Dictionary.SILENCE_SPELLING;
		    initialNode = createGrammarNode(silence);
		    finalNode = createGrammarNode(silence);
		    GrammarNode lastNode = createGrammarNode(silence);
		    initialNode.add(lastNode, LogMath.getLogOne());
		    lastNode.add(initialNode, LogMath.getLogOne());
		    GrammarNode lastWordGrammarNode = initialNode;
            /*Iterator<string> iter = tokens.iterator();
		    while(iter.hasNext()){
			    GrammarNode currNode = createGrammarNode(iter.next());
			    lastWordGrammarNode.add(currNode, logMath.getLogOne());
			    lastWordGrammarNode = currNode;
			
			    // Parallel keyword topology
			    //initialNode.add(currNode, logMath.getLogOne());
			
			    //currNode.add(finalNode, logMath.getLogOne());
		    }*/
	        foreach (var token in tokens)
	        {
                GrammarNode currNode = createGrammarNode(token);
                lastWordGrammarNode.add(currNode, LogMath.getLogOne());
                lastWordGrammarNode = currNode;
	        }
		    lastWordGrammarNode.add(lastNode, LogMath.getLogOne());
		    lastNode.add(finalNode, logMath.linearToLog(0.0001));
		    finalNode.setFinalNode(true);
		    return initialNode;		
	    }
	
	
	    //@Override
	    public void newResult(edu.cmu.sphinx.result.Result result) {
		    return ;		
	    }
	
	       /*
         * (non-Javadoc)
         *
         * @see edu.cmu.sphinx.util.props.Configurable#newProperties(edu.cmu.sphinx.util.props.PropertySheet)
         */
         //@Override
         public override void newProperties(PropertySheet ps){
             base.newProperties(ps);
             logMath = (LogMath) ps.getComponent(PROP_LOG_MATH);
         }
    }
}
