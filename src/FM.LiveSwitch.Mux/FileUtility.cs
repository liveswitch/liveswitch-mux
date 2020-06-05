using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    public class FileUtility
    {
        public static async Task<string> GetContents(string path)
        {
            using (var file = await GetStream(path, FileAccess.Read))
            {
                using (var reader = new StreamReader(file))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        private const int GetFileStreamRetryDelay = 200;
        private const int GetFileStreamRetryAttempts = 30000 / GetFileStreamRetryDelay;

        public static async Task<FileStream> GetStream(string path, FileAccess access)
        {
            var delay = 0;

            var i = 0;
            while (true)
            {
                try
                {
                    return File.Open(path, FileMode.Open, access, FileShare.None);
                }
                catch (IOException e) when (IsLocked(e))
                {
                    // retry for approximately 30 seconds before giving up
                    if (i > GetFileStreamRetryAttempts)
                    {
                        throw;
                    }

                    await Task.Delay(delay);

                    // retry immediately the first time, but 200ms thereafter
                    delay = GetFileStreamRetryDelay;
                }
                i++;
            }
        }

        private static bool IsLocked(IOException exception)
        {
            int errorCode = exception.HResult & 0xffff;
            switch (errorCode)
            {
                case 0x20: // ERROR_SHARING_VIOLATION
                case 0x21: // ERROR_LOCK_VIOLATION
                    return true;
                default:
                    return false;
            }
        }

        public static bool Exists(string path)
        {
            return path != null && File.Exists(path) && new FileInfo(path).Length > 0;
        }
    }
}
