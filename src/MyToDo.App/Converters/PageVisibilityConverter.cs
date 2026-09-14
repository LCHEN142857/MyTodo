using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MyToDo.App.ViewModels;

namespace MyToDo.App.Converters;

public sealed class PageVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var page = value is AppPage p ? p.ToString() : value?.ToString();
        var expected = parameter?.ToString();
        return string.Equals(page, expected, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
