using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using edu.cmu.sphinx.frontend.util;
using edu.cmu.sphinx.recognizer;
using edu.cmu.sphinx.util.props;
using java.util;

namespace AudioAligner.Classes.PhraseSpotter.SimplePhraseSpotter
{
    class SimplePhraseSpotter : PhraseSpotter.PraseSpotter
    {
        private List<PhraseSpotterResult> result;
	    private List<string> phrase = null;
	    private string phraseText;
	    private List<TimedData> timedData;
	    private bool isPhraseSetP = false;

	    private Recognizer recognizer;
	    private PhraseSpottingFlatLinguist linguist;
	    private NoSkipGrammar grammar;
	    private AudioFileDataSource dataSource;

	    public SimplePhraseSpotter() {

	    }

	    public SimplePhraseSpotter(string phraseSpotterConfig) {
		    ConfigurationManager cm = new ConfigurationManager(phraseSpotterConfig);
		    recognizer = (Recognizer) cm.lookup(PROP_RECOGNIZER);
		    linguist = (PhraseSpottingFlatLinguist) cm.lookup(PROP_LINGUIST);
		    grammar = (NoSkipGrammar) cm.lookup(PROP_GRAMMAR);
		    dataSource = (AudioFileDataSource) cm.lookup(PROP_AUDIO_DATA_SOURCE);
	    }

	    //@Override
	    public override void setPhrase(string phraseText) {
		    this.phraseText = phraseText;
		    this.phrase = new List<string>();
		    stringTokenizer st = new stringTokenizer(phraseText);
		    while (st.hasMoreTokens()) {
			    phrase.Add(st.nextToken());
		    }
		    grammar.setText(phraseText);
		    this.isPhraseSet = true;
	    }

	    //@Override
	    public override void setAudioDataSource(Uri audioFile) {
		    dataSource.setAudioFile(audioFile, null);
	    }

	    public void setAudioDataSource(string audioFile){
		    setAudioDataSource(new Uri("file:" + audioFile));
	    }

	
	    private void allocate() {
		    if (!isPhraseSetP) {
			    throw new Exception("Phrase to search can't be null");
		    }
		    result = new LinkedList<PhraseSpotterResult>();
		    recognizer.allocate();
	    }

	    //@Override
	    public override void deallocate() {
		    recognizer.deallocate();
	    }

	    //@Override
	    public override List<PhraseSpotterResult> getTimedResult() {
		    return result;
	    }

	    private bool isPhraseSet() {
		    if (phrase != null) {
			    return true;
		    } else {
			    return false;
		    }
	    }

	    //@Override
	    public override void startSpotting(){
		    allocate();
		    edu.cmu.sphinx.result.Result recognizedResult = recognizer.recognize();
		    string timedResult = recognizedResult.getTimedBestResult(false, true);

		    // Break the result into tokens and extract all time info from it.
		    // I guess there should be better implementations for this using the
		    // tokens
		    // used to generate this result in the first place. Guess that's why I
		    // call this a simple Phrase Spotter

		    stringTokenizer st = new stringTokenizer(timedResult);
		    //System.out.println(timedResult);
		    timedData = new List<TimedData>();

		    while (st.hasMoreTokens()) {
			    string currentToken = st.nextToken();

			    // typically this token will be like word(startTime,endTime)
			    string word = currentToken.Substring(0, currentToken.IndexOf("("));
			    string timedPart = currentToken.Substring(
					    currentToken.IndexOf("(") + 1, currentToken.IndexOf(")"));
			    string startTime = timedPart.Substring(0, timedPart.IndexOf(","));
			    string endTime = timedPart.Substring(timedPart.IndexOf(",") + 1);
			    if (word.CompareTo("<unk>") != 0) {
				    timedData.Add(new TimedData(word, float.Parse(startTime),
						    float.Parse(endTime)));
			    }
		    }

		    // Now since we have eliminated <unk> from the result in TimedData
		    // the list should look like Phrase - Phrase - Phrase ....
		    // If this is not the case, raise error.
		    Iterator<TimedData> resultIter = timedData.iterator();
		    while (resultIter.hasNext()) {
			    Iterator<string> phraseIter = phrase.iterator();
			    bool startOfPhrase = true;
			    float startTime = 0;
			    float endTime = 0;
			    while (phraseIter.hasNext()) {
				    string word = phraseIter.next();
				    if (resultIter.hasNext()) {
					    TimedData data = resultIter.next();
					    // if phrase is begining store the start time
					    if (startOfPhrase) {
						    startTime = data.getStartTime();
						    startOfPhrase = false;
					    }
					    //System.out.println(data.getText());
					    if (!(word.Equals(data.getText(), StringComparison.InvariantCultureIgnoreCase))) {
						    grammar.getInitialNode().dumpDot("./PSGraph.dot");
						    throw new Exception("Words in result don't match phrase ("
								    + word + "," + data.getText() + ")");
					    }
					    endTime = data.getEndTime();
				    } else {
					    grammar.getInitialNode().dumpDot("./PSGraph.dot");
					    throw new Exception(
							    "The recognizer for phrase spotting didn't exit gracefully.");
				    }
			    }
			    result.Add(new PhraseSpotterResult(phraseText, startTime, endTime));
		    }

	    }

	    //@Override
	    public override void newProperties(PropertySheet ps){
		    // Configure whatever property needs to be reset
	    }

	    public class TimedData {
		    public string text;
		    public float startTime;
		    public float endTime;

		    public TimedData(string text, float startTime, float endTime) {
			    this.text = text;
			    this.startTime = startTime;
			    this.endTime = endTime;
		    }

		    public float getStartTime() {
			    return startTime;
		    }

		    public float getEndTime() {
			    return endTime;
		    }

		    public string getText() {
			    return text;
		    }
	    }
    }
}
