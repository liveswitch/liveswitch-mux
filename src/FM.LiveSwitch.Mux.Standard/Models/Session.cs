using System;

namespace FM.LiveSwitch.Mux.Models
{
    public class Session
    {
        public Guid Id { get; set; }

        public string ChannelId { get; set; }

        public string ApplicationId { get; set; }

        public DateTime StartTimestamp { get; set; }

        public DateTime StopTimestamp { get; set; }

        public string File { get; set; }

        public string AudioFile { get; set; }

        public string VideoFile { get; set; }

        public Client[] Clients { get; set; }
    }
}
