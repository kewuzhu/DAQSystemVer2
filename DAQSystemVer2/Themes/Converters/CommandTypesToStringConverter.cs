using DAQSystem.DataAcquisition;
using System.Globalization;
using System.Windows.Data;

namespace DAQSystem.Application.Themes.Converters
{
    public class CommandTypesToStringConverter : BaseConverter, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value is not CommandTypes cmdType)
                return null;

            return cmdType switch
            {
                CommandTypes.StopAndReset => Theme.GetString(Strings.StopAndReset),
                CommandTypes.StartToCollect => Theme.GetString(Strings.StartToCollect),
                CommandTypes.SetCollectDuration => Theme.GetString(Strings.Set) + Theme.GetString(Strings.CollectDuration),
                CommandTypes.SetInitialThreshold => Theme.GetString(Strings.Set) + Theme.GetString(Strings.InitialThreshold),
                CommandTypes.SetSignalSign => Theme.GetString(Strings.Set) + Theme.GetString(Strings.SignalSign),
                CommandTypes.SetSignalBaseline => Theme.GetString(Strings.Set) + Theme.GetString(Strings.SignalBaseline),
                CommandTypes.SetTimeInterval => Theme.GetString(Strings.Set) + Theme.GetString(Strings.TimeInterval),
                CommandTypes.SetGain => Theme.GetString(Strings.Set) + Theme.GetString(Strings.Gain),
                _ => value.ToString(),
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value is not string s)
                return null;

            if (s == Theme.GetString(Strings.StopAndReset))
                return CommandTypes.StopAndReset;
            if (s == Theme.GetString(Strings.StartToCollect))
                return CommandTypes.StartToCollect;
            if (s == Theme.GetString(Strings.Set) + Theme.GetString(Strings.CollectDuration))
                return CommandTypes.SetCollectDuration;
            if (s == Theme.GetString(Strings.Set) + Theme.GetString(Strings.InitialThreshold))
                return CommandTypes.SetInitialThreshold;
            if (s == Theme.GetString(Strings.Set) + Theme.GetString(Strings.SignalSign))
                return CommandTypes.SetSignalSign;
            if (s == Theme.GetString(Strings.Set) + Theme.GetString(Strings.SignalBaseline))
                return CommandTypes.SetSignalBaseline;
            if (s == Theme.GetString(Strings.Set) + Theme.GetString(Strings.TimeInterval))
                return CommandTypes.SetTimeInterval;
            if (s == Theme.GetString(Strings.Set) + Theme.GetString(Strings.Gain))
                return CommandTypes.SetGain;

            return Enum.Parse(typeof(CommandTypes), value.ToString());
        }
    }
}
