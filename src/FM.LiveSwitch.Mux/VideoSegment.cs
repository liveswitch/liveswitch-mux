using Newtonsoft.Json;
using System;

namespace FM.LiveSwitch.Mux
{
    public class VideoSegment
    {
        public Size Size { get; set; }

        public string ConnectionTag { get; set; }

        public bool AudioMuted { get; set; }

        public bool AudioDisabled { get; set; }

        public bool VideoMuted { get; set; }

        public bool VideoDisabled { get; set; }

        public DateTime StartTimestamp { get; set; }

        public DateTime StopTimestamp { get; set; }

        [JsonIgnore]
        public TimeSpan Duration { get { return StopTimestamp - StartTimestamp; } }

        [JsonIgnore]
        public Recording Recording { get; set; }

        public LayoutInput GetLayoutInput()
        {
            return new LayoutInput
            {
                ConnectionId = Recording.Connection.Id,
                ConnectionTag = ConnectionTag,
                ClientId = Recording.Connection.ClientId,
                DeviceId = Recording.Connection.DeviceId,
                UserId = Recording.Connection.UserId,
                Size = Size,
                AudioMuted = AudioMuted,
                AudioDisabled = AudioDisabled,
                VideoMuted = VideoMuted,
                VideoDisabled = VideoDisabled
            };
        }

        public VideoSegment Clone(RecordingUpdate update = null)
        {
            return new VideoSegment
            {
                Size = Size.Clone(),
                ConnectionTag = update == null ? ConnectionTag : update.ConnectionTag,
                AudioMuted = update == null ? AudioMuted : update.AudioMuted,
                AudioDisabled = update == null ? AudioDisabled : update.AudioDisabled,
                VideoMuted = update == null ? VideoMuted : update.VideoMuted,
                VideoDisabled = update == null ? VideoDisabled : update.VideoDisabled,
                StartTimestamp = StartTimestamp,
                StopTimestamp = StopTimestamp,
                Recording = Recording
            };
        }
    }
}
