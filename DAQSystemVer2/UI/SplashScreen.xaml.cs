using DAQSystem.Common.Utility;
using System.Windows;
using static DAQSystem.Application.ApplicationConstants;

namespace DAQSystem.Application.UI
{
    /// <summary>
    /// SplashScreen.xaml 的交互逻辑
    /// </summary>
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();

            this.Loaded += OnSplashScreenLoadedAsync;
        }

        private async void OnSplashScreenLoadedAsync(object sender, RoutedEventArgs e)
        {
            await LogUtils.ScanAndClearAppConfigFilesInAllDriversAsync(APP_CONFIG_FILE_NAME);
        }
    }
}
