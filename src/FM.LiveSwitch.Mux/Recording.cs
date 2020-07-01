using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace FM.LiveSwitch.Mux
{
    public class Recording
    {
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

        public double VideoDelay { get; set; }

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
                            Type = VideoEventType.Replace,
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
            return $"{inputTag}atrim=start={trim},asetpts=PTS-STARTPTS{outputTag}";
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
            return $"{inputTag}atrim=end={trim}{outputTag}";
        }
    }
}
