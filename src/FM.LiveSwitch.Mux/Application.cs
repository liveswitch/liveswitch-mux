using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FM.LiveSwitch.Mux
{
    public class Application
    {
        public string Id { get; private set; }

        public Channel[] Channels
        {
            get { return _Channels.Values.ToArray(); }
        }

        private Dictionary<string, Channel> _Channels = new Dictionary<string, Channel>();

        public Application(string id)
        {
            Id = id;
        }

        public bool ProcessLogEntry(LogEntry logEntry, MuxOptions options)
        {
            var channelId = logEntry.ChannelId;
            if (channelId == null)
            {
                return false;
            }

            if (!_Channels.TryGetValue(channelId, out var channel))
            {
                _Channels[channelId] = channel = new Channel(channelId, Id);
            }

            return channel.ProcessLogEntry(logEntry, options);
        }
    }
}
