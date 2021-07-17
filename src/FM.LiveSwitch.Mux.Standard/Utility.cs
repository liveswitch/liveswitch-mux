using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    public enum StandardStream
    {
        Output = 1,
        Error = 2
    }

    public class Utility
    {
        private readonly ILogger _Logger;

        public Utility(ILogger logger)
        {
            _Logger = logger;
        }

        /// <summary>
        /// Call FFmpeg while capturing and logging the standard error stream.
        /// </summary>
        /// <param name="arguments">The FFmpeg arguments.</param>
        /// <returns>The lines written to the standard error stream.</returns>
        public Task<string[]> FFmpeg(string arguments)
        {
            return FFmpeg(arguments, StandardStream.Error);
        }

        /// <summary>
        /// Call FFmpeg while capturing and logging the standard error or output stream.
        /// </summary>
        /// <param name="arguments">The FFmpeg arguments.</param>
        /// <param name="standardStream">The standard stream to read.</param>
        /// <returns>The lines written to the standard error or output stream.</returns>
        public Task<string[]> FFmpeg(string arguments, StandardStream standardStream)
        {
            return FFmpeg(arguments, standardStream, true);
        }

        /// <summary>
        /// Call FFmpeg while capturing and optionally logging the standard error or output stream.
        /// </summary>
        /// <param name="arguments">The FFmpeg arguments.</param>
        /// <param name="standardStream">The standard stream to read.</param>
        /// <param name="logOutput">Whether to log output from the standard stream.</param>
        /// <returns>The lines written to the standard error or output stream.</returns>
        public Task<string[]> FFmpeg(string arguments, StandardStream standardStream, bool logOutput)
        {
            return Execute("ffmpeg", arguments, standardStream, logOutput);
        }

        /// <summary>
        /// Call FFprobe while capturing and logging the standard output stream.
        /// </summary>
        /// <param name="arguments">The FFprobe arguments.</param>
        /// <returns>The lines written to the standard output stream.</returns>
        public Task<string[]> FFprobe(string arguments)
        {
            return FFprobe(arguments, StandardStream.Output);
        }

        /// <summary>
        /// Call FFprobe while capturing and logging the standard output or error stream.
        /// </summary>
        /// <param name="arguments">The FFprobe arguments.</param>
        /// <param name="standardStream">The standard stream to read.</param>
        /// <returns>The lines written to the standard output or error stream.</returns>
        public Task<string[]> FFprobe(string arguments, StandardStream standardStream)
        {
            return FFprobe(arguments, standardStream, false);
        }

        /// <summary>
        /// Call FFprobe while capturing and optionally logging the standard output or error stream.
        /// </summary>
        /// <param name="arguments">The FFprobe arguments.</param>
        /// <param name="standardStream">The standard stream to read.</param>
        /// <param name="logOutput">Whether to log output from the standard stream.</param>
        /// <returns>The lines written to the standard output or error stream.</returns>
        public Task<string[]> FFprobe(string arguments, StandardStream standardStream, bool logOutput)
        {
            return Execute("ffprobe", arguments, standardStream, logOutput);
        }

        private async Task<string[]> Execute(string command, string arguments, StandardStream standardStream, bool logOutput)
        {
            // prep the process arguments
            var processStartInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false
            };

            if (standardStream == StandardStream.Error)
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
                var stream = standardStream == StandardStream.Error ? process.StandardError : process.StandardOutput;
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
