using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace WorkshopStudio.Desktop.Services;

/// <summary>Colors native window chrome without changing system-wide Windows settings.</summary>
public static class WindowTheme
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(WindowTheme), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window) return;
        if ((bool)e.NewValue)
        {
            window.SourceInitialized += Window_SourceInitialized;
            window.Closed += Window_Closed;
            Apply(window);
        }
        else Detach(window);
    }

    private static void Window_SourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window) Apply(window);
    }

    private static void Window_Closed(object? sender, EventArgs e)
    {
        if (sender is Window window) Detach(window);
    }

    private static void Detach(Window window)
    {
        window.SourceInitialized -= Window_SourceInitialized;
        window.Closed -= Window_Closed;
    }

    public static void Apply(Window window)
    {
        // These DWM attributes are documented starting at Windows 11 build 22000.
        // Older Windows keeps native caption behavior; the WPF content is still themed.
        if (!GetIsEnabled(window) || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        int dark = ThemeService.IsLight ? 0 : 1;
        SetAttribute(handle, 20, dark); // DWMWA_USE_IMMERSIVE_DARK_MODE
        if (window.WindowStyle == WindowStyle.None) return;
        SetColor(handle, 34, "BorderBrush"); // DWMWA_BORDER_COLOR
        SetColor(handle, 35, "SidebarBrush"); // DWMWA_CAPTION_COLOR
        SetColor(handle, 36, "TextBrush"); // DWMWA_TEXT_COLOR
    }

    private static void SetColor(IntPtr handle, int attribute, string key)
    {
        if (Application.Current.TryFindResource(key) is not SolidColorBrush brush) return;
        Color color = brush.Color;
        SetAttribute(handle, attribute, color.R | (color.G << 8) | (color.B << 16));
    }

    private static void SetAttribute(IntPtr handle, int attribute, int value)
    {
        // DWM can decline an attribute on a particular system. This is not an app error.
        _ = DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
