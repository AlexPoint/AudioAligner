using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var pathToAudioFile = "";
            var pathToTranscriptFile = "";
            Aligner aligner = new Aligner("./src/config.xml",	Args[0], Args[1]);	
		    //Aligner aligner = new Aligner("./src/config.xml",	relativePathToAudio, relativePathToTranscript);
		
		    aligner.setAddOutOfGrammarBranchProperty("true");
		    aligner.allowDeletions();
		    aligner.setNumGrammarJumps(2);
		    aligner.allowBackwardJumps();
		
		    aligner.setForwardJumpProbability(0.12);
		    aligner.setBackwardJumpProbability(0.001);
		    //BufferedReader reader = new BufferedReader(new FileReader("./result.txt"));
		    String ref = aligner.align();
		    System.out.println(ref);
        }
    }
}
