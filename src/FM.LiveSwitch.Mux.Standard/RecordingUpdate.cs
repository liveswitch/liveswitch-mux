using System;

namespace FM.LiveSwitch.Mux
{
    public class RecordingUpdate
    {
        public DateTime StartTimestamp { get; set; }

        public DateTime StopTimestamp { get; set; }

        public string ConnectionTag { get; set; }

        public bool AudioMuted { get; set; }

        public bool VideoMuted { get; set; }

        public bool AudioDisabled { get; set; }

        public bool VideoDisabled { get; set; }

        public string AudioContent { get; set; }

        public string VideoContent { get; set; }
    }
}
