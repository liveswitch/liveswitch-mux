using CommandLine;
using System;
using System.Collections.Generic;

namespace FM.LiveSwitch.Mux
{
    [Verb("mux", true)]
    public class MuxOptions
    {
        [Option('i', "input-path", HelpText = "The input path, i.e. the recording path used by the media server. Defaults to the current directory.")]
        public string InputPath { get; set; }

        [Option('o', "output-path", HelpText = "The output path for muxed sessions. Defaults to the input path.")]
        public string OutputPath { get; set; }

        [Option('t', "temp-path", HelpText = "The path for temporary intermediate files. Defaults to the input path.")]
        public string TempPath { get; set; }

        [Option('s', "strategy", Default = StrategyType.AutoDetect, HelpText = "The recording strategy used by the media server.")]
        public StrategyType Strategy { get; set; }

        [Option('l', "layout", Default = LayoutType.HGrid, HelpText = "The video layout to use.")]
        public LayoutType Layout { get; set; }

        [Option('m', "margin", Default = 0, HelpText = "The margin, in pixels, to insert between videos in the layout.")]
        public int Margin { get; set; }

        [Option('w', "width", Default = 1920, HelpText = "The pixel width of the output video.")]
        public int Width { get; set; }

        [Option('h', "height", Default = 1080, HelpText = "The pixel height of the output video.")]
        public int Height { get; set; }

        [Option('f', "frame-rate", Default = 30, HelpText = "The frames per second of the output video.")]
        public int FrameRate { get; set; }

        [Option("background-color", Default = "000000", HelpText = "The background colour.")]
        public string BackgroundColor { get; set; }

        [Option("audio-codec", Default = "libopus", HelpText = "The output audio codec and options.")]
        public string AudioCodec { get; set; }

        [Option("video-codec", Default = "libvpx -auto-alt-ref 0", HelpText = "The output video codec and options.")]
        public string VideoCodec { get; set; }

        [Option("audio-container", Default = "mka", HelpText = "The output audio container (file extension).")]
        public string AudioContainer{ get; set; }

        [Option("video-container", Default = "mkv", HelpText = "The output video container (file extension).")]
        public string VideoContainer { get; set; }

        [Option("dynamic", HelpText = "Dynamically update the video layout as recordings start and stop.")]
        public bool Dynamic { get; set; }

        [Option("crop", HelpText = "Crop video in order to use all available layout space.")]
        public bool Crop { get; set; }

        [Option("no-audio", HelpText = "Do not mux audio.")]
        public bool NoAudio { get; set; }

        [Option("no-video", HelpText = "Do not mux video.")]
        public bool NoVideo { get; set; }

        [Option("move-inputs", HelpText = "Move input files to the move path.")]
        public bool MoveInputs { get; set; }

        [Option("move-path", HelpText = "The destination path for moved files. Defaults to the output path.")]
        public string MovePath { get; set; }

        [Option("delete-inputs", HelpText = "Delete input files from the input path.")]
        public bool DeleteInputs { get; set; }

        [Option("no-prompt", HelpText = "Do not prompt before deleting.")]
        public bool NoPrompt { get; set; }

        [Option("application-id", HelpText = "The application ID to mux.")]
        public string ApplicationId { get; set; }

        [Option("channel-id", HelpText = "The channel ID to mux.")]
        public string ChannelId { get; set; }

        [Option("output-file-name", Default = "session_{startTimestamp}_to_{stopTimestamp}_{sessionId}", HelpText = "The output file name template. Uses curly-brace syntax (e.g. {channelId}). Valid variables: applicationId, channelId, sessionId, startTimestamp, stopTimestamp")]
        public string OutputFileName { get; set; }

        [Option("js-file", HelpText = "For JS layout, the JavaScript file path. Defaults to layout.js in the input path.")]
        public string JavaScriptFile { get; set; }

        [Option("continue-on-failure", HelpText = "Continue processing remaining sessions if a session fails to be processed.")]
        public bool ContinueOnFailure { get; set; }

        [Option("trim-first", HelpText = "Trim audio from the first participant before any other participants have joined. Requires the --no-video flag.")]
        public bool TrimFirst { get; set; }

        [Option("trim-last", HelpText = "Trim audio from the last participant after all other participants have left. Requires the --no-video flag.")]
        public bool TrimLast { get; set; }

        [Option("no-filter-files", HelpText = "Do not use files for the filters. Pass the filters as arguments.")]
        public bool NoFilterFiles { get; set; }

        [Option("save-temp-files", HelpText = "Do not delete the temporary intermediate files.")]
        public bool SaveTempFiles { get; set; }

        [Option("dry-run", HelpText = "Do a dry-run with no muxing.")]
        public bool DryRun { get; set; }

        [Option("session-id", HelpText = "The session ID to mux, obtained from a dry-run.")]
        public Guid? SessionId { get; set; }

        [Option("input-filter", HelpText = "A regular expression used to filter the input file list.")]
        public string InputFilter { get; set; }

        [Option("input-file-names", Separator = ',', HelpText = "A comma separated list of input files to target instead of scanning the directory.")]
        public IEnumerable<string> InputFileNames { get; set; }
    }
}
