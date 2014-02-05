using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AudioAligner.Classes.Decoder.Search;
using AudioAligner.Classes.Linguist.Grammar;
using AudioAligner.Classes.PhraseSpotter;
using AudioAligner.Classes.PhraseSpotter.SimplePhraseSpotter;
using AudioAligner.Classes.Util;
using edu.cmu.sphinx.decoder.search;
using edu.cmu.sphinx.frontend.util;
using edu.cmu.sphinx.recognizer;
using edu.cmu.sphinx.result;
using edu.cmu.sphinx.util.props;
using java.io;
using java.net;
using java.util;
using Console = System.Console;

namespace AudioAligner.Classes.AudioAligner
{
    public class Aligner : IAudioAligner
    {
        private string PROP_GRAMMAR; // which grammar to use from config
	    private string PROP_RECOGNIZER; // which recognizer to use from config
	    private double PROP_FORWARD_JUMP_PROB = 0.0;
	    private double PROP_BACKWARD_JUMP_PROB = 0.0;
	    private double PROP_SELF_LOOP_PROB = 0.0;
	    private int PROP_NUM_GRAMMAR_JUMPS = 0;
	    private string PROP_AUDIO_DATA_SOURCE;
	    private bool PROP_PERFORM_SPOTTING;
	    private bool PROP_MODEL_DELETIONS;
	    private bool PROP_MODEL_REPETITIONS;
	    private bool PROP_MODEL_BACKWARDJUMPS;

	    private string absoluteBeamWidth;
	    private string relativeBeamWidth;
	    private string addOutOfGrammarBranch;
	    private string outOfGrammarProbability;
	    private string phoneInsertionProbability;

	    private ConfigurationManager cm;
	    private Recognizer recognizer;
	    private AlignerGrammar grammar;
	    private AudioFileDataSource datasource;

	    private bool optimizeP; // by default set this false

	    private string config;
	    private string psConfig;
	    private string audioFile;
	    private string textFile;
	    private string txtInTranscription;
	    private List<PhraseSpotterResult> phraseSpotterResult;

	    public Aligner(string config, string audioFile, string textFile): 
            this(config, audioFile, textFile, "recognizer", "AlignerGrammar", "audioFileDataSource"){}

	    public Aligner(string config, string audioFile, string textFile, string recognizerName, string grammarName, string audioDataSourceName):
		    this(config, audioFile, textFile, recognizerName, grammarName, "", audioDataSourceName){}

	    public Aligner(string config, string audioFile, string textFile, string recognizerName, string grammarName, string grammarType, string audioDataSourceName):
		    this(config, audioFile, textFile, recognizerName, grammarName, audioDataSourceName, true){}

	    public Aligner(string config, string audioFile, string textFile, string recognizerName, string grammarName, string audioDataSourceName, bool optimize){
		    this.config = config;
		    this.audioFile = audioFile;
		    this.textFile = textFile;
		    this.PROP_RECOGNIZER = recognizerName;
		    this.PROP_GRAMMAR = grammarName;
		    this.PROP_AUDIO_DATA_SOURCE = audioDataSourceName;
		    this.optimizeP = optimize;
		    this.PROP_PERFORM_SPOTTING = false;
		    this.PROP_MODEL_BACKWARDJUMPS = false;
		    this.PROP_MODEL_DELETIONS = false;
		    this.PROP_MODEL_REPETITIONS =  false;
		    txtInTranscription = readTranscription();
		    phraseSpotterResult = new List<PhraseSpotterResult>();

            // initializing the ConfigurationManager raises an error if com.sun.org.apache.xerces.internal.jaxp.DocumentBuilderFactoryImpl class is not loaded 
            var s = new com.sun.org.apache.xerces.@internal.jaxp.SAXParserFactoryImpl();
		    cm = new ConfigurationManager(config);
		    absoluteBeamWidth = cm.getGlobalProperty("absoluteBeamWidth");
		    relativeBeamWidth = cm.getGlobalProperty("relativeBeamWidth");
		    addOutOfGrammarBranch = cm.getGlobalProperty("addOOVBranch");
		    outOfGrammarProbability = cm
				    .getGlobalProperty("outOfGrammarProbability");
		    phoneInsertionProbability = cm
				    .getGlobalProperty("phoneInsertionProbability");
	    }

	    
	    public bool setAudio(string path) {
		    this.audioFile = path;
		    return true;
	    }

	    
	    public bool setText(string text){
		    txtInTranscription = text;
		    return true;
	    }

