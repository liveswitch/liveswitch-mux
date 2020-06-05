using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    public class LogUtility
    {
        public static async Task<LogEntry[]> GetEntries(string filePath)
        {
            var json = await FileUtility.GetContents(filePath);
            if (json.StartsWith("["))
            {
                var entries = JsonConvert.DeserializeObject<List<LogEntry>>(json);
                foreach (var entry in entries)
                {
                    entry.FilePath = filePath;
                }
                return entries.ToArray();
            }
            return new LogEntry[0]; // not a log file
        }

        public static async Task<LogEntry> GetEntry(string filePath)
        {
            var json = await FileUtility.GetContents(filePath);
            var entry = JsonConvert.DeserializeObject<LogEntry>(json);
            entry.FilePath = filePath;
            return entry;
        }
    }
}
