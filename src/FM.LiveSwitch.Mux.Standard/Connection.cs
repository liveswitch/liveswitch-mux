using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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
        public Recording ActiveRecording { get; private set; }

        [JsonProperty("recordings")]
        public Recording[] CompletedRecordings { get { return _Recordings.OrderBy(x => x.StartTimestamp).ToArray(); } }

        [JsonIgnore]
        public bool Active { get { return ActiveRecording != null; } }

        [JsonIgnore]
        public bool Completed { get { return ActiveRecording == null && CompletedRecordings.Length > 0; } }

        private readonly List<Recording> _Recordings = new List<Recording>();

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

        public bool ProcessLogEntry(LogEntry logEntry, MuxOptions options)
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

                if (ActiveRecording.AudioFile != null)
                {
                    if (logEntry.Data?.AudioFirstFrameTimestamp != null && logEntry.Data?.AudioLastFrameTimestamp != null)
                    {
                        ActiveRecording.AudioStartTimestamp = logEntry.Data?.AudioFirstFrameTimestamp ?? ActiveRecording.StartTimestamp;
                        ActiveRecording.AudioStopTimestamp = logEntry.Data?.AudioLastFrameTimestamp ?? ActiveRecording.StopTimestamp;
                    }
                    else
                    {
                        ActiveRecording.AudioStartTimestamp = null;
                        ActiveRecording.AudioStopTimestamp = null;
                    }

                    if (!Path.IsPathRooted(ActiveRecording.AudioFile))
                    {
                        ActiveRecording.AudioFile = Path.Combine(options.InputPath, ActiveRecording.AudioFile);
                    }
                }

                if (ActiveRecording.VideoFile != null)
                {
                    if (logEntry.Data?.VideoFirstFrameTimestamp != null && logEntry.Data?.VideoLastFrameTimestamp != null)
                    {
                        ActiveRecording.VideoStartTimestamp = logEntry.Data?.VideoFirstFrameTimestamp ?? ActiveRecording.StartTimestamp;
                        ActiveRecording.VideoStopTimestamp = logEntry.Data?.VideoLastFrameTimestamp ?? ActiveRecording.StopTimestamp;
                    }
                    else
                    {
                        ActiveRecording.VideoStartTimestamp = null;
                        ActiveRecording.VideoStopTimestamp = null;
                    }

                    if (!Path.IsPathRooted(ActiveRecording.VideoFile))
                    {
                        ActiveRecording.VideoFile = Path.Combine(options.InputPath, ActiveRecording.VideoFile);
                    }
                }

                var videoDelay = logEntry.Data?.VideoDelay ?? 0D;
                if (videoDelay != 0 && ActiveRecording.AudioFile != null && ActiveRecording.VideoStartTimestamp.HasValue && ActiveRecording.VideoStopTimestamp.HasValue)
                {
                    ActiveRecording.VideoStartTimestamp = ActiveRecording.VideoStartTimestamp.Value.AddSeconds(videoDelay);
                    ActiveRecording.VideoStopTimestamp = ActiveRecording.VideoStopTimestamp.Value.AddSeconds(videoDelay);
                }

                // ensure consistency on start/stop timestamps
                var audioStartTimestampTicks = long.MaxValue;
                var audioStopTimestampTicks = long.MinValue;
                if (ActiveRecording.AudioFile != null)
                {
                    audioStartTimestampTicks = ActiveRecording.AudioStartTimestamp.Value.Ticks;
                    audioStopTimestampTicks = ActiveRecording.AudioStopTimestamp.Value.Ticks;
                }
                var videoStartTimestampTicks = long.MaxValue;
                var videoStopTimestampTicks = long.MinValue;
                if (ActiveRecording.VideoFile != null && ActiveRecording.VideoStartTimestamp.HasValue && ActiveRecording.VideoStopTimestamp.HasValue)
                {
                    videoStartTimestampTicks = ActiveRecording.VideoStartTimestamp.Value.Ticks;
                    videoStopTimestampTicks = ActiveRecording.VideoStopTimestamp.Value.Ticks;
                }

                StartTimestamp = ActiveRecording.StartTimestamp = new DateTime(Math.Min(audioStartTimestampTicks, videoStartTimestampTicks));
                StopTimestamp = ActiveRecording.StopTimestamp = new DateTime(Math.Max(audioStopTimestampTicks, videoStopTimestampTicks));

                _Recordings.Add(ActiveRecording);
                ActiveRecording = null;
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

        public Models.Connection ToModel()
        {
            return new Models.Connection
            {
                Id = Id,
                StartTimestamp = StartTimestamp,
                StopTimestamp = StopTimestamp,
                Recordings = CompletedRecordings.Select(recording => recording.ToModel()).ToArray()
            };
        }
    }
}
