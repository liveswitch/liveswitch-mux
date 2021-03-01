using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace FM.LiveSwitch.Mux
{
    public class SessionTracker
    {
        [JsonProperty]
        public string JsonFile { get; private set; }

        [JsonProperty]
        public string AudioFile { get; set; }

        [JsonProperty]
        public string VideoFile { get; set; }

        [JsonProperty]
        public FileSizeMeasurement AudioSizeMeasurement { get; set; }

        [JsonProperty]
        public FileSizeMeasurement VideoSizeMeasurement { get; set; }

        public SessionTracker()
        {
        }

        public SessionTracker(string jsonFile)
        {
            JsonFile = jsonFile;
        }
    }
}
