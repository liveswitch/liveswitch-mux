using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    public class LogUtility
    {
        private readonly IFileUtility _FileUtility;

        public LogUtility(IFileUtility fileUtility)
        {
            _FileUtility = fileUtility;
        }

        public async Task<LogEntry[]> GetEntries(string filePath, ILogger logger)
        {
            var json = await _FileUtility.GetContents(filePath);
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
                catch (Exception ex)
                {
                    logger.LogError(ex, "Could not read from {FilePath} as the file is malformatted. Is another process running that could have modified it?", filePath);
                }
            }
            return new LogEntry[0]; // not a log file
        }

        public async Task<LogEntry> GetEntry(string filePath)
        {
            var json = await _FileUtility.GetContents(filePath);
            var entry = JsonConvert.DeserializeObject<LogEntry>(json);
            entry.FilePath = filePath;
            return entry;
        }
    }
}