	    // Idea is to automate the process of selection and setting of
	    // Global properties for alignment giving hands free experience
	    // to first time users.
	    public void optimize() {

	    }

	    private void setGlobalProperties() {
		    cm.setGlobalProperty("absoluteBeamWidth", absoluteBeamWidth);

		    cm.setGlobalProperty("relativeBeamWidth", relativeBeamWidth);
		    cm.setGlobalProperty("addOOVBranch", addOutOfGrammarBranch);
		    cm.setGlobalProperty("outOfGrammarProbability", outOfGrammarProbability);
		    cm.setGlobalProperty("phoneInsertionProbability",
				    phoneInsertionProbability);
	    }

	    public void setPhraseSpottingConfig(string configFile) {
		    psConfig = configFile;
	    }

	    
	    public string align(){
		    if (PROP_PERFORM_SPOTTING) {
			    phraseSpotterResult = new List<PhraseSpotterResult>();
			    collectPhraseSpottingResult();
		    }

		    cm = new ConfigurationManager(config);
		    AlignerSearchManager sm = (AlignerSearchManager) cm
				    .lookup("searchManager");
		    sm.setSpotterResult(phraseSpotterResult);
		    optimize();
		    setGlobalProperties();
		    recognizer = (Recognizer) cm.lookup(PROP_RECOGNIZER);
		    grammar = (AlignerGrammar) cm.lookup(PROP_GRAMMAR);
		    datasource = (AudioFileDataSource) cm.lookup(PROP_AUDIO_DATA_SOURCE);
		    datasource.setAudioFile(new File(audioFile), null);
		    allocate();
		    return start_align();
	    }

	    private string start_align(){
		    // grammar.getInitialNode().dumpDot("./graph.dot");
		    Result result = recognizer.recognize();
		    string timedResult = result.getTimedBestResult(false, true);
		    Token finalToken = result.getBestFinalToken();
		    //System.out.println(result.getBestToken().getWordUnitPath());
		    deallocate();
		    return timedResult;
	    }

	    private void collectPhraseSpottingResult(){
		    StringTokenizer tok = new StringTokenizer(txtInTranscription);
		    while (tok.hasMoreTokens()) {
			    string phraseToSpot = "";
			    int iter = 0;
			    while (iter < 3 && tok.hasMoreTokens()) {
				    phraseToSpot += tok.nextToken() + " ";
				    iter++;
			    }
			    //System.out.println("\n Spotting Phrase: " + phraseToSpot);
                Console.WriteLine("\n Spotting Phrase: " + phraseToSpot);
			    try {

				    List<PhraseSpotterResult> tmpResult = phraseSpotting(phraseToSpot);
			        foreach (var result in tmpResult)
			        {
                        Console.WriteLine(result);

                        phraseSpotterResult.Add(result);
			        }
				    /*ListIterator<PhraseSpotterResult> iterator = tmpResult
						    .listIterator();
				    // System.out.println(tmpResult.size());
				    while (iterator.hasNext()) {

					    PhraseSpotterResult nextResult = iterator.next();

					    //System.out.println(nextResult);
                        Console.WriteLine(nextResult);

					    phraseSpotterResult.add(nextResult);
				    }*/
			    } catch (Exception e) {
				    /*System.out
						    .println("An unknown exception occured in phrase Spotter."
								    + " But Aligner will not stop");
				    e.printStackTrace();*/
                    Console.WriteLine("An unknown exception occured in phrase Spotter."
								    + " But Aligner will not stop. " + e);
			    }
			    //System.out.println("Skipping 5 words in transcription to select next phrase");
                Console.WriteLine("Skipping 5 words in transcription to select next phrase");
			    iter = 0;
			    while (iter < 5 && tok.hasMoreTokens()) {
				    tok.nextToken();
				    iter++;
			    }
		    }
	    }

	    private List<PhraseSpotterResult> phraseSpotting(string phrase){

		    SimplePhraseSpotter phraseSpotter = new SimplePhraseSpotter(psConfig);
		    phraseSpotter.setAudioDataSource(audioFile);
		    phraseSpotter.setPhrase(phrase);
		    //long initTime = System.currentTimeMillis();
		    phraseSpotter.startSpotting();
		    return phraseSpotter.getTimedResult();
	    }

	    private void allocate(){
		    datasource.setAudioFile(new URL("file:" + audioFile), null);

		    //System.out.println("Transcription: " + txtInTranscription);
            Console.WriteLine("Transcription: " + txtInTranscription);
		    grammar.setText(txtInTranscription);
		    grammar.allowBackwardJumps(PROP_MODEL_BACKWARDJUMPS);
		    grammar.allowDeletions(PROP_MODEL_DELETIONS);
		    grammar.allowRepetions(PROP_MODEL_REPETITIONS);
		    grammar.setBackWardTransitionProbability(PROP_BACKWARD_JUMP_PROB);
		    grammar.setForwardJumpProbability(PROP_FORWARD_JUMP_PROB);
		    grammar.setSelfLoopProbability(PROP_SELF_LOOP_PROB);
		    grammar.setNumAllowedGrammarJumps(PROP_NUM_GRAMMAR_JUMPS);

		    recognizer.allocate();
	    }

	    public void deallocate() {
		    recognizer.deallocate();
	    }

	    private string readTranscription(){
		    BufferedReader txtReader = new BufferedReader(new FileReader(textFile));
		    string line;
		    string finalText = "";
		    while ((line = txtReader.readLine()) != null) {
			    finalText += " " + line;
		    }
		    StringCustomize sc = new StringCustomize();
		    return sc.customise(finalText);
	    }

	    public void generateError(float wer){
		    StringErrorGenerator seg = new StringErrorGenerator(wer,
				    txtInTranscription);
		    seg.process();
		    string newText = seg.getTranscription();
		    setText(newText);
	    }

	    public void generateError(float ir, float dr, float sr){
		    StringErrorGenerator seg = new StringErrorGenerator(ir, dr, sr,
				    txtInTranscription);
		    seg.process();
		    string newText = seg.getTranscription();
		    setText(newText);
	    }

	    
	    public void setAbsoluteBeamWidth(string absoluteBeamWidth) {
		    this.absoluteBeamWidth = absoluteBeamWidth;

	    }

	    
	    public void setRelativeBeamWidth(string relativeBeamWidth) {
		    this.relativeBeamWidth = relativeBeamWidth;

	    }

	    
	    public void setOutOfGrammarProbability(string outOfGrammarProbability) {
		    this.outOfGrammarProbability = outOfGrammarProbability;

	    }

	    
	    public void setPhoneInsertionProbability(string phoneInsertionProbability) {
		    this.phoneInsertionProbability = phoneInsertionProbability;

	    }

	    
	    public void setForwardJumpProbability(double prob) {
		    this.PROP_FORWARD_JUMP_PROB = prob;

	    }

	    
	    public void setBackwardJumpProbability(double prob) {
		    this.PROP_BACKWARD_JUMP_PROB = prob;

	    }

	    
	    public void setSelfLoopProbability(double prob) {
		    this.PROP_SELF_LOOP_PROB = prob;

	    }

	    
	    public void setNumGrammarJumps(int n) {
		    this.PROP_NUM_GRAMMAR_JUMPS = n;
	    }

	    
	    public void performPhraseSpotting(bool doPhraseSpotting) {
		    this.PROP_PERFORM_SPOTTING = doPhraseSpotting;
	    }

	    
	    public void setAddOutOfGrammarBranchProperty(string addOutOfGrammarBranch) {
		    this.addOutOfGrammarBranch = addOutOfGrammarBranch;
	    }

	    
	    public bool allowDeletions() {
		    PROP_MODEL_DELETIONS = true;
		    return true;
	    }

	    
	    public bool allowRepetions() {
		    PROP_MODEL_REPETITIONS = true;
		    return true;
	    }

	    public bool allowBackwardJumps() {
		    PROP_MODEL_BACKWARDJUMPS = true;
		    return true;
	    }
    }
}
