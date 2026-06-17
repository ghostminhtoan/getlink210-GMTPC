using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace get_link_manga
{
    internal static class PortableRuntimeBootstrap
    {
        private const string LoaderResourcePrefix = "runtimes/webview2/";
        private const string LoaderFileName = "WebView2Loader.dll";

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        internal static void EnsurePortableRuntime()
        {
            try
            {
                string nativeFolder = PortablePaths.GetRuntimeNativeFolder();
                Directory.CreateDirectory(nativeFolder);

                string resourceName = GetRuntimeResourceName();
                if (!string.IsNullOrWhiteSpace(resourceName))
                {
                    string destination = Path.Combine(nativeFolder, LoaderFileName);
                    ExtractEmbeddedResource(resourceName, destination);
                }

                SetDllDirectory(nativeFolder);
            }
            catch
            {
                // Best effort only. If the native loader cannot be extracted,
                // WebView2 will fall back to whatever runtime is available.
            }
        }

        internal static void ResetPortableRuntimeStorage()
        {
            TryDeleteDirectory(PortablePaths.WebView2UserDataFolder);

            try
            {
                string runtimeRoot = PortablePaths.WebView2RuntimeRoot;
                if (!Directory.Exists(runtimeRoot))
                {
                    return;
                }

                foreach (string directory in Directory.GetDirectories(runtimeRoot))
                {
                    if (string.Equals(directory, PortablePaths.WebView2UserDataFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string name = Path.GetFileName(directory);
                    if (string.Equals(name, "win-x64", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "win-x86", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "win-arm64", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    TryDeleteDirectory(directory);
                }
            }
            catch
            {
            }
        }

        private static string GetRuntimeResourceName()
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                return $"{LoaderResourcePrefix}win-arm64/native/{LoaderFileName}";
            }

            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                return $"{LoaderResourcePrefix}win-x64/native/{LoaderFileName}";
            }

            return $"{LoaderResourcePrefix}win-x86/native/{LoaderFileName}";
        }

        private static void ExtractEmbeddedResource(string resourceName, string destinationPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
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

        private static void TryDeleteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch
            {
            }
        }
    }
}
