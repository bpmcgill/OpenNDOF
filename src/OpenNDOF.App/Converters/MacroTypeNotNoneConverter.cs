using OpenNDOF.Core.Profiles;
using System.Globalization;
using System.Windows.Data;

namespace OpenNDOF.App.Converters;

/// <summary>Returns <c>true</c> (enabled) when the <see cref="MacroType"/> is not <see cref="MacroType.None"/>.</summary>
[ValueConversion(typeof(MacroType), typeof(bool))]
public sealed class MacroTypeNotNoneConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is MacroType t && t != MacroType.None;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
