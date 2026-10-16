using System.Runtime.InteropServices;
using System.Windows;

namespace WorkshopStudio.Desktop.Services;
internal static class DesktopClipboard
{
    private static async Task<T> RetryAsync<T>(Func<T> action)
    {
        for(var attempt=0;;attempt++) {
            try{return action();}
            catch(COMException ex) when(ex.HResult==unchecked((int)0x800401D0)&&attempt<7){await Task.Delay(75);}
        }
    }
    public static Task SetTextAsync(string text)=>RetryAsync(()=>{
        Clipboard.SetText(text);
        if(Clipboard.GetText()!=text)throw new COMException("Die Zwischenablage hat den Text noch nicht übernommen.",unchecked((int)0x800401D0));
        return true;
    });
}
