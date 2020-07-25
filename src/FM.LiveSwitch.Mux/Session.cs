using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    public class Session
    {
        [JsonIgnore]
        public string FileName { get; set; }

        [JsonIgnore]
        public string AudioFileName { get; set; }

        [JsonIgnore]
        public string VideoFileName { get; set; }

        public Guid Id
        { 
            get
            {
                var input = string.Join(":", CompletedRecordings.Select(x => x.Id).OrderBy(x => x));
                using var md5 = MD5.Create();
                return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(input)));
            } 
        }

        public string ChannelId { get; private set; }

        public string ApplicationId { get; private set; }

        public DateTime StartTimestamp { get { return CompletedClients.Select(x => x.StartTimestamp).Min().Value; } }

        public DateTime StopTimestamp { get { return CompletedClients.Select(x => x.StopTimestamp).Max().Value; } }

        [JsonIgnore]
        public TimeSpan Duration { get { return StopTimestamp - StartTimestamp; } }

        public string File { get; set; }

        public string AudioFile { get; set; }

        public string VideoFile { get; set; }

        [JsonIgnore]
        public string MetadataFile { get; set; }

        [JsonIgnore]
        public bool FileExists
        {
            get { return FileUtility.Exists(File); }
        }

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
        public bool MetadataFileExists
        {
            get { return FileUtility.Exists(MetadataFile); }
        }

        [JsonIgnore]
        public bool HasAudio
        {
            get
            {
                return CompletedClients.Any(client =>
                {
                    return client.CompletedConnections.Any(connection =>
                    {
                        return connection.CompletedRecordings.Any(recording => recording.AudioFileExists);
                    });
                });
            }
        }

        [JsonIgnore]
        public bool HasVideo
        {
            get
            {
                return CompletedClients.Any(client =>
                {
                    return client.CompletedConnections.Any(connection =>
                    {
                        return connection.CompletedRecordings.Any(recording => recording.VideoFileExists);
                    });
                });
            }
        }

        [JsonIgnore]
        public VideoSegment[] CompletedVideoSegments { get { return CompletedClients.Where(x => x.CompletedVideoSegments != null).SelectMany(x => x.CompletedVideoSegments).OrderBy(x => x.StartTimestamp).ToArray(); } }

        [JsonIgnore]
        public Recording[] CompletedRecordings { get { return CompletedClients.SelectMany(x => x.CompletedRecordings).OrderBy(x => x.StartTimestamp).ToArray(); } }

        [JsonIgnore]
        public Connection[] CompletedConnections { get { return CompletedClients.SelectMany(x => x.CompletedConnections).OrderBy(x => x.StartTimestamp).ToArray(); } }

        [JsonProperty("clients")]
        public Client[] CompletedClients { get; private set; }

        private const int MAX_AMIX_INPUTS = 32;

        private int AudioMixIndex = 0;

        public string[] GetAudioMixFilterChains(IEnumerable<string> inputTags, out string outputTag)
        {
            AudioMixIndex = 0;
            return DoGetAudioMixFilterChains(inputTags, out outputTag);
        }

        private string[] DoGetAudioMixFilterChains(IEnumerable<string> inputTags, out string outputTag)
        {
            // base case
            var inputTagCount = inputTags.Count();
            if (inputTagCount <= MAX_AMIX_INPUTS)
            {
                if (inputTagCount == 1)
                {
                    outputTag = inputTags.First();
                    return new string[0];
                }

                outputTag = $"[amix_{AudioMixIndex++}]";
                return new[]
                {
                    $"{string.Join(string.Empty, inputTags)}amix=inputs={inputTagCount}{outputTag}"
                };
            }

            var filterChains = new List<string>();
            var mixOutputTags = new List<string>();
            for (var inputTagIndex = 0; inputTagIndex < inputTagCount; inputTagIndex += MAX_AMIX_INPUTS)
            {
                filterChains.AddRange(DoGetAudioMixFilterChains(inputTags.Skip(inputTagIndex).Take(MAX_AMIX_INPUTS), out var mixOutputTag));
                mixOutputTags.Add(mixOutputTag);
            }

            // recursive case
            filterChains.AddRange(DoGetAudioMixFilterChains(mixOutputTags, out outputTag));
            return filterChains.ToArray();
        }

        public Session(string channelId, string applicationId, Client[] completedClients)
        {
            ChannelId = channelId;
            ApplicationId = applicationId;
            CompletedClients = completedClients.OrderBy(x => x.StartTimestamp).ToArray();
        }

        public async Task<bool> Mux(MuxOptions options)
        {
            // set output file name
            FileName = $"{ProcessOutputFileName(options.OutputFileName)}.{(options.NoVideo ? options.AudioContainer : options.VideoContainer)}";

            // get output file path
            File = Path.Combine(GetOutputPath(options), FileName);

            var inputArguments = new List<string>();

            // process audio
            if (HasAudio && !options.NoAudio)
            {
                if (!await MuxAudio(options))
                {
                    return false;
                }
                inputArguments.Add($"-i {AudioFile}");
            }

            // process video
            if (HasVideo && !options.NoVideo)
            {
                if (!await MuxVideo(options))
                {
                    return false;
                }
                inputArguments.Add($"-i {VideoFile}");
            }

            if (inputArguments.Count == 0)
            {
                Console.Error.WriteLine("No media files found.");
                return false;
            }

            // pull together the final arguments list
            var arguments = new List<string>
            {
                "-y" // overwrite output files without asking
            };
            arguments.AddRange(inputArguments);
            if (!options.NoAudio)
            {
                arguments.Add($"-codec:a copy");
            }
            if (!options.NoVideo)
            {
                arguments.Add($"-codec:v copy");
            }
            arguments.Add(File);

            if (options.DryRun)
            {
                return true;
            }

            var outputPath = Path.GetDirectoryName(File);
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // run it
            await FFmpeg(string.Join(" ", arguments));

            return FileExists;
        }

        private const string Iso8601FileSafeFormat = @"yyyy-MM-dd_HH-mm-ss";

        private string ProcessOutputFileName(string outputFileName)
        {
            return outputFileName
                .Replace("{applicationId}", ApplicationId)
                .Replace("{channelId}", ChannelId)
                .Replace("{sessionId}", Id.ToString())
                .Replace("{startTimestamp}", StartTimestamp.ToString(Iso8601FileSafeFormat))
                .Replace("{stopTimestamp}", StopTimestamp.ToString(Iso8601FileSafeFormat));
        }

        private string GetOutputPath(MuxOptions options)
        {
            switch (options.Strategy)
            {
                case StrategyType.Hierarchical:
                    return Path.Combine(options.OutputPath, ApplicationId, ChannelId);
                case StrategyType.Flat:
                    return options.OutputPath;
                default:
                    throw new Exception("Unrecognized strategy.");
            }
        }

        private async Task<bool> MuxAudio(MuxOptions options)
        {
            // set output file name
            AudioFileName = $"{ProcessOutputFileName(options.OutputFileName)}_audio.{options.AudioContainer}";

            // get output file path
            AudioFile = Path.Combine(GetOutputPath(options), AudioFileName);

            // initialize recordings
            var recordingIndex = 0;
            var recordings = CompletedRecordings.Where(x => x.AudioFileExists).ToArray();
            foreach (var recording in recordings)
            {
                recording.AudioIndex = recordingIndex++;
                recording.AudioTag = $"[{recording.AudioIndex}:a]";
            }

            // build filter chains
            var filterChains = GetAudioFilterChains(recordings, options);
            var filterChainFileName = $"{ProcessOutputFileName(options.OutputFileName)}_audio.filter";
            var filterChainFile = Path.Combine(GetOutputPath(options), filterChainFileName);
            try
            {
                // pull together the final arguments list
                var arguments = new List<string>
                {
                    "-y" // overwrite output files without asking
                };
                arguments.AddRange(recordings.Select(x => $"-i {x.AudioFile}"));
                if (filterChains.Length > 0)
                {
                    try
                    {
                        if (options.NoFilterFiles)
                        {
                            arguments.Add($@"-filter_complex ""{string.Join(";", filterChains)}""");
                        }
                        else
                        {
                            System.IO.File.WriteAllText(filterChainFile, string.Join(";", filterChains));
                            arguments.Add($@"-filter_complex_script {filterChainFile}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Could not create temporary filter chain file '{filterChainFileName}': {ex}");
                        Console.Error.WriteLine($"Filter chain will be passed as command-line argument.");
                        arguments.Add($@"-filter_complex ""{string.Join(";", filterChains)}""");
                    }
                }
                arguments.Add($@"-map ""[aout]""");
                if (options.AudioCodec != null)
                {
                    arguments.Add($"-codec:a {options.AudioCodec}");
                }
                arguments.Add(AudioFile);

                if (options.DryRun)
                {
                    return true;
                }

                var outputPath = Path.GetDirectoryName(AudioFile);
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }

                // run it
                await FFmpeg(string.Join(" ", arguments));

                return AudioFileExists;
            }
            finally
            {
                try
                {
                    if (System.IO.File.Exists(filterChainFile) && !options.SaveFilterFiles)
                    {
                        System.IO.File.Delete(filterChainFile);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Could not delete temporary filter chain file '{filterChainFileName}': {ex}");
                }
            }
        }

        private string[] GetAudioFilterChains(Recording[] recordings, MuxOptions options)
        {
            // build filter chains
            var filterChains = new List<string>();
            var recordingTags = new List<string>();

            var startDelays = new List<double>();
            var endDelays = new List<double>();
            var trimFirst = 0D;
            var trimLast = 0D;
            //--trim-first --trim-last
            if (options.TrimFirst || options.TrimLast)
            {
                foreach (var recording in recordings)
                {
                    startDelays.Add((recording.StartTimestamp - StartTimestamp).TotalSeconds);
                    endDelays.Add((StopTimestamp - recording.StopTimestamp).TotalSeconds);
                }

                //Find the second lowest delay. This is the offset we will use for audio recordings.
                startDelays.Sort();
                endDelays.Sort();

                if (recordings.Length > 1)
                {
                    if (options.TrimFirst)
                    {
                        trimFirst = startDelays[1];
                    }
                    if (options.TrimLast)
                    {
                        trimLast = endDelays[1];
                    }
                }
            }

            foreach (var recording in recordings)
            {
                // initialize tag
                var recordingTag = recording.AudioTag;
                var resampleTag = $"[aresample_{recording.AudioIndex}]";
                var delayTag = $"[adelay_{recording.AudioIndex}]";
                var trimTag = $"[atrim_{recording.AudioIndex}]";

                // resample
                filterChains.Add(recording.GetAudioResampleFilterChain(recordingTag, recordingTag = resampleTag));

                // delay
                filterChains.Add(recording.GetAudioDelayFilterChain(StartTimestamp, recordingTag, recordingTag = delayTag));

                if (trimFirst > 0)
                {
                    // atrim start - removes the beginning of the audio of the first track.
                    filterChains.Add(recording.GetAudioStartTrimFilterChain(recordingTag, recordingTag = trimTag, trimFirst));
                }

                if (trimLast > 0 && recording.StopTimestamp == StopTimestamp)
                {
                    // atrim end - removes the ending of the audio for the last track.
                    filterChains.Add(recording.GetAudioEndTrimFilterChain(recordingTag, recordingTag = trimTag, trimLast));
                }

                // keep track of tags
                recordingTags.Add(recordingTag);
            }

            // mix tags
            filterChains.AddRange(GetAudioMixFilterChains(recordingTags, out var outputTag));

            // null out
            filterChains.Add($"{outputTag}anull[aout]");

            return filterChains.ToArray();
        }

        private async Task<bool> MuxVideo(MuxOptions options)
        {
            if (options.TrimFirst || options.TrimLast)
            {
                Console.Error.WriteLine("--trim-first and --trim-last are not supported for video.");
                return false;
            }

            // set output file name
            VideoFileName = $"{ProcessOutputFileName(options.OutputFileName)}_video.{options.VideoContainer}";

            // get output file path
            VideoFile = Path.Combine(GetOutputPath(options), VideoFileName);

            // initialize recordings
            var recordingIndex = 0;
            var recordings = CompletedRecordings.Where(x => x.VideoFileExists).ToArray();
            foreach (var recording in recordings)
            {
                if (options.DryRun)
                {
                    recording.SetVideoSegments();
                }
                else
                {
                    recording.SetVideoSegments(await ParseVideoSegments(recording));
                }
            }

            recordings = recordings.Where(x => x.VideoSegments.Length > 0).ToArray();

            if (recordings.Length == 0)
            {
                VideoFile = null;
                Console.Error.WriteLine("Session has no video segments.");
                return false;
            }

            foreach (var recording in recordings)
            {
                recording.VideoIndex = recordingIndex++;
                recording.VideoTag = $"[{recording.VideoIndex}:v]";
            }

            // initialize layout output
            var layoutOutput = new LayoutOutput
            {
                ApplicationId = ApplicationId,
                ChannelId = ChannelId,
                Margin = options.Margin,
                Size = new Size(options.Width, options.Height)
            };

            // initialize unique connections
            var connections = new HashSet<Connection>();
            foreach (var recording in recordings)
            {
                connections.Add(recording.Connection);
            }

            // initialize static layout inputs
            var staticLayoutInputs = new List<LayoutInput>();
            foreach (var connection in connections)
            {
                staticLayoutInputs.Add(connection.GetLayoutInput());
            }

            // convert recordings into event timeline
            var events = new List<VideoEvent>();
            foreach (var recording in recordings)
            {
                events.AddRange(recording.VideoEvents);
            }

            // sort event timeline
            events = events.OrderBy(x => x.Timestamp).ThenBy(x => (int)x.Type).ToList();

            // convert event timeline into chunks
            var chunks = new List<VideoChunk>();
            var lastChunk = (VideoChunk)null;
            foreach (var @event in events)
            {
                // blank chunk mid-session
                if (chunks.Count > 0 && lastChunk == null)
                {
                    chunks.Add(new VideoChunk
                    {
                        StartTimestamp = chunks.Last().StopTimestamp,
                        StopTimestamp = @event.Timestamp,
                        Layout = Layout.Calculate(options.Layout, new LayoutInput[0], layoutOutput, options.JavaScriptFile),
                        Segments = new VideoSegment[0]
                    });
                }

                var chunk = (VideoChunk)null;
                if (chunks.Count == 0)
                {
                    chunk = VideoChunk.First(@event);
                }
                else
                {
                    chunk = chunks.Last().Next(@event);
                }

                lastChunk = chunk;

                if (chunk != null)
                {
                    // keep the segments sorted by their time of first join
                    chunk.Segments = chunk.Segments.OrderBy(x => x.Recording.VideoIndex).ToArray();

                    // calculate the layout
                    if (options.Dynamic)
                    {
                        chunk.Layout = Layout.Calculate(options.Layout, chunk.Segments.Select(x => x.GetLayoutInput()).ToArray(), layoutOutput, options.JavaScriptFile);
                    }
                    else
                    {
                        chunk.Layout = Layout.Calculate(options.Layout, staticLayoutInputs.ToArray(), layoutOutput, options.JavaScriptFile);
                    }

                    chunks.Add(chunk);
                }
            }

            // insert blank chunk if needed
            if (chunks.Count > 0 && chunks[0].StartTimestamp != StartTimestamp)
            {
                chunks.Insert(0, new VideoChunk
                {
                    StartTimestamp = StartTimestamp,
                    StopTimestamp = chunks[0].StartTimestamp,
                    Layout = Layout.Calculate(options.Layout, new LayoutInput[0], layoutOutput, options.JavaScriptFile),
                    Segments = new VideoSegment[0]
                });
            }

            // build filter chains
            var filterChains = GetVideoFilterChains(chunks.ToArray(), options);
            var filterChainFileName = $"{ProcessOutputFileName(options.OutputFileName)}_video.filter";
            var filterChainFile = Path.Combine(GetOutputPath(options), filterChainFileName);
            try
            {
                // construct argument list
                var arguments = new List<string>
                {
                    "-y" // overwrite output files without asking
                };
                arguments.AddRange(recordings.Select(x => $"-i {x.VideoFile}"));
                if (filterChains.Length > 0)
                {
                    try
                    {
                        if (options.NoFilterFiles)
                        {
                            arguments.Add($@"-filter_complex ""{string.Join(";", filterChains)}""");
                        }
                        else
                        {
                            System.IO.File.WriteAllText(filterChainFile, string.Join(";", filterChains));
                            arguments.Add($@"-filter_complex_script {filterChainFile}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Could not create temporary filter chain file '{filterChainFileName}': {ex}");
                        Console.Error.WriteLine($"Filter chain will be passed as command-line argument.");
                        arguments.Add($@"-filter_complex ""{string.Join(";", filterChains)}""");
                    }
                }
                arguments.Add($@"-map ""[vout]""");
                if (options.VideoCodec != null)
                {
                    arguments.Add($"-codec:v {options.VideoCodec}");
                }
                arguments.Add(VideoFile);

                if (options.DryRun)
                {
                    return true;
                }

                var outputPath = Path.GetDirectoryName(VideoFile);
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }

                // run it
                await FFmpeg(string.Join(" ", arguments));

                return VideoFileExists;
            }
            finally
            {
                try
                {
                    if (System.IO.File.Exists(filterChainFile) && !options.SaveFilterFiles)
                    {
                        System.IO.File.Delete(filterChainFile);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Could not delete temporary filter chain file '{filterChainFileName}': {ex}");
                }
            }
        }

        private string[] GetVideoFilterChains(VideoChunk[] chunks, MuxOptions options)
        {
            // build filter chains
            var filterChains = new List<string>();

            // process each chunk
            var chunkTags = new List<string>();
            for (var chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                var chunk = chunks[chunkIndex];

                // initialize tag
                var colorTag = $"[vcolor_{chunkIndex}]";
                var chunkTag = colorTag;

                // color
                filterChains.Add(chunk.GetColorFilterChain(options.BackgroundColor, colorTag));

                // process each chunk segment
                var segments = chunk.Segments;
                var views = chunk.Layout.Views;
                for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
                {
                    var segment = segments[segmentIndex];
                    var view = views[segment.Recording.Connection.Id];
                    var recording = segment.Recording;

                    // scale bounds to segment
                    view.Bounds = new Rectangle(Point.Zero, segment.Size);

                    if (segment.Size != Size.Empty)
                    {
                        view.ScaleBounds(options.Crop);
                    }

                    // initialize tags
                    var segmentTag = recording.VideoTag;
                    var fpsTag = $"[vfps_{chunkIndex}_{segmentIndex}]";
                    var trimTag = $"[vtrim_{chunkIndex}_{segmentIndex}]";
                    var scaleTag = $"[vscale_{chunkIndex}_{segmentIndex}]";
                    var cropTag = $"[vcrop_{chunkIndex}_{segmentIndex}]";
                    var overlayTag = $"[voverlay_{chunkIndex}_{segmentIndex}]";

                    // fps
                    filterChains.Add(chunk.GetFpsFilterChain(options.FrameRate, segmentTag, segmentTag = fpsTag));

                    // trim
                    filterChains.Add(chunk.GetTrimFilterChain(recording, segmentTag, segmentTag = trimTag));

                    // then scale
                    filterChains.Add(view.GetSizeFilterChain(segmentTag, segmentTag = scaleTag));

                    // then crop (optional)
                    if (options.Crop)
                    {
                        filterChains.Add(view.GetCropFilterChain(segmentTag, segmentTag = cropTag));
                    }

                    // then overlay
                    filterChains.Add(view.GetOverlayChain(options.Crop, chunkTag, segmentTag, chunkTag = overlayTag));
                }

                // keep track of each chunk's final tag
                chunkTags.Add(chunkTag);
            }

            // concatenate the chunks
            filterChains.Add($"{string.Join(string.Empty, chunkTags)}concat=n={chunkTags.Count}[vout]");

            return filterChains.ToArray();
        }

        public async Task<VideoSegment[]> ParseVideoSegments(Recording recording)
        {
            var lines = await FFprobe($"-v quiet -select_streams v:0 -show_frames -show_entries frame=pkt_pts_time,width,height -print_format csv=item_sep=|:nokey=1:print_section=0 {recording.VideoFile}");

            var currentSize = Size.Empty;
            var segments = new List<VideoSegment>();
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length >= 3)
                {
                    var readSeconds = double.TryParse(parts[0], out var seconds);
                    var readWidth = int.TryParse(parts[1], out var width);
                    var readHeight = int.TryParse(parts[2], out var height);
                    if (readSeconds && readWidth && readHeight)
                    {
                        var size = new Size(width, height);
                        if (!size.Equals(currentSize))
                        {
                            var timestamp = recording.VideoStartTimestamp.Value.AddSeconds(seconds);

                            // stop last segment (if applicable)
                            if (segments.Count > 0)
                            {
                                segments.Last().StopTimestamp = timestamp;
                            }

                            // start new segment
                            segments.Add(new VideoSegment
                            {
                                Recording = recording,
                                Size = size,
                                StartTimestamp = timestamp
                            });

                            // update size
                            currentSize = size;
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"Could not parse ffprobe output: {line}");
                    }
                }
                else
                {
                    Console.Error.WriteLine($"Unexpected ffprobe output: {line}");
                }
            }

            // stop last segment (if applicable)
            if (segments.Count > 0)
            {
                segments.Last().StopTimestamp = recording.VideoStopTimestamp.Value;
            }

            return segments.ToArray();
        }

        private Task<string[]> FFmpeg(string arguments)
        {
            return Execute("ffmpeg", arguments, true, true);
        }

        private Task<string[]> FFprobe(string arguments)
        {
            return Execute("ffprobe", arguments, false, false);
        }

        private async Task<string[]> Execute(string command, string arguments, bool useStandardError, bool logOutput)
        {
            // prep the process arguments
            var processStartInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = command,
                Arguments = arguments
            };

            if (useStandardError)
            {
                processStartInfo.RedirectStandardError = true;
            }
            else
            {
                processStartInfo.RedirectStandardOutput = true;
            }

            // log what we're about to do
            Console.Error.WriteLine();
            Console.Error.WriteLine($"{processStartInfo.FileName} {processStartInfo.Arguments}");

            try
            {
                // let 'er rip
                var process = Process.Start(processStartInfo);

                // process each line
                var lines = new List<string>();
                var stream = useStandardError ? process.StandardError : process.StandardOutput;
                while (!stream.EndOfStream)
                {
                    var line = await stream.ReadLineAsync();
                    if (line != null)
                    {
                        if (logOutput)
                        {
                            Console.Error.WriteLine(line);
                        }
                        lines.Add(line);
                    }
                }

                // make sure everything is finished
                process.WaitForExit();

                return lines.ToArray();
            }
            catch (Win32Exception wex)
            {
                throw new Exception($"Could not start {command}. Is ffmpeg installed and available on your PATH?", wex);
            }
        }

        public bool WriteMetadata(MuxOptions options)
        {
            MetadataFile = Path.Combine(Path.GetDirectoryName(File), $"{Path.GetFileNameWithoutExtension(File)}.json");

            var outputPath = Path.GetDirectoryName(MetadataFile);
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            var file = File;
            var audioFile = AudioFile;
            var videoFile = VideoFile;
            try
            {
                if (options.DryRun)
                {
                    File = null;
                    AudioFile = null;
                    VideoFile = null;
                }

                System.IO.File.WriteAllText(MetadataFile, JsonConvert.SerializeObject(this));

                return MetadataFileExists;
            }
            finally
            {
                File = file;
                AudioFile = audioFile;
                VideoFile = videoFile;
            }
        }
    }
}
