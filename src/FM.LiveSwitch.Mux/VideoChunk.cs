using Newtonsoft.Json;
using System;
using System.Linq;

namespace FM.LiveSwitch.Mux
{
    public class VideoChunk
    {
        public DateTime StartTimestamp { get; set; }

        public DateTime StopTimestamp { get; set; }

        [JsonIgnore]
        public TimeSpan Duration { get { return StopTimestamp - StartTimestamp; } }

        public VideoSegment[] Segments { get; set; }

        public Layout Layout { get; set; }

        public VideoChunk Clone()
        {
            return new VideoChunk
            {
                StartTimestamp = StartTimestamp,
                StopTimestamp = StopTimestamp,
                Segments = Segments,
                Layout = Layout
            };
        }

        public string GetColorFilterChain(string color, string outputTag)
        {
            return $"color={color}:size={Layout.Size.Width}x{Layout.Size.Height}:duration={Duration.TotalSeconds.Round(3)}{outputTag}";
        }

        public string GetTrimFilterChain(Recording recording, string inputTag, string outputTag)
        {
            return $"{inputTag}trim=start={(StartTimestamp - recording.VideoStartTimestamp.Value).TotalSeconds.Round(3)}:end={(StopTimestamp - recording.VideoStartTimestamp.Value).TotalSeconds.Round(3)},setpts=PTS-STARTPTS{outputTag}";
        }

        public string GetFpsFilterChain(int frameRate, string inputTag, string outputTag)
        {
            return $"{inputTag}fps=fps={frameRate}{outputTag}";
        }

        public static VideoChunk First(VideoEvent @event)
        {
            if (@event.Type != VideoEventType.Add)
            {
                throw new Exception("Unexpected video event type.");
            }

            return new VideoChunk
            {
                Segments = new[] { @event.Segment },
                StartTimestamp = @event.Timestamp,
            };
        }

        public VideoChunk Next(VideoEvent @event)
        {
            StopTimestamp = @event.Timestamp;

            switch (@event.Type)
            {
                case VideoEventType.Add:
                    return new VideoChunk
                    {
                        Segments = Segments.Concat(new[] { @event.Segment }).ToArray(),
                        StartTimestamp = @event.Timestamp
                    };
                case VideoEventType.Remove:
                    var segments = Segments.Where(x => x != @event.LastSegment).ToArray();
                    if (segments.Length == 0)
                    {
                        return null;
                    }
                    return new VideoChunk
                    {
                        Segments = segments,
                        StartTimestamp = @event.Timestamp
                    };
                case VideoEventType.Update:
                    return new VideoChunk
                    {
                        Segments = Segments.Where(x => x != @event.LastSegment).Concat(new[] { @event.Segment }).ToArray(),
                        StartTimestamp = @event.Timestamp
                    };
                default:
                    throw new Exception("Unrecognized video event type.");
            }
        }
    }
}
