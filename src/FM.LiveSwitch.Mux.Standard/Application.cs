using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        private readonly ILoggerFactory _LoggerFactory;

        public Application(string id, string externalId, ILoggerFactory loggerFactory)
        {
            Id = id;
            ExternalId = externalId;

            _LoggerFactory = loggerFactory;
        }

        public async Task<bool> ProcessLogEntry(LogEntry logEntry, MuxOptions options)
        {
            var channelId = logEntry.ChannelId;
            if (channelId == null)
            {
                return false;
            }

            if (!_Channels.TryGetValue(channelId, out var channel))
            {
                _Channels[channelId] = channel = new Channel(channelId, Id, ExternalId, _LoggerFactory);
            }

            return await channel.ProcessLogEntry(logEntry, options).ConfigureAwait(false);
        }
    }
}
