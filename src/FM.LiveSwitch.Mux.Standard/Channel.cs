using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FM.LiveSwitch.Mux
{
    public class Channel
    {
        public string Id { get; private set; }

        public string ApplicationId { get; private set; }

        public DateTime? StartTimestamp
        {
            get
            {
                var startTimestamps = CompletedClients.Select(x => x.StartTimestamp).Where(x => x.HasValue);
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
                var stopTimestamps = CompletedClients.Select(x => x.StopTimestamp).Where(x => x.HasValue);
                if (stopTimestamps.Count() == 0)
                {
                    return null;
                }
                return stopTimestamps.Max();
            }
        }

        public Client[] ActiveClients { get { return _Clients.Values.Where(x => x.Active).ToArray(); } }

        public Client[] CompletedClients { get { return _Clients.Values.Where(x => x.Completed).ToArray(); } }

        public bool Active { get { return ActiveClients.Length > 0; } }

        public bool Completed { get { return ActiveClients.Length == 0 && CompletedClients.Length > 0; } }

        private Dictionary<string, Client> _Clients = new Dictionary<string, Client>();

        public Session[] CompletedSessions
        {
            get { return _CompletedSessions.ToArray(); }
        }

        private readonly List<Session> _CompletedSessions = new List<Session>();

        public Channel(string id, string applicationId)
        {
            Id = id;
            ApplicationId = applicationId;
        }

        public bool ProcessLogEntry(LogEntry logEntry, MuxOptions options, ILoggerFactory loggerFactory)
        {
            var clientId = logEntry.ClientId;
            if (clientId == null)
            {
                return false;
            }

            if (!_Clients.TryGetValue(clientId, out var client))
            {
                _Clients[clientId] = client = new Client(clientId, logEntry.DeviceId, logEntry.UserId, Id, ApplicationId);
            }

            var result = client.ProcessLogEntry(logEntry, options);

            if (Completed)
            {
                _CompletedSessions.Add(new Session(Id, ApplicationId, CompletedClients, loggerFactory));
                _Clients = new Dictionary<string, Client>();
            }

            return result;
        }
    }
}
