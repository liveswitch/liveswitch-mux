using Newtonsoft.Json;
using System;

namespace FM.LiveSwitch.Mux
{
    public class VideoSegment
    {
        public Size Size { get; set; }

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
                ClientId = Recording.Connection.ClientId,
                DeviceId = Recording.Connection.DeviceId,
                UserId = Recording.Connection.UserId,
                Size = Size
            };
        }
    }
}
