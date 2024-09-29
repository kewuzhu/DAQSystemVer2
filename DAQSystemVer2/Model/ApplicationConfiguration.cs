using DAQSystem.Common.Model;

namespace DAQSystem.Application.Model
{
    internal class ApplicationConfiguration
    {
        public Language Language { get; set; } = Language.Chinese;

        public LogLevel ConsoleLoggerLogLevel { get; set; }

        public LogLevel? FileLoggerLogLevel { get; set; }

        public string WorkingDirectory { get; set; }

        public SerialConfiguration SerialConfiguration { get; set; }
    }
}
