using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Test
{
    /// <summary>
    /// The audio aligner jar returns strings like:
    /// # --------------- Summary statistics ---------
    /// Total Time Audio: 6,22s  Proc: 0,50s  Speed: 0,08 X real time
    /// for(1.0176871,1.2072562) those(1.2072562,1.5863945) who(1.5863945,1.6662132) protect(1.6662132,2.1351473) wild(2.1351473,2.4943311) places(2.4943311,3.1528344) and(3.4222221,3.601814) to(3.601814,3.6916099) the(3.6916099,3.8013606) snowman(3.8013606,4.3900228) that(4.3900228,4.5496597) lives(4.5496597,4.898866) in(4.898866,5.038549) every(5.038549,5.317914) childs(5.317914,5.7170067) heart(5.7170067,6.026304)
    /// </summary>
    public class AlignmentResult
    {
        public AlignmentStats Stats { get; set; }
        public List<TimestampedWord> TimestampedWords { get; set; }

        public AlignmentResult(string alignmentResult)
        {
            this.Stats = new AlignmentStats(alignmentResult);
            this.TimestampedWords = SplitInTimestampedWords(alignmentResult);
        }

        private List<TimestampedWord> SplitInTimestampedWords(string line)
        {
            var words = new List<TimestampedWord>();

            var wordRegex = @"\b\w+\([0-9\.]+,[0-9\.]+\)";
            var matches = Regex.Matches(line, wordRegex);
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var word = new TimestampedWord(match.Value);
                words.Add(word);
            }

            return words;
        }
    }

    public class AlignmentStats
    {
        public int AudioLengthInMs { get; set; }
        public int TimeToProcessInMs { get; set; }

        public AlignmentStats(string statsLine)
        {
            var alignmentTimeRegex = "Total Time Audio: ([0-9,]+)s";
            this.AudioLengthInMs = ExtractTimeInMs(alignmentTimeRegex, statsLine);

            var processTimeRegex = "Proc: ([0-9,]+)s";
            this.TimeToProcessInMs = ExtractTimeInMs(processTimeRegex, statsLine);
        }

        private int ExtractTimeInMs(string regex, string input)
        {
            var match = Regex.Match(input, regex);
            if (match.Success)
            {
                if (match.Groups.Count > 1)
                {
                    var timeString = match.Groups[1].Value;
                    var time = float.Parse(timeString);
                    return (int) (time*1000);
                }
            }

            return -1;
        }
    }

    public class TimestampedWord
    {
        public int Start { get; set; }
        public int End { get; set; }
        public string Word { get; set; }

        public TimestampedWord(string value)
        {
            var regex = @"(\w+)\(([0-9\.]+),([0-9\.]+)\)";
            var match = Regex.Match(value, regex);
            if (match.Success)
            {
                if (match.Groups.Count > 3)
                {
                    this.Word = match.Groups[1].Value;
                    this.Start = (int) (float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)*1000);
                    this.End = (int)(float.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) * 1000);
                }
            }
        }

    }
}
