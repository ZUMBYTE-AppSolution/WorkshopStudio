using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using WorkshopStudio.Desktop.Views;

namespace WorkshopStudio.Desktop.Services;

public static class DesktopDialogs
{
    public static bool HasValidationErrors(System.Windows.DependencyObject root)
    {
        if(System.Windows.Controls.Validation.GetHasError(root))return true;
        for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++)
            if(HasValidationErrors(System.Windows.Media.VisualTreeHelper.GetChild(root,i)))return true;
        return false;
    }
    public static Window? Owner => Application.Current.Windows.Cast<Window>().LastOrDefault(w => w.IsActive && w.IsVisible)
        ?? (Application.Current.MainWindow?.IsVisible == true ? Application.Current.MainWindow : null);

    public static bool Confirm(string message, string title = "Workshop Studio") => ShowMessage(message, title, true, MessageBoxImage.Question);
    public static void Info(string message, string title = "Workshop Studio") => ShowMessage(message, title, false, MessageBoxImage.Information);
    public static void Error(Exception e, string title = "Workshop Studio · Fehler") => ShowMessage(e.Message, title, false, MessageBoxImage.Error);

    private static bool ShowMessage(string message, string title, bool confirmation, MessageBoxImage kind)
    {
        var owner = Owner;
        try
        {
            var dialog = new MessageWindow(title, message, confirmation, kind);
            if (owner is not null) dialog.Owner = owner;
            else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return dialog.ShowDialog() == true;
        }
        catch (Exception dialogError)
        {
            // Last-resort diagnostic: an invalid/missing XAML resource must not hide errors.
            Debug.WriteLine("Themed message dialog failed: " + dialogError.GetType().Name);
            var buttons = confirmation ? MessageBoxButton.YesNo : MessageBoxButton.OK;
            var defaultResult = confirmation ? MessageBoxResult.No : MessageBoxResult.OK;
            var result = owner is not null
                ? MessageBox.Show(owner, message, title, buttons, kind, defaultResult)
                : MessageBox.Show(message, title, buttons, kind, defaultResult);
            return result is MessageBoxResult.Yes or MessageBoxResult.OK;
        }
    }
    public static string? Folder(string title,string? initial=null)
    {
        var d=new OpenFolderDialog{Title=title,Multiselect=false}; if(Directory.Exists(initial))d.InitialDirectory=initial!;
        return d.ShowDialog(Owner)==true?d.FolderName:null;
    }
    public static string? OpenFile(string filter="Alle Dateien|*.*") { var d=new OpenFileDialog{Filter=filter};return d.ShowDialog(Owner)==true?d.FileName:null; }
    public static string? SaveFile(string name,string filter) { var d=new SaveFileDialog{FileName=name,Filter=filter};return d.ShowDialog(Owner)==true?d.FileName:null; }
    public static void OpenUrl(string url)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https")throw new InvalidDataException("Nur HTTPS-Adressen können geöffnet werden.");
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});
    }
    public static void OpenFolder(string path)
    {
        if(!Directory.Exists(path))throw new DirectoryNotFoundException(path);
        var start=new ProcessStartInfo("explorer.exe"){UseShellExecute=false};start.ArgumentList.Add(Path.GetFullPath(path));Process.Start(start);
    }
}
