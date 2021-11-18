using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace FM.LiveSwitch.Mux.Test
{
    public class ContextTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(1)]
        public async Task VideoDelayUpdatesSession(double videoDelay)
        {
            var fileUtilityMock = new Mock<IFileUtility>();
            fileUtilityMock.Setup(fu => fu.Exists(It.IsAny<string>())).Returns(true);

            using var loggerFactory = LoggerFactory.Create(builder => { });
            var start = new DateTime(1970, 1, 1, 0, 0, 0);
            var stop = start.AddMilliseconds(20);

            var externalId = "externalId";
            var applicationId = "applicationId";
            var channelId = "channelId";
            var clientId = "clientId";
            var connectionId = "connectionId";
            var deviceId = "deviceId";
            var userId = "userId";

            var options = new MuxOptions();

            var context = new Context(fileUtilityMock.Object, loggerFactory);
            await context.ProcessLogEntry(new LogEntry
            {
                ExternalId = externalId,
                ApplicationId = applicationId,
                ChannelId = channelId,
                ClientId = clientId,
                ConnectionId = connectionId,
                DeviceId = deviceId,
                Type = LogEntry.TypeStartRecording,
                Timestamp = start,
                UserId = userId,
            }, options).ConfigureAwait(false);
            await context.ProcessLogEntry(new LogEntry
            {
                ExternalId = externalId,
                ApplicationId = applicationId,
                ChannelId = channelId,
                ClientId = clientId,
                ConnectionId = connectionId,
                DeviceId = deviceId,
                Data = new LogEntryData
                {
                    AudioFile = $"/null/{connectionId}.mka",
                    AudioFirstFrameTimestamp = start,
                    VideoFile = $"/null/{connectionId}.mkv",
                    VideoFirstFrameTimestamp = start,
                    VideoDelay = videoDelay
                },
                Type = LogEntry.TypeStopRecording,
                Timestamp = stop,
                UserId = userId,
            }, options).ConfigureAwait(false);

            var audioStart = start;
            var videoStart = start;
            var audioStop = audioStart.AddMilliseconds(20);
            var videoStop = videoStart.AddMilliseconds(1);

            videoStart = videoStart.AddSeconds(videoDelay);
            videoStop = videoStop.AddSeconds(videoDelay);

            start = new DateTime(Math.Min(audioStart.Ticks, videoStart.Ticks));
            stop = new DateTime(Math.Max(audioStop.Ticks, videoStop.Ticks));

            Assert.Single(context.Applications);

            var application = context.Applications.Single();

            Assert.Equal(externalId, application.ExternalId);
            Assert.Equal(applicationId, application.Id);
            Assert.Single(application.Channels);

            var channel = application.Channels.Single();

            Assert.False(channel.Active);
            Assert.Empty(channel.ActiveClients);
            Assert.Equal(externalId, channel.ExternalId);
            Assert.Equal(applicationId, channel.ApplicationId);
            Assert.Single(channel.CompletedSessions);
            Assert.Empty(channel.CompletedClients);
            Assert.Equal(channelId, channel.Id);
            Assert.Null(channel.StartTimestamp);
            Assert.Null(channel.StopTimestamp);

            var session = channel.CompletedSessions.Single();

            Assert.Equal(externalId, session.ExternalId);
            Assert.Equal(applicationId, session.ApplicationId);
            Assert.Equal(channelId, session.ChannelId);
            Assert.Single(session.CompletedClients);
            Assert.Single(session.CompletedConnections);
            Assert.Single(session.CompletedRecordings);
            Assert.Equal(stop - start, session.Duration);
            Assert.Equal(start, session.StartTimestamp);
            Assert.Equal(stop, session.StopTimestamp);

            var client = session.CompletedClients.Single();

            Assert.False(client.Active);
            Assert.Empty(client.ActiveConnections);
            Assert.Empty(client.ActiveRecordings);
            Assert.Equal(externalId, client.ExternalId);
            Assert.Equal(applicationId, client.ApplicationId);
            Assert.Equal(channelId, client.ChannelId);
            Assert.Single(client.CompletedConnections);
            Assert.Single(client.CompletedRecordings);
            Assert.Equal(deviceId, client.DeviceId);
            Assert.Equal(clientId, client.Id);
            Assert.Equal(start, client.StartTimestamp);
            Assert.Equal(stop, client.StopTimestamp);
            Assert.Equal(userId, client.UserId);

            var connection = session.CompletedConnections.Single();

            Assert.Equal(connection, client.CompletedConnections.Single());
            Assert.False(connection.Active);
            Assert.Null(connection.ActiveRecording);
            Assert.Equal(externalId, connection.ExternalId);
            Assert.Equal(applicationId, connection.ApplicationId);
            Assert.Equal(channelId, connection.ChannelId);
            Assert.Equal(client, connection.Client);
            Assert.Equal(clientId, connection.ClientId);
            Assert.Equal(deviceId, connection.DeviceId);
            Assert.Equal(connectionId, connection.Id);
            Assert.Equal(start, connection.StartTimestamp);
            Assert.Equal(stop, connection.StopTimestamp);
            Assert.Equal(userId, connection.UserId);

            var recording = session.CompletedRecordings.Single();
            var recordingAudioId = GetRecordingAudioId(connectionId, audioStart);
            var recordingVideoId = GetRecordingVideoId(connectionId, videoStart);
            var recordingId = GetRecordingId(recordingAudioId, recordingVideoId);

            Assert.Equal(recording, client.CompletedRecordings.Single());
            Assert.Equal(recordingAudioId, recording.AudioId);
            Assert.Equal(0, recording.AudioIndex);
            Assert.Equal(audioStart, recording.AudioStartTimestamp);
            Assert.Equal(audioStop, recording.AudioStopTimestamp);
            Assert.Equal(connection, recording.Connection);
            Assert.Equal(stop - start, recording.Duration);
            Assert.Equal(recordingId, recording.Id);
            Assert.Equal(start, recording.StartTimestamp);
            Assert.Equal(stop, recording.StopTimestamp);
            Assert.Empty(recording.Updates);
            Assert.Equal(recordingVideoId, recording.VideoId);
            Assert.Equal(0, recording.VideoIndex);
            Assert.Equal(videoStart, recording.VideoStartTimestamp);
            Assert.Equal(videoStop, recording.VideoStopTimestamp);

            var sessionId = GetSessionId(new[] { recordingId });

            Assert.Equal(sessionId, session.Id);
        }

        private Guid GetSessionId(Guid[] recordingIds)
        {
            var input = string.Join(":", recordingIds.OrderBy(x => x));
            using var md5 = MD5.Create();
            return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(input)));
        }

        private Guid GetRecordingId(Guid recordingAudioId, Guid recordingVideoId)
        {
            var input = $"{recordingAudioId}:{recordingVideoId}";
            using var md5 = MD5.Create();
            return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(input)));
        }

        public Guid GetRecordingAudioId(string connectionId, DateTimeOffset audioStartTimestamp)
        {
            var input = $"{audioStartTimestamp.Ticks}:{connectionId}:audio";
            using var md5 = MD5.Create();
            return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(input)));
        }

        public Guid GetRecordingVideoId(string connectionId, DateTimeOffset videoStartTimestamp)
        {
            var input = $"{videoStartTimestamp.Ticks}:{connectionId}:video";
            using var md5 = MD5.Create();
            return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(input)));
        }
    }
}
