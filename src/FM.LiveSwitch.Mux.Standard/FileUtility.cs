using IO = System.IO;
using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    public static class FileUtility
    {
        public static async Task<string> GetContents(string path)
        {
            using (var file = await GetStream(path, IO.FileAccess.Read).ConfigureAwait(false))
            {
                using (var reader = new IO.StreamReader(file))
                {
                    return await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }
        }

        private const int GetFileStreamRetryDelay = 200;
        private const int GetFileStreamRetryAttempts = 30000 / GetFileStreamRetryDelay;

        public static async Task<IO.FileStream> GetStream(string path, IO.FileAccess access)
        {
            var delay = 0;

            var i = 0;
            while (true)
            {
                try
                {
                    return IO.File.Open(path, IO.FileMode.Open, access, IO.FileShare.None);
                }
                catch (IO.IOException e) when (IsLocked(e))
                {
                    // retry for approximately 30 seconds before giving up
                    if (i > GetFileStreamRetryAttempts)
                    {
                        throw;
                    }

                    await Task.Delay(delay).ConfigureAwait(false);

                    // retry immediately the first time, but 200ms thereafter
                    delay = GetFileStreamRetryDelay;
                }
                i++;
            }
        }

        private static bool IsLocked(IO.IOException ex)
        {
            return !(ex is IO.DirectoryNotFoundException ||
                     ex is IO.DriveNotFoundException ||
                     ex is IO.EndOfStreamException ||
                     ex is IO.FileLoadException ||
                     ex is IO.FileNotFoundException ||
                     ex is IO.PathTooLongException
                    );
        }

        public static bool Exists(string path)
        {
            return path != null && IO.File.Exists(path);
        }
    }
}
