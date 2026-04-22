using System.Globalization;
using System.Windows.Data;

namespace OpenNDOF.App.Converters;

/// <summary>Inverts a boolean value.</summary>
[ValueConversion(typeof(bool), typeof(bool))]
public sealed class BoolNegationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}
