using DAQSystem.Application.Model;
using DAQSystem.Application.Themes;
using DAQSystem.Application.UI;
using DAQSystem.Application.Utility;
using DAQSystem.Common.Utility;
using DAQSystem.DataAcquisition;
using NLog;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;

namespace DAQSystem.Application
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private static readonly string APP_CONFIG_FILE_PATH = "C://DAQSystem//appconfig.json";
        private static readonly string LOG_DIRECTORY = "C://DAQSystem//SessionLogs";
        private static readonly string APP_LOG_FILE_NAME = "application.log";
        private static readonly string ROOT_DIRECTORY = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                base.OnStartup(e);

                InitializeLogging();

                var appConfig = JsonSerializer.Deserialize<ApplicationConfiguration>(File.ReadAllText(APP_CONFIG_FILE_PATH));
                appConfig.WorkingDirectory = Path.Combine(ROOT_DIRECTORY, appConfig.WorkingDirectory);

                LogUtils.InitializeExtendedLogging(appConfig.FileLoggerLogLevel, appLogTargetName_, appConfig.ConsoleLoggerLogLevel);

                Theme.AddStringsDictionary(appConfig.Language);

                mainWindowViewModel_ = new MainWindowViewModel(appConfig.SerialConfiguration);
                MainWindow = new MainWindow { DataContext = mainWindowViewModel_ };

                MainWindow.Show();
            }
            catch (Exception ex)
            {
                UserCommunication.ShowMessage($"{Theme.GetString(Strings.Error)}", ex.Message, MessageType.Critical);
            }
        }

        private void InitializeLogging()
        {
            var appAssemblyName = typeof(App).Assembly.GetName();
            var appVersion = appAssemblyName.Version;

            logDirectory_ = Path.Combine(
                LOG_DIRECTORY,
                $"{appAssemblyName.Name}-{appVersion.Major}.{appVersion.Minor}.{appVersion.Build}",
                $"{DateTime.Now:yyMMddHHmm}");

            appLogTargetName_ = LogUtils.InitializeLogging(logDirectory_, APP_LOG_FILE_NAME);

            logger_.Info($"{appAssemblyName.Name} {appVersion} is starting...");
        }

        private async void OnApplicationExit(object sender, ExitEventArgs e) => await TerminateApplication();

        private async Task TerminateApplication(int exitCode = (int)ApplicationExitCode.Success)
        {
            if (shuttingDown_) return;

            try
            {
                shuttingDown_ = true;
                logger_.Info("Shutting down...");

                await mainWindowViewModel_.CleanUp();
                Current.Shutdown(exitCode);
            }
            catch (Exception e)
            {
                logger_.Info(e, "Secondary exception in TerminateApplication.");
                KillCurrentProcess();
            }
        }

        private static void KillCurrentProcess()
        {
            logger_.Info("Killing current process...");
            Process.GetCurrentProcess().Kill();
        }

        private static readonly Logger logger_ = LogManager.GetCurrentClassLogger();

        private string logDirectory_;
        private string appLogTargetName_;
        private MainWindowViewModel mainWindowViewModel_;
        private bool shuttingDown_;

        private DataAcquisitionControl daq_ = new();
    }
}