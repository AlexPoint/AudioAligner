using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AudioAligner.Classes.Util
{
    class StringErrorGenerator
    {
        private string text = null;
	    private double wer = 0.03;
	    private double ir = 0.01; // insertion rate (default= 1%)
	    private double dr = 0.01; // deletion rate (default= 1%)
	    private double sr = 0.01; // substitution rate (default= 1%)
	    private int numSubstitutions;
	    private int numInsertions;
	    private int numDeletions;
	    private Random rand;
	    private List<string> wordsToInsert; // Contains words that will be inserted
	    // or substituted.

	    private int numWords = 0; // total number of words in the text
	    private List<Word> words;
	    private Uri pathToWordFile ;

	    public StringErrorGenerator(){
		    this.pathToWordFile = new Uri("file:./resource/models/wordFile.txt");
	    }

	    /*
	     * Divides the input word error rate equally into insertions, deletions and
	     * substitution rates.
	     */
	    public StringErrorGenerator(double wer, string text): this(){
		    this.wer = wer;
		    this.ir = wer / 3;
		    this.dr = wer / 3;
		    this.sr = wer / 3;
		    this.text = text;
	    }

	    // intialise un-equal error rates
	    public StringErrorGenerator(double ir, double dr, double sr, string text): this(){
		    this.wer = ir + dr + sr;
		    this.ir = ir;
		    this.dr = dr;
		    this.sr = sr;
		    this.text = text;
	    }
	
	    // set Text to be corrupted
	    public void setText(string text) {
		    this.text = text;
	    }

	    /*
	     * Allocates Error Generator by assigning text dependent variables. Throws
	     * error when text is not set
	     */
	    public void process(){
		    rand = new Random();
		    textToWordList();

            wordsToInsert = new List<string>();
	        var lines = File.ReadAllLines(pathToWordFile.AbsolutePath);
	        foreach (var line in lines)
	        {
	            wordsToInsert.Add(line);
	        }
		    // Load words to inserted from word file
		    /*BufferedReader reader = new BufferedReader(new InputStreamReader(
				    pathToWordFile.openStream()));
		    string line;
	        while ((line = reader.readLine()) != null) {
			    wordsToInsert.Add(line);
		    }*/
		    // Check for compatible word error rates
		    check_compatible();
		
		    // make errors
		    processDeletions();
		    processInsertions();
		    processSubstitution();
		    printErrorStats();
	    }
	
	    // Throws error if error rates exceed acceptable bounds
	    private void check_compatible() {
		    if( wer > 1.0 || wer < 0) {
			    throw new Exception("Error: wer should be between 0 and 1.0");
		    } else if (ir > 1.0 || ir < 0 ||
				       dr >1.0  || dr < 0 ||
				       sr >1.0  || sr < 0) {
			    throw new Exception("Error: insertion/deletion/substitution rates must be b/w 0 and 1.0");
		    }
		
	    }

	    private void processSubstitution() {
		    Double numSubs = sr * numWords;
		    numSubstitutions = (int) numSubs;
		    int substitutionCount = 0;
		    int currIndex = 0;
		    Iterator<Word> iter = words.listIterator(0);
		
		    // while number of substitution is less than total number of required
		    // substitutioniterate over the list and substitute word at random
		    // locations with another one.
		    while (substitutionCount < numSubstitutions) {
			    if (currIndex < words.Count) {
				    double random = rand.nextGaussian();
				    if (random <= sr && random >= -sr && 
						    words[currIndex].getFlag().CompareTo("")== 0) {
					    // Substitute a word here
					    Word currWord = words[currIndex];
					    words[currIndex].substitute();					
					    string wordToInsert= wordsToInsert[rand.Next(wordsToInsert.Count)];
					    Word word = new Word(wordToInsert,currWord.getStartTime() , 
							    currWord.getEndTime(), 0.01);
					    word.substituteWord();
					    words.Add(currIndex,word);
					    iter = words.listIterator(currIndex);
					    substitutionCount++;
					    currIndex--;
				    }
				    currIndex++;
			    } else {
				    // if current index has exceeded the total number of words,
				    // start over again
				    iter = words.listIterator(0);
				    currIndex = 0;
			    }
		    }
	    }

	    /*
	     * Deletes words from random locations such that the total number of
	     * deletions equals the specified number.
	     */
	    private void processDeletions() {
		    Double numDel = dr * numWords;
		    numDeletions = (int)numDel;
		    int deletionCount = 0;
		    int currIndex = 0;
		    Iterator<Word> iter = words.listIterator(0);
		    // while number of deletions is less than total number of required
		    // deletions
		    // iterate over the list and delete word from random locations.
		    while (deletionCount < numDeletions) {
			    if (currIndex < words.Count) {
				    double random = rand.nextGaussian();
				    if (random <= dr && random >= -dr &&
						    words[currIndex].getFlag().CompareTo("")== 0) {
					
					    // Delete word from here
					
					    words[currIndex].delete();
					    iter = words.listIterator(currIndex);
					    deletionCount++;
					    currIndex--;
				    }
				    currIndex++;
			    } else {
				    // if current index has exceeded the total number of words,
				    // start over again
				    iter = words.listIterator(0);
				    currIndex = 0;
			    }
		    }
	    }

	    /*
	     * Inserts new words at random locations such that the total number of
	     * insertions equals the specified number.
	     */
	    private void processInsertions() {
		    Double numIns = ir * numWords;
		    numInsertions = (int)numIns;
		    int insertionCount = 0;
		    int currIndex = 0;
		    Iterator<Word> iter = words.iterator();
		    // while number of insertions is less than total number of required
		    // insertions iterate over the list and insert random word at random
		    // locations.
		    while (insertionCount < numInsertions) {
			    if (currIndex < words.Count) {
				    double random = rand.nextGaussian();
				    if (random <= ir && random >= -ir &&
						    words[currIndex].getFlag().CompareTo("")==0) {
					    // Insert a new word here
					    string wordToInsert= wordsToInsert[rand.Next(wordsToInsert.Count)];
					    Word word = new Word(wordToInsert);
					    word.insert();
					    words.Add(currIndex, word );
					    iter = words.listIterator(currIndex);
					    insertionCount = insertionCount + 1;
				    }
				    iter.next();
				    currIndex++;
			    } else {
				    // if current index has exceeded the total number of words,
				    // start over again
				    iter = words.listIterator(0);
				    currIndex = 0;
			    }
		    }
	    }
	    public string getTranscription() {
		    return wordListTostring();
	    }
	
	    public List<Word> getWordList() {
		    return words;
	    }
	
	    private void textToWordList(){
		    if (text != null) {
			    string[] wordTokens = text.Split(' ');
			    words = new List<Word>();
			    for (int i = 0; i < wordTokens.Length; i++) {
				    if (wordTokens[i].CompareTo("") != 0) {
					    //words.add(new Word(wordTokens[i]));			
					    words.Add(new Word(wordTokens[i], 0.0, 0.0, 0.0));
				    }
			    }
			    numWords = words.Count;
		    } else {
			    throw new Exception("ERROR: Can not allocate on a <null> text. ");
		    }
	    }
	
	    private string wordListTostring() {
		    //ListIterator<Word> iter = words.listIterator();
		    string result="";
		    /*while(iter.hasNext()){
			    Word nextTok = iter.next();
			    if(!(nextTok.isDeleted() || nextTok.isSubstituted()))
			    result = result.concat(nextTok.getWord()+" ");
		    }*/
	        foreach (var word in words)
	        {
	            if (!(word.isDeleted() || word.isSubstituted()))
	            {
	                result += word.getWord() + " ";
	            }
	        }
		    return result;
	    }

	    public void printErrorStats() {
		    Console.WriteLine("================== ERROR GENERATOR STATS ======================");
		    //System.out.println("================== ERROR GENERATOR STATS ======================");
		    Console.WriteLine("Total Number Of Insertions Made:        "+ numInsertions);
		    //System.out.println("Total Number Of Insertions Made:        "+ numInsertions);
		    Console.WriteLine("Total Number Of Deletions Made:         "+ numDeletions);
		    //System.out.println("Total Number Of Deletions Made:         "+ numDeletions);
		    Console.WriteLine("Total Number Of Substitutions Made:     "+ numSubstitutions);
		    //System.out.println("Total Number Of Substitutions Made:     "+ numSubstitutions);
		    double totalErr = (double)(numDeletions+numSubstitutions+numInsertions)/(double)numWords;
		    string WER = totalErr.ToString();
		    if(WER.Length > 6)
			    WER = WER.Substring(0, WER.IndexOf(".")+4);
		    Console.WriteLine("WER Introduced:                         "+WER);
		    //System.out.println("WER Introduced:                         "+WER);
		    //System.out.println("--------ERROR GENERATOR STATS END------");
	    }
    }
}
