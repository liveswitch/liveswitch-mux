using System;
using System.IO;
using System.Reflection;

namespace FM.LiveSwitch.Mux
{
    static class EmbeddedResource
    {
        public static byte[] Read(string key)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.{key}"))
            {
                if (stream == null)
                {
                    throw new Exception("Embedded resource not found.");
                }

                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }
    }
}
