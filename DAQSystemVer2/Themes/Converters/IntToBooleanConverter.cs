using DAQSystem.DataAcquisition;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DAQSystem.Application.Themes.Converters
{
    [ValueConversionAttribute(typeof(int), typeof(bool))]
    class IntToBooleanConverter : BaseConverter, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value is not int intValue)
                return null;

            bool inverse = (parameter as string) == "inverse";

            return inverse ? (intValue >= 1 ? false : true) : (intValue >= 1 ? true : false);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value is not bool boolValue)
                return null;

            bool inverse = (parameter as string) == "inverse";

            return inverse ? (boolValue ? 0 : 1) : (boolValue ? 1 : 0);
        }
    }
}
