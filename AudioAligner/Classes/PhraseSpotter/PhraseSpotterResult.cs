using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AudioAligner.Classes.PhraseSpotter
{
    class PhraseSpotterResult
    {
        private string phraseText;
	    private float startTime;
	    private float endTime;
	    private List<string> phrase;
		
	    public PhraseSpotterResult() {
		
	    }
	
	    public PhraseSpotterResult(string phraseText, float startTime, float endTime) {
		    this.phraseText = phraseText;
		    this.startTime = startTime;
		    this.endTime = endTime;
		    this.phrase = new List<string>();
		    processPhrase();
	    }
	
	    private void processPhrase()
	    {
	        var tokens = Regex.Split(phraseText, @"\W+");
            phrase.AddRange(tokens);
	        /*stringTokenizer st = new stringTokenizer(phraseText);
		    while(st.hasMoreTokens()){
			    phrase.Add(st.nextToken());
		    }*/
	    }
	
	    public float getStartTime(){
		    return startTime;
	    }
	
	    public float getEndTime(){
		    return endTime;
	    }
	
	    public void setStartTime(float time){
		    startTime = time;
	    }
	
	    public void setEndTime(float time) {
		    endTime = time;
	    }
	
	    //@Override
	    public override string ToString(){
		    return phraseText + "(" + startTime + "," + endTime + ")" ;
	    }

	
	    public int equals(PhraseSpotterResult obj) {
		    if((Math.Abs(this.getStartTime() - obj.getStartTime()) < 0.05) && 
				    (Math.Abs(this.getEndTime() - obj.getEndTime()) < 0.05)) {
			    return 0;
		    }
		
		    return 1;
	    }
	    public string getPhraseFirstWord(){
		    return phrase[0];
	    }
	
	    public string getLastWord(){
		    return phrase[phrase.Count-1];
	    }
    }
}
