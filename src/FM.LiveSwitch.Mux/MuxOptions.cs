using CommandLine;

namespace FM.LiveSwitch.Mux
{
    public class MuxOptions
    {
        public const int MinMargin = 0;
        public const int MinWidth = 160;
        public const int MinHeight = 120;

        [Option('i', "input-path", Required = false, Default = null, HelpText = "The input path, i.e. the recording path used by the media server. Defaults to the current directory.")]
        public string InputPath { get; set; }

        [Option('o', "output-path", Required = false, Default = null, HelpText = "The output path for muxed sessions. Defaults to the input path.")]
        public string OutputPath { get; set; }

        [Option('s', "strategy", Required = false, Default = StrategyType.AutoDetect, HelpText = "The recording strategy used by the media server.")]
        public StrategyType Strategy { get; set; }

        [Option('l', "layout", Required = false, Default = LayoutType.HGrid, HelpText = "The video layout to use.")]
        public LayoutType Layout { get; set; }

        [Option('m', "margin", Required = false, Default = 0, HelpText = "The margin, in pixels, to insert between videos in the layout.")]
        public int Margin { get; set; }

        [Option('w', "width", Required = false, Default = 1920, HelpText = "The pixel width of the output video.")]
        public int Width { get; set; }

        [Option('h', "height", Required = false, Default = 1080, HelpText = "The pixel height of the output video.")]
        public int Height { get; set; }

        [Option('f', "frame-rate", Required = false, Default = 30, HelpText = "The frames per second of the output video.")]
        public int FrameRate { get; set; }

        [Option("background-color", Required = false, Default = "000000", HelpText = "The background colour.")]
        public string BackgroundColor { get; set; }

        [Option("audio-codec", Required = false, Default = "libopus", HelpText = "The output audio codec and options.")]
        public string AudioCodec { get; set; }

        [Option("video-codec", Required = false, Default = "libvpx -auto-alt-ref 0", HelpText = "The output video codec and options.")]
        public string VideoCodec { get; set; }

        [Option("audio-container", Required = false, Default = "mka", HelpText = "The output audio container (file extension).")]
        public string AudioContainer{ get; set; }

        [Option("video-container", Required = false, Default = "mkv", HelpText = "The output video container (file extension).")]
        public string VideoContainer { get; set; }

        [Option("dynamic", Required = false, HelpText = "Dynamically update the video layout as recordings start and stop.")]
        public bool Dynamic { get; set; }

        [Option("crop", Required = false, HelpText = "Crop video in order to use all available layout space.")]
        public bool Crop { get; set; }

        [Option("no-audio", Required = false, HelpText = "Do not mux audio.")]
        public bool NoAudio { get; set; }

        [Option("no-video", Required = false, HelpText = "Do not mux video.")]
        public bool NoVideo { get; set; }

        [Option("move-inputs", Required = false, HelpText = "Move input files to the output path.")]
        public bool MoveInputs { get; set; }

        [Option("delete-inputs", Required = false, HelpText = "Delete input files from the input path.")]
        public bool DeleteInputs { get; set; }

        [Option("no-prompt", Required = false, HelpText = "Do not prompt before deleting.")]
        public bool NoPrompt { get; set; }

        [Option("application-id", Required = false, Default = null, HelpText = "The application ID to mux.")]
        public string ApplicationId { get; set; }

        [Option("channel-id", Required = false, Default = null, HelpText = "The channel ID to mux.")]
        public string ChannelId { get; set; }

        [Option("output-file-name", Required = false, Default = "session_{startTimestamp}_to_{stopTimestamp}", HelpText = "The output file name template. Uses curly-brace syntax (e.g. {channelId}). Valid variables: applicationId, channelId, startTimestamp, stopTimestamp")]
        public string OutputFileName { get; set; }

        [Option("js-file", Required = false, Default = null, HelpText = "For JS layout, the JavaScript file path. Defaults to layout.js in the input path.")]
        public string JavaScriptFile { get; set; }

        [Option("trim-first", Default = false, Required = false, HelpText = "Trim audio from the first participant before any other participants have joined. Requires the --no-video flag.")]
        public bool TrimFirst { get; set; }

        [Option("trim-last", Default = false, Required = false, HelpText = "Trim audio from the last participant after all other participants have left. Requires the --no-video flag.")]
        public bool TrimLast { get; set; }

        [Option("no-filter-files", Required = false, HelpText = "Do not use files for the filters. Pass the filters as arguments.")]
        public bool NoFilterFiles { get; set; }

        [Option("save-filter-files", Required = false, HelpText = "Do not delete the filter files. Ignored if --no-filter-files is set.")]
        public bool SaveFilterFiles { get; set; }

        [Option("dry-run", Required = false, HelpText = "Do a dry-run with no muxing.")]
        public bool DryRun { get; set; }
    }
}
