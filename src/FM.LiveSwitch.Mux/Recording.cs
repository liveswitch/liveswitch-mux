using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FM.LiveSwitch.Mux
{
    public class Recording
    {
        public Guid Id
        {
            get
            {
                var input = $"{AudioId}:{VideoId}";
                using var md5 = MD5.Create();
                return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(input)));
            }
        }

        public Guid? AudioId
        {
            get
            {
                var input = $"{AudioStartTimestamp?.Ticks}:{Connection.Id}:audio";
                using var md5 = MD5.Create();
                return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(input)));
            }
        }

        public Guid? VideoId
        {
            get
            {
                var input = $"{VideoStartTimestamp?.Ticks}:{Connection.Id}:video";
                using var md5 = MD5.Create();
                return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(input)));
            }
        }

        public DateTime StartTimestamp { get; set; }

        public DateTime StopTimestamp { get; set; }

        public DateTime? AudioStartTimestamp { get; set; }

        public DateTime? AudioStopTimestamp { get; set; }

        public DateTime? VideoStartTimestamp { get; set; }

        public DateTime? VideoStopTimestamp { get; set; }

        [JsonIgnore]
        public TimeSpan Duration { get { return StopTimestamp - StartTimestamp; } }

        public string AudioFile { get; set; }

        public string VideoFile { get; set; }

        public string LogFile { get; set; }

        public string AudioCodec { get; set; }

        public string VideoCodec { get; set; }

        [JsonIgnore]
        public bool AudioFileExists
        {
            get { return FileUtility.Exists(AudioFile); }
        }

        [JsonIgnore]
        public bool VideoFileExists
        {
            get { return FileUtility.Exists(VideoFile); }
        }

        [JsonIgnore]
        public bool LogFileExists
        {
            get { return FileUtility.Exists(LogFile); }
        }

        public VideoSegment[] VideoSegments { get; set; }

        [JsonIgnore]
        public Connection Connection { get; set; }

        [JsonIgnore]
        public int AudioIndex { get; set; }

        [JsonIgnore]
        public int VideoIndex { get; set; }

        [JsonIgnore]
        public string AudioTag { get; set; }

        [JsonIgnore]
        public string VideoTag { get; set; }

        [JsonIgnore]
        public VideoEvent[] VideoEvents
        {
            get
            {
                if (VideoSegments == null)
                {
                    return null;
                }

                var lastSegment = (VideoSegment)null;
                var videoEvents = new List<VideoEvent>();
                foreach (var segment in VideoSegments)
                {
                    if (videoEvents.Count == 0)
                    {
                        videoEvents.Add(new VideoEvent
                        {
                            Type = VideoEventType.Add,
                            Timestamp = VideoStartTimestamp.Value,
                            Segment = segment
                        });
                    }
                    else
                    {
                        videoEvents.Add(new VideoEvent
                        {
                            Type = VideoEventType.Update,
                            Timestamp = segment.StartTimestamp,
                            LastSegment = lastSegment,
                            Segment = segment
                        });
                    }
                    lastSegment = segment;
                }
                videoEvents.Add(new VideoEvent
                {
                    Type = VideoEventType.Remove,
                    Timestamp = VideoStopTimestamp.Value,
                    LastSegment = lastSegment
                });
                return videoEvents.ToArray();
            }
        }

        [JsonIgnore]
        public RecordingUpdate[] Updates { get { return _Updates.ToArray(); } }

        private readonly List<RecordingUpdate> _Updates = new List<RecordingUpdate>();

        public void Update(LogEntry logEntry)
        {
            Update(logEntry, false);
        }

        public void Update(LogEntry logEntry, bool final)
        {
            var data = logEntry.Data;
            if (data != null)
            {
                if (_Updates.Any())
                {
                    _Updates.Last().StopTimestamp = logEntry.Timestamp;
                }

                if (!final)
                {
                    _Updates.Add(new RecordingUpdate
                    {
                        StartTimestamp = logEntry.Timestamp,
                        ConnectionTag = data.ConnectionTag,
                        AudioMuted = data.AudioMuted == true,
                        VideoMuted = data.VideoMuted == true,
                        AudioDisabled = data.AudioDisabled == true,
                        VideoDisabled = data.VideoDisabled == true,
                    });
                }
            }
        }

        public void SetVideoSegments()
        {
            SetVideoSegments(null);
        }

        public void SetVideoSegments(VideoSegment[] parsedVideoSegments)
        {
            var videoSegments = new List<VideoSegment>();
            if (parsedVideoSegments == null || parsedVideoSegments.Length == 0)
            {
                // dry run
                if (Updates.Length == 0)
                {
                    videoSegments.Add(new VideoSegment
                    {
                        Recording = this,
                        Size = Size.Empty,
                        StartTimestamp = StartTimestamp,
                        StopTimestamp = StopTimestamp
                    });
                }
                else
                {
                    foreach (var update in Updates)
                    {
                        videoSegments.Add(new VideoSegment
                        {
                            Recording = this,
                            Size = Size.Empty,
                            StartTimestamp = update.StartTimestamp,
                            StopTimestamp = update.StopTimestamp,
                            AudioDisabled = update.AudioDisabled,
                            VideoDisabled = update.VideoDisabled,
                            AudioMuted = update.AudioMuted,
                            VideoMuted = update.VideoMuted,
                            ConnectionTag = update.ConnectionTag
                        });
                    }
                }
            }
            else
            {
                if (Updates.Length == 0)
                {
                    videoSegments.AddRange(parsedVideoSegments);
                }
                else
                {
                    // identify video range
                    var minTicks = parsedVideoSegments.First().StartTimestamp.Ticks;
                    var maxTicks = parsedVideoSegments.Last().StopTimestamp.Ticks;

                    // get all unique timestamps
                    var timestampSet = new HashSet<DateTime>();
                    foreach (var update in Updates)
                    {
                        // filter updates outside video range
                        timestampSet.Add(new DateTime(Math.Min(Math.Max(update.StartTimestamp.Ticks, minTicks), maxTicks)));
                        timestampSet.Add(new DateTime(Math.Min(Math.Max(update.StopTimestamp.Ticks, minTicks), maxTicks)));
                    }
                    foreach (var parsedVideoSegment in parsedVideoSegments)
                    {
                        timestampSet.Add(parsedVideoSegment.StartTimestamp);
                        timestampSet.Add(parsedVideoSegment.StopTimestamp);
                    }

                    // sort timestamps
                    var timestamps = timestampSet.OrderBy(x => x).ToArray();
                    {
                        var updateIndex = 0;
                        var update = Updates[updateIndex];
                        var parsedVideoSegmentIndex = 0;
                        var parsedVideoSegment = parsedVideoSegments[parsedVideoSegmentIndex];
                        for (var i = 0; i < timestamps.Length - 1; i++)
                        {
                            var timestamp = timestamps[i];

                            // next video segment
                            while (timestamp >= parsedVideoSegment.StopTimestamp)
                            {
                                parsedVideoSegmentIndex++;
                                parsedVideoSegment = parsedVideoSegments[parsedVideoSegmentIndex];
                            }

                            // next update
                            while (timestamp >= update.StopTimestamp && Updates.Length > updateIndex+1)
                            {
                                updateIndex++;
                                update = Updates[updateIndex];
                            }

                            var videoSegment = parsedVideoSegment.Clone(update);
                            videoSegment.StartTimestamp = timestamp;
                            if (videoSegments.Any())
                            {
                                videoSegments.Last().StopTimestamp = timestamp;
                            }
                            videoSegments.Add(videoSegment);
                        }
                    }
                }
            }
            VideoSegments = videoSegments.ToArray();
        }

        public string GetAudioResampleFilterChain(string inputTag, string outputTag)
        {
            // sync to timestamps using stretching, squeezing, filling and trimming
            return $"{inputTag}aresample=async=1{outputTag}";
        }

        public string GetAudioDelayFilterChain(DateTime sessionStartTimestamp, string inputTag, string outputTag)
        {
            var delay = AudioStartTimestamp.Value - sessionStartTimestamp;
            return $"{inputTag}adelay=delays={(int)delay.TotalMilliseconds}|{(int)delay.TotalMilliseconds}{outputTag}";
        }

        /// <summary>
        /// Adds a atrim parameter to the filter chain for ffmpeg. Crops the start of an audio recording. Asetpts resets the timeline to zero. 
        /// </summary>
        /// <param name="inputTag">The input tag name</param>
        /// <param name="outputTag">The output tag name</param>
        /// <param name="trim">The length in seconds to crop the audio recording by</param>
        /// <returns></returns>
        public string GetAudioStartTrimFilterChain(string inputTag, string outputTag, double trim)
        {
            return $"{inputTag}atrim=start={trim.Round(3)},asetpts=PTS-STARTPTS{outputTag}";
        }

        /// <summary>
        /// Adds a atrim parameter to the filter chain for ffmpeg. Crops the end of an audio recording.
        /// </summary>
        /// <param name="inputTag">The input tag name</param>
        /// <param name="outputTag">The output tag name</param>
        /// <param name="trim">The length in seconds to crop the audio recording by</param>
        /// <returns></returns>
        public string GetAudioEndTrimFilterChain(string inputTag, string outputTag, double trim)
        {
            return $"{inputTag}atrim=end={trim.Round(3)}{outputTag}";
        }
    }
}
