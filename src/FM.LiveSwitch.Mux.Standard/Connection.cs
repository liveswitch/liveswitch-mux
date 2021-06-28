using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

        [JsonIgnore]
        public string ExternalId { get; private set; }

        public DateTime? StartTimestamp
        {
            get
            {
                var startTimestamps = CompletedRecordings.Select(x => x.StartTimestamp);
                if (startTimestamps.Count() == 0)
                {
                    return null;
                }
                return startTimestamps.Min();
            }
        }

        public DateTime? StopTimestamp
        {
            get
            {
                var stopTimestamps = CompletedRecordings.Select(x => x.StopTimestamp);
                if (stopTimestamps.Count() == 0)
                {
                    return null;
                }
                return stopTimestamps.Max();
            }
        }

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

        private readonly ILoggerFactory _LoggerFactory;
        private readonly ILogger _Logger;
        private readonly Utility _Utility;

        public Connection(string id, string clientId, string deviceId, string userId, string channelId, string applicationId, string externalId, ILoggerFactory loggerFactory)
        {
            Id = id;
            ClientId = clientId;
            DeviceId = deviceId;
            UserId = userId;
            ChannelId = channelId;
            ApplicationId = applicationId;
            ExternalId = externalId;

            _LoggerFactory = loggerFactory;
            _Logger = loggerFactory.CreateLogger(nameof(Connection));
            _Utility = new Utility(_Logger);
        }

        public async Task<bool> ProcessLogEntry(LogEntry logEntry, MuxOptions options)
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

                ActiveRecording.StopTimestamp = logEntry.Timestamp;
                ActiveRecording.AudioFile = logEntry.Data?.AudioFile;
                ActiveRecording.VideoFile = logEntry.Data?.VideoFile;

                if (ActiveRecording.AudioFile != null)
                {
                    ActiveRecording.AudioStartTimestamp = logEntry.Data?.AudioFirstFrameTimestamp ?? ActiveRecording.StartTimestamp;
                    ActiveRecording.AudioStopTimestamp = logEntry.Data?.AudioLastFrameTimestamp ?? ActiveRecording.StopTimestamp;

                    if (!Path.IsPathRooted(ActiveRecording.AudioFile))
                    {
                        ActiveRecording.AudioFile = Path.Combine(options.InputPath, ActiveRecording.AudioFile);
                    }

                    if (ActiveRecording.AudioStartTimestamp.HasValue)
                    {
                        var audioDuration = await GetDuration(true, ActiveRecording.AudioFile).ConfigureAwait(false);
                        if (audioDuration.HasValue)
                        {
                            _Logger.LogInformation("Audio recording with connection ID '{ConnectionId}' and start timestamp '{StartTimestamp}' has a duration of {DurationSeconds} seconds.", Id, ActiveRecording.StartTimestamp, audioDuration.Value.TotalSeconds);
                            ActiveRecording.AudioStopTimestamp = ActiveRecording.AudioStartTimestamp.Value.Add(audioDuration.Value);
                        }

                        if (!ActiveRecording.AudioStopTimestamp.HasValue)
                        {
                            _Logger.LogWarning("Audio recording with connection ID '{ConnectionId}' and start timestamp '{StartTimestamp}' is missing a stop timestamp.", Id, ActiveRecording.StartTimestamp);
                            ActiveRecording.AudioStopTimestamp = ActiveRecording.AudioStartTimestamp;
                        }
                    }
                }

                if (ActiveRecording.VideoFile != null)
                {
                    ActiveRecording.VideoStartTimestamp = logEntry.Data?.VideoFirstFrameTimestamp ?? ActiveRecording.StartTimestamp;
                    ActiveRecording.VideoStopTimestamp = logEntry.Data?.VideoLastFrameTimestamp ?? ActiveRecording.StopTimestamp;

                    if (!Path.IsPathRooted(ActiveRecording.VideoFile))
                    {
                        ActiveRecording.VideoFile = Path.Combine(options.InputPath, ActiveRecording.VideoFile);
                    }

                    if (ActiveRecording.VideoStartTimestamp.HasValue)
                    {
                        var videoDuration = await GetDuration(false, ActiveRecording.VideoFile).ConfigureAwait(false);
                        if (videoDuration.HasValue)
                        {
                            _Logger.LogInformation("Video recording with connection ID '{ConnectionId}' and start timestamp '{StartTimestamp}' has a duration of {DurationSeconds} seconds.", Id, ActiveRecording.StartTimestamp, videoDuration.Value.TotalSeconds);
                            ActiveRecording.VideoStopTimestamp = ActiveRecording.VideoStartTimestamp.Value.Add(videoDuration.Value);
                        }

                        if (!ActiveRecording.VideoStopTimestamp.HasValue)
                        {
                            _Logger.LogWarning("Video recording with connection ID '{ConnectionId}' and start timestamp '{StartTimestamp}' is missing a stop timestamp.", Id, ActiveRecording.StartTimestamp);
                            ActiveRecording.VideoStopTimestamp = ActiveRecording.VideoStartTimestamp;
                        }
                    }
                }

                var videoDelay = logEntry.Data?.VideoDelay ?? 0D;
                if (videoDelay != 0 &&
                    ActiveRecording.VideoStartTimestamp.HasValue &&
                    ActiveRecording.VideoStopTimestamp.HasValue)
                {
                    ActiveRecording.VideoStartTimestamp = ActiveRecording.VideoStartTimestamp.Value.AddSeconds(videoDelay);
                    ActiveRecording.VideoStopTimestamp = ActiveRecording.VideoStopTimestamp.Value.AddSeconds(videoDelay);
                }

                // ensure consistency on start/stop timestamps
                var audioStartTimestampTicks = long.MaxValue;
                var audioStopTimestampTicks = long.MinValue;
                if (ActiveRecording.AudioFile != null && ActiveRecording.AudioStartTimestamp.HasValue && ActiveRecording.AudioStopTimestamp.HasValue)
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

                ActiveRecording.StartTimestamp = new DateTime(Math.Min(audioStartTimestampTicks, videoStartTimestampTicks));
                ActiveRecording.StopTimestamp = new DateTime(Math.Max(audioStopTimestampTicks, videoStopTimestampTicks));

                logEntry.Timestamp = ActiveRecording.StopTimestamp;
                ActiveRecording.Update(logEntry, true);

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

        private async Task<TimeSpan?> GetDuration(bool audio, string file)
        {
            var type = audio ? "a" : "v";
            var lines = await _Utility.FFprobe($"-v quiet -select_streams {type}:0 -show_frames -show_entries frame=pkt_pts_time -print_format csv=item_sep=|:nokey=1:print_section=0 {file}").ConfigureAwait(false);

            TimeSpan? duration = null;
            double? firstSeconds = null;
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length >= 1)
                {
                    var readSeconds = double.TryParse(parts[0], out var seconds);
                    if (readSeconds)
                    {
                        if (firstSeconds == null)
                        {
                            firstSeconds = seconds;
                        }
                        else
                        {
                            duration = TimeSpan.FromSeconds(seconds - firstSeconds.Value);
                        }
                    }
                    else
                    {
                        _Logger.LogError("Could not parse ffprobe output: {Line}", line);
                    }
                }
                else
                {
                    _Logger.LogError("Unexpected ffprobe output: {Line}", line);
                }
            }

            return duration;
        }
    }
}
