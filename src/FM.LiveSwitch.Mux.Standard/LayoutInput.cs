using System.Collections.Generic;

namespace FM.LiveSwitch.Mux
{
    public class LayoutInput
    {
        public string ConnectionId { get; set; }

        public string ConnectionTag { get; set; }

        public string ClientId { get; set; }

        public string DeviceId { get; set; }

        public string UserId { get; set; }

        public Size Size { get; set; }

        public bool AudioMuted { get; set; }

        public bool VideoMuted { get; set; }

        public bool AudioDisabled { get; set; }

        public bool VideoDisabled { get; set; }

        public string AudioContent { get; set; }

        public string VideoContent { get; set; }

        public dynamic ToDynamic()
        {
            return new
            {
                connectionId = ConnectionId,
                connectionTag = ConnectionTag,
                clientId = ClientId,
                deviceId = DeviceId,
                userId = UserId,
                size = Size.ToDynamic(),
                audioMuted = AudioMuted,
                videoMuted = VideoMuted,
                audioDisabled = AudioDisabled,
                videoDisabled = VideoDisabled,
                audioContent = AudioContent,
                videoContent = VideoContent
            };
        }

        public static LayoutInput FromDynamic(dynamic value)
        {
            if (value == null)
            {
                return null;
            }

            var lookup = (IDictionary<string, object>)value;

            var size = lookup.ContainsKey("size") ? Size.FromDynamic(value.size) as Size? : null;
            if (size == null)
            {
                return null;
            }

            return new LayoutInput
            {
                ConnectionId = lookup.ContainsKey("connectionId") ? value.connectionId as string : null,
                ConnectionTag = lookup.ContainsKey("connectionTag") ? value.connectionTag as string : null,
                ClientId = lookup.ContainsKey("clientId") ? value.clientId as string : null,
                DeviceId = lookup.ContainsKey("deviceId") ? value.deviceId as string : null,
                UserId = lookup.ContainsKey("userId") ? value.userId as string : null,
                Size = size.Value,
                AudioMuted = (lookup.ContainsKey("audioMuted") ? value.audioMuted as bool? : null) == true,
                VideoMuted = (lookup.ContainsKey("videoMuted") ? value.videoMuted as bool? : null) == true,
                AudioDisabled = (lookup.ContainsKey("audioDisabled") ? value.audioDisabled as bool? : null) == true,
                VideoDisabled = (lookup.ContainsKey("videoDisabled") ? value.videoDisabled as bool? : null) == true,
                AudioContent = lookup.ContainsKey("audioContent") ? value.audioContent as string : null,
                VideoContent = lookup.ContainsKey("videoContent") ? value.videoContent as string : null,
            };
        }
    }
}
