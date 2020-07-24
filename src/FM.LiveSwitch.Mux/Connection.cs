using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FM.LiveSwitch.Mux
{
    public class Connection
    {
        public string Id { get; private set; }
        
        [JsonIgnore]
        public string ClientId { get; private set; }

        [JsonIgnore]
        public string DeviceId { get; private set; }

        [JsonIgnore]
        public string UserId { get; private set; }

        [JsonIgnore]
        public string ChannelId { get; private set; }

        [JsonIgnore]
        public string ApplicationId { get; private set; }

        public DateTime? StartTimestamp { get; private set; }

        public DateTime? StopTimestamp { get; private set; }

        [JsonIgnore]
        public VideoSegment[] CompletedVideoSegments { get { return CompletedRecordings.Where(x => x.VideoSegments != null).SelectMany(x => x.VideoSegments).OrderBy(x => x.StartTimestamp).ToArray(); } }

        [JsonIgnore]
        public Recording ActiveRecording { get; private set; }

        [JsonProperty("recordings")]
        public Recording[] CompletedRecordings { get { return _Recordings.OrderBy(x => x.StartTimestamp).ToArray(); } }

        [JsonIgnore]
        public bool Active { get { return ActiveRecording != null; } }

        [JsonIgnore]
        public bool Completed { get { return ActiveRecording == null && CompletedRecordings.Length > 0; } }

        private List<Recording> _Recordings = new List<Recording>();

        [JsonIgnore]
        public Client Client { get; set; }

        public Connection(string id, string clientId, string deviceId, string userId, string channelId, string applicationId)
        {
            Id = id;
            ClientId = clientId;
            DeviceId = deviceId;
            UserId = userId;
            ChannelId = channelId;
            ApplicationId = applicationId;
        }

        public bool ProcessLogEntry(LogEntry logEntry)
        {
            if (logEntry.Type == LogEntry.TypeStartRecording)
            {
                if (ActiveRecording != null)
                {
                    // already recording, shouldn't happen
                    return false;
                }

                ActiveRecording = new Recording
                {
                    Connection = this,
                    StartTimestamp = logEntry.Timestamp,
                    LogFile = logEntry.FilePath
                };

                if (StartTimestamp == null)
                {
                    StartTimestamp = logEntry.Timestamp;
                }

                ActiveRecording.Update(logEntry);
            }
            else if (logEntry.Type == LogEntry.TypeUpdateRecording)
            {
                if (ActiveRecording == null)
                {
                    // not recording, shouldn't happen
                    return false;
                }

                ActiveRecording.Update(logEntry);
            }
            else if (logEntry.Type == LogEntry.TypeStopRecording)
            {
                if (ActiveRecording == null)
                {
                    // not recording, shouldn't happen
                    return false;
                }

                ActiveRecording.Update(logEntry, true);

                ActiveRecording.StopTimestamp = logEntry.Timestamp;
                ActiveRecording.AudioFile = logEntry.Data?.AudioFile;
                ActiveRecording.VideoFile = logEntry.Data?.VideoFile;
                ActiveRecording.VideoDelay = logEntry.Data?.VideoDelay ?? 0D;

                if (ActiveRecording.AudioFile != null)
                {
                    ActiveRecording.AudioStartTimestamp = logEntry.Data?.AudioFirstFrameTimestamp ?? ActiveRecording.StartTimestamp;
                    ActiveRecording.AudioStopTimestamp = logEntry.Data?.AudioLastFrameTimestamp ?? ActiveRecording.StopTimestamp;
                }

                if (ActiveRecording.VideoFile != null)
                {
                    ActiveRecording.VideoStartTimestamp = logEntry.Data?.VideoFirstFrameTimestamp ?? ActiveRecording.StartTimestamp;
                    ActiveRecording.VideoStopTimestamp = logEntry.Data?.VideoLastFrameTimestamp ?? ActiveRecording.StopTimestamp;
                }

                _Recordings.Add(ActiveRecording);
                ActiveRecording = null;

                StopTimestamp = logEntry.Timestamp;
            }
            return true;
        }

        public LayoutInput GetLayoutInput()
        {
            return new LayoutInput
            {
                ConnectionId = Id,
                ClientId = ClientId,
                DeviceId = DeviceId,
                UserId = UserId,
                Size = new Size(CompletedRecordings.SelectMany(x => x.VideoSegments).Max(x => x.Size.Width), CompletedRecordings.SelectMany(x => x.VideoSegments).Max(x => x.Size.Height))
            };
        }
    }
}
