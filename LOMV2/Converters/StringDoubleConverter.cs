using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace LOM.Converters;

[ValueConversion(typeof(String), typeof(Double))]
public class StringBoolDataConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double doValue = (double)value;
        return doValue.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string strValue = value as string;
        if (double.TryParse(strValue, out double result))
        {
            return result;
        }
        return DependencyProperty.UnsetValue;
    }
}
