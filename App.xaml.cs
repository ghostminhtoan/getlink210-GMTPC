using System;
using System.Net;
using System.Windows;

namespace get_link_manga
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ServicePointManager.DefaultConnectionLimit = Math.Max(ServicePointManager.DefaultConnectionLimit, 256);
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;

            PortableRuntimeBootstrap.EnsurePortableRuntime();
            PortableArchiveBootstrap.EnsurePortableSevenZip();
            PortableArchiveBootstrap.EnsurePortableFastStone();
            try
            {
                System.IO.Directory.SetCurrentDirectory(PortablePaths.AppRoot);
            }
            catch
            {
            }

            base.OnStartup(e);
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
