using System;

namespace FM.LiveSwitch.Mux.Models
{
    public class Recording
    {
        public Guid Id { get; set; }

        public Guid? AudioId { get; set; }

        public Guid? VideoId { get; set; }

        public DateTime StartTimestamp { get; set; }

        public DateTime StopTimestamp { get; set; }

        public DateTime? AudioStartTimestamp { get; set; }

        public DateTime? AudioStopTimestamp { get; set; }

        public DateTime? VideoStartTimestamp { get; set; }

        public DateTime? VideoStopTimestamp { get; set; }

        public string AudioFile { get; set; }

        public string VideoFile { get; set; }

        public string LogFile { get; set; }

        public string AudioCodec { get; set; }

        public string VideoCodec { get; set; }

        public VideoSegment[] VideoSegments { get; set; }

        public LogEntry[] LogEntries { get; set; }
    }
}
