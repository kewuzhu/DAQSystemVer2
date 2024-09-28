using NLog;
using NLog.Config;
using NLog.Targets;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DAQSystem.Common.Utility
{
    public class LogUtils
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        public static string InitializeLogging(string logDirectory, string logFileName, string logTargetName = null)
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
                throw new ArgumentNullException(nameof(logDirectory));

            if (string.IsNullOrWhiteSpace(logFileName))
                throw new ArgumentNullException(nameof(logFileName));

            if (!Directory.Exists(logDirectory))
                Directory.CreateDirectory(logDirectory);

            logTargetName ??= Guid.NewGuid().ToString();

            var loggingConfig = new LoggingConfiguration();

            var logfile = new FileTarget(logTargetName) { FileName = Path.Combine(logDirectory, logFileName) };
            var defaultFileLoggerLogLevel = Model.LogLevel.All;
            ConfigureTargetLogLevel(loggingConfig, logfile, defaultFileLoggerLogLevel);
            loggingConfig.AddTarget(logfile);

            LogManager.Configuration = loggingConfig;

            return logTargetName;
        }

        public static string InitializeExtendedLogging(Model.LogLevel? fileLoggerLogLevel, string fileLogTargetName,
            Model.LogLevel? consoleLoggerLogLevel, string consoleLogTargetName = null)
        {
            var loggingConfig = LogManager.Configuration;

            if (fileLoggerLogLevel != null)
            {
                if (string.IsNullOrWhiteSpace(fileLogTargetName))
                    throw new ArgumentNullException($"{nameof(fileLogTargetName)}");

                var logfile = loggingConfig.FindTargetByName(fileLogTargetName);
                ConfigureTargetLogLevel(loggingConfig, logfile, fileLoggerLogLevel.Value);
            }

            if (consoleLoggerLogLevel != Model.LogLevel.None)
            {
                AllocConsole();
                Console.OutputEncoding = Encoding.UTF8;
                consoleLogTargetName ??= Guid.NewGuid().ToString();
                var logConsole = new ColoredConsoleTarget(consoleLogTargetName);
                ConfigureTargetLogLevel(loggingConfig, logConsole, consoleLoggerLogLevel.Value);

                logConsole.UseDefaultRowHighlightingRules = true;
                loggingConfig.AddTarget(logConsole);
            }

            LogManager.Configuration = loggingConfig;
            return consoleLogTargetName;
        }

        private static void ConfigureTargetLogLevel(LoggingConfiguration config,
            Target target, Model.LogLevel logLevel)
        {
            var rule = new LoggingRule("*", target);
            rule.RuleName = target.Name;
            if (logLevel.HasFlag(Model.LogLevel.Fatal))
                rule.EnableLoggingForLevel(NLog.LogLevel.Fatal);
            else
                rule.DisableLoggingForLevel(NLog.LogLevel.Fatal);

            if (logLevel.HasFlag(Model.LogLevel.Error))
                rule.EnableLoggingForLevel(NLog.LogLevel.Error);
            else
                rule.DisableLoggingForLevel(NLog.LogLevel.Error);

            if (logLevel.HasFlag(Model.LogLevel.Warning))
                rule.EnableLoggingForLevel(NLog.LogLevel.Warn);
            else
                rule.DisableLoggingForLevel(NLog.LogLevel.Warn);

            if (logLevel.HasFlag(Model.LogLevel.Info))
                rule.EnableLoggingForLevel(NLog.LogLevel.Info);
            else
                rule.DisableLoggingForLevel(NLog.LogLevel.Info);

            if (logLevel.HasFlag(Model.LogLevel.Debug))
                rule.EnableLoggingForLevel(NLog.LogLevel.Debug);
            else
                rule.DisableLoggingForLevel(NLog.LogLevel.Debug);

            config.LoggingRules.Add(rule);
        }
    }
}
