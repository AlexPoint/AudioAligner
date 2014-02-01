using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using com.sun.tools.javac.resources;
using edu.cmu.sphinx.decoder.search;
using edu.cmu.sphinx.linguist.flat;
using java.util;

namespace AudioAligner.Classes.Util
{
    public static class JavaToCs
    {

        public static bool ConvertBool(java.lang.Boolean jBool)
        {
            var csBool = bool.Parse(jBool.ToString());
            return csBool;
        }

        public static List<Token> GetTokenCollection(ActiveList activeList)
        {
            var tokens = new List<Token>();
            Iterator iter = activeList.iterator();
            while (iter.hasNext())
            {
                var token = (Token)iter.next();
                tokens.Add(token);
            }

            return tokens;
        }

        public static List<object> SetToCollection(Set javaSet)
        {
            var objects = new List<object>();
            var iter = javaSet.iterator();
            while (iter.hasNext())
            {
                var obj = iter.next();
                objects.Add(obj);
            }

            return objects;
        }
        public static List<SentenceHMMState> SentenceHMMStateSetToCollection(Set javaSet)
        {
            var objects = new List<SentenceHMMState>();
            var iter = javaSet.iterator();
            while (iter.hasNext())
            {
                var obj = (SentenceHMMState) iter.next();
                objects.Add(obj);
            }

            return objects;
        }
    }

    /*
     * All the constants that we cannot retrieve in sphinx4
     */
    public static class Constants
    {
        // instead of Dictionary.SILENCE_SPELLING
        public const string SILENCE_SPELLING = "<sil>";

        // instead of Linguist.PROP_WORD_INSERTION_PROBABILITY
        public const string PROP_WORD_INSERTION_PROBABILITY = "wordInsertionProbability";

        // instead of Linguist.PROP_UNIT_INSERTION_PROBABILITY
        public const string PROP_UNIT_INSERTION_PROBABILITY = "unitInsertionProbability";

        // instead of Linguist.PROP_SILENCE_INSERTION_PROBABILITY 
        public const string PROP_SILENCE_INSERTION_PROBABILITY = "silenceInsertionProbability";

        // instead of Linguist.PROP_FILLER_INSERTION_PROBABILITY
        public const string PROP_FILLER_INSERTION_PROBABILITY = "fillerInsertionProbability";

        // instead of Linguist.PROP_LANGUAGE_WEIGHT
        public const string PROP_LANGUAGE_WEIGHT = "languageWeight";

        // instead of Dictionary.SENTENCE_START_SPELLING
        public const string SENTENCE_START_SPELLING = "<s>";
    }
}
