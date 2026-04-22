using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace OpenNDOF.App.Converters;

/// <summary>Converts a boolean IsConnected value to an InfoBar severity level.</summary>
[ValueConversion(typeof(bool), typeof(InfoBarSeverity))]
public sealed class ConnectedSeverityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? InfoBarSeverity.Success : InfoBarSeverity.Warning;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
