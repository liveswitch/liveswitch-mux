using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    public class FfmpegUtility
    {
        private readonly ILogger _Logger;

        public FfmpegUtility(ILogger logger)
        {
            _Logger = logger;
        }

        public async Task<TimeSpan?> GetDuration(string fileName)
        {
            var option = Path.GetExtension(fileName) == ".mka" ? "a" : "v";
            var lines = await FFprobe($"-v quiet -select_streams {option}:0 -show_frames -show_entries frame=pkt_pts_time -print_format csv=item_sep=|:nokey=1:print_section=0 {fileName}").ConfigureAwait(false);

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

        public Task<string[]> FFmpeg(string arguments)
        {
            return Execute("ffmpeg", arguments, true, true);
        }

        public Task<string[]> FFprobe(string arguments)
        {
            return Execute("ffprobe", arguments, false, false);
        }

        private async Task<string[]> Execute(string command, string arguments, bool useStandardError, bool logOutput)
        {
            // prep the process arguments
            var processStartInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = command,
                Arguments = arguments
            };

            if (useStandardError)
            {
                processStartInfo.RedirectStandardError = true;
            }
            else
            {
                processStartInfo.RedirectStandardOutput = true;
            }

            // log what we're about to do
            _Logger.LogInformation(arguments);

            try
            {
                // let 'er rip
                var process = Process.Start(processStartInfo);

                // process each line
                var lines = new List<string>();
                var stream = useStandardError ? process.StandardError : process.StandardOutput;
                while (!stream.EndOfStream)
                {
                    var line = await stream.ReadLineAsync();
                    if (line != null)
                    {
                        if (logOutput)
                        {
                            _Logger.LogInformation(line);
                        }
                        lines.Add(line);
                    }
                }

                // make sure everything is finished
                process.WaitForExit();

                return lines.ToArray();
            }
            catch (Exception ex)
            {
                throw new ExecuteException($"Could not start {command}. Is ffmpeg installed and available on your PATH?", ex);
            }
        }
    }
}
