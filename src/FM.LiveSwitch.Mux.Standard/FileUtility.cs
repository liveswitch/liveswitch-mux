using System.IO;
using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    public class FileUtility : IFileUtility
    {
        public async Task<string> GetContents(string path)
        {
            using (var file = await GetStream(path, FileAccess.Read).ConfigureAwait(false))
            {
                using (var reader = new StreamReader(file))
                {
                    return await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }
        }

        private const int GetFileStreamRetryDelay = 200;
        private const int GetFileStreamRetryAttempts = 30000 / GetFileStreamRetryDelay;

        public async Task<FileStream> GetStream(string path, FileAccess access)
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

                    await Task.Delay(delay).ConfigureAwait(false);

                    // retry immediately the first time, but 200ms thereafter
                    delay = GetFileStreamRetryDelay;
                }
                i++;
            }
        }

        private bool IsLocked(IOException ex)
        {
            return !(ex is DirectoryNotFoundException ||
                     ex is DriveNotFoundException ||
                     ex is EndOfStreamException ||
                     ex is FileLoadException ||
                     ex is FileNotFoundException ||
                     ex is PathTooLongException
                    );
        }

        public bool Exists(string path)
        {
            return path != null && File.Exists(path);
        }

        public void Copy(string sourcePath, string destinationPath, bool overwrite)
        {
            File.Copy(sourcePath, destinationPath, overwrite);
        }

        public void Delete(string path)
        {
            File.Delete(path);
        }

        public void Write(string path, byte[] bytes)
        {
            File.WriteAllBytes(path, bytes);
        }

        public long GetLength(string path)
        {
            if (!Exists(path))
            {
                return 0;
            }
            return new FileInfo(path).Length;
        }
    }
}
