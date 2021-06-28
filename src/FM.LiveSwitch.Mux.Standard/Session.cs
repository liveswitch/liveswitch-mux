using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
        public ILoggerFactory LoggerFactory { get; private set; }

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
                using (var md5 = MD5.Create())
                {
                    return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(input)));
                }
            } 
        }

        public string ChannelId { get; private set; }

        public string ApplicationId { get; private set; }

        public string ExternalId { get; private set; }

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
                        return connection.CompletedRecordings.Any(recording => recording.AudioFileExists && recording.AudioStartTimestamp.HasValue && recording.AudioStopTimestamp.HasValue);
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
                        return connection.CompletedRecordings.Any(recording => recording.VideoFileExists && recording.VideoStartTimestamp.HasValue && recording.VideoStopTimestamp.HasValue);
                    });
                });
            }
        }

        [JsonIgnore]
        public Recording[] CompletedRecordings { get { return CompletedClients.SelectMany(x => x.CompletedRecordings).OrderBy(x => x.StartTimestamp).ToArray(); } }

        [JsonIgnore]
        public Connection[] CompletedConnections { get { return CompletedClients.SelectMany(x => x.CompletedConnections).OrderBy(x => x.StartTimestamp).ToArray(); } }

        [JsonProperty("clients")]
        public Client[] CompletedClients { get; private set; }

        private const int MAX_AMIX_INPUTS = 32;

        private int AudioMixIndex;

        private string[] GetAudioMixFilterChains(IEnumerable<string> inputTags, out string outputTag)
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

        private readonly ILogger _Logger;
        private readonly Utility _Utility;

        public Session(string channelId, string applicationId, string externalId, Client[] completedClients, ILoggerFactory loggerFactory)
        {
            ChannelId = channelId;
            ApplicationId = applicationId;
            ExternalId = externalId;
            CompletedClients = completedClients.OrderBy(x => x.StartTimestamp).ToArray();
            LoggerFactory = loggerFactory;

            _Logger = loggerFactory.CreateLogger(nameof(Session));
            _Utility = new Utility(_Logger);
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
                if (!await MuxAudio(options).ConfigureAwait(false))
                {
                    return false;
                }
                inputArguments.Add($"-i {AudioFile}");
            }

            // process video
            if (HasVideo && !options.NoVideo)
            {
                if (!await MuxVideo(options).ConfigureAwait(false))
                {
                    return false;
                }
                inputArguments.Add($"-i {VideoFile}");
            }

            if (inputArguments.Count == 0)
            {
                _Logger.LogInformation("No media files found.");
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
            await _Utility.FFmpeg(string.Join(" ", arguments)).ConfigureAwait(false);

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
                    throw new InvalidOperationException($"Unexpected strategy type '{options.Strategy}'.");
            }
        }

        private string GetTempPath(MuxOptions options)
        {
            switch (options.Strategy)
            {
                case StrategyType.Hierarchical:
                    return Path.Combine(options.TempPath, ApplicationId, ChannelId);
                case StrategyType.Flat:
                    return options.TempPath;
                default:
                    throw new InvalidOperationException($"Unexpected strategy type '{options.Strategy}'.");
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
            var recordings = CompletedRecordings.Where(x => x.AudioFileExists && x.AudioStartTimestamp != null && x.AudioStopTimestamp != null).ToArray();
            foreach (var recording in recordings)
            {
                recording.AudioIndex = recordingIndex++;
                recording.AudioTag = $"[{recording.AudioIndex}:a]";

                if (!options.DryRun)
                {
                    recording.AudioCodec = await GetAudioCodec(recording).ConfigureAwait(false);
                }
            }

            // build filter chains
            var filterChains = GetAudioFilterChains(recordings, options);
            var filterChainFileName = $"{ProcessOutputFileName(options.OutputFileName)}_audio.filter";
            var filterChainFile = Path.Combine(GetTempPath(options), filterChainFileName);
            try
            {
                // pull together the final arguments list
                var arguments = new List<string>
                {
                    "-y" // overwrite output files without asking
                };
                arguments.AddRange(recordings.Select(recording =>
                {
                    if (recording.AudioCodec == "opus")
                    {
                        // 'opus' doesn't support SILK, but 'libopus' does,
                        // so prefer that when decoding to avoid audio loss
                        return $"-codec:a libopus -i {recording.AudioFile}";
                    }
                    return $"-i {recording.AudioFile}";
                }));
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
                            var filterChainFilePath = Path.GetDirectoryName(filterChainFile);
                            if (!Directory.Exists(filterChainFilePath))
                            {
                                Directory.CreateDirectory(filterChainFilePath);
                            }

                            System.IO.File.WriteAllText(filterChainFile, string.Join(";", filterChains));
                            arguments.Add($@"-filter_complex_script {filterChainFile}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logger.LogError(ex, "Could not create temporary filter chain file '{FilterChainFileName}'.", filterChainFileName);
                        _Logger.LogWarning($"Filter chain will be passed as command-line argument.");
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
                await _Utility.FFmpeg(string.Join(" ", arguments)).ConfigureAwait(false);

                return AudioFileExists;
            }
            finally
            {
                try
                {
                    if (System.IO.File.Exists(filterChainFile) && !options.SaveTempFiles)
                    {
                        System.IO.File.Delete(filterChainFile);
                    }
                }
                catch (Exception ex)
                {
                    _Logger.LogError(ex, "Could not delete temporary filter chain file '{FilterChainFileName}'.", filterChainFileName);
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
                var trimFirstTag = $"[atrimfirst_{recording.AudioIndex}]";
                var trimLastTag = $"[atrimlast_{recording.AudioIndex}]";

                // resample
                filterChains.Add(recording.GetAudioResampleFilterChain(recordingTag, resampleTag));
                recordingTag = resampleTag;

                // delay
                filterChains.Add(recording.GetAudioDelayFilterChain(StartTimestamp, recordingTag, delayTag));
                recordingTag = delayTag;

                if (trimFirst > 0)
                {
                    // atrim start - removes the beginning of the audio of the first track.
                    filterChains.Add(recording.GetAudioStartTrimFilterChain(recordingTag, trimFirstTag, trimFirst));
                    recordingTag = trimFirstTag;
                }

                if (trimLast > 0 && recording.StopTimestamp == StopTimestamp)
                {
                    // atrim end - removes the ending of the audio for the last track.
                    filterChains.Add(recording.GetAudioEndTrimFilterChain(recordingTag, trimLastTag, trimLast));
                    recordingTag = trimLastTag;
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
                _Logger.LogError("--trim-first and --trim-last are not supported for video.");
                return false;
            }

            // set output file name
            VideoFileName = $"{ProcessOutputFileName(options.OutputFileName)}_video.{options.VideoContainer}";

            // get output file path
            VideoFile = Path.Combine(GetOutputPath(options), VideoFileName);

            // initialize recordings
            var recordingIndex = 0;
            var recordings = CompletedRecordings.Where(x => x.VideoFileExists && x.VideoStartTimestamp != null && x.VideoStopTimestamp != null).ToArray();
            foreach (var recording in recordings)
            {
                if (options.DryRun)
                {
                    recording.SetVideoSegments();
                }
                else
                {
                    recording.VideoCodec = await GetVideoCodec(recording).ConfigureAwait(false);
                    recording.SetVideoSegments(await ParseVideoSegments(recording).ConfigureAwait(false));
                }
            }

            recordings = recordings.Where(x => x.VideoSegments.Length > 0).ToArray();

            if (recordings.Length == 0)
            {
                _Logger.LogInformation("Session has no video segments.");
                return true;
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
                        Layout = Layout.Calculate(options.Layout, options.CameraWeight, options.ScreenWeight, new LayoutInput[0], layoutOutput, options.JavaScriptFile),
                        Segments = new VideoSegment[0]
                    });
                }

                VideoChunk chunk;
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
                        chunk.Layout = Layout.Calculate(options.Layout, options.CameraWeight, options.ScreenWeight, chunk.Segments.Select(x => x.GetLayoutInput()).ToArray(), layoutOutput, options.JavaScriptFile);
                    }
                    else
                    {
                        chunk.Layout = Layout.Calculate(options.Layout, options.CameraWeight, options.ScreenWeight, staticLayoutInputs.ToArray(), layoutOutput, options.JavaScriptFile);
                    }

                    chunks.Add(chunk);
                }
            }

            var ONE_MS = new TimeSpan(10000);                       // 1 Tick = 100ns, 10000 Ticks = 1ms

            // insert blank chunk if needed
            if (chunks.Count > 0 && (chunks[0].StartTimestamp - StartTimestamp).Duration() >= ONE_MS)
            {
                chunks.Insert(0, new VideoChunk
                {
                    StartTimestamp = StartTimestamp,
                    StopTimestamp = chunks[0].StartTimestamp,
                    Layout = Layout.Calculate(options.Layout, options.CameraWeight, options.ScreenWeight, new LayoutInput[0], layoutOutput, options.JavaScriptFile),
                    Segments = new VideoSegment[0]
                });
            }
            
            chunks.RemoveAll(chunk => chunk.Duration < ONE_MS);

            // build filter chains
            var filterChainsAndTags = GetVideoFilterChainsAndTags(chunks.ToArray(), options);

            // each filter chain represents a single chunk
            var chunkFiles = new List<string>();
            for (var i = 0; i < filterChainsAndTags.Length; i++)
            {
                var filterChainAndTag = filterChainsAndTags[i];
                var filterChain = filterChainAndTag.Item1;
                var filterTag = filterChainAndTag.Item2;

                var chunkFilterChainFileName = $"{ProcessOutputFileName(options.OutputFileName)}_video_chunk_{i}.filter";
                var chunkFilterChainFile = Path.Combine(GetTempPath(options), chunkFilterChainFileName);
                var chunkFileName = $"{ProcessOutputFileName(options.OutputFileName)}_video_chunk_{i}.mkv";
                var chunkFile = Path.Combine(GetTempPath(options), chunkFileName);

                chunkFiles.Add(chunkFile);

                try
                {
                    // construct argument list
                    var arguments = new List<string>
                    {
                        "-y" // overwrite output files without asking
                    };
                    arguments.AddRange(recordings.Select(recording =>
                    {
                        return $"-i {recording.VideoFile}";
                    }));
                    try
                    {
                        if (options.NoFilterFiles)
                        {
                            arguments.Add($@"-filter_complex ""{filterChain}""");
                        }
                        else
                        {
                            var chunkFilterChainFilePath = Path.GetDirectoryName(chunkFilterChainFile);
                            if (!Directory.Exists(chunkFilterChainFilePath))
                            {
                                Directory.CreateDirectory(chunkFilterChainFilePath);
                            }

                            System.IO.File.WriteAllText(chunkFilterChainFile, filterChain);
                            arguments.Add($@"-filter_complex_script {chunkFilterChainFile}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logger.LogError(ex, "Could not create temporary chunk filter chain file '{ChunkFilterChainFileName}'.", chunkFilterChainFileName);
                        _Logger.LogWarning($"Chunk filter chain will be passed as command-line argument.");
                        arguments.Add($@"-filter_complex ""{filterChain}""");
                    }
                    arguments.Add($@"-map ""{filterTag}""");
                    if (options.VideoCodec != null)
                    {
                        arguments.Add($"-codec:v {options.VideoCodec}");
                    }
                    arguments.Add(chunkFile);

                    if (options.DryRun)
                    {
                        return true;
                    }

                    var outputPath = Path.GetDirectoryName(chunkFile);
                    if (!Directory.Exists(outputPath))
                    {
                        Directory.CreateDirectory(outputPath);
                    }

                    // run it
                    await _Utility.FFmpeg(string.Join(" ", arguments)).ConfigureAwait(false);
                }
                finally
                {
                    try
                    {
                        if (System.IO.File.Exists(chunkFilterChainFile) && !options.SaveTempFiles)
                        {
                            System.IO.File.Delete(chunkFilterChainFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logger.LogError(ex, "Could not delete temporary chunk filter chain file '{ChunkFilterChainFileName}'.", chunkFilterChainFileName);
                    }
                }
            }

            if (chunkFiles.Count == 0)
            {
                return true;
            }

            var chunkListFileName = $"{ProcessOutputFileName(options.OutputFileName)}_video_chunks.list";
            var chunkListFile = Path.Combine(GetTempPath(options), chunkListFileName);
            var chunkListFilePath = Path.GetDirectoryName(chunkListFile);
            if (!Directory.Exists(chunkListFilePath))
            {
                Directory.CreateDirectory(chunkListFilePath);
            }

            System.IO.File.WriteAllText(chunkListFile, string.Join(Environment.NewLine, chunkFiles.Select(chunkFile => $"file '{chunkFile}'")));

            try
            {
                // construct argument list
                var arguments = new List<string>
                {
                    "-y", // overwrite output files without asking
                    "-safe 0",
                    "-f concat",
                };
                arguments.Add($"-i {chunkListFile}");
                arguments.Add("-c copy");
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
                await _Utility.FFmpeg(string.Join(" ", arguments)).ConfigureAwait(false);

                return VideoFileExists;
            }
            finally
            {
                try
                {
                    if (System.IO.File.Exists(chunkListFile) && !options.SaveTempFiles)
                    {
                        System.IO.File.Delete(chunkListFile);
                    }
                }
                catch (Exception ex)
                {
                    _Logger.LogError(ex, "Could not delete temporary chunk list file '{ChunkListFile}'.", chunkListFile);
                }

                foreach (var chunkFile in chunkFiles)
                {
                    try
                    {
                        if (System.IO.File.Exists(chunkFile) && !options.SaveTempFiles)
                        {
                            System.IO.File.Delete(chunkFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logger.LogError(ex, "Could not delete temporary chunk file '{ChunkFile}'.", chunkFile);
                    }
                }
            }
        }

        private ValueTuple<string, string>[] GetVideoFilterChainsAndTags(VideoChunk[] chunks, MuxOptions options)
        {
            // process each chunk
            var filterChainsAndTags = new List<ValueTuple<string, string>>();
            for (var chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                var chunk = chunks[chunkIndex];
                var chunkFilterChains = new List<string>();

                // initialize tag
                var colorTag = $"[vcolor_{chunkIndex}]";
                var chunkTag = colorTag;

                // color
                chunkFilterChains.Add(chunk.GetColorFilterChain(options.BackgroundColor, colorTag));

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
                    chunkFilterChains.Add(chunk.GetFpsFilterChain(options.FrameRate, segmentTag, fpsTag));
                    segmentTag = fpsTag;

                    // trim
                    chunkFilterChains.Add(chunk.GetTrimFilterChain(recording, segmentTag, trimTag));
                    segmentTag = trimTag;

                    // then scale
                    chunkFilterChains.Add(view.GetSizeFilterChain(segmentTag, scaleTag));
                    segmentTag = scaleTag;

                    // then crop (optional)
                    if (options.Crop)
                    {
                        chunkFilterChains.Add(view.GetCropFilterChain(segmentTag, cropTag));
                        segmentTag = cropTag;
                    }

                    // then overlay
                    chunkFilterChains.Add(view.GetOverlayChain(options.Crop, chunkTag, segmentTag, overlayTag));
                    chunkTag = overlayTag;
                }

                filterChainsAndTags.Add((string.Join(";", chunkFilterChains), chunkTag));
            }

            return filterChainsAndTags.ToArray();
        }

        public async Task<string> GetAudioCodec(Recording recording)
        {
            var lines = await _Utility.FFprobe($"-v quiet -select_streams a:0 -show_entries stream=codec_name -print_format csv=print_section=0 {recording.AudioFile}").ConfigureAwait(false);
            if (lines.Length == 0)
            {
                return null;
            }
            return lines[0].Trim();
        }

        public async Task<string> GetVideoCodec(Recording recording)
        {
            var lines = await _Utility.FFprobe($"-v quiet -select_streams v:0 -show_entries stream=codec_name -print_format csv=print_section=0 {recording.VideoFile}").ConfigureAwait(false);
            if (lines.Length == 0)
            {
                return null;
            }
            return lines[0].Trim();
        }

        public async Task<VideoSegment[]> ParseVideoSegments(Recording recording)
        {
            var lines = await _Utility.FFprobe($"-v quiet -select_streams v:0 -show_frames -show_entries frame=pkt_pts_time,width,height -print_format csv=item_sep=|:nokey=1:print_section=0 {recording.VideoFile}").ConfigureAwait(false);

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
                        _Logger.LogError("Could not parse ffprobe output: {Line}", line);
                    }
                }
                else
                {
                    _Logger.LogError("Unexpected ffprobe output: {Line}", line);
                }
            }

            // stop last segment (if applicable)
            if (segments.Count > 0)
            {
                segments.Last().StopTimestamp = recording.VideoStopTimestamp.Value;
            }

            return segments.ToArray();
        }

        public bool WriteMetadata(MuxOptions options)
        {
            MetadataFile = Path.Combine(Path.GetDirectoryName(File), $"{Path.GetFileNameWithoutExtension(File)}.json");

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

                var metadataFilePath = Path.GetDirectoryName(MetadataFile);
                if (!Directory.Exists(metadataFilePath))
                {
                    Directory.CreateDirectory(metadataFilePath);
                }

                System.IO.File.WriteAllText(MetadataFile, JsonConvert.SerializeObject(ToModel()));

                return MetadataFileExists;
            }
            finally
            {
                File = file;
                AudioFile = audioFile;
                VideoFile = videoFile;
            }
        }

        public Models.Session ToModel()
        {
            return new Models.Session
            {
                Id = Id,
                ExternalId = ExternalId,
                ApplicationId = ApplicationId,
                ChannelId = ChannelId,
                StartTimestamp = StartTimestamp,
                StopTimestamp = StopTimestamp,
                File = File,
                AudioFile = AudioFile,
                VideoFile = VideoFile,
                Clients = CompletedClients.Select(client => client.ToModel()).ToArray(),
            };
        }
    }
}
