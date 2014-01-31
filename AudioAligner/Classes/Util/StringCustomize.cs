using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using java.lang;
using java.util;
using String = System.String;

namespace AudioAligner.Classes.Util
{
    public class StringCustomize
    {
        HashSet<char> ignoreChar= new HashSet<char>();

	    public StringCustomize() {
		    ignoreChar.Add(' ');
	    }

	    public StringCustomize(List<char> ignoreList) {

		    // always ignore a blank space
		    ignoreChar.Add(' ');
	        foreach (var character in ignoreList)
	        {
	            ignoreChar.Add(character);
	        }
		    /*for (Iterator<Character> iter = ignoreList.iterator(); iter.hasNext();) {
			    ignoreChar.add(iter.next());
		    }*/
	    }

	    public String customise(String text) {
		    StringTokenizer st = new StringTokenizer(text);
		    List<String> wordTokens = new List<String>();
		    while (st.hasMoreTokens()) {
			    String word = st.nextToken();
			    String processedWord = process(word);
			    if (processedWord.CompareTo("") != 0) {
				    wordTokens.Add(processedWord);
			    }
		    }
		    String result = "";
		    /*for (Iterator<String> iter = wordTokens.iterator(); iter.hasNext();) {
			    result = result.concat(iter.next() + " ");
		    }*/
	        foreach (var wordToken in wordTokens)
	        {
	            result += wordToken + " ";
	        }
		    return result;
	    }

	    private String process(String word) {	
		
		    if(word.Length >= 4) {
			    if(word.Substring(0, 4).Equals("SIL_", StringComparison.InvariantCultureIgnoreCase)) {
				
				    return " ";
			    }
		    }
		    word = word.ToLower();
		    int length = word.Length;
		    String processedWord = "";
		    bool notBlank=false;
		    for (int i = 0; i < length; i++) {
			    char c = word[i];
			    if (Character.isLetter(c)|| Character.isDigit(c)) {
				
				    // if character is in [a - z], [0 - 9]
				    processedWord = string.Concat(processedWord, c.ToString());
				    notBlank=true;
			    } else {

				    // if this is a ignored character, then add it
				    if (ignoreChar.Contains(c)) {
					    string.Concat(processedWord, c.ToString());
				    }
			    }
		    }
		    if(notBlank) {
			    return processedWord;
		    }
		    else {
			    return "";
		    }
	    }
    }
}
