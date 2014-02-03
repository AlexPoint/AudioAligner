using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AudioAligner.Classes.AudioAligner;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var pathToProject = Environment.CurrentDirectory + "/../..";
            var pathToAudioFile = pathToProject + "/resource/wav/dedication.wav";
            var pathToTranscriptFile = pathToProject + "/resource/transcription/dedication.txt";
            Aligner aligner = new Aligner("../../resource/config.xml",	pathToAudioFile, pathToTranscriptFile);	
		    //Aligner aligner = new Aligner("./src/config.xml",	relativePathToAudio, relativePathToTranscript);

            aligner.setAddOutOfGrammarBranchProperty("true");
		    aligner.allowDeletions();
		    aligner.setNumGrammarJumps(2);
		    aligner.allowBackwardJumps();
		
		    aligner.setForwardJumpProbability(0.12);
		    aligner.setBackwardJumpProbability(0.001);
		    //BufferedReader reader = new BufferedReader(new FileReader("./result.txt"));
		    string result = aligner.align();
		    Console.WriteLine(result);

            Console.ReadLine();
        }
    }
}
