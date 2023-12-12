using Gpfm.Core;
using Gpfm.Desktop;
using System.Globalization;

namespace Gpfm.Desktop.Converters;

public class StepNumberConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not JobStep step)
            return null;

        var indexOf = MainPage.Steps.IndexOf(step);

        if (indexOf < 0)
            throw new InvalidOperationException();

        return indexOf + 1;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
