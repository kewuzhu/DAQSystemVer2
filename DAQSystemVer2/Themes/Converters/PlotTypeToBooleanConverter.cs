using DAQSystem.Application.Model;
using System.Windows.Data;

namespace DAQSystem.Application.Themes.Converters
{
    [ValueConversionAttribute(typeof(PlotTypes), typeof(bool))]
    internal class PlotTypeToBooleanConverter : BaseConverter, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value?.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value?.Equals(true) == true ? parameter : System.Windows.Data.Binding.DoNothing;
        }
    }
}
