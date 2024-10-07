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

                byte[] bytes = {
            0xAA, 0xBB, 0x00, 0x00, 0x00, 0x01, 0xEE, 0xFF,
            0xAA, 0xBB, 0x00, 0x00, 0x01, 0x02, 0xEE, 0xFF,
            0xAA, 0xBB, 0x00, 0x00, 0x00, 0x03, 0xEE, 0xFF
        };

                int[] result = ParseBytes(bytes);

                Console.WriteLine(string.Join(", ", result)); // 输出: 1, 2, 3

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

        public static int[] ParseBytes(byte[] bytes)
        {
            // 1. 将字节数组转换为十六进制字符串
            string hexString = BitConverter.ToString(bytes).Replace("-", "").ToLower();

            // 2. 去掉指定的子串 "aabb0000" 和 "eeff"
            hexString = hexString.Replace("aabb0000", "").Replace("eeff", "");

            // 3. 将剩余的字节每两个字符合并为一个int
            List<int> result = new List<int>();

            for (int i = 0; i < hexString.Length; i += 4)
            {
                // 确保我们有足够的字符形成一个完整的字节
                if (i + 1 < hexString.Length)
                {
                    string byteString = hexString.Substring(i, 4); // 获取两个字符
                    var value = Convert.ToInt32(byteString, 16);   // 转换为字节
                    result.Add(value); // 添加到结果列表
                }
            }

            return result.ToArray();
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
    }
}