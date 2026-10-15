using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WorkshopStudio.Core;

namespace WorkshopStudio.Desktop.Services;

public static class ThemeService
{
    public static bool IsLight { get; private set; }
    public static event EventHandler? ThemeChanged;

    public static void Apply(StudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var app = Application.Current;
        app.Dispatcher.VerifyAccess();
        bool light = string.Equals(settings.Theme, "Light", StringComparison.OrdinalIgnoreCase);
        IsLight = light;

        void SetBrush(string name, string value)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            brush.Freeze();
            app.Resources[name] = brush;
        }

        SetBrush("WindowBrush", light ? "#F4F5F8" : "#111416");
        SetBrush("SidebarBrush", light ? "#FFFFFF" : "#14181B");
        SetBrush("PanelBrush", light ? "#FFFFFF" : "#1A1F23");
        SetBrush("InputBrush", light ? "#F1F3F7" : "#121619");
        SetBrush("HoverBrush", light ? "#E9ECF2" : "#252C31");
        SetBrush("BorderBrush", light ? "#DCE1EA" : "#343B41");
        SetBrush("TextBrush", light ? "#192335" : "#F0F2F4");
        SetBrush("MutedBrush", light ? "#606D81" : "#AAB2BC");
        try { SetBrush("AccentBrush", settings.Accent); }
        catch (Exception ex) when (ex is FormatException or ArgumentException or NotSupportedException)
        {
            SetBrush("AccentBrush", "#FF781F");
        }
        void SetColor(string name, string value) => app.Resources[name] = (Color)ColorConverter.ConvertFromString(value);
        SetColor("PanelTopColor", light ? "#FFFFFF" : "#22282C");
        SetColor("PanelBottomColor", light ? "#F7F8FA" : "#181D20");
        SetColor("ShellTopColor", light ? "#FFFFFF" : "#1F2529");
        SetColor("ShellBottomColor", light ? "#F3F5F8" : "#14191C");
        var accent = ((SolidColorBrush)app.Resources["AccentBrush"]).Color;
        app.Resources["AccentTopColor"] = accent;
        app.Resources["AccentBottomColor"] = Color.FromRgb((byte)(accent.R * .93), (byte)(accent.G * .82), (byte)(accent.B * .70));
        SetBrush("AccentTextBrush", ContrastForeground(((SolidColorBrush)app.Resources["AccentBrush"]).Color));
        SetBrush("SuccessBrush", light ? "#087D60" : "#68D7AE");
        SetBrush("DangerBrush", light ? "#B52F43" : "#FF7C8D");

        // Fallback resources for framework-generated parts, selection and menu gutters.
        // These aliases are scoped to this app; Windows registry/system theme is untouched.
        void Alias(ResourceKey key, string brush) => app.Resources[key] = app.Resources[brush];
        Alias(SystemColors.WindowBrushKey, "InputBrush");
        Alias(SystemColors.WindowTextBrushKey, "TextBrush");
        Alias(SystemColors.ControlBrushKey, "PanelBrush");
        Alias(SystemColors.ControlTextBrushKey, "TextBrush");
        Alias(SystemColors.ControlLightBrushKey, "HoverBrush");
        Alias(SystemColors.ControlLightLightBrushKey, "PanelBrush");
        Alias(SystemColors.ControlDarkBrushKey, "BorderBrush");
        Alias(SystemColors.ControlDarkDarkBrushKey, "BorderBrush");
        Alias(SystemColors.HighlightBrushKey, "HoverBrush");
        Alias(SystemColors.HighlightTextBrushKey, "TextBrush");
        Alias(SystemColors.InactiveSelectionHighlightBrushKey, "HoverBrush");
        Alias(SystemColors.InactiveSelectionHighlightTextBrushKey, "TextBrush");
        Alias(SystemColors.GrayTextBrushKey, "MutedBrush");
        Alias(SystemColors.HotTrackBrushKey, "AccentBrush");
        Alias(SystemColors.InfoBrushKey, "PanelBrush");
        Alias(SystemColors.InfoTextBrushKey, "TextBrush");
        Alias(SystemColors.MenuBrushKey, "PanelBrush");
        Alias(SystemColors.MenuTextBrushKey, "TextBrush");
        Alias(SystemColors.MenuBarBrushKey, "PanelBrush");
        Alias(SystemColors.MenuHighlightBrushKey, "HoverBrush");
        Alias(SystemColors.ScrollBarBrushKey, "InputBrush");
        Alias(SystemColors.ActiveBorderBrushKey, "BorderBrush");
        Alias(SystemColors.InactiveBorderBrushKey, "BorderBrush");

        foreach (Window window in app.Windows) WindowTheme.Apply(window);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static string ContrastForeground(Color color)
    {
        static double Linear(byte component)
        {
            double value = component / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        double luminance = 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        return luminance > 0.179 ? "#101217" : "#FFFFFF";
    }
}
public sealed class PageVisibilityConverter:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>Equals(value,parameter)?Visibility.Visible:Visibility.Collapsed;
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
public sealed class BoolVisibilityConverter:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>((value is true)^(parameter?.ToString()=="Invert"))?Visibility.Visible:Visibility.Collapsed;
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
public sealed class BoolInverseConverter:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is not true;
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>value is not true;
}

public sealed class PageActiveConverter:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>Equals(value,parameter);
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
public sealed class CachedImageConverter:IValueConverter
{
    public object? Convert(object value,Type targetType,object parameter,CultureInfo culture)
    {
        try{if(value is not string p||!File.Exists(p))return null;var image=new System.Windows.Media.Imaging.BitmapImage();image.BeginInit();image.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;image.UriSource=new Uri(Path.GetFullPath(p));image.EndInit();image.Freeze();return image;}catch{return null;}
    }
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
