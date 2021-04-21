using System;

namespace FM.LiveSwitch.Mux
{
    public class VideoEvent
    {
        public VideoEventType Type { get; set; }

        public DateTime Timestamp { get; set; }

        public VideoSegment Segment { get; set; }

        public VideoSegment LastSegment { get; set; }
    }
}