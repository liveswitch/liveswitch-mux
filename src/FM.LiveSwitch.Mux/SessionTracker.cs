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
        public string AudioFile;

        [JsonProperty]
        public string VideoFile;

        [JsonProperty]
        public FileSizeMeasurement AudioSizeMeasurement;

        [JsonProperty]
        public FileSizeMeasurement VideoSizeMeasurement;

        public SessionTracker()
        {
        }

        public SessionTracker(string jsonFile)
        {
            JsonFile = jsonFile;
        }
    }
}
