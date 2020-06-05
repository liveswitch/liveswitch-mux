using System;

namespace FM.LiveSwitch.Mux
{
    public class LogEntryData
    {
        public string AudioFile { get; set; }

        public string VideoFile { get; set; }

        public DateTime? AudioFirstFrameTimestamp { get; set; } // ISO-8601

        public DateTime? VideoFirstFrameTimestamp { get; set; } // ISO-8601

        public DateTime? AudioLastFrameTimestamp { get; set; } // ISO-8601

        public DateTime? VideoLastFrameTimestamp { get; set; } // ISO-8601

        public double? VideoDelay { get; set; }

        public bool IsEquivalent(LogEntryData other)
        {
            return other != null &&
                other.AudioFile == AudioFile &&
                other.VideoFile == VideoFile &&
                other.AudioFirstFrameTimestamp == AudioFirstFrameTimestamp &&
                other.VideoFirstFrameTimestamp == VideoFirstFrameTimestamp &&
                other.AudioLastFrameTimestamp == AudioLastFrameTimestamp &&
                other.VideoLastFrameTimestamp == VideoLastFrameTimestamp &&
                other.VideoDelay == VideoDelay;
        }
    }
}
