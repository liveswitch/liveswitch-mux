using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace FM.LiveSwitch.Mux
{
    public class JsonPreprocessor
    {
        public int MinimumOrphanDuration { get; set; } = 120;

        private readonly ILogger _Logger;
        private readonly string _InputDirectory;

        private static readonly string OrphanSessionsFileName = ".orphan-sessions.stored.$$$";

        public JsonPreprocessor(ILogger Logger, string InputDirectory)
        {
            _Logger = Logger;
            _InputDirectory = InputDirectory;
        }

        public async Task ProcessDirectory()
        {
            for (var i = 0; i < 3; i++)
            {
                if (await JsonIntegrityCheck().ConfigureAwait(false))
                {
                    break;
                }
                else
                {
                    Thread.Sleep(3000);
                }
            }

            await ProcessOrphanSessions().ConfigureAwait(false);
        }

        private async Task<bool> JsonIntegrityCheck()
        {
            var tempFiles = new List<Tuple<string, string>>();
            var noErrors = true;

            _Logger.LogDebug($"Starting integrity check of the directory {_InputDirectory}");

            foreach (var jsonFile in Directory.EnumerateFiles(_InputDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                _Logger.LogDebug($"JsonIntegrityCheck starting processing file {jsonFile}");

                try
                {
                    if (File.Exists(jsonFile + ".fail"))
                    {
                        _Logger.LogDebug($"File {jsonFile} is already marked as fail-checked. Skipping...");
                        continue;
                    }

                    var logEntries = await LogUtility.GetEntries(jsonFile, _Logger).ConfigureAwait(false);
                    if (logEntries == null)
                    {
                        continue;
                    }

                    var errorList = new List<string>();
                    var isValid = true;

                    void AddToErrorList(string message)
                    {
                        isValid = false;
                        _Logger.LogDebug($"JsonIntegrityCheck adds new line to error list for file {jsonFile}: {message}");
                        errorList.Add(message);
                    }

                    try
                    {
                        var startEntry = GetEvent(logEntries, LogEntry.TypeStartRecording);
                        var stopEntry = GetEvent(logEntries, LogEntry.TypeStopRecording);
                        string audioFilePath = null;
                        string videoFilePath = null;
                        DateTime? firstAudioTimestamp = null;
                        DateTime? firstVideoTimestamp = null;
                        DateTime? lastAudioTimestamp = null;
                        DateTime? lastVideoTimestamp = null;
                        var addDataBlock = false;
                        var addRecordStopTS = false;
                        var addFirstAudioTS = false;
                        var addFirstVideoTS = false;
                        var calcLastAudioTS = false;
                        var calcLastVideoTS = false;
                        var addAudioPath = false;
                        var addVideoPath = false;

                        if (startEntry == null)
                        {
                            AddToErrorList($"Event \"{LogEntry.TypeStartRecording}\" is missing");
                        }

                        if (stopEntry == null)
                        {
                            AddToErrorList($"Event \"{LogEntry.TypeStopRecording}\" is missing");
                        }

                        if (isValid)
                        {
                            bool ReportIfMissing<T>(T var, string nodeName, string sectionName)
                            {
                                if (var?.Equals(default(T)) == null)
                                {
                                    AddToErrorList($"JSON node \"{nodeName}\" is missing in section \"{sectionName}\".");
                                    return true;
                                }

                                return false;
                            }

                            void ReportIfDifferent<T>(T var1, T var2, string nodeName) where T : class, IComparable<T>
                            {
                                if (var1 != null && var2 != null && !var1.Equals(var2))
                                {
                                    AddToErrorList($"Value of JSON node \"{nodeName}\" is different between section \"{LogEntry.TypeStartRecording}\" and \"{LogEntry.TypeStopRecording}\".");
                                }
                            }

                            ReportIfMissing((startEntry.Timestamp.Ticks == 0) ? (DateTime?)null : startEntry.Timestamp, nameof(startEntry.Timestamp), LogEntry.TypeStartRecording);
                            ReportIfMissing(startEntry.ApplicationId, nameof(startEntry.ApplicationId), LogEntry.TypeStartRecording);
                            ReportIfMissing(startEntry.ExternalId, nameof(startEntry.ExternalId), LogEntry.TypeStartRecording);
                            ReportIfMissing(startEntry.ChannelId, nameof(startEntry.ChannelId), LogEntry.TypeStartRecording);
                            ReportIfMissing(startEntry.ConnectionId, nameof(startEntry.ConnectionId), LogEntry.TypeStartRecording);
                            ReportIfMissing(startEntry.ApplicationConfigId, nameof(startEntry.ApplicationConfigId), LogEntry.TypeStartRecording);
                            ReportIfMissing(startEntry.ChannelConfigId, nameof(startEntry.ChannelConfigId), LogEntry.TypeStartRecording);

                            ReportIfMissing(stopEntry.ApplicationId, nameof(stopEntry.ApplicationId), LogEntry.TypeStopRecording);
                            ReportIfMissing(stopEntry.ExternalId, nameof(stopEntry.ExternalId), LogEntry.TypeStopRecording);
                            ReportIfMissing(stopEntry.ChannelId, nameof(stopEntry.ChannelId), LogEntry.TypeStopRecording);
                            ReportIfMissing(stopEntry.ConnectionId, nameof(stopEntry.ConnectionId), LogEntry.TypeStopRecording);
                            ReportIfMissing(stopEntry.ApplicationConfigId, nameof(stopEntry.ApplicationConfigId), LogEntry.TypeStopRecording);
                            ReportIfMissing(stopEntry.ChannelConfigId, nameof(stopEntry.ChannelConfigId), LogEntry.TypeStopRecording);

                            ReportIfDifferent(startEntry.ApplicationId, stopEntry.ApplicationId, nameof(stopEntry.ApplicationId));
                            ReportIfDifferent(startEntry.ExternalId, stopEntry.ExternalId, nameof(stopEntry.ExternalId));
                            ReportIfDifferent(startEntry.ChannelId, stopEntry.ChannelId, nameof(stopEntry.ChannelId));
                            ReportIfDifferent(startEntry.ConnectionId, stopEntry.ConnectionId, nameof(stopEntry.ConnectionId));
                            ReportIfDifferent(startEntry.ApplicationConfigId, stopEntry.ApplicationConfigId, nameof(stopEntry.ApplicationConfigId));
                            ReportIfDifferent(startEntry.ChannelConfigId, stopEntry.ChannelConfigId, nameof(stopEntry.ChannelConfigId));

                            var data = stopEntry.Data;
                            if (data != null)
                            {
                                audioFilePath = data.AudioFile;
                                videoFilePath = data.VideoFile;
                                firstAudioTimestamp = data.AudioFirstFrameTimestamp;
                                firstVideoTimestamp = data.VideoFirstFrameTimestamp;
                                lastAudioTimestamp = data.AudioFirstFrameTimestamp;
                                lastVideoTimestamp = data.VideoLastFrameTimestamp;
                            }

                            if (stopEntry.Timestamp.Ticks > 0 && startEntry.Timestamp > stopEntry.Timestamp)
                            {
                                AddToErrorList("Start recording timestamp must not be greater than stop recording timestamp.");
                            }

                            addRecordStopTS = (stopEntry.Timestamp.Ticks == 0);

                            if (audioFilePath != null && !File.Exists(audioFilePath))
                            {
                                AddToErrorList($"Audio file {audioFilePath} is missing.");
                            }

                            if (videoFilePath != null && !File.Exists(videoFilePath))
                            {
                                AddToErrorList($"Video file {videoFilePath} is missing.");
                            }

                            if (isValid)
                            {
                                var baseName = Path.GetFileNameWithoutExtension(jsonFile);
                                var audioPath = Path.Combine(_InputDirectory, baseName + ".mka");
                                var videoPath = Path.Combine(_InputDirectory, baseName + ".mkv");

                                if (data == null)
                                {
                                    addDataBlock = true;
                                }

                                if (File.Exists(audioPath))
                                {
                                    if (firstAudioTimestamp == null)
                                    {
                                        addFirstAudioTS = true;
                                        firstAudioTimestamp = (startEntry.Timestamp.Ticks == 0) ? (DateTime?)null : startEntry.Timestamp;

                                        if (firstAudioTimestamp == null)
                                        {
                                            AddToErrorList($"JSON file has audio file but neither \"timestamp\" nor \"audioFirstFrameTimestamp\" defined in section \"{LogEntry.TypeStopRecording}\". File cannot be recovered");
                                        }
                                    }

                                    if (audioFilePath == null)
                                    {
                                        addAudioPath = true;
                                        audioFilePath = audioPath;
                                    }
                                }

                                if (File.Exists(videoPath))
                                {
                                    if (firstVideoTimestamp == null)
                                    {
                                        addFirstVideoTS = true;
                                        firstVideoTimestamp = (startEntry.Timestamp.Ticks == 0) ? (DateTime?)null : startEntry.Timestamp;

                                        if (firstVideoTimestamp == null)
                                        {
                                            AddToErrorList($"JSON file has audio file but neither \"timestamp\" nor \"videoFirstFrameTimestamp\" defined in section \"{LogEntry.TypeStopRecording}\". File cannot be recovered");
                                        }
                                    }

                                    if (videoFilePath == null)
                                    {
                                        addVideoPath = true;
                                        videoFilePath = videoPath;
                                    }
                                }

                                if (audioFilePath == null && videoFilePath == null)
                                {
                                    AddToErrorList($"JSON file does not have any media file referred in \"data\" block of the section \"{LogEntry.TypeStopRecording}\" and it cannot be reconstructed (no auido or video file found).");
                                }
                                else
                                {
                                    if (audioFilePath != null && lastAudioTimestamp == null)
                                    {
                                        calcLastAudioTS = true;
                                    }

                                    if (videoFilePath != null && lastVideoTimestamp == null)
                                    {
                                        calcLastVideoTS = true;
                                    }
                                }
                            }
                        }

                        if (isValid && (addDataBlock || addAudioPath || addVideoPath || addRecordStopTS || addFirstAudioTS || addFirstVideoTS || calcLastAudioTS || calcLastVideoTS))
                        {
                            _Logger.LogDebug($"Calculating last frame durations for file {jsonFile}");

                            var tempFile = jsonFile + ".tmp.$$$";
                            var pair = Tuple.Create(jsonFile, tempFile);

                            if (calcLastAudioTS)
                            {
                                var duration = await GetDuration(audioFilePath, true).ConfigureAwait(false);
                                if (duration != null)
                                {
                                    lastAudioTimestamp = firstAudioTimestamp + duration;
                                    _Logger.LogDebug($"Calculated last audio frame timestamp for file {jsonFile}: {lastAudioTimestamp} (duration: {duration}).");
                                }
                                else
                                {
                                    lastAudioTimestamp = firstAudioTimestamp;
                                    _Logger.LogDebug($"Calculated last audio frame timestamp for file {jsonFile}: {lastAudioTimestamp} (duration: cannot be calculated).");
                                }
                            }

                            if (calcLastVideoTS)
                            {
                                var duration = await GetDuration(videoFilePath, false).ConfigureAwait(false);
                                if (duration != null)
                                {
                                    lastVideoTimestamp = firstVideoTimestamp + duration;
                                    _Logger.LogDebug($"Calculated last video frame timestamp for file {jsonFile}: {lastVideoTimestamp} (duration: {duration}).");
                                }
                                else
                                {
                                    lastVideoTimestamp = firstVideoTimestamp;
                                    _Logger.LogDebug($"Calculated last audio frame timestamp for file {jsonFile}: {lastVideoTimestamp} (duration: cannot be calculated).");
                                }
                            }

                            if (addDataBlock)
                            {
                                stopEntry.Data = new LogEntryData();
                            }

                            if (addFirstAudioTS)
                            {
                                stopEntry.Data.AudioFirstFrameTimestamp = firstAudioTimestamp;
                            }

                            if (addFirstVideoTS)
                            {
                                stopEntry.Data.VideoFirstFrameTimestamp = firstVideoTimestamp;
                            }

                            if (addFirstAudioTS)
                            {
                                stopEntry.Data.AudioFirstFrameTimestamp = firstAudioTimestamp;
                            }

                            if (addFirstAudioTS)
                            {
                                stopEntry.Data.AudioFirstFrameTimestamp = firstAudioTimestamp;
                            }

                            if (addAudioPath)
                            {
                                stopEntry.Data.AudioFile = audioFilePath;
                            }

                            if (addVideoPath)
                            {
                                stopEntry.Data.VideoFile = videoFilePath;
                            }

                            if (addRecordStopTS)
                            {
                                DateTime? stopRecTimestamp;
                                if (lastAudioTimestamp != null && lastVideoTimestamp != null)
                                {
                                    stopRecTimestamp = ((lastAudioTimestamp > lastVideoTimestamp) ? lastAudioTimestamp : lastVideoTimestamp);
                                }
                                else
                                {
                                    stopRecTimestamp = lastAudioTimestamp ?? lastVideoTimestamp;
                                }

                                if (stopRecTimestamp != null)
                                {
                                    stopEntry.Timestamp = (DateTime)stopRecTimestamp;
                                }
                            }

                            if (calcLastAudioTS && lastAudioTimestamp != null)
                            {
                                stopEntry.Data.AudioLastFrameTimestamp = lastAudioTimestamp;
                            }

                            if (calcLastVideoTS && lastVideoTimestamp != null)
                            {
                                stopEntry.Data.VideoLastFrameTimestamp = lastVideoTimestamp;
                            }

                            File.WriteAllText(tempFile, JsonConvert.SerializeObject(logEntries, new JsonSerializerSettings
                            {
                                ContractResolver = new CamelCasePropertyNamesContractResolver()
                            }));

                            tempFiles.Add(pair);
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logger.LogError($"Exception during validation of the document {jsonFile}: {ex}");
                        AddToErrorList($"Exception during document validation: {ex}");
                    }

                    if (errorList.Count > 0)
                    {
                        var jsonBlob = File.ReadAllText(jsonFile);

                        for (int i = errorList.Count - 1; i >= 0; i--)
                        {
                            var num = i + 1;
                            var error = errorList[i];
                            _Logger.LogError($"Integrity check error #{num} for the file {jsonFile}: {error}");
                            jsonBlob = "ERROR: " + error + "\n" + jsonBlob;
                        }

                        File.WriteAllText(jsonFile + ".fail", jsonBlob);
                        _Logger.LogDebug($"Finishing integrity check for JSON file {jsonFile} with {errorList.Count} errors found.");
                    }
                    else
                    {
                        _Logger.LogDebug($"Finishing integrity check for JSON file {jsonFile} with no errors found.");
                    }
                }
                catch (Exception ex)
                {
                    noErrors = false;
                    _Logger.LogError($"JsonIntegrityCheck exception while processing file {jsonFile} : {ex}");
                }

                _Logger.LogDebug($"JsonIntegrityCheck finishing processing file {jsonFile}.");
            }

            foreach (var pair in tempFiles)
            {
                try
                {
                    _Logger.LogDebug($"JsonIntegrityCheck has temporary files and moving file {pair.Item2} to file {pair.Item1}");

                    if (File.Exists(pair.Item1))
                    {
                        File.Delete(pair.Item1);
                    }
                    File.Move(pair.Item2, pair.Item1);
                }
                catch (Exception ex)
                {
                    noErrors = false;
                    _Logger.LogError($"JsonIntegrityCheck exception while moving file {pair.Item2} to file {pair.Item1} : {ex}");
                }
            }

            _Logger.LogDebug($"Finishing integrity check of the directory {_InputDirectory}");

            return noErrors;
        }

        private async Task ProcessOrphanSessions()
        {
            _Logger.LogDebug("Starting processing orphan sessions...");
            _Logger.LogDebug("Starting cleaning up missing files...");

            Dictionary<string, SessionTracker> orphanSessions = new Dictionary<string, SessionTracker>();
            var orphanFileName = Path.Combine(_InputDirectory, OrphanSessionsFileName);

            if (File.Exists(orphanFileName))
            {
                try
                {
                    var contents = File.ReadAllText(orphanFileName);
                    orphanSessions = JsonConvert.DeserializeObject<Dictionary<string, SessionTracker>>(contents);
                }
                catch (Exception ex)
                {
                    _Logger.LogError($"Error reading stored orphan sessions: {ex}. Deleting file {OrphanSessionsFileName}");
                    File.Delete(orphanFileName);
                    orphanSessions = new Dictionary<string, SessionTracker>();
                }
            }

            var invalidSessions = new List<string>();
            foreach (var session in orphanSessions)
            {
                if (!File.Exists(session.Value.JsonFile))
                {
                    _Logger.LogDebug($"Orphan session with JSON file {session.Value.JsonFile} is invalid and it will be removed.");
                    invalidSessions.Add(session.Key);
                }
            }

            foreach (var sessionKey in invalidSessions)
            {
                orphanSessions.Remove(sessionKey);
            }

            _Logger.LogDebug("Finishing cleaning up missing files...");

            foreach (var jsonFile in Directory.EnumerateFiles(_InputDirectory, "*.json.rec", SearchOption.TopDirectoryOnly))
            {
                _Logger.LogDebug($"ProcessOrphanSessions starting analysis of the file {jsonFile}");

                try
                {
                    var baseName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(jsonFile));
                    var match = Regex.Match(jsonFile, "(([A-Za-z0-9])+)(?:(-(\\d)+)?\\.json\\.rec)");
                    var connectionId = "";
                    var sessionTracker = new SessionTracker(jsonFile);

                    if (match.Success && match.Groups.Count > 1)
                    {
                        connectionId = match.Groups[1].Value;
                        _Logger.LogDebug($"Extracted ConnectionId = {connectionId} from JSON file {jsonFile}.");
                    }
                    else
                    {
                        _Logger.LogDebug($"File {jsonFile} does not follow naming conventions. Skipping.");
                        continue;
                    }

                    if (orphanSessions.ContainsKey(connectionId))
                    {
                        _Logger.LogDebug($"ConnectionId {connectionId} is already in processing. Skipping.");
                        continue;
                    }

                    var mediaFile = Path.Combine(_InputDirectory, $"{baseName}.mka");
                    if (File.Exists(mediaFile))
                    {
                        _Logger.LogDebug($"Audio file {mediaFile} for {connectionId} is found.");
                        sessionTracker.AudioFile = mediaFile;
                    }

                    mediaFile = Path.Combine(_InputDirectory, $"{baseName}.mkv");
                    if (File.Exists(mediaFile))
                    {
                        _Logger.LogDebug($"Video file {mediaFile} for {connectionId} is found.");
                        sessionTracker.VideoFile = mediaFile;
                    }

                    mediaFile = Path.Combine(_InputDirectory, $"{connectionId}.mka.rec");
                    if (File.Exists(mediaFile))
                    {
                        _Logger.LogDebug($"Incomplete audio file {mediaFile} for {connectionId} is found.");
                        sessionTracker.AudioSizeMeasurement = new FileSizeMeasurement(mediaFile, MinimumOrphanDuration);
                    }

                    mediaFile = Path.Combine(_InputDirectory, $"{connectionId}.mkv.rec");
                    if (File.Exists(mediaFile))
                    {
                        _Logger.LogDebug($"Incomplete video file {mediaFile} for {connectionId} is found.");
                        sessionTracker.VideoSizeMeasurement = new FileSizeMeasurement(mediaFile, MinimumOrphanDuration);
                    }

                    if (sessionTracker.AudioSizeMeasurement != null || sessionTracker.VideoSizeMeasurement != null)
                    {
                        orphanSessions.Add(connectionId, sessionTracker);
                    }
                    else
                    {
                        _Logger.LogDebug($"No matching audio or video  file was found for {jsonFile}.");
                    }
                }
                catch (Exception ex)
                {
                    _Logger.LogError($"ProcessOrphanSessions exception during analysis of the file {jsonFile}: {ex}");
                }

                _Logger.LogDebug($"ProcessOrphanSessions finishing analysis of the file {jsonFile}.");
            }

            var finishedSessions = new HashSet<string>();
            foreach ((string connectionId, SessionTracker sessionTracker) in orphanSessions.Select(x => (x.Key, x.Value)))
            {
                _Logger.LogDebug($"ProcessOrphanSessions starting processing orphan sessions for {sessionTracker.JsonFile}");

                try
                {
                    var baseName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(sessionTracker.JsonFile));

                    if ((sessionTracker.AudioSizeMeasurement != null && !sessionTracker.AudioSizeMeasurement.IsOrphan) ||
                        (sessionTracker.VideoSizeMeasurement != null && !sessionTracker.VideoSizeMeasurement.IsOrphan))
                    {
                        _Logger.LogDebug($"File {sessionTracker.JsonFile} with ConnectionId={connectionId} is not an orphan yet. Skipping.");
                        continue;
                    }

                    string audioFile = null;
                    string videoFile = null;

                    if (sessionTracker.AudioSizeMeasurement != null)
                    {
                        if (sessionTracker.AudioFile != null)
                        {
                            _Logger.LogDebug($"Complete audio file {sessionTracker.AudioFile} already exists. Removing.");
                            File.Delete(sessionTracker.AudioFile);
                            sessionTracker.AudioFile = null;
                        }

                        var srcName = sessionTracker.AudioSizeMeasurement.FileName;
                        audioFile = Path.Combine(Path.GetDirectoryName(srcName), $"{baseName}.mka");
                        File.Move(srcName, audioFile);
                        sessionTracker.AudioSizeMeasurement = null;

                        _Logger.LogDebug($"Moved audio file from {srcName} to {audioFile}");
                    }

                    if (sessionTracker.VideoSizeMeasurement != null)
                    {
                        if (sessionTracker.VideoFile != null)
                        {
                            File.Delete(sessionTracker.VideoFile);
                            sessionTracker.VideoFile = null;
                        }

                        var srcName = sessionTracker.VideoSizeMeasurement.FileName;
                        videoFile = Path.Combine(Path.GetDirectoryName(srcName), $"{baseName}.mkv");
                        File.Move(srcName, videoFile);
                        sessionTracker.VideoSizeMeasurement = null;

                        _Logger.LogDebug($"Moved audio file from {srcName} to {videoFile}");
                    }

                    var targetJsonName = Path.Combine(Path.GetDirectoryName(sessionTracker.JsonFile), Path.GetFileNameWithoutExtension(sessionTracker.JsonFile));
                    var logEntries = await LogUtility.GetEntries(sessionTracker.JsonFile, _Logger).ConfigureAwait(false);
                    var startEntry = GetEvent(logEntries, LogEntry.TypeStartRecording);

                    if (startEntry != null)
                    {
                        var entryData = new LogEntryData();
                        DateTime? lastTimestamp = null;

                        _Logger.LogDebug($"Got first frame timestamp: {startEntry.Timestamp}");

                        if (audioFile != null)
                        {
                            _Logger.LogDebug($"Calculating duration for audio file {audioFile}");

                            entryData.AudioFile = audioFile;
                            entryData.AudioFirstFrameTimestamp = startEntry.Timestamp;

                            var duration = await GetDuration(audioFile, true).ConfigureAwait(false);
                            if (duration != null)
                            {
                                var lastFrameTimestamp = startEntry.Timestamp + duration;
                                lastTimestamp = lastFrameTimestamp;
                                entryData.AudioLastFrameTimestamp = lastFrameTimestamp;
                                _Logger.LogDebug($"Measured duration of audio file {audioFile} is {duration}. Last frame timestamp: {lastFrameTimestamp}");
                            }
                            else
                            {
                                _Logger.LogError($"Duration of audio file {audioFile} cannot be calculated");
                            }
                        }

                        if (videoFile != null)
                        {
                            _Logger.LogDebug($"Calculating duration for video file {videoFile}");

                            entryData.VideoFile = videoFile;
                            entryData.VideoFirstFrameTimestamp = startEntry.Timestamp;

                            var duration = await GetDuration(videoFile, false).ConfigureAwait(false);
                            if (duration != null)
                            {
                                var lastFrameTimestamp = startEntry.Timestamp + duration;
                                if (lastTimestamp == null)
                                {
                                    lastTimestamp = lastFrameTimestamp;
                                }
                                else
                                {
                                    lastTimestamp = (lastTimestamp > lastFrameTimestamp) ? lastTimestamp : lastFrameTimestamp;
                                }
                                entryData.VideoLastFrameTimestamp = lastFrameTimestamp;
                                _Logger.LogDebug($"Measured duration of video file {videoFile} is {duration}. Last frame timestamp: {lastFrameTimestamp}");
                            }
                            else
                            {
                                _Logger.LogError($"Duration of video file {videoFile} cannot be calculated");
                            }
                        }

                        var stopEntry = new LogEntry();
                        stopEntry.Type = LogEntry.TypeStopRecording;
                        if (lastTimestamp != null)
                        {
                            stopEntry.Timestamp = (DateTime)lastTimestamp;
                        }
                        stopEntry.ApplicationId = startEntry.ApplicationId;
                        stopEntry.ApplicationConfigId = startEntry.ApplicationConfigId;
                        stopEntry.ExternalId = startEntry.ExternalId;
                        stopEntry.ChannelId = startEntry.ChannelId;
                        stopEntry.ChannelConfigId = startEntry.ChannelConfigId;
                        stopEntry.UserId = startEntry.UserId;
                        stopEntry.DeviceId = startEntry.DeviceId;
                        stopEntry.ClientId = startEntry.ClientId;
                        stopEntry.ConnectionId = startEntry.ConnectionId;
                        stopEntry.Data = entryData;

                        _Logger.LogDebug($"Copying section attributes from {sessionTracker.JsonFile} to {targetJsonName}");

                        var listEntries = new List<LogEntry>(logEntries);
                        listEntries.Add(stopEntry);
                        var newEntries = listEntries.ToArray();

                        File.WriteAllText(targetJsonName, JsonConvert.SerializeObject(newEntries, new JsonSerializerSettings
                        {
                            ContractResolver = new CamelCasePropertyNamesContractResolver()
                        }));
                    }

                    File.Delete(sessionTracker.JsonFile);
                    finishedSessions.Add(connectionId);

                    _Logger.LogDebug($"Enlisting processed file {sessionTracker.JsonFile} with ConnectionId = {connectionId} for deletion.");
                }
                catch (Exception ex)
                {
                    _Logger.LogError($"ProcessOrphanSessions exception during processing of orphan sessions for {sessionTracker.JsonFile}: {ex}");
                }

                _Logger.LogDebug($"ProcessOrphanSessions finishing processing orphan sessions for {sessionTracker.JsonFile}.");
            }

            foreach (var connectionId in finishedSessions)
            {
                _Logger.LogDebug($"Removing processed file with ConnectionId = {connectionId}");
                orphanSessions.Remove(connectionId);
            }

            _Logger.LogDebug("Serializing orphan sessions object.");

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    var contents = JsonConvert.SerializeObject(orphanSessions);
                    File.WriteAllText(orphanFileName, contents);
                }
                catch (Exception ex)
                {
                    _Logger.LogError($"Error writing orphan sessions: {ex}.");
                    Thread.Sleep(1000);
                    continue;
                }
                break;
            }

            _Logger.LogDebug("Finished processing orphan sessions...");
        }

        public LogEntry GetEvent(LogEntry[] logEntries, string eventName)
        {
            foreach (var entry in logEntries)
            {
                if (entry.Type == eventName)
                {
                    return entry;
                }
            }
            return null;
        }

        private void AddProperty(JObject obj, string propertyName, JToken token)
        {
            if (obj.ContainsKey(propertyName))
            {
                obj.Remove(propertyName);
            }

            if (token != null && token.Type != JTokenType.Null)
            {
                obj.Add(propertyName, token);
            }
        }

        private async Task<TimeSpan?> GetDuration(string filePath, bool audio)
        {
            try
            {
                var ffmpegUtil = new Utility(_Logger);
                var type = audio ? "a" : "v";
                var lines = await ffmpegUtil.FFprobe($"-v quiet -select_streams {type}:0 -show_frames -show_entries frame=pkt_pts_time -print_format csv=item_sep=|:nokey=1:print_section=0 {filePath}").ConfigureAwait(false);

                TimeSpan? duration = null;
                double? firstSeconds = null;
                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 1)
                    {
                        var readSeconds = double.TryParse(parts[0], out var seconds);
                        if (readSeconds)
                        {
                            if (firstSeconds == null)
                            {
                                firstSeconds = seconds;
                            }
                            else
                            {
                                duration = TimeSpan.FromSeconds(seconds - firstSeconds.Value);
                            }
                        }
                        else
                        {
                            _Logger.LogError("Could not parse ffprobe output: {Line}", line);
                        }
                    }
                    else
                    {
                        _Logger.LogError("Unexpected ffprobe output: {Line}", line);
                    }
                }

                return duration;
            }
            catch (Exception ex)
            {
                _Logger.LogDebug($"Error during extracting duration from the media file {filePath}: {ex}");
                return null;
            }
        }
    }
}
