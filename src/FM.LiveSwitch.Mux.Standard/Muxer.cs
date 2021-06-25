using Microsoft.Extensions.Logging;
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

        public ILoggerFactory LoggerFactory { get; private set; }

        private readonly ILogger _Logger;

        const string HierarchicalLogFileName = "log.json";

        public Muxer(MuxOptions options, ILoggerFactory loggerFactory)
        {
            Options = options;
            LoggerFactory = loggerFactory;

            _Logger = LoggerFactory.CreateLogger(nameof(Muxer));
        }

        public async Task<bool> Run()
        {
            var retryDelay = 200; //milliseconds
            var retryCount = 0;
            var retryCountMax = 10000 / retryDelay; // 10 seconds
            while (true)
            {
                try
                {
                    if (Options.InputPath == null)
                    {
                        Options.InputPath = Environment.CurrentDirectory;
                        _Logger.LogInformation("Input path defaulting to: {InputPath}", Options.InputPath);
                    }

                    if (Options.OutputPath == null)
                    {
                        Options.OutputPath = Options.InputPath;
                        _Logger.LogInformation("Output path defaulting to: {OutputPath}", Options.OutputPath);
                    }

                    if (Options.TempPath == null)
                    {
                        Options.TempPath = Options.InputPath;
                        _Logger.LogInformation("Temp path defaulting to: {TempPath}", Options.TempPath);
                    }

                    if (Options.MoveInputs && Options.MovePath == null)
                    {
                        Options.MovePath = Options.OutputPath;
                        _Logger.LogInformation("Move path defaulting to: {MovePath}", Options.MovePath);
                    }

                    if (Options.Layout == LayoutType.JS)
                    {
                        if (Options.JavaScriptFile == null)
                        {
                            Options.JavaScriptFile = Path.Combine(Options.InputPath, "layout.js");
                            _Logger.LogInformation("JavaScript file defaulting to: {JavaScriptFile}", Options.JavaScriptFile);
                        }

                        if (!FileUtility.Exists(Options.JavaScriptFile))
                        {
                            _Logger.LogError("Cannot find {JavaScriptFile}.", Options.JavaScriptFile);
                            return false;
                        }
                    }

                    var minMargin = 0;
                    if (Options.Margin < minMargin)
                    {
                        _Logger.LogInformation("Margin updated from {Margin} to the minimum value of {MinMargin}.", Options.Margin, minMargin);
                        Options.Margin = minMargin;
                    }

                    var minWidth = 160;
                    if (Options.Width < minWidth)
                    {
                        _Logger.LogInformation("Width updated from {Width} to the minimum value of {MinWidth}.", Options.Width, minWidth);
                        Options.Width = minWidth;
                    }

                    var minHeight = 120;
                    if (Options.Height < minHeight)
                    {
                        _Logger.LogInformation("Height updated from {Height} to the minimum value of {MinHeight}.", Options.Height, minHeight);
                        Options.Height = minHeight;
                    }

                    var minCameraWeight = 1;
                    if (Options.CameraWeight < minCameraWeight)
                    {
                        _Logger.LogInformation("Camera weight updated from {CameraWeight} to the minimum value of {MinCameraWeight}.", Options.CameraWeight, minCameraWeight);
                        Options.CameraWeight = minCameraWeight;
                    }

                    var minScreenWeight = 1;
                    if (Options.ScreenWeight < minScreenWeight)
                    {
                        _Logger.LogInformation("Screen weight updated from {ScreenWeight} to the minimum value of {MinScreenWeight}.", Options.ScreenWeight, minScreenWeight);
                        Options.ScreenWeight = minScreenWeight;
                    }

                    if (Options.InputFileNames.Count() > 0)
                    {
                        // CommandLine.Parser returns empty strings when there is a space after the separator.
                        // Also, CommandLine.Parser leaves the separator in the string sometimes.
                        Options.InputFileNames = string.Join(",", Options.InputFileNames).Split(',').Where(fileName => fileName.Length > 0);
                    }

                    if (Options.InputFilePaths.Count() > 0)
                    {
                        // CommandLine.Parser returns empty strings when there is a space after the separator.
                        // Also, CommandLine.Parser leaves the separator in the string sometimes.
                        Options.InputFilePaths = string.Join(",", Options.InputFilePaths).Split(',').Where(filePath => filePath.Length > 0);
                    }

                    if (Options.InputFileNames.Count() == 0 && Options.InputFilePaths.Count() == 0)
                    {
                        await new JsonPreprocessor(_Logger, Options).ProcessDirectory().ConfigureAwait(false);
                    }

                    var logEntries = await GetLogEntries(Options).ConfigureAwait(false);
                    if (logEntries == null)
                    {
                        _Logger.LogInformation($"No recordings found. Log file(s) not found.");
                        return true;
                    }
                    if (logEntries.Length == 0)
                    {
                        _Logger.LogInformation($"No recordings found.");
                        return true;
                    }

                    // sort log entries by timestamp
                    logEntries = logEntries.OrderBy(x => x.Timestamp).ToArray();
                    _Logger.LogDebug("Found {Count} log entries.", logEntries.Count());

                    // process each log entry
                    var context = new Context();
                    foreach (var logEntry in logEntries)
                    {
                        _Logger.LogDebug("Processing log entry for application ID '{ApplicationId}', channel ID '{ChannelId}', client ID '{ClientId}', and connection ID '{ConnectionId}'.",
                            logEntry.ApplicationId,
                            logEntry.ChannelId,
                            logEntry.ClientId,
                            logEntry.ConnectionId);
                        context.ProcessLogEntry(logEntry, Options, LoggerFactory);
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

                                _Logger.LogInformation("Session with application ID '{ApplicationId}' and channel ID '{ChannelId}' is ready for muxing ({StartTimestamp} to {StopTimestamp}).",
                                    application.Id,
                                    channel.Id,
                                    session.StartTimestamp,
                                    session.StopTimestamp);

                                try
                                {
                                    if (await session.Mux(Options).ConfigureAwait(false))
                                    {
                                        _Logger.LogInformation("Session with application ID '{ApplicationId}' and channel ID '{ChannelId}' has been muxed ({StartTimestamp} to {StopTimestamp}).",
                                            application.Id,
                                            channel.Id,
                                            session.StartTimestamp,
                                            session.StopTimestamp);

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
                                catch (Exception ex)
                                {
                                    _Logger.LogError(ex, "Session with application ID '{ApplicationId}' and channel ID '{ChannelId}' could not be muxed ({StartTimestamp} to {StopTimestamp}).",
                                        application.Id,
                                        channel.Id,
                                        session.StartTimestamp,
                                        session.StopTimestamp);

                                    if (!Options.ContinueOnFailure)
                                    {
                                        return false;
                                    }
                                }
                            }

                            if (channel.Active)
                            {
                                _Logger.LogInformation("Session with application ID '{ApplicationId}' and channel ID '{ChannelId}' is currently active.",
                                    application.Id,
                                    channel.Id);
                            }
                        }
                    }

                    // write metadata files to stdout
                    foreach (var metadataFile in metadataFiles)
                    {
                        _Logger.LogDebug("Metadata written to: {MetadataFile}", metadataFile);

                        Console.WriteLine(metadataFile);
                    }

                    return true;
                }
                catch (FileNotFoundException ex)
                {
                    // retry for approximately 10 seconds before giving up
                    if (retryCount >= retryCountMax)
                    {
                        _Logger.LogError($"A temporary exception was encountered, but retries have been exhausted.", ex);
                        throw;
                    }

                    retryCount++;
                    _Logger.LogWarning($"A temporary exception was encountered. Will retry after {retryDelay} milliseconds (attempt #{retryCount}).", ex);
                    await Task.Delay(retryDelay).ConfigureAwait(false);
                }
            }
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
                    default:
                        continue;
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

                        return await LogUtility.GetEntries(logFilePath, _Logger).ConfigureAwait(false);
                    }
                case StrategyType.Flat:
                    {
                        var logEntries = new List<LogEntry>();
                        IEnumerable<string> filePaths;

                        if (Options.InputFilePaths.Count() > 0)
                        {
                            filePaths = Options.InputFilePaths;
                        }
                        else if (Options.InputFileNames.Count() > 0)
                        {
                            filePaths = Options.InputFileNames.Select(inputFileName => Path.Combine(Options.InputPath, inputFileName));
                        }
                        else
                        {
                            filePaths = Directory.EnumerateFiles(Options.InputPath, "*.*", SearchOption.TopDirectoryOnly);
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
                                    logEntries.AddRange(await LogUtility.GetEntries(filePath, _Logger).ConfigureAwait(false));
                                }
                                catch (FileNotFoundException)
                                {
                                    _Logger.LogWarning("Could not read from {FilePath} as it no longer exists. Is another process running that could have removed it?", filePath);
                                }
                                catch (IOException ex) when (ex.Message.Contains("Stale file handle")) // for Linux
                                {
                                    _Logger.LogWarning("Could not read from {FilePath} as the file handle is stale. Is another process running that could have removed it?", filePath);
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
            var newFile = file.Replace(options.InputPath, options.MovePath);

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
                _Logger.LogError(ex, "Could not move {File} to {NewFile}.", file, newFile);
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
                _Logger.LogError(ex, "Could not delete {File}.", file);
            }
            return false;
        }
    }
}
