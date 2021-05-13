using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

namespace FM.LiveSwitch.Mux
{
    public class Application
    {
        public string Id { get; private set; }

        public string ExternalId { get; private set; }

        public Channel[] Channels
        {
            get { return _Channels.Values.ToArray(); }
        }

        private readonly Dictionary<string, Channel> _Channels = new Dictionary<string, Channel>();

        public Application(string id, string externalId)
        {
            Id = id;
            ExternalId = externalId;
        }

        public bool ProcessLogEntry(LogEntry logEntry, MuxOptions options, ILoggerFactory loggerFactory)
        {
            var channelId = logEntry.ChannelId;
            if (channelId == null)
            {
                return false;
            }

            if (!_Channels.TryGetValue(channelId, out var channel))
            {
                _Channels[channelId] = channel = new Channel(channelId, Id, ExternalId);
            }

            return channel.ProcessLogEntry(logEntry, options, loggerFactory);
        }
    }
}
