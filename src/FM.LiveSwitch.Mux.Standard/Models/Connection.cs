using System;

namespace FM.LiveSwitch.Mux.Models
{
    public class Connection
    {
        public string Id { get; set; }

        public DateTime? StartTimestamp { get; set; }

        public DateTime? StopTimestamp { get; set; }

        public Recording[] Recordings { get; set; }
    }
}
