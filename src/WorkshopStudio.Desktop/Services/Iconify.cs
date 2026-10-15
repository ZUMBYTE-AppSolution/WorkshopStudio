using System.Windows;
using System.Windows.Media;

namespace WorkshopStudio.Desktop.Services;

/// <summary>Optional offline Iconify geometry for standard action buttons.</summary>
public static class Iconify
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
        "Icon", typeof(Geometry), typeof(Iconify), new PropertyMetadata(null));

    public static Geometry? GetIcon(DependencyObject target) => (Geometry?)target.GetValue(IconProperty);

    public static void SetIcon(DependencyObject target, Geometry? value) => target.SetValue(IconProperty, value);
}
