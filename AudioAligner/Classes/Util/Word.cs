using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioAligner.Classes.Util
{
    class Word : IComparable<Word>
    {
        private  string word;
	    private double startTime;
	    private double endTime;
	    private double tolerance;
	    private string FLAG; // contains information of whether the word is inserted
						     // or deleted. 
	
	
	    public Word () :this(null,"",0.0, 0.0,0.0){}
	    public Word(string word) : this (word,"",0.0, 0.0, 0.0){}
	    public Word (string word, double startTime, double endTime, double tolerance): this(word,"", startTime, endTime, tolerance){}
	    public Word(string word, string FLAG, double startTime, double endTime, double tolerance ) {
		    this.word = word;
		    this.FLAG = FLAG;
		    this.startTime = startTime;
		    this.endTime = endTime;
		    this.tolerance = tolerance;
		    //System.out.println(word+"("+startTime+","+endTime+") ");
	    }
	
	    public void insert() {
		    //System.out.println("inserting:"+word);
		    setFlag("insert");
	    }
	
	    public void delete() {
		    //System.out.println("deleting:"+word);
		    setFlag("delete");
	    }
	
	    public void substitute() {
		    setFlag("del+substitute");
	    }
	    public void substituteWord() {
		    setFlag("ins+substitute");
	    }
	    //get functions
	    public string getWord() {
		    return word;
	    }
	    public string getFlag() {
		    return FLAG;
	    }
	
	    //set functions
	    public void setFlag(string flag) {
		    this.FLAG= flag;
	    }
	
	    public bool isInserted() {
		    if(FLAG.CompareTo("insert")==0) {
			    return true;
		    }else
			    return false;
	    }
	    public bool isDeleted() {
		    if(FLAG.CompareTo("delete")== 0) {
			    return true;
		    } else {
			    return false;
		    }
	    }
	    public bool isSubstituted() {
		    if(FLAG.Equals("del+substitute", StringComparison.InvariantCultureIgnoreCase)){
			    return true;
		    }else {
			    return false;
		    }
	    }
	    public bool isAddedAsSubstitute() {
		    if(FLAG.Equals("ins+substitute", StringComparison.InvariantCultureIgnoreCase)){
			    return true;
		    } else {
			    return false;
		    }
	    }
	
	    double getStartTime() {
		    return startTime;
	    }
	
	    double getEndTime() {
		    return endTime;
	    }
	
	    public bool isEqual(Word e) {
		    if(e.getWord().Equals("<unk>", StringComparison.InvariantCultureIgnoreCase) || 
				    this.getWord().Equals("<unk>", StringComparison.InvariantCultureIgnoreCase)){
			    return false;
		    }
		    if (e.getWord().Equals(this.getWord(), StringComparison.InvariantCultureIgnoreCase) &&
				    Math.Abs(e.getStartTime() - this.getStartTime())<=tolerance &&
				    Math.Abs(e.getEndTime()-this.getEndTime())<=tolerance) {
			    return true;
		    } else {
			    return false;
		    }
	    }
	    public bool isEqualNoTolerance(Word e) {
		    if(e.getWord().Equals(this.getWord(), StringComparison.InvariantCultureIgnoreCase)){
			    return true;
		    } else {
			    return false;
		    }
	    }
	    public bool isUnknownWord(){
		    if(this.getWord().Equals("<unk>", StringComparison.InvariantCultureIgnoreCase)){
			    return true;
		    } else {
			    return false;
		    }
	    }
	
	    // Returns 1 if not equal 
	    //@Override
	    public int CompareTo(Word arg0) {
		    if(this.isEqual(arg0)){
			    return 0;
		    } else if(this.startTime < arg0.startTime) {
			    return -1;
		    } else{
			    return 1;
		    }
	    }
    }
}
