using DAQSystem.DataAcquisition;
using System.Globalization;
using System.Windows.Data;

namespace DAQSystem.Application.Themes.Converters
{
    public class SettingParametersToString : BaseConverter, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value is not CommandTypes cmdType)
                return null;

            return cmdType switch
            {
                CommandTypes.SetCollectDuration => Theme.GetString(Strings.CollectDuration),
                CommandTypes.SetInitialThreshold => Theme.GetString(Strings.InitialThreshold),
                CommandTypes.SetSignalSign => Theme.GetString(Strings.SignalSign),
                CommandTypes.SetSignalBaseline => Theme.GetString(Strings.SignalBaseline),
                CommandTypes.SetTimeInterval => Theme.GetString(Strings.TimeInterval),
                CommandTypes.SetGain => Theme.GetString(Strings.Gain),
                _ => value.ToString(),
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value is not string s)
                return null;

            if (s == Theme.GetString(Strings.CollectDuration))
                return CommandTypes.SetCollectDuration;
            if (s == Theme.GetString(Strings.InitialThreshold))
                return CommandTypes.SetInitialThreshold;
            if (s == Theme.GetString(Strings.SignalSign))
                return CommandTypes.SetSignalSign;
            if (s == Theme.GetString(Strings.SignalBaseline))
                return CommandTypes.SetSignalBaseline;
            if (s == Theme.GetString(Strings.TimeInterval))
                return CommandTypes.SetTimeInterval;
            if (s == Theme.GetString(Strings.Gain))
                return CommandTypes.SetGain;

            return Enum.Parse(typeof(CommandTypes), value.ToString());
        }
    }
}