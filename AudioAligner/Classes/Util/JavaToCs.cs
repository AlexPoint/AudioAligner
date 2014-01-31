using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.sun.tools.javac.resources;
using edu.cmu.sphinx.decoder.search;
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
    }
}
