using System.Windows;
using FileConverter.Services;

namespace FileConverter
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ThemeManager.Initialize();
            LocalizationManager.Initialize();

            base.OnStartup(e);
        }
    }
}
