using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static string PathToProject ;

        static void Main(string[] args)
        {
            PathToProject = Environment.CurrentDirectory + "/../..";
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


            // Louis CK video
            var youtubeId = "Y8ynUspj4c8";
            var audioFilePath = DownloadYoutubeAudio(youtubeId);

            Console.WriteLine("Start conversion");

            ConvertFile(audioFilePath);

            Console.ReadLine();
        }

        private static string DownloadYoutubeAudio(string youtubeId)
        {
            var downloadFolderPath = PathToProject + "/downloads";
            
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
            var pathToAudioFile = Path.Combine(downloadFolderPath, video.Title + video.AudioExtension);
            var audioDownloader = new AudioDownloader(video, pathToAudioFile);

            // Register the progress events. We treat the download progress as 85% of the progress and the extraction progress only as 15% of the progress,
            // because the download will take much longer than the audio extraction.
            audioDownloader.DownloadProgressChanged += (sender, arguments) => Console.WriteLine("Download at " + arguments.ProgressPercentage + "%");
            audioDownloader.AudioExtractionProgressChanged += (sender, arguments) => Console.WriteLine("Audio extraction at " + arguments.ProgressPercentage + "%");

            /*
             * Execute the audio downloader.
             * For GUI applications note, that this method runs synchronously.
             */
            audioDownloader.Execute();

            Console.WriteLine("Download ok");

            return pathToAudioFile;
        }

        private static string GetYoutubeLink(string youtubeId)
        {
            return string.Format("http://www.youtube.com/watch?v={0}", youtubeId);
        }

        private static void ConvertFile(string inputFilePath/*, Guid taskID*/)
        {
            // cmd line: ffmpeg -i input.mp3 -acodec pcm_s16le -ac 1 -ar 16000 output.wav

            var ffmpegPath = PathToProject + "/resource/ffmpeg/ffmpeg.exe";

            string outputFilePath = inputFilePath.Replace(".mp3", ".wav");
            /*try
            {
                Process process = new Process();
                process.StartInfo.FileName = ffmpegPath;
                process.StartInfo.Arguments = string.Format(@"-i ""{0}"" -acodec pcm_s16le -ac 1 -ar 16000 ""{1}""", inputFilePath, outputFilePath);
                process.Start();
            }*/
            var psi = new ProcessStartInfo();
            psi.FileName = ffmpegPath;
            psi.Arguments = string.Format(@"-i ""{0}"" -acodec pcm_s16le -ac 1 -ar 16000 ""{1}""", inputFilePath, outputFilePath);
            psi.CreateNoWindow = true;
            psi.ErrorDialog = false;
            psi.UseShellExecute = false;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardInput = false;
            psi.RedirectStandardError = true;
            try
            {
                // Start the process with the info we specified.
                // Call WaitForExit and then the using statement will close.
                using (Process exeProcess = Process.Start(psi))
                {
                    exeProcess.PriorityClass = ProcessPriorityClass.High;
                    string outString = string.Empty;
                    // use ansynchronous reading for at least one of the streams
                    // to avoid deadlock
                    exeProcess.OutputDataReceived += (s, e) =>
                    {
                        outString += e.Data;
                    };
                    exeProcess.BeginOutputReadLine();
                    // now read the StandardError stream to the end
                    // this will cause our main thread to wait for the
                    // stream to close (which is when ffmpeg quits)
                    string errString = exeProcess.StandardError.ReadToEnd();
                    Trace.WriteLine(outString);
                    Trace.TraceError(errString);
                    byte[] fileBytes = File.ReadAllBytes(outputFilePath);
                    /*if (fileBytes.Length > 0)
                    {
                        this._sSystem.SaveOutputFile(
                            fileBytes,
                            tmpName.Substring(tmpName.LastIndexOf("\\") + 1),
                            taskID
                            );
                    }*/
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
