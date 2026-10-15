using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using WorkshopStudio.Desktop.Services;
using WorkshopStudio.Desktop.ViewModels;
namespace WorkshopStudio.Desktop;
public partial class MainWindow:Window
{
    private readonly MainViewModel _vm;
    private bool _canClose;
    public MainWindow(MainViewModel vm){InitializeComponent();_vm=vm;DataContext=vm;if(vm.Settings.StartMaximized)WindowState=WindowState.Maximized;}
    private void Window_SourceInitialized(object? sender,EventArgs e)
    {
        if(PresentationSource.FromVisual(this) is HwndSource source)source.AddHook(WindowMessageHook);
    }
    private static IntPtr WindowMessageHook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        const int wmGetMinMaxInfo=0x24;
        if(message!=wmGetMinMaxInfo)return IntPtr.Zero;
        var monitor=MonitorFromWindow(hwnd,2);
        var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
        if(monitor==IntPtr.Zero || !GetMonitorInfo(monitor,ref info))return IntPtr.Zero;
        var bounds=Marshal.PtrToStructure<MinMaxInfo>(lParam);
        bounds.MaxPosition=new NativePoint(info.WorkArea.Left-info.MonitorArea.Left,info.WorkArea.Top-info.MonitorArea.Top);
        bounds.MaxSize=new NativePoint(info.WorkArea.Right-info.WorkArea.Left,info.WorkArea.Bottom-info.WorkArea.Top);
        bounds.MaxTrackSize=bounds.MaxSize;
        Marshal.StructureToPtr(bounds,lParam,false);
        handled=true;
        return IntPtr.Zero;
    }
    internal bool CaptionButtonsInsideWorkArea()
    {
        var handle=new WindowInteropHelper(this).Handle;
        var monitor=MonitorFromWindow(handle,2);
        var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
        if(monitor==IntPtr.Zero || !GetMonitorInfo(monitor,ref info))return false;
        return new[]{MinimizeButton,MaximizeButton,CloseButton}.All(button=>ButtonInsideWorkArea(button,info.WorkArea));
    }
    private static bool ButtonInsideWorkArea(Button button,NativeRect workArea)
    {
        if(!button.IsVisible || button.ActualWidth<1 || button.ActualHeight<1)return false;
        var topLeft=button.PointToScreen(new Point(0,0));
        var bottomRight=button.PointToScreen(new Point(button.ActualWidth,button.ActualHeight));
        return topLeft.X>=workArea.Left-1 && topLeft.Y>=workArea.Top-1 &&
            bottomRight.X<=workArea.Right+1 && bottomRight.Y<=workArea.Bottom+1;
    }
    [StructLayout(LayoutKind.Sequential)]private readonly record struct NativePoint(int X,int Y);
    [StructLayout(LayoutKind.Sequential)]private struct MinMaxInfo
    {
        public NativePoint Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;
    }
    [StructLayout(LayoutKind.Sequential)]private readonly record struct NativeRect(int Left,int Top,int Right,int Bottom);
    [StructLayout(LayoutKind.Sequential)]private struct MonitorInfo
    {
        public int Size;
        public NativeRect MonitorArea,WorkArea;
        public int Flags;
    }
    [DllImport("user32.dll")]private static extern IntPtr MonitorFromWindow(IntPtr hwnd,int flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)]
    [return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    private async void Window_Loaded(object sender,RoutedEventArgs e){try{await _vm.InitializeAsync();}catch(Exception ex){DesktopDialogs.Error(ex);}}
    private void Minimize_Click(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
    private void Maximize_Click(object sender,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
    private void Close_Click(object sender,RoutedEventArgs e)=>Close();
    private async void Window_Closing(object? sender,CancelEventArgs e)
    {
        if(_canClose)return;
        e.Cancel=true;
        if(_vm.IsBusy){DesktopDialogs.Info("Zuerst den laufenden Vorgang stoppen oder abschließen. So bleibt der Uploadstatus nachvollziehbar.");return;}
        try{await _vm.SaveOnExitAsync();_vm.Dispose();_canClose=true;Close();}
        catch(Exception ex){DesktopDialogs.Error(ex);}
    }
}
