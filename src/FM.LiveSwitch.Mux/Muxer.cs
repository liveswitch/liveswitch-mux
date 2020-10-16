using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    public class Muxer
    {
        public MuxOptions Options { get; private set; }

        const string HierarchicalLogFileName = "log.json";

        public Muxer(MuxOptions options)
        {
            Options = options;
        }

        public async Task<bool> Run()
        {
            if (Options.InputPath == null)
            {
                Options.InputPath = Environment.CurrentDirectory;
                Console.Error.WriteLine($"Input path defaulting to: {Options.InputPath}");
            }

            if (Options.OutputPath == null)
            {
                Options.OutputPath = Options.InputPath;
                Console.Error.WriteLine($"Output path defaulting to: {Options.OutputPath}");
            }

            if (Options.TempPath == null)
            {
                Options.TempPath = Options.InputPath;
                Console.Error.WriteLine($"Temp path defaulting to: {Options.TempPath}");
            }

            if (Options.MoveInputs && Options.MovePath == null)
            {
                Options.MovePath = Options.OutputPath;
                Console.Error.WriteLine($"Move path defaulting to: {Options.MovePath}");
            }

            if (Options.Layout == LayoutType.JS)
            {
                if (Options.JavaScriptFile == null)
                {
                    Options.JavaScriptFile = Path.Combine(Options.InputPath, "layout.js");
                    Console.Error.WriteLine($"JavaScript file defaulting to: {Options.JavaScriptFile}");
                }

                if (!FileUtility.Exists(Options.JavaScriptFile))
                {
                    Console.Error.WriteLine($"Cannot find {Options.JavaScriptFile}.");
                    return false;
                }
            }

            var minimumMargin = 0;
            if (Options.Margin < minimumMargin)
            {
                Console.Error.WriteLine($"Margin updated from {Options.Margin} to the minimum value of {minimumMargin}.");
                Options.Margin = minimumMargin;
            }

            var minWidth = 160;
            if (Options.Width < minWidth)
            {
                Console.Error.WriteLine($"Width updated from {Options.Width} to the minimum value of {minWidth}.");
                Options.Width = minWidth;
            }

            var minHeight = 120;
            if (Options.Height < minHeight)
            {
                Console.Error.WriteLine($"Height updated from {Options.Height} to the minimum value of {minHeight}.");
                Options.Height = minHeight;
            }

            if (Options.InputFileNames.Count() > 0)
            {
                // CommandLine.Parser returns empty strings when there is a space after the separator.
                // Also, CommandLine.Parser leaves the separator in the string sometimes.
                Options.InputFileNames = string.Join(',', Options.InputFileNames).Split(',').Where(fileName => fileName.Length > 0);
            }

            var logEntries = await GetLogEntries(Options).ConfigureAwait(false);
            if (logEntries == null)
            {
                Console.Error.WriteLine($"No recordings found. Log file(s) not found.");
                return false;
            }
            if (logEntries.Length == 0)
            {
                Console.Error.WriteLine($"No recordings found.");
                return false;
            }

            // sort log entries by timestamp
            logEntries = logEntries.OrderBy(x => x.Timestamp).ToArray();

            // process each log entry
            var context = new Context();
            foreach (var logEntry in logEntries)
            {
                context.ProcessLogEntry(logEntry, Options);
            }

            // process the results
            var metadataFiles = new List<string>();
            foreach (var application in context.Applications)
            {
                if (Options.ApplicationId != null && Options.ApplicationId != application.Id)
                {
                    continue;
                }
                foreach (var channel in application.Channels)
                {
                    if (Options.ChannelId != null && Options.ChannelId != channel.Id)
                    {
                        continue;
                    }
                    foreach (var session in channel.CompletedSessions)
                    {
                        if (Options.SessionId != null && Options.SessionId != session.Id)
                        {
                            continue;
                        }

                        Console.Error.WriteLine();
                        Console.Error.WriteLine($"Channel {channel.Id} from application {application.Id} is ready for muxing ({session.StartTimestamp} to {session.StopTimestamp}).");

                        if (await session.Mux(Options))
                        {
                            Console.Error.WriteLine();
                            Console.Error.WriteLine($"Channel {channel.Id} from application {application.Id} has been muxed ({session.StartTimestamp} to {session.StopTimestamp}).");

                            if (Options.MoveInputs)
                            {
                                if (Options.InputPath != Options.MovePath)
                                {
                                    foreach (var recording in session.CompletedRecordings)
                                    {
                                        if (recording.AudioFileExists)
                                        {
                                            recording.AudioFile = Move(recording.AudioFile, Options);
                                        }
                                        if (recording.VideoFileExists)
                                        {
                                            recording.VideoFile = Move(recording.VideoFile, Options);
                                        }
                                        if (recording.LogFileExists && Path.GetFileName(recording.LogFile) != HierarchicalLogFileName)
                                        {
                                            recording.LogFile = Move(recording.LogFile, Options);
                                        }
                                    }
                                }
                            }
                            else if (Options.DeleteInputs)
                            {
                                foreach (var recording in session.CompletedRecordings)
                                {
                                    if (recording.AudioFileExists)
                                    {
                                        Delete(recording.AudioFile, Options);
                                    }
                                    if (recording.VideoFileExists)
                                    {
                                        Delete(recording.VideoFile, Options);
                                    }
                                    if (recording.LogFileExists && Path.GetFileName(recording.LogFile) != HierarchicalLogFileName)
                                    {
                                        Delete(recording.LogFile, Options);
                                    }
                                }
                            }

                            if (session.WriteMetadata(Options))
                            {
                                metadataFiles.Add(session.MetadataFile);
                            }
                        }
                    }

                    if (channel.Active)
                    {
                        Console.Error.WriteLine($"Channel {channel.Id} from application {application.Id} is currently active.");
                    }
                }
            }

            // write metadata files to stdout
            foreach (var metadataFile in metadataFiles)
            {
                Console.WriteLine(metadataFile);
            }

            return true;
        }

        private bool ConfirmDelete(MuxOptions options, string path)
        {
            if (options.NoPrompt)
            {
                return true;
            }

            while (true)
            {
                Console.Error.Write($"Delete {path} ([y]es/[n]o/[a]ll)? ");
                var key = Console.ReadKey().Key;
                Console.Error.WriteLine();
                switch (key)
                {
                    case ConsoleKey.A:
                        options.NoPrompt = true;
                        return true;
                    case ConsoleKey.Y:
                        return true;
                    case ConsoleKey.N:
                        return false;
                }
            }
        }

        private async Task<LogEntry[]> GetLogEntries(MuxOptions options)
        {
            switch (options.Strategy)
            {
                case StrategyType.AutoDetect:
                    {
                        var logFilePath = Path.Combine(options.InputPath, HierarchicalLogFileName);
                        if (File.Exists(logFilePath))
                        {
                            options.Strategy = StrategyType.Hierarchical;
                        }
                        else
                        {
                            options.Strategy = StrategyType.Flat;
                        }
                        return await GetLogEntries(options).ConfigureAwait(false);
                    }
                case StrategyType.Hierarchical:
                    {
                        var logFilePath = Path.Combine(options.InputPath, HierarchicalLogFileName);
                        if (!File.Exists(logFilePath))
                        {
                            return null;
                        }

                        return await LogUtility.GetEntries(logFilePath);
                    }
                case StrategyType.Flat:
                    {
                        var logEntries = new List<LogEntry>();
                        IEnumerable<string> filePaths;

                        if (Options.InputFileNames.Count() == 0)
                        {
                            filePaths = Directory.EnumerateFiles(Options.InputPath, "*.*", SearchOption.TopDirectoryOnly);
                        }
                        else
                        {
                            filePaths = Options.InputFileNames.Select(inputFileName => Path.Combine(Options.InputPath, inputFileName));
                        }

                        foreach (var filePath in filePaths)
                        {
                            // filter the input files if a filter is provided.
                            if (Options.InputFilter != null && !Regex.Match(Path.GetFileName(filePath), Options.InputFilter).Success)
                            {
                                continue;
                            }

                            if (filePath.EndsWith(".json") || filePath.EndsWith(".json.rec"))
                            {
                                try
                                {
                                    logEntries.AddRange(await LogUtility.GetEntries(filePath));
                                }
                                catch (FileNotFoundException)
                                {
                                    Console.Error.WriteLine($"Could not read from {filePath} as it no longer exists. Is another process running that could have removed it?");
                                }
                                catch (IOException ex) when (ex.Message.Contains("Stale file handle")) // for Linux
                                {
                                    Console.Error.WriteLine($"Could not read from {filePath} as the file handle is stale. Is another process running that could have removed it?");
                                }
                            }
                        }
                        return logEntries.ToArray();
                    }
                default:
                    throw new InvalidOperationException($"Unexpected strategy type '{options.Strategy}'.");
            }
        }

        private string Move(string file, MuxOptions options)
        {
            var newFile = file.Replace(options.InputPath, options.MovePath, StringComparison.InvariantCultureIgnoreCase);

            var movePath = Path.GetDirectoryName(newFile);
            if (!Directory.Exists(movePath))
            {
                Directory.CreateDirectory(movePath);
            }

            try
            {
                File.Move(file, newFile);
                return newFile;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not move {file} to {newFile}. {ex}");
            }
            return file;
        }

        private bool Delete(string file, MuxOptions options)
        {
            try
            {
                if (ConfirmDelete(options, file))
                {
                    File.Delete(file);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not delete {file}. {ex}");
            }
            return false;
        }
    }
}
