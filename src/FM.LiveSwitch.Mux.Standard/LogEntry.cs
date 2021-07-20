using System;

namespace FM.LiveSwitch.Mux
{
    public class LogEntry
    {
        public static readonly string TypeStartRecording = "startRecording";
        public static readonly string TypeStopRecording = "stopRecording";
        public static readonly string TypeUpdateRecording = "updateRecording";

        public string Type { get; set; }

        public string ExternalId { get; set; }

        public string ApplicationId { get; set; }

        public string ChannelId { get; set; }

        public string UserId { get; set; }

        public string DeviceId { get; set; }

        public string ClientId { get; set; }

        public string ClientProtocol { get; set; }

        public string ConnectionId { get; set; }

        public string ApplicationConfigId { get; set; }

        public string ChannelConfigId { get; set; }

        public string ConnectionType { get; set; }

        public string Tag { get; set; }

        public DateTime Timestamp { get; set; } // ISO-8601

        public LogEntryData Data { get; set; }

        public string FilePath { get; set; }

        public bool IsEquivalent(LogEntry other)
        {
            return other != null &&
                other.Type == Type &&
                other.ExternalId == ExternalId &&
                other.ApplicationId == ApplicationId &&
                other.ChannelId == ChannelId &&
                other.ClientId == ClientId &&
                other.ClientProtocol == ClientProtocol &&
                other.ConnectionId == ConnectionId &&
                other.ConnectionType == ConnectionType &&
                other.Tag == Tag &&
                other.Timestamp == Timestamp &&
                (
                    other.Data == Data || (other.Data != null && other.Data.IsEquivalent(Data))
                );
        }
    }
}
