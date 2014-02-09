using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using YoutubeExtractor;

namespace Test
{
    class Program
    {
        private static string PathToProject ;

        static void Main(string[] args)
        {
            PathToProject = Environment.CurrentDirectory + "/../..";

            var downloadFolderPath = PathToProject + "/downloads";

            var existingAudioFiles = Directory.GetFiles(downloadFolderPath);
            foreach (var existingAudioFile in existingAudioFiles)
            {
                Console.WriteLine(Path.GetFileNameWithoutExtension(existingAudioFile));
            }
            
            Console.WriteLine("Please write youtube id");
            var youtubeId = Console.ReadLine();
            var audioFilePath = DownloadYoutubeAudio(youtubeId);

            Console.WriteLine("Start conversion");

            var wavFilePath = ConvertFile(audioFilePath);
            Console.WriteLine("Done converting, result is available at " + wavFilePath);

            Console.WriteLine("Splitting audio file");

            Console.WriteLine("Write starting time (in ms)");
            var output = Console.ReadLine();
            var startTime = int.Parse(output);

            Console.WriteLine("Write starting time (in ms)");
            var output2 = Console.ReadLine();
            var endTime = int.Parse(output2);

            var extractFilePath = SplitAudioFile(wavFilePath, startTime, endTime);

            Console.WriteLine("Done splitting, result is available at " + extractFilePath);

            Console.WriteLine("Write the transcript");
            var transcript = Console.ReadLine();

            var pathToConfigFile = PathToProject + "/resource/config.xml";
            var pathToTranscript = PathToProject + "/resource/tempTranscript.txt";
            File.WriteAllText(pathToTranscript, transcript);
            var alignerResult = AlignTranscript(pathToConfigFile, extractFilePath, pathToTranscript);

            var alignmentResult = new AlignmentResult(alignerResult);

            Console.Write("Aligner result:");
            Console.WriteLine(string.Join(", ", alignmentResult.TimestampedWords.Select(w => w.Word + " (" + w.Start.ToString() + ")")hey what do you think about italian? for a first date? depends. Are we talking pizza or pasta?));

            Console.ReadLine();
        }

        private static string AlignTranscript(string pathToConfig, string audioFilePath, string transcriptFilePath)
        {
            // cmd line: java.exe -jar aligner.jar pathToConfig pathToAudio pathToTranscript

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var javaPath = PathToProject + "/resource/aligner/java.exe";
            var alignerJarPath = PathToProject + "/resource/aligner/aligner.jar";

            
            var psi = new ProcessStartInfo();
            psi.FileName = javaPath;
            psi.Arguments = string.Format(@"-jar ""{0}"" ""{1}"" ""{2}"" ""{3}""", alignerJarPath, pathToConfig, audioFilePath, transcriptFilePath);
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
                    //Console.WriteLine(outString);
                    Console.WriteLine(errString);
                    //byte[] fileBytes = File.ReadAllBytes(outputFilePath);
                    /*if (fileBytes.Length > 0)
                    {
                        this._sSystem.SaveOutputFile(
                            fileBytes,
                            tmpName.Substring(tmpName.LastIndexOf("\\") + 1),
                            taskID
                            );
                    }*/
                    stopWatch.Stop();
                    Console.WriteLine("Aligning done in " + stopWatch.Elapsed);
                    return outString;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return "";
            }
        }

        private static string DownloadYoutubeAudio(string youtubeId)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            
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
            var pathToAudioFile = Path.Combine(downloadFolderPath, youtubeId + video.AudioExtension);

            if (!File.Exists(pathToAudioFile))
            {
                var audioDownloader = new AudioDownloader(video, pathToAudioFile);

                // Register the progress events. We treat the download progress as 85% of the progress and the extraction progress only as 15% of the progress,
                // because the download will take much longer than the audio extraction.
                //audioDownloader.DownloadStarted += (sender, args) => Console.WriteLine("Download started");
                //audioDownloader.DownloadFinished += (sender, arguments) => Console.WriteLine("Download finsihed");
                audioDownloader.DownloadProgressChanged += AudioDownloaderOnDownloadProgressChanged;

                /*
                 * Execute the audio downloader.
                 * For GUI applications note, that this method runs synchronously.
                 */
                audioDownloader.Execute();

                stopWatch.Stop();
                Console.WriteLine("Download done in " + stopWatch.Elapsed); 
            }

            return pathToAudioFile;
        }

        private static int lastPercent = 0;
        private static void AudioDownloaderOnDownloadProgressChanged(object sender, ProgressEventArgs progressEventArgs)
        {
            var percent = (int)progressEventArgs.ProgressPercentage;
            if (percent > lastPercent)
            {
                Console.WriteLine("Download at " + percent + "%");
                lastPercent = percent;
            }
        }

        private static string GetYoutubeLink(string youtubeId)
        {
            return string.Format("http://www.youtube.com/watch?v={0}", youtubeId);
        }

        private static string SplitAudioFile(string inputFilePath, int extractStartInMs, int extractEndInMs)
        {
            //ffmpeg -i your_audio_file.mp3 -acodec copy -t 00:30:00 -ss 00:00:00 half_hour_split.mp3
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var ffmpegPath = PathToProject + "/resource/ffmpeg/ffmpeg.exe";

            var filename = Path.GetFileNameWithoutExtension(inputFilePath);
            var outputFileName = filename + "_" + extractStartInMs + "_" + extractEndInMs;

            var outputFilePath = Path.GetDirectoryName(inputFilePath) + "/" + outputFileName + "." +
                                 Path.GetExtension(inputFilePath);

            File.Delete(outputFilePath);

            var startTime = new TimeSpan(0, 0, 0, 0, extractStartInMs).ToString();
            var duration = new TimeSpan(0, 0, 0, 0, (extractEndInMs - extractStartInMs)).ToString();

            var psi = new ProcessStartInfo();
            psi.FileName = ffmpegPath;
            psi.Arguments = string.Format(@"-i ""{0}"" -acodec copy -t {1} -ss {2} ""{3}""", inputFilePath, duration, startTime, outputFilePath);
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
                    Console.WriteLine(outString);
                    Console.WriteLine(errString);
                    //byte[] fileBytes = File.ReadAllBytes(outputFilePath);
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

            stopWatch.Stop();
            Console.WriteLine("Transcoding done in " + stopWatch.Elapsed);

            return outputFilePath;
        }

        private static string ConvertFile(string inputFilePath/*, Guid taskID*/)
        {
            // cmd line: ffmpeg -i input.mp3 -acodec pcm_s16le -ac 1 -ar 16000 output.wav

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var ffmpegPath = PathToProject + "/resource/ffmpeg/ffmpeg.exe";

            string outputFilePath = inputFilePath.Replace(".mp3", ".wav");

            // cleanup previous file
            File.Delete(outputFilePath);
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
                    //Console.WriteLine(outString);
                    Console.WriteLine(errString);
                    //byte[] fileBytes = File.ReadAllBytes(outputFilePath);
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

            stopWatch.Stop();
            Console.WriteLine("Transcoding done in " + stopWatch.Elapsed);

            return outputFilePath;
        }
    }
}
