using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace get_link_manga
{
    internal static class PortableArchiveBootstrap
    {
        private const string SevenZipResourcePrefix = "7-Zip/";

        internal static void EnsurePortableSevenZip()
        {
            try
            {
                Directory.CreateDirectory(PortablePaths.SevenZipRoot);

                var assembly = Assembly.GetExecutingAssembly();
                var resourceNames = assembly
                    .GetManifestResourceNames()
                    .Where(name => name.StartsWith(SevenZipResourcePrefix, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                foreach (string resourceName in resourceNames)
                {
                    string relativePath = resourceName.Substring(SevenZipResourcePrefix.Length)
                        .Replace('/', Path.DirectorySeparatorChar);
                    string destinationPath = Path.Combine(PortablePaths.SevenZipRoot, relativePath);
                    ExtractEmbeddedResource(assembly, resourceName, destinationPath);
                }
            }
            catch
            {
                // Best effort only. Compression features will validate the tool
                // again when the user clicks the archive buttons.
            }
        }

        private static void ExtractEmbeddedResource(Assembly assembly, string resourceName, string destinationPath)
        {
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return;
                }

                string directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var fileStream = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    stream.CopyTo(fileStream);
                }
            }
        }
    }
}
