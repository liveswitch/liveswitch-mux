using CommandLine;
using Divergic.Logging.Xunit;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace FM.LiveSwitch.Mux.Test
{
    public class RecordingTests
    {
        private readonly ILoggerFactory _LoggerFactory;

        public RecordingTests(ITestOutputHelper output)
        {
            _LoggerFactory = LogFactory.Create(output);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void SetVideoSegments(bool dryRun, bool withUpdates)
        {
            var start = new DateTime(1970, 1, 1, 0, 0, 0);
            var recording = new Recording
            {
                StartTimestamp = start,
                StopTimestamp = start.AddMinutes(5)
            };

            if (withUpdates)
            {
                recording.Update(new LogEntry
                {
                    Type = LogEntry.TypeStartRecording,
                    Timestamp = start,
                    Data = new LogEntryData
                    {
                        ConnectionTag = "0"
                    }
                });
                recording.Update(new LogEntry
                {
                    Type = LogEntry.TypeUpdateRecording,
                    Timestamp = start.AddMinutes(2),
                    Data = new LogEntryData
                    {
                        AudioMuted = true,
                        AudioDisabled = true,
                        ConnectionTag = "2"
                    }
                });
                recording.Update(new LogEntry
                {
                    Type = LogEntry.TypeUpdateRecording,
                    Timestamp = start.AddMinutes(3.5),
                    Data = new LogEntryData
                    {
                        ConnectionTag = "3.5"
                    }
                });
                recording.Update(new LogEntry
                {
                    Type = LogEntry.TypeUpdateRecording,
                    Timestamp = start.AddMinutes(4.25),
                    Data = new LogEntryData
                    {
                        VideoMuted = true,
                        VideoDisabled = true,
                        ConnectionTag = "4.25"
                    }
                });
                recording.Update(new LogEntry
                {
                    Type = LogEntry.TypeUpdateRecording,
                    Timestamp = start.AddMinutes(4.75),
                    Data = new LogEntryData
                    {
                        ConnectionTag = "4.75"
                    }
                });
                recording.Update(new LogEntry
                {
                    Type = LogEntry.TypeStopRecording,
                    Timestamp = start.AddMinutes(5),
                    Data = new LogEntryData
                    {
                        ConnectionTag = "4.75"
                    }
                }, true);
            }

            if (dryRun)
            {
                recording.SetVideoSegments();
            }
            else
            {
                recording.SetVideoSegments(new[]
                {
                    new VideoSegment
                    {
                        Recording = recording,
                        StartTimestamp = start.AddSeconds(1), // video starts 1 second after audio
                        StopTimestamp = start.AddMinutes(1),
                        Size = new Size(320, 240)
                    },
                    new VideoSegment
                    {
                        Recording = recording,
                        StartTimestamp = start.AddMinutes(1),
                        StopTimestamp = start.AddMinutes(2),
                        Size = new Size(640, 480)
                    },
                    new VideoSegment
                    {
                        Recording = recording,
                        StartTimestamp = start.AddMinutes(2),
                        StopTimestamp = start.AddMinutes(3),
                        Size = new Size(320, 240)
                    },
                    new VideoSegment
                    {
                        Recording = recording,
                        StartTimestamp = start.AddMinutes(3),
                        StopTimestamp = start.AddMinutes(4),
                        Size = new Size(640, 480)
                    },
                    new VideoSegment
                    {
                        Recording = recording,
                        StartTimestamp = start.AddMinutes(4),
                        StopTimestamp = start.AddMinutes(5).AddSeconds(-1), // video ends 1 second before audio
                        Size = new Size(320, 240)
                    },
                });
            }

            if (dryRun && withUpdates)
            {
                // dry-run, updates
                Assert.Equal(5, recording.VideoSegments.Length);
                
                Assert.Equal(start, recording.VideoSegments[0].StartTimestamp);
                Assert.Equal(start.AddMinutes(2), recording.VideoSegments[0].StopTimestamp);
                Assert.Equal(Size.Empty, recording.VideoSegments[0].Size);
                Assert.Equal("0", recording.VideoSegments[0].ConnectionTag);
                Assert.False(recording.VideoSegments[0].AudioDisabled);
                Assert.False(recording.VideoSegments[0].AudioMuted);
                Assert.False(recording.VideoSegments[0].VideoDisabled);
                Assert.False(recording.VideoSegments[0].VideoMuted);

                Assert.Equal(start.AddMinutes(2), recording.VideoSegments[1].StartTimestamp);
                Assert.Equal(start.AddMinutes(3.5), recording.VideoSegments[1].StopTimestamp);
                Assert.Equal(Size.Empty, recording.VideoSegments[1].Size);
                Assert.Equal("2", recording.VideoSegments[1].ConnectionTag);
                Assert.True(recording.VideoSegments[1].AudioDisabled);
                Assert.True(recording.VideoSegments[1].AudioMuted);
                Assert.False(recording.VideoSegments[1].VideoDisabled);
                Assert.False(recording.VideoSegments[1].VideoMuted);

                Assert.Equal(start.AddMinutes(3.5), recording.VideoSegments[2].StartTimestamp);
                Assert.Equal(start.AddMinutes(4.25), recording.VideoSegments[2].StopTimestamp);
                Assert.Equal(Size.Empty, recording.VideoSegments[2].Size);
                Assert.Equal("3.5", recording.VideoSegments[2].ConnectionTag);
                Assert.False(recording.VideoSegments[2].AudioDisabled);
                Assert.False(recording.VideoSegments[2].AudioMuted);
                Assert.False(recording.VideoSegments[2].VideoDisabled);
                Assert.False(recording.VideoSegments[2].VideoMuted);

                Assert.Equal(start.AddMinutes(4.25), recording.VideoSegments[3].StartTimestamp);
                Assert.Equal(start.AddMinutes(4.75), recording.VideoSegments[3].StopTimestamp);
                Assert.Equal(Size.Empty, recording.VideoSegments[3].Size);
                Assert.Equal("4.25", recording.VideoSegments[3].ConnectionTag);
                Assert.False(recording.VideoSegments[3].AudioDisabled);
                Assert.False(recording.VideoSegments[3].AudioMuted);
                Assert.True(recording.VideoSegments[3].VideoDisabled);
                Assert.True(recording.VideoSegments[3].VideoMuted);

                Assert.Equal(start.AddMinutes(4.75), recording.VideoSegments[4].StartTimestamp);
                Assert.Equal(start.AddMinutes(5), recording.VideoSegments[4].StopTimestamp);
                Assert.Equal(Size.Empty, recording.VideoSegments[4].Size);
                Assert.Equal("4.75", recording.VideoSegments[4].ConnectionTag);
                Assert.False(recording.VideoSegments[4].AudioDisabled);
                Assert.False(recording.VideoSegments[4].AudioMuted);
                Assert.False(recording.VideoSegments[4].VideoDisabled);
                Assert.False(recording.VideoSegments[4].VideoMuted);
            }
            else if (dryRun)
            {
                // dry-run, no updates
                Assert.Single(recording.VideoSegments);

                Assert.Equal(start, recording.VideoSegments[0].StartTimestamp);
                Assert.Equal(start.AddMinutes(5), recording.VideoSegments[0].StopTimestamp);
                Assert.Equal(Size.Empty, recording.VideoSegments[0].Size);
                Assert.Null(recording.VideoSegments[0].ConnectionTag);
                Assert.False(recording.VideoSegments[0].AudioDisabled);
                Assert.False(recording.VideoSegments[0].AudioMuted);
                Assert.False(recording.VideoSegments[0].VideoDisabled);
                Assert.False(recording.VideoSegments[0].VideoMuted);
            }
            else if (withUpdates)
            {
                // not dry-run, updates
                Assert.Equal(8, recording.VideoSegments.Length);

                Assert.Equal(start.AddSeconds(1), recording.VideoSegments[0].StartTimestamp);
                Assert.Equal(start.AddMinutes(1), recording.VideoSegments[0].StopTimestamp);
                Assert.Equal(new Size(320, 240), recording.VideoSegments[0].Size);
                Assert.Equal("0", recording.VideoSegments[0].ConnectionTag);
                Assert.False(recording.VideoSegments[0].AudioDisabled);
                Assert.False(recording.VideoSegments[0].AudioMuted);
                Assert.False(recording.VideoSegments[0].VideoDisabled);
                Assert.False(recording.VideoSegments[0].VideoMuted);

                Assert.Equal(start.AddMinutes(1), recording.VideoSegments[1].StartTimestamp);
                Assert.Equal(start.AddMinutes(2), recording.VideoSegments[1].StopTimestamp);
                Assert.Equal(new Size(640, 480), recording.VideoSegments[1].Size);
                Assert.Equal("0", recording.VideoSegments[1].ConnectionTag);
                Assert.False(recording.VideoSegments[1].AudioDisabled);
                Assert.False(recording.VideoSegments[1].AudioMuted);
                Assert.False(recording.VideoSegments[1].VideoDisabled);
                Assert.False(recording.VideoSegments[1].VideoMuted);

                Assert.Equal(start.AddMinutes(2), recording.VideoSegments[2].StartTimestamp);
                Assert.Equal(start.AddMinutes(3), recording.VideoSegments[2].StopTimestamp);
                Assert.Equal(new Size(320, 240), recording.VideoSegments[2].Size);
                Assert.Equal("2", recording.VideoSegments[2].ConnectionTag);
                Assert.True(recording.VideoSegments[2].AudioDisabled);
                Assert.True(recording.VideoSegments[2].AudioMuted);
                Assert.False(recording.VideoSegments[2].VideoDisabled);
                Assert.False(recording.VideoSegments[2].VideoMuted);

                Assert.Equal(start.AddMinutes(3), recording.VideoSegments[3].StartTimestamp);
                Assert.Equal(start.AddMinutes(3.5), recording.VideoSegments[3].StopTimestamp);
                Assert.Equal(new Size(640, 480), recording.VideoSegments[3].Size);
                Assert.Equal("2", recording.VideoSegments[3].ConnectionTag);
                Assert.True(recording.VideoSegments[3].AudioDisabled);
                Assert.True(recording.VideoSegments[3].AudioMuted);
                Assert.False(recording.VideoSegments[3].VideoDisabled);
                Assert.False(recording.VideoSegments[3].VideoMuted);

                Assert.Equal(start.AddMinutes(3.5), recording.VideoSegments[4].StartTimestamp);
                Assert.Equal(start.AddMinutes(4), recording.VideoSegments[4].StopTimestamp);
                Assert.Equal(new Size(640, 480), recording.VideoSegments[4].Size);
                Assert.Equal("3.5", recording.VideoSegments[4].ConnectionTag);
                Assert.False(recording.VideoSegments[4].AudioDisabled);
                Assert.False(recording.VideoSegments[4].AudioMuted);
                Assert.False(recording.VideoSegments[4].VideoDisabled);
                Assert.False(recording.VideoSegments[4].VideoMuted);

                Assert.Equal(start.AddMinutes(4), recording.VideoSegments[5].StartTimestamp);
                Assert.Equal(start.AddMinutes(4.25), recording.VideoSegments[5].StopTimestamp);
                Assert.Equal(new Size(320, 240), recording.VideoSegments[5].Size);
                Assert.Equal("3.5", recording.VideoSegments[5].ConnectionTag);
                Assert.False(recording.VideoSegments[5].AudioDisabled);
                Assert.False(recording.VideoSegments[5].AudioMuted);
                Assert.False(recording.VideoSegments[5].VideoDisabled);
                Assert.False(recording.VideoSegments[5].VideoMuted);

                Assert.Equal(start.AddMinutes(4.25), recording.VideoSegments[6].StartTimestamp);
                Assert.Equal(start.AddMinutes(4.75), recording.VideoSegments[6].StopTimestamp);
                Assert.Equal(new Size(320, 240), recording.VideoSegments[6].Size);
                Assert.Equal("4.25", recording.VideoSegments[6].ConnectionTag);
                Assert.False(recording.VideoSegments[6].AudioDisabled);
                Assert.False(recording.VideoSegments[6].AudioMuted);
                Assert.True(recording.VideoSegments[6].VideoDisabled);
                Assert.True(recording.VideoSegments[6].VideoMuted);

                Assert.Equal(start.AddMinutes(4.75), recording.VideoSegments[7].StartTimestamp);
                Assert.Equal(start.AddMinutes(5).AddSeconds(-1), recording.VideoSegments[7].StopTimestamp);
                Assert.Equal(new Size(320, 240), recording.VideoSegments[7].Size);
                Assert.Equal("4.75", recording.VideoSegments[7].ConnectionTag);
                Assert.False(recording.VideoSegments[7].AudioDisabled);
                Assert.False(recording.VideoSegments[7].AudioMuted);
                Assert.False(recording.VideoSegments[7].VideoDisabled);
                Assert.False(recording.VideoSegments[7].VideoMuted);
            }
            else
            {
                // not dry-run, no updates
                Assert.Equal(5, recording.VideoSegments.Length);

                Assert.Equal(start.AddSeconds(1), recording.VideoSegments[0].StartTimestamp);
                Assert.Equal(start.AddMinutes(1), recording.VideoSegments[0].StopTimestamp);
                Assert.Equal(new Size(320, 240), recording.VideoSegments[0].Size);
                Assert.Null(recording.VideoSegments[0].ConnectionTag);
                Assert.False(recording.VideoSegments[0].AudioDisabled);
                Assert.False(recording.VideoSegments[0].AudioMuted);
                Assert.False(recording.VideoSegments[0].VideoDisabled);
                Assert.False(recording.VideoSegments[0].VideoMuted);

                Assert.Equal(start.AddMinutes(1), recording.VideoSegments[1].StartTimestamp);
                Assert.Equal(start.AddMinutes(2), recording.VideoSegments[1].StopTimestamp);
                Assert.Equal(new Size(640, 480), recording.VideoSegments[1].Size);
                Assert.Null(recording.VideoSegments[1].ConnectionTag);
                Assert.False(recording.VideoSegments[1].AudioDisabled);
                Assert.False(recording.VideoSegments[1].AudioMuted);
                Assert.False(recording.VideoSegments[1].VideoDisabled);
                Assert.False(recording.VideoSegments[1].VideoMuted);

                Assert.Equal(start.AddMinutes(2), recording.VideoSegments[2].StartTimestamp);
                Assert.Equal(start.AddMinutes(3), recording.VideoSegments[2].StopTimestamp);
                Assert.Equal(new Size(320, 240), recording.VideoSegments[2].Size);
                Assert.Null(recording.VideoSegments[2].ConnectionTag);
                Assert.False(recording.VideoSegments[2].AudioDisabled);
                Assert.False(recording.VideoSegments[2].AudioMuted);
                Assert.False(recording.VideoSegments[2].VideoDisabled);
                Assert.False(recording.VideoSegments[2].VideoMuted);

                Assert.Equal(start.AddMinutes(3), recording.VideoSegments[3].StartTimestamp);
                Assert.Equal(start.AddMinutes(4), recording.VideoSegments[3].StopTimestamp);
                Assert.Equal(new Size(640, 480), recording.VideoSegments[3].Size);
                Assert.Null(recording.VideoSegments[3].ConnectionTag);
                Assert.False(recording.VideoSegments[3].AudioDisabled);
                Assert.False(recording.VideoSegments[3].AudioMuted);
                Assert.False(recording.VideoSegments[3].VideoDisabled);
                Assert.False(recording.VideoSegments[3].VideoMuted);

                Assert.Equal(start.AddMinutes(4), recording.VideoSegments[4].StartTimestamp);
                Assert.Equal(start.AddMinutes(5).AddSeconds(-1), recording.VideoSegments[4].StopTimestamp);
                Assert.Equal(new Size(320, 240), recording.VideoSegments[4].Size);
                Assert.Null(recording.VideoSegments[4].ConnectionTag);
                Assert.False(recording.VideoSegments[4].AudioDisabled);
                Assert.False(recording.VideoSegments[4].AudioMuted);
                Assert.False(recording.VideoSegments[4].VideoDisabled);
                Assert.False(recording.VideoSegments[4].VideoMuted);
            }
        }

        [Fact]
        public async Task JsonIntegrityCheckTest()
        {
            var dirPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}");
            Directory.CreateDirectory(dirPath);

            try
            {
                var jsonFilePath = CreateTempFileFromExample(destinationPath: dirPath);
                var result = await RunMuxer(dirPath).ConfigureAwait(false);

                Assert.True(result, "Muxer run failed");
                Assert.True(!File.Exists(jsonFilePath + ".fail"));

                foreach (var field in new[] { "timestamp", "applicationId", "externalId", "channelId", "connectionId", "applicationConfigId", "channelConfigId" })
                {
                    jsonFilePath = CreateTempFileFromExample(destinationPath: dirPath);
                    var jsonBlob = File.ReadAllText(jsonFilePath);

                    jsonBlob = Regex.Replace(jsonBlob, $"\"{field}\"\\s*:\\s*\".*\"\\s*,?", "");
                    File.WriteAllText(jsonFilePath, jsonBlob);
                    result = await RunMuxer(dirPath).ConfigureAwait(false);

                    Assert.True(result, "Muxer run failed");
                    Assert.True(File.Exists(jsonFilePath + ".fail"));
                }

                foreach (var pair in new Dictionary<string, string> { { "timestamp", "1000-09-25T09:08:22.752223Z" }, { "applicationId", "app" }, { "channelId", "channel" },
                                                                  { "connectionId", "ca2f89d2-1d2e-6bf9-81a2-23fa39142aaa" }, { "externalId", "BBBBBBBB" },
                                                                  { "applicationConfigId", "caaff9d2-112e-6b09-8fa2-23fa391421f0" },
                                                                  { "channelConfigId", "ff84a09d-b436-9af2-01f5-ec53cbbdfa37" } })
                {
                    jsonFilePath = CreateTempFileFromExample(destinationPath: dirPath);
                    var jsonBlob = File.ReadAllText(jsonFilePath);

                    jsonBlob = new Regex($"\"{pair.Key}\"([^,]*)(,)?").Replace(jsonBlob, $"\"{pair.Key}\":\"{pair.Value}\"$2", 2);
                    File.WriteAllText(jsonFilePath, jsonBlob);
                    result = await RunMuxer(dirPath).ConfigureAwait(false);

                    Assert.True(result, "Muxer run failed");
                    Assert.True(File.Exists(jsonFilePath + ".fail"));
                }
            }
            finally
            {
                foreach (var file in Directory.GetFiles(dirPath))
                {
                    File.Delete(file);
                }
                Directory.Delete(dirPath);
            }
        }

        [Fact]
        public async Task OrphanSessionTest()
        {
            string jsonFile = null;
            string audioFile = null;
            string videoFile = null;

            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}");
            Directory.CreateDirectory(tempPath);

            try
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine("./", "OrphanSessions"), "*.*", SearchOption.TopDirectoryOnly))
                {
                    File.Copy(file, Path.Combine(tempPath, Path.GetFileName(file)));
                }

                foreach (var file in Directory.EnumerateFiles(tempPath, "*.*", SearchOption.TopDirectoryOnly))
                {
                    if (file.EndsWith("json.rec"))
                    {
                        jsonFile = file;
                    }
                    else if (file.EndsWith("mka.rec"))
                    {
                        audioFile = file;
                    }
                    else if (file.EndsWith("mkv.rec"))
                    {
                        videoFile = file;
                    }
                }

                Assert.NotNull(jsonFile);
                Assert.NotNull(audioFile);
                Assert.NotNull(videoFile);

                var newJsonFile = Path.Combine(tempPath, Path.GetFileNameWithoutExtension(jsonFile));
                var baseName = Path.GetFileNameWithoutExtension(newJsonFile);
                var newAudioFile = Path.Combine(tempPath, baseName) + ".mka";
                var newVideoFile = Path.Combine(tempPath, baseName) + ".mkv";
                var result = await RunMuxer(tempPath).ConfigureAwait(false);

                Assert.True(result, "Muxer run failed");
                Assert.False(File.Exists(jsonFile));
                Assert.False(File.Exists(audioFile));
                Assert.False(File.Exists(videoFile));

                Assert.True(File.Exists(newJsonFile));
                Assert.True(File.Exists(newAudioFile));
                Assert.True(File.Exists(newVideoFile));

                using (FileStream stream = new FileStream(newJsonFile, FileMode.Open, FileAccess.Read, FileShare.None))
                using (StreamReader sr = new StreamReader(stream))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    var jsonDoc = JArray.Load(reader);
                    Assert.Equal(2, jsonDoc.Count);

                    var section1 = jsonDoc[0];
                    var section2 = jsonDoc[1];
                    var timestamp1 = section1["timestamp"]?.Value<DateTime>();
                    var timestamp2 = section2["timestamp"]?.Value<DateTime>();

                    Assert.NotNull(timestamp1);
                    Assert.NotNull(timestamp2);
                    Assert.True(timestamp1 <= timestamp2);

                    var data = section2["data"];
                    var jsonAudioFile = data["audioFile"]?.Value<string>();
                    var jsonVideoFile = data["videoFile"]?.Value<string>();

                    Assert.NotNull(jsonAudioFile);
                    Assert.NotNull(jsonVideoFile);
                    Assert.Equal(jsonAudioFile, newAudioFile);
                    Assert.Equal(jsonVideoFile, newVideoFile);

                    var firstVideoTimestamp = data["videoFirstFrameTimestamp"]?.Value<DateTime>();
                    var lastVideoTimestamp = data["videoLastFrameTimestamp"]?.Value<DateTime>();
                    var firstAudioTimestamp = data["audioFirstFrameTimestamp"]?.Value<DateTime>();
                    var lastAudioTimestamp = data["audioLastFrameTimestamp"]?.Value<DateTime>();

                    Assert.NotNull(firstVideoTimestamp);
                    Assert.NotNull(lastVideoTimestamp);
                    Assert.NotNull(firstAudioTimestamp);
                    Assert.NotNull(lastAudioTimestamp);
                    Assert.Equal(firstAudioTimestamp, timestamp1);
                    Assert.Equal(firstVideoTimestamp, timestamp1);
                    Assert.True(firstVideoTimestamp <= lastVideoTimestamp);
                    Assert.True(firstAudioTimestamp <= lastAudioTimestamp);

                    var audioDuration = (lastAudioTimestamp - firstAudioTimestamp)?.Seconds;
                    var videoDuration = (lastVideoTimestamp - firstVideoTimestamp)?.Seconds;

                    Assert.Equal(16, audioDuration);
                    Assert.Equal(9, videoDuration);
                }
            }
            finally
            {
                foreach (var file in Directory.EnumerateFiles(tempPath, "*.*", SearchOption.TopDirectoryOnly))
                {
                    File.Delete(file);
                }
                Directory.Delete(tempPath);
            }
        }

        private string CreateTempFileFromExample(string extension = "json", string destinationPath = null)
        {
            var jsonFilePath = Path.Combine("./", "ExampleJson", "example.json");
            var audioFile = Path.Combine(destinationPath ?? Path.GetTempPath(), $"{Guid.NewGuid()}.tmp");
            var videoFile = Path.Combine(destinationPath ?? Path.GetTempPath(), $"{Guid.NewGuid()}.tmp");
            var jsonBlob = File.ReadAllText(jsonFilePath);

            jsonBlob = jsonBlob
                .Replace("--AudioFile--", audioFile.Replace("\\", "\\\\"))
                .Replace("--VideoFile--", videoFile.Replace("\\", "\\\\"))
                .Replace("b30b91854dca48f5913aa4a63a3801b9", Guid.NewGuid().ToString().Replace("-", ""))
                .Replace("9360c8d8a19b474283c500fd2c1abcff", Guid.NewGuid().ToString().Replace("-", ""));

            File.WriteAllText(audioFile, Guid.NewGuid().ToString());
            File.WriteAllText(videoFile, Guid.NewGuid().ToString());

            var tempFilePath = Path.Combine(destinationPath ?? Path.GetTempPath(), $"{Guid.NewGuid()}.{extension}");
            File.WriteAllText(tempFilePath, jsonBlob);

            return tempFilePath;
        }

        private async Task<bool> RunMuxer(string inputPath)
        {
            var outPath = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            var parseOptions = new MuxOptions();
            var parser = new Parser();
            var success = false;

            try
            {
                parser.ParseArguments<MuxOptions>(new[] 
                {
                    "--dry-run",
                    "--min-orphan-duration",
                    "0",
                    $"-s{StrategyType.Flat}",
                    $"-i{inputPath}",
                    $"-o{outPath}"
                }).WithParsed(options => parseOptions = options);

                success = await new Muxer(parseOptions, _LoggerFactory).Run().ConfigureAwait(false);
            }
            finally
            {
                foreach (var file in outPath.EnumerateFiles())
                {
                    file.Delete();
                }
                outPath.Delete();
            }

            return success;
        }
    }
}
