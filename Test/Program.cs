using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AudioAligner.Classes.AudioAligner;
using YoutubeExtractor;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var pathToProject = Environment.CurrentDirectory + "/../..";
            /*var pathToAudioFile = pathToProject + "/resource/wav/dedication.wav";
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
		    Console.WriteLine(result);*/


            var downloadFolderPath = pathToProject + "/downloads";
            // Louis CK video
            var youtubeId = "Y8ynUspj4c8";
            var youtubeLink = GetYoutubeLink(youtubeId);

            IEnumerable<VideoInfo> videoInfos = DownloadUrlResolver.GetDownloadUrls(youtubeLink);

            /*
            * We want the first extractable video with the highest audio quality.
            */
            VideoInfo video = videoInfos
                .Where(info => info.CanExtractAudio)
                .OrderByDescending(info => info.AudioBitrate)
                .First();

            /*
             * Create the audio downloader.
             * The first argument is the video where the audio should be extracted from.
             * The second argument is the path to save the audio file.
             */
            var audioDownloader = new AudioDownloader(video, Path.Combine(downloadFolderPath, video.Title + video.AudioExtension));

            // Register the progress events. We treat the download progress as 85% of the progress and the extraction progress only as 15% of the progress,
            // because the download will take much longer than the audio extraction.
            audioDownloader.DownloadProgressChanged += (sender, arguments) => Console.WriteLine(arguments.ProgressPercentage * 0.85);
            audioDownloader.AudioExtractionProgressChanged += (sender, arguments) => Console.WriteLine(85 + arguments.ProgressPercentage * 0.15);

            /*
             * Execute the audio downloader.
             * For GUI applications note, that this method runs synchronously.
             */
            audioDownloader.Execute();

            Console.WriteLine("Download ok");
            Console.ReadLine();
        }

        private static string GetYoutubeLink(string youtubeId)
        {
            return string.Format("http://www.youtube.com/watch?v={0}", youtubeId);
        }
    }
}
