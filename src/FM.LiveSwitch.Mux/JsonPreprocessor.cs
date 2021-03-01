using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using IO = System.IO;

namespace FM.LiveSwitch.Mux
{
    public class JsonPreprocessor
    {
        private int _MinimumOrphanDuration = 120;

        public int MinimumOrphanDuration
        {
            get
            {
                return _MinimumOrphanDuration;
            }

            set
            {
                _MinimumOrphanDuration = value;
            }
        }

        private readonly ILogger _Logger;
        private readonly string _InputDirectory;
        private readonly bool _ProcessInvalidMedia;

        // Add this field to the section "stopRecording" of the JSON file
        // to have invalid media files processed by the recording service.
        private static readonly string ProcessInvalidMediaFieldName = "processInvalidMedia";

        private static readonly string OrphanSessionsFileName = ".orphan-sessions.stored.$$$";

        public JsonPreprocessor(ILogger Logger, string InputDirectory, bool ProcessInvalidMedia)
        {
            _Logger = Logger;
            _InputDirectory = InputDirectory;
            _ProcessInvalidMedia = ProcessInvalidMedia;
        }

        public void processDirectory()
        {
            for (var i = 0; i < 3; i++)
            {
                if (JsonIntegrityCheck())
                {
                    break;
                }
                else
                {
                    Thread.Sleep(3000);
                }
            }

            ProcessOrphanSessions();
        }

