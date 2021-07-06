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
        private readonly MuxOptions _Options;
        private readonly IFileUtility _FileUtility;
        private readonly ILoggerFactory _LoggerFactory;

        private readonly ILogger _Logger;
        private readonly LogUtility _LogUtility;

        const string HierarchicalLogFileName = "log.json";

        public Muxer(MuxOptions options, IFileUtility fileUtility, ILoggerFactory loggerFactory)
        {
            _Options = options;
            _FileUtility = fileUtility;
            _LoggerFactory = loggerFactory;

            _Logger = _LoggerFactory.CreateLogger(nameof(Muxer));
            _LogUtility = new LogUtility(_FileUtility);
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
                    if (_Options.InputPath == null)
                    {
                        _Options.InputPath = Environment.CurrentDirectory;
                        _Logger.LogInformation("Input path defaulting to: {InputPath}", _Options.InputPath);
                    }

                    if (_Options.OutputPath == null)
                    {
                        _Options.OutputPath = _Options.InputPath;
                        _Logger.LogInformation("Output path defaulting to: {OutputPath}", _Options.OutputPath);
                    }

                    if (_Options.TempPath == null)
                    {
                        _Options.TempPath = _Options.InputPath;
                        _Logger.LogInformation("Temp path defaulting to: {TempPath}", _Options.TempPath);
                    }

                    if (_Options.MoveInputs && _Options.MovePath == null)
                    {
                        _Options.MovePath = _Options.OutputPath;
                        _Logger.LogInformation("Move path defaulting to: {MovePath}", _Options.MovePath);
                    }

                    if (_Options.Layout == LayoutType.JS)
                    {
                        if (_Options.JavaScriptFile == null)
                        {
                            _Options.JavaScriptFile = Path.Combine(_Options.InputPath, "layout.js");
                            _Logger.LogInformation("JavaScript file defaulting to: {JavaScriptFile}", _Options.JavaScriptFile);
                        }

                        if (!_FileUtility.Exists(_Options.JavaScriptFile))
                        {
                            _Logger.LogError("Cannot find {JavaScriptFile}.", _Options.JavaScriptFile);
                            return false;
                        }
                    }

                    var minMargin = 0;
                    if (_Options.Margin < minMargin)
                    {
                        _Logger.LogInformation("Margin updated from {Margin} to the minimum value of {MinMargin}.", _Options.Margin, minMargin);
                        _Options.Margin = minMargin;
                    }

                    var minWidth = 160;
                    if (_Options.Width < minWidth)
                    {
                        _Logger.LogInformation("Width updated from {Width} to the minimum value of {MinWidth}.", _Options.Width, minWidth);
                        _Options.Width = minWidth;
                    }

                    var minHeight = 120;
                    if (_Options.Height < minHeight)
                    {
                        _Logger.LogInformation("Height updated from {Height} to the minimum value of {MinHeight}.", _Options.Height, minHeight);
                        _Options.Height = minHeight;
                    }

                    var minCameraWeight = 1;
                    if (_Options.CameraWeight < minCameraWeight)
                    {
                        _Logger.LogInformation("Camera weight updated from {CameraWeight} to the minimum value of {MinCameraWeight}.", _Options.CameraWeight, minCameraWeight);
                        _Options.CameraWeight = minCameraWeight;
                    }

                    var minScreenWeight = 1;
                    if (_Options.ScreenWeight < minScreenWeight)
                    {
                        _Logger.LogInformation("Screen weight updated from {ScreenWeight} to the minimum value of {MinScreenWeight}.", _Options.ScreenWeight, minScreenWeight);
                        _Options.ScreenWeight = minScreenWeight;
                    }

                    if (_Options.InputFileNames.Count() > 0)
                    {
                        // CommandLine.Parser returns empty strings when there is a space after the separator.
                        // Also, CommandLine.Parser leaves the separator in the string sometimes.
                        _Options.InputFileNames = string.Join(",", _Options.InputFileNames).Split(',').Where(fileName => fileName.Length > 0);
                    }

                    if (_Options.InputFilePaths.Count() > 0)
                    {
                        // CommandLine.Parser returns empty strings when there is a space after the separator.
                        // Also, CommandLine.Parser leaves the separator in the string sometimes.
                        _Options.InputFilePaths = string.Join(",", _Options.InputFilePaths).Split(',').Where(filePath => filePath.Length > 0);
                    }

                    if (_Options.InputFileNames.Count() == 0 && _Options.InputFilePaths.Count() == 0)
                    {
                        await new JsonPreprocessor(_FileUtility, _Logger, _Options).ProcessDirectory().ConfigureAwait(false);
                    }

                    var logEntries = await GetLogEntries(_Options).ConfigureAwait(false);
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
                    var context = new Context(_FileUtility, _LoggerFactory);
                    foreach (var logEntry in logEntries)
                    {
                        _Logger.LogDebug("Processing log entry for application ID '{ApplicationId}', channel ID '{ChannelId}', client ID '{ClientId}', and connection ID '{ConnectionId}'.",
                            logEntry.ApplicationId,
                            logEntry.ChannelId,
                            logEntry.ClientId,
                            logEntry.ConnectionId);
                        await context.ProcessLogEntry(logEntry, _Options).ConfigureAwait(false);
                    }

                    // process the results
                    var metadataFiles = new List<string>();
                    foreach (var application in context.Applications)
                    {
                        if (_Options.ApplicationId != null && _Options.ApplicationId != application.Id)
                        {
                            continue;
                        }
                        foreach (var channel in application.Channels)
                        {
                            if (_Options.ChannelId != null && _Options.ChannelId != channel.Id)
                            {
                                continue;
                            }
                            foreach (var session in channel.CompletedSessions)
                            {
                                if (_Options.SessionId != null && _Options.SessionId != session.Id)
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
                                    if (await session.Mux(_Options).ConfigureAwait(false))
                                    {
                                        _Logger.LogInformation("Session with application ID '{ApplicationId}' and channel ID '{ChannelId}' has been muxed ({StartTimestamp} to {StopTimestamp}).",
                                            application.Id,
                                            channel.Id,
                                            session.StartTimestamp,
                                            session.StopTimestamp);

                                        if (_Options.MoveInputs)
                                        {
                                            if (_Options.InputPath != _Options.MovePath)
                                            {
                                                foreach (var recording in session.CompletedRecordings)
                                                {
                                                    if (recording.AudioFileExists)
                                                    {
                                                        recording.AudioFile = Move(recording.AudioFile, _Options);
                                                    }
                                                    if (recording.VideoFileExists)
                                                    {
                                                        recording.VideoFile = Move(recording.VideoFile, _Options);
                                                    }
                                                    if (recording.LogFileExists && Path.GetFileName(recording.LogFile) != HierarchicalLogFileName)
                                                    {
                                                        recording.LogFile = Move(recording.LogFile, _Options);
                                                    }
                                                }
                                            }
                                        }
                                        else if (_Options.DeleteInputs)
                                        {
                                            foreach (var recording in session.CompletedRecordings)
                                            {
                                                if (recording.AudioFileExists)
                                                {
                                                    Delete(recording.AudioFile, _Options);
                                                }
                                                if (recording.VideoFileExists)
                                                {
                                                    Delete(recording.VideoFile, _Options);
                                                }
                                                if (recording.LogFileExists && Path.GetFileName(recording.LogFile) != HierarchicalLogFileName)
                                                {
                                                    Delete(recording.LogFile, _Options);
                                                }
                                            }
                                        }

                                        if (session.WriteMetadata(_Options))
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

                                    if (!_Options.ContinueOnFailure)
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

                        return await _LogUtility.GetEntries(logFilePath, _Logger).ConfigureAwait(false);
                    }
                case StrategyType.Flat:
                    {
                        var logEntries = new List<LogEntry>();
                        IEnumerable<string> filePaths;

                        if (_Options.InputFilePaths.Count() > 0)
                        {
                            filePaths = _Options.InputFilePaths;
                        }
                        else if (_Options.InputFileNames.Count() > 0)
                        {
                            filePaths = _Options.InputFileNames.Select(inputFileName => Path.Combine(_Options.InputPath, inputFileName));
                        }
                        else
                        {
                            filePaths = Directory.EnumerateFiles(_Options.InputPath, "*.*", SearchOption.TopDirectoryOnly);
                        }

                        foreach (var filePath in filePaths)
                        {
                            // filter the input files if a filter is provided.
                            if (_Options.InputFilter != null && !Regex.Match(Path.GetFileName(filePath), _Options.InputFilter).Success)
                            {
                                continue;
                            }

                            if (filePath.EndsWith(".json") || filePath.EndsWith(".json.rec"))
                            {
                                try
                                {
                                    logEntries.AddRange(await _LogUtility.GetEntries(filePath, _Logger).ConfigureAwait(false));
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
