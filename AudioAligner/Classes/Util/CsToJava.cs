using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using edu.cmu.sphinx.decoder.search;

namespace AudioAligner.Classes.Util
{
    public static class CsToJava
    {

        public static java.util.List ConvertToJList(List<Token> tokens)
        {
            var list = new java.util.LinkedList();
            foreach (var token in tokens)
            {
                list.add(token);
            }
            return list;
        }
    }
}
