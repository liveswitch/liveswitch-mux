using System.Threading.Tasks;

namespace FM.LiveSwitch.Mux
{
    public interface IFileUtility
    {
        Task<string> GetContents(string path);
        bool Exists(string path);
        void Copy(string sourcePath, string destinationPath, bool overwrite);
        void Delete(string path);
        void Write(string path, byte[] bytes);
        long GetLength(string path);
    }
}
