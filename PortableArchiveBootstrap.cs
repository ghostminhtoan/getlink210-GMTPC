using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace get_link_manga
{
    internal static class PortableArchiveBootstrap
    {
        private const string SevenZipResourcePrefix = "7-Zip/";
        private const string BandiViewResourcePrefix = "Bandiview/";

        internal static void EnsurePortableSevenZip()
        {
            try
            {
                ExtractEmbeddedResourceTree(SevenZipResourcePrefix, PortablePaths.SevenZipRoot);
            }
            catch
            {
                // Best effort only. Compression features will validate the tool
                // again when the user clicks the archive buttons.
            }
        }

        internal static void EnsurePortableBandiView()
        {
            try
            {
                ExtractEmbeddedResourceTree(BandiViewResourcePrefix, PortablePaths.BandiViewRoot);
            }
            catch
            {
                // Best effort only. Reader actions will validate the tool again
                // when the user opens Bandiview.
            }
        }

        private static void ExtractEmbeddedResourceTree(string resourcePrefix, string destinationRoot)
        {
            Directory.CreateDirectory(destinationRoot);

            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly
                .GetManifestResourceNames()
                .Where(name => name.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (string resourceName in resourceNames)
            {
                string relativePath = resourceName.Substring(resourcePrefix.Length)
                    .Replace('/', Path.DirectorySeparatorChar);
                string destinationPath = Path.Combine(destinationRoot, relativePath);
                ExtractEmbeddedResource(assembly, resourceName, destinationPath);
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
