using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    public static class LogUtility
    {
        public static async Task<LogEntry[]> GetEntries(string filePath)
        {
            var json = await FileUtility.GetContents(filePath);
            if (json != null && json.StartsWith("["))
            {
                try
                {
                    var entries = JsonConvert.DeserializeObject<List<LogEntry>>(json);
                    foreach (var entry in entries)
                    {
                        entry.FilePath = filePath;
                    }
                    return entries.ToArray();
                }
                catch (Exception)
                {
                    Console.Error.WriteLine($"Could not read from {filePath} as the file is malformatted. Is another process running that could have modified it?");
                }
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
