using System;
using Xunit;

namespace FM.LiveSwitch.Mux.Test
{
    public class RecordingTests
    {
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
    }
}