        private bool JsonIntegrityCheck()
        {
            var tempFiles = new List<Tuple<string, string>>();
            var noErrors = true;

            _Logger.LogDebug($"Starting integrity check of the directory {_InputDirectory}");

            foreach (var jsonFile in IO.Directory.EnumerateFiles(_InputDirectory, "*.json", IO.SearchOption.TopDirectoryOnly))
            {
                _Logger.LogDebug($"JsonIntegrityCheck starting processing file {jsonFile}");

                try
                {
                    if (IO.File.Exists(jsonFile + ".fail"))
                    {
                        _Logger.LogDebug($"File {jsonFile} is already marked as fail-checked. Skipping...");
                        continue;
                    }

                    var errorList = new List<string>();

                    using (IO.FileStream stream = new IO.FileStream(jsonFile, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.None))
                    using (IO.StreamReader sr = new IO.StreamReader(stream))
                    using (JsonReader reader = new JsonTextReader(sr))
                    {
                        var isValid = true;

                        void AddToErrorList(string message)
                        {
                            isValid = false;
                            _Logger.LogDebug($"JsonIntegrityCheck adds new line to error list for file {jsonFile}: {message}");
                            errorList.Add(message);
                        }

                        try
                        {
                            var jsonDoc = JArray.Load(reader);
                            var startEvent = GetEvents(jsonDoc, LogEntry.TypeStartRecording).FirstOrDefault();
                            var stopEvent = GetEvents(jsonDoc, LogEntry.TypeStopRecording).FirstOrDefault();
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
                            var processInvalidMedia = false;

                            if (startEvent == default(JToken))
                            {
                                AddToErrorList($"Event \"{LogEntry.TypeStartRecording}\" is missing");
                            }

                            if (stopEvent == default(JToken))
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

                                var timestamp = startEvent["timestamp"]?.Value<DateTime?>();
                                var applicationId = startEvent["applicationId"]?.Value<string>();
                                var externalId = startEvent["externalId"]?.Value<string>();
                                var connectionId = startEvent["connectionId"]?.Value<string>();
                                var channelId = startEvent["channelId"]?.Value<string>();
                                Guid? applicationConfigId = null;
                                Guid? channelConfigId = null;

                                if (Guid.TryParse(startEvent["applicationConfigId"]?.Value<string>() ?? "", out var parseResult))
                                {
                                    applicationConfigId = parseResult;
                                }

                                if (Guid.TryParse(startEvent["channelConfigId"]?.Value<string>() ?? "", out parseResult))
                                {
                                    channelConfigId = parseResult;
                                }

                                ReportIfMissing(timestamp, nameof(timestamp), LogEntry.TypeStartRecording);
                                ReportIfMissing(applicationId, nameof(applicationId), LogEntry.TypeStartRecording);
                                ReportIfMissing(externalId, nameof(externalId), LogEntry.TypeStartRecording);
                                ReportIfMissing(channelId, nameof(channelId), LogEntry.TypeStartRecording);
                                ReportIfMissing(connectionId, nameof(connectionId), LogEntry.TypeStartRecording);
                                ReportIfMissing(applicationConfigId, nameof(applicationConfigId), LogEntry.TypeStartRecording);
                                ReportIfMissing(channelConfigId, nameof(channelConfigId), LogEntry.TypeStartRecording);

                                var timestamp2 = stopEvent["timestamp"]?.Value<DateTime?>();
                                var applicationId2 = stopEvent["applicationId"]?.Value<string>();
                                var externalId2 = stopEvent["externalId"]?.Value<string>();
                                var channelId2 = stopEvent["channelId"]?.Value<string>();
                                var connectionId2 = stopEvent["connectionId"]?.Value<string>();
                                Guid? applicationConfigId2 = null;
                                Guid? channelConfigId2 = null;

                                processInvalidMedia = (stopEvent[ProcessInvalidMediaFieldName] != null) || _ProcessInvalidMedia;

                                if (timestamp2 != null && timestamp > timestamp2)
                                {
                                    AddToErrorList("Start recording timestamp must not be greater than stop recording timestamp.");
                                }

                                if (Guid.TryParse(stopEvent["applicationConfigId"]?.Value<string>() ?? "", out parseResult))
                                {
                                    applicationConfigId2 = parseResult;
                                }

                                if (Guid.TryParse(stopEvent["channelConfigId"]?.Value<string>() ?? "", out parseResult))
                                {
                                    channelConfigId2 = parseResult;
                                }

                                ReportIfMissing(applicationId2, nameof(applicationId), LogEntry.TypeStopRecording);
                                ReportIfMissing(externalId2, nameof(externalId), LogEntry.TypeStopRecording);
                                ReportIfMissing(channelId2, nameof(channelId), LogEntry.TypeStopRecording);
                                ReportIfMissing(connectionId2, nameof(connectionId), LogEntry.TypeStopRecording);
                                ReportIfMissing(applicationConfigId2, nameof(applicationConfigId), LogEntry.TypeStopRecording);
                                ReportIfMissing(channelConfigId2, nameof(channelConfigId), LogEntry.TypeStopRecording);

                                ReportIfDifferent(applicationId, applicationId2, nameof(applicationId));
                                ReportIfDifferent(externalId, externalId2, nameof(externalId));
                                ReportIfDifferent(channelId, channelId2, nameof(channelId));
                                ReportIfDifferent(connectionId, connectionId2, nameof(connectionId));
                                ReportIfDifferent(applicationConfigId?.ToString(), applicationConfigId2?.ToString(), nameof(applicationConfigId));
                                ReportIfDifferent(channelConfigId?.ToString(), channelConfigId2?.ToString(), nameof(channelConfigId));

                                var data = stopEvent["data"];
                                audioFilePath = data?["audioFile"]?.Value<string>();
                                videoFilePath = data?["videoFile"]?.Value<string>();
                                firstAudioTimestamp = data?["audioFirstFrameTimestamp"]?.Value<DateTime?>();
                                firstVideoTimestamp = data?["videoFirstFrameTimestamp"]?.Value<DateTime?>();
                                lastAudioTimestamp = data?["audioLastFrameTimestamp"]?.Value<DateTime?>();
                                lastVideoTimestamp = data?["videoLastFrameTimestamp"]?.Value<DateTime?>();

                                addRecordStopTS = (timestamp2 == null);

                                if (audioFilePath != null && !IO.File.Exists(audioFilePath))
                                {
                                    AddToErrorList($"Audio file {audioFilePath} is missing.");
                                }

                                if (videoFilePath != null && !IO.File.Exists(videoFilePath))
                                {
                                    AddToErrorList($"Video file {videoFilePath} is missing.");
                                }

                                if (isValid)
                                {
                                    var baseName = IO.Path.GetFileNameWithoutExtension(jsonFile);
                                    var audioPath = IO.Path.Combine(_InputDirectory, baseName + ".mka");
                                    var videoPath = IO.Path.Combine(_InputDirectory, baseName + ".mkv");

                                    if (data == null)
                                    {
                                        addDataBlock = true;
                                    }

                                    if (IO.File.Exists(audioPath))
                                    {
                                        if (firstAudioTimestamp == null)
                                        {
                                            addFirstAudioTS = true;
                                            firstAudioTimestamp = timestamp;

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

                                    if (IO.File.Exists(videoPath))
                                    {
                                        if (firstVideoTimestamp == null)
                                        {
                                            addFirstVideoTS = true;
                                            firstVideoTimestamp = timestamp;

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
                                    var duration = GetDuration(_Logger, audioFilePath);
                                    if (duration != null)
                                    {
                                        lastAudioTimestamp = firstAudioTimestamp + duration;
                                        _Logger.LogDebug($"Calculated last audio frame timestamp for file {jsonFile}: {lastAudioTimestamp} (duration: {duration}).");
                                    }
                                    else if (processInvalidMedia)
                                    {
                                        lastAudioTimestamp = firstAudioTimestamp;
                                        _Logger.LogDebug($"Calculated last audio frame timestamp for file {jsonFile}: {lastAudioTimestamp} (duration: cannot be calculated).");
                                    }
                                    else
                                    {
                                        AddToErrorList($"Duration of audio file \"{audioFilePath}\" cannot be determined.");
                                    }
                                }

                                if (calcLastVideoTS)
                                {
                                    var duration = GetDuration(_Logger, videoFilePath);
                                    if (duration != null)
                                    {
                                        lastVideoTimestamp = firstVideoTimestamp + duration;
                                        _Logger.LogDebug($"Calculated last video frame timestamp for file {jsonFile}: {lastVideoTimestamp} (duration: {duration}).");
                                    }
                                    else if (processInvalidMedia)
                                    {
                                        lastVideoTimestamp = firstVideoTimestamp;
                                        _Logger.LogDebug($"Calculated last audio frame timestamp for file {jsonFile}: {lastVideoTimestamp} (duration: cannot be calculated).");
                                    }
                                    else
                                    {
                                        AddToErrorList($"Duration of video file \"{videoFilePath}\" cannot be determined.");
                                    }
                                }

                                using (IO.FileStream outStream = new IO.FileStream(tempFile, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.None))
                                using (IO.StreamWriter sw = new IO.StreamWriter(outStream))
                                using (JsonWriter writer = new JsonTextWriter(sw))
                                {
                                    try
                                    {
                                        var stopEventObj = stopEvent as JObject;
                                        if (addDataBlock)
                                        {
                                            AddProperty(stopEventObj, "data", new JObject());
                                        }

                                        var dataObj = stopEvent["data"] as JObject;

                                        if (addFirstAudioTS)
                                        {
                                            AddProperty(dataObj, "audioFirstFrameTimestamp", firstAudioTimestamp);
                                        }

                                        if (addFirstVideoTS)
                                        {
                                            AddProperty(dataObj, "videoFirstFrameTimestamp", firstVideoTimestamp);
                                        }

                                        if (addAudioPath)
                                        {
                                            AddProperty(dataObj, "audioFile", audioFilePath);
                                        }

                                        if (addVideoPath)
                                        {
                                            AddProperty(dataObj, "videoFile", videoFilePath);
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

                                            AddProperty(stopEventObj, "timestamp", stopRecTimestamp);
                                        }

                                        if (calcLastAudioTS && lastAudioTimestamp != null)
                                        {
                                            AddProperty(dataObj, "audioLastFrameTimestamp", lastAudioTimestamp);
                                        }

                                        if (calcLastVideoTS && lastVideoTimestamp != null)
                                        {
                                            AddProperty(dataObj, "videoLastFrameTimestamp", lastVideoTimestamp);
                                        }

                                        jsonDoc.WriteTo(writer);
                                        writer.Flush();
                                    }
                                    finally
                                    {
                                        writer.Close();
                                    }
                                }

                                tempFiles.Add(pair);
                            }
                        }
                        catch (Exception ex)
                        {
                            _Logger.LogError($"Exception during validation of the document {jsonFile}: {ex}");
                            AddToErrorList($"Exception during document validation: {ex}");
                        }
                        finally
                        {
                            stream.Close();
                        }
                    }

                    if (errorList.Count > 0)
                    {
                        var jsonBlob = IO.File.ReadAllText(jsonFile);

                        for (int i = errorList.Count - 1; i >= 0; i--)
                        {
                            var num = i + 1;
                            var error = errorList[i];
                            _Logger.LogError($"Integrity check error #{num} for the file {jsonFile}: {error}");
                            jsonBlob = "ERROR: " + error + "\n" + jsonBlob;
                        }

                        IO.File.WriteAllText(jsonFile + ".fail", jsonBlob);
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
                    IO.File.Move(pair.Item2, pair.Item1, true);
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

        private void ProcessOrphanSessions()
        {
            _Logger.LogDebug("Starting processing orphan sessions...");
            _Logger.LogDebug("Starting cleaning up missing files...");

            Dictionary<string, SessionTracker> orphanSessions = new Dictionary<string, SessionTracker>();
            var orphanFileName = IO.Path.Combine(_InputDirectory, OrphanSessionsFileName);

            if (IO.File.Exists(orphanFileName))
            {
                try
                {
                    var contents = IO.File.ReadAllText(orphanFileName);
                    orphanSessions = JsonConvert.DeserializeObject<Dictionary<string, SessionTracker>>(contents);
                }
                catch (Exception ex)
                {
                    _Logger.LogError($"Error reading stored orphan sessions: {ex}. Deleting file {OrphanSessionsFileName}");
                    IO.File.Delete(orphanFileName);
                    orphanSessions = new Dictionary<string, SessionTracker>();
                }
            }

            var invalidSessions = new List<string>();
            foreach (var session in orphanSessions)
            {
                if (!IO.File.Exists(session.Value.JsonFile))
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

            foreach (var jsonFile in IO.Directory.EnumerateFiles(_InputDirectory, "*.json.rec", IO.SearchOption.TopDirectoryOnly))
            {
                _Logger.LogDebug($"ProcessOrphanSessions starting analysis of the file {jsonFile}");

                try
                {
                    var baseName = IO.Path.GetFileNameWithoutExtension(IO.Path.GetFileNameWithoutExtension(jsonFile));
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

                    var mediaFile = IO.Path.Combine(_InputDirectory, $"{baseName}.mka");
                    if (IO.File.Exists(mediaFile))
                    {
                        _Logger.LogDebug($"Audio file {mediaFile} for {connectionId} is found.");
                        sessionTracker.AudioFile = mediaFile;
                    }

                    mediaFile = IO.Path.Combine(_InputDirectory, $"{baseName}.mkv");
                    if (IO.File.Exists(mediaFile))
                    {
                        _Logger.LogDebug($"Video file {mediaFile} for {connectionId} is found.");
                        sessionTracker.VideoFile = mediaFile;
                    }

                    mediaFile = IO.Path.Combine(_InputDirectory, $"{connectionId}.mka.rec");
                    if (IO.File.Exists(mediaFile))
                    {
                        _Logger.LogDebug($"Incomplete audio file {mediaFile} for {connectionId} is found.");
                        sessionTracker.AudioSizeMeasurement = new FileSizeMeasurement(mediaFile, MinimumOrphanDuration);
                    }

                    mediaFile = IO.Path.Combine(_InputDirectory, $"{connectionId}.mkv.rec");
                    if (IO.File.Exists(mediaFile))
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
            foreach (var (connectionId, sessionTracker) in orphanSessions)
            {
                _Logger.LogDebug($"ProcessOrphanSessions starting processing orphan sessions for {sessionTracker.JsonFile}");

                try
                {
                    var baseName = IO.Path.GetFileNameWithoutExtension(IO.Path.GetFileNameWithoutExtension(sessionTracker.JsonFile));

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
                            IO.File.Delete(sessionTracker.AudioFile);
                            sessionTracker.AudioFile = null;
                        }

                        var srcName = sessionTracker.AudioSizeMeasurement.FileName;
                        audioFile = IO.Path.Combine(IO.Path.GetDirectoryName(srcName), $"{baseName}.mka");
                        IO.File.Move(srcName, audioFile);
                        sessionTracker.AudioSizeMeasurement = null;

                        _Logger.LogDebug($"Moved audio file from {srcName} to {audioFile}");
                    }

                    if (sessionTracker.VideoSizeMeasurement != null)
                    {
                        if (sessionTracker.VideoFile != null)
                        {
                            IO.File.Delete(sessionTracker.VideoFile);
                            sessionTracker.VideoFile = null;
                        }

                        var srcName = sessionTracker.VideoSizeMeasurement.FileName;
                        videoFile = IO.Path.Combine(IO.Path.GetDirectoryName(srcName), $"{baseName}.mkv");
                        IO.File.Move(srcName, videoFile);
                        sessionTracker.VideoSizeMeasurement = null;

                        _Logger.LogDebug($"Moved audio file from {srcName} to {videoFile}");
                    }

                    var targetJsonName = IO.Path.Combine(IO.Path.GetDirectoryName(sessionTracker.JsonFile), IO.Path.GetFileNameWithoutExtension(sessionTracker.JsonFile));

                    using (IO.FileStream inStream = new IO.FileStream(sessionTracker.JsonFile, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.None))
                    using (IO.FileStream outStream = new IO.FileStream(targetJsonName, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.None))
                    using (IO.StreamReader sr = new IO.StreamReader(inStream))
                    using (IO.StreamWriter sw = new IO.StreamWriter(outStream))
                    using (JsonWriter writer = new JsonTextWriter(sw))
                    using (JsonReader reader = new JsonTextReader(sr))
                    {
                        var jsonDoc = JArray.Load(reader);
                        var startEvent = GetEvents(jsonDoc, LogEntry.TypeStartRecording).FirstOrDefault();

                        if (startEvent != null)
                        {
                            JObject dataObj = new JObject();
                            DateTime? lastTimestamp = null;

                            var firstFrameTimestamp = startEvent["timestamp"].Value<DateTime>();
                            _Logger.LogDebug($"Got first frame timestamp: {firstFrameTimestamp}");

                            if (audioFile != null)
                            {
                                _Logger.LogDebug($"Calculating duration for audio file {audioFile}");

                                AddProperty(dataObj, "audioFile", audioFile);
                                AddProperty(dataObj, "audioFirstFrameTimestamp", firstFrameTimestamp);

                                var duration = GetDuration(_Logger, audioFile);
                                if (duration != null)
                                {
                                    var lastFrameTimestamp = firstFrameTimestamp + duration;
                                    lastTimestamp = lastFrameTimestamp;
                                    AddProperty(dataObj, "audioLastFrameTimestamp", lastFrameTimestamp);
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

                                AddProperty(dataObj, "videoFile", videoFile);
                                AddProperty(dataObj, "videoFirstFrameTimestamp", firstFrameTimestamp);

                                var duration = GetDuration(_Logger, videoFile);
                                if (duration != null)
                                {
                                    var lastFrameTimestamp = firstFrameTimestamp + duration;
                                    if (lastTimestamp == null)
                                    {
                                        lastTimestamp = lastFrameTimestamp;
                                    }
                                    else
                                    {
                                        lastTimestamp = (lastTimestamp > lastFrameTimestamp) ? lastTimestamp : lastFrameTimestamp;
                                    }
                                    AddProperty(dataObj, "videoLastFrameTimestamp", lastFrameTimestamp);
                                    _Logger.LogDebug($"Measured duration of video file {videoFile} is {duration}. Last frame timestamp: {lastFrameTimestamp}");
                                }
                                else
                                {
                                    _Logger.LogError($"Duration of video file {videoFile} cannot be calculated");
                                }
                            }

                            JObject newObj = new JObject();
                            jsonDoc.Add(newObj);

                            AddProperty(newObj, "type", "stopRecording");
                            AddProperty(newObj, "timestamp", lastTimestamp);

                            AddProperty(newObj, "applicationId", startEvent["applicationId"]);
                            AddProperty(newObj, "applicationConfigId", startEvent["applicationConfigId"]);
                            AddProperty(newObj, "externalId", startEvent["externalId"]);
                            AddProperty(newObj, "channelId", startEvent["channelId"]);
                            AddProperty(newObj, "channelConfigId", startEvent["channelConfigId"]);
                            AddProperty(newObj, "userId", startEvent["userId"]);
                            AddProperty(newObj, "deviceId", startEvent["deviceId"]);
                            AddProperty(newObj, "clientId", startEvent["clientId"]);
                            AddProperty(newObj, "connectionId", startEvent["connectionId"]);

                            _Logger.LogDebug($"Copying section attributes from {sessionTracker.JsonFile} to {targetJsonName}");

                            AddProperty(newObj, "data", dataObj);

                            jsonDoc.WriteTo(writer);
                            writer.Flush();
                        }
                    }

                    IO.File.Delete(sessionTracker.JsonFile);
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
                    IO.File.WriteAllText(orphanFileName, contents);
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

        public List<JToken> GetEvents(JArray jsonDoc, string eventName)
        {
            var list = new List<JToken>();
            if (jsonDoc.Type == JTokenType.Array)
            {
                foreach (var obj in jsonDoc)
                {
                    var type = obj["type"]?.Value<string>();
                    if (type == eventName)
                    {
                        list.Add(obj);
                    }
                }
            }

            return list;
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

        private long? GetFirstTimecode(Matroska.Cluster cluster)
        {
            if (cluster.SimpleBlocks != null && cluster.SimpleBlocks.Length > 0)
            {
                return cluster.Timecode + cluster.SimpleBlocks.First().Timecode;
            }

            if (cluster.BlockGroups != null && cluster.BlockGroups.Length > 0)
            {
                return cluster.Timecode + cluster.BlockGroups.First().Block.Timecode;
            }

            return null;
        }

        private long? GetLastTimecode(Matroska.Cluster cluster)
        {
            if (cluster.SimpleBlocks != null && cluster.SimpleBlocks.Length > 0)
            {
                return cluster.Timecode + cluster.SimpleBlocks.Last().Timecode;
            }

            if (cluster.BlockGroups != null && cluster.BlockGroups.Length > 0)
            {
                return cluster.Timecode + cluster.BlockGroups.Last().Block.Timecode;
            }

            return null;
        }

        private TimeSpan? GetDuration(ILogger _Logger, string filePath)
        {
            try
            {
                var file = new Matroska.File(IO.File.ReadAllBytes(filePath));

                // get the clusters
                var clusters = file.Segment?.Clusters;
                if (clusters == null || clusters.Length == 0)
                {
                    return null;
                }

                // get the first timecode
                var firstTimecode = GetFirstTimecode(clusters.First());
                if (firstTimecode == null)
                {
                    return null;
                }

                // get the last timecode
                var lastTimecode = GetLastTimecode(clusters.Last());
                if (lastTimecode == null)
                {
                    return null;
                }

                // convert to nanoseconds
                var timecodeScale = file.Segment?.SegmentInfo?.TimecodeScale ?? Matroska.SegmentInfo.DefaultTimecodeScale;
                var durationNanos = (lastTimecode.Value - firstTimecode.Value) * timecodeScale;

                // convert to timespan
                return new TimeSpan(durationNanos / 100);
            }
            catch (Exception ex)
            {
                _Logger.LogDebug($"Error during extracting duration from the media file {filePath}: {ex}");
                return null;
            }
        }
    }
}
