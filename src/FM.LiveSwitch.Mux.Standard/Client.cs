using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    public class Client
    {
        public string Id { get; private set; }

        public string DeviceId { get; private set; }

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
                var startTimestamps = CompletedConnections.Select(x => x.StartTimestamp).Where(x => x.HasValue);
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
                var stopTimestamps = CompletedConnections.Select(x => x.StopTimestamp).Where(x => x.HasValue);
                if (stopTimestamps.Count() == 0)
                {
                    return null;
                }
                return stopTimestamps.Max();
            }
        }

        [JsonIgnore]
        public Recording[] ActiveRecordings { get { return ActiveConnections.Select(x => x.ActiveRecording).ToArray(); } }

        [JsonIgnore]
        public Recording[] CompletedRecordings { get { return CompletedConnections.SelectMany(x => x.CompletedRecordings).OrderBy(x => x.StartTimestamp).ToArray(); } }

        [JsonIgnore]
        public Connection[] ActiveConnections { get { return _Connections.Values.Where(x => x.Active).ToArray(); } }

        [JsonProperty("connections")]
        public Connection[] CompletedConnections { get { return _Connections.Values.Where(x => x.Completed).OrderBy(x => x.StartTimestamp).ToArray(); } }

        [JsonIgnore]
        public bool Active { get { return ActiveConnections.Length > 0; } }

        [JsonIgnore]
        public bool Completed { get { return ActiveConnections.Length == 0 && CompletedConnections.Length > 0; } }

        private readonly Dictionary<string, Connection> _Connections = new Dictionary<string, Connection>();

        private readonly ILoggerFactory _LoggerFactory;

        public Client(string id, string deviceId, string userId, string channelId, string applicationId, string externalId, ILoggerFactory loggerFactory)
        {
            Id = id;
            DeviceId = deviceId;
            UserId = userId;
            ChannelId = channelId;
            ApplicationId = applicationId;
            ExternalId = externalId;

            _LoggerFactory = loggerFactory;
        }

        public async Task<bool> ProcessLogEntry(LogEntry logEntry, MuxOptions options)
        {
            var connectionId = logEntry.ConnectionId;
            if (connectionId == null)
            {
                return false;
            }

            if (!_Connections.TryGetValue(connectionId, out var connection))
            {
                _Connections[connectionId] = connection = new Connection(connectionId, Id, DeviceId, UserId, ChannelId, ApplicationId, ExternalId, _LoggerFactory)
                {
                    Client = this
                };
            }

            return await connection.ProcessLogEntry(logEntry, options).ConfigureAwait(false);
        }

        public Models.Client ToModel()
        {
            return new Models.Client
            {
                Id = Id,
                UserId = UserId,
                DeviceId = DeviceId,
                StartTimestamp = StartTimestamp,
                StopTimestamp = StopTimestamp,
                Connections = CompletedConnections.Select(connection => connection.ToModel()).ToArray()
            };
        }
    }
}
