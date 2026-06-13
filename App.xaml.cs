using System.Windows;

namespace get_link_manga
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            PortableRuntimeBootstrap.EnsurePortableRuntime();
            PortableArchiveBootstrap.EnsurePortableSevenZip();
            PortableArchiveBootstrap.EnsurePortableBandiView();
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
