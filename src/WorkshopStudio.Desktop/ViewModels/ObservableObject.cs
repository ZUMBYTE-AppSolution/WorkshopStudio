using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace WorkshopStudio.Desktop.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Raise([CallerMemberName] string? name=null) => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
    protected bool Set<T>(ref T field,T value,[CallerMemberName] string? name=null) { if(EqualityComparer<T>.Default.Equals(field,value))return false;field=value;Raise(name);return true; }
}
/// <summary>Tracks each action separately so Stop remains available during a publish command.</summary>
public sealed class StudioCommand : ICommand
{
    private readonly Func<object?,Task> _execute;
    private readonly Action<Exception> _error;
    private readonly HashSet<object> _running=[];
    private static readonly object NullParameter=new();
    public StudioCommand(Func<object?,Task> execute,Action<Exception> error) { _execute=execute; _error=error; }
    public bool CanExecute(object? parameter)=>!_running.Contains(parameter??NullParameter);
    public event EventHandler? CanExecuteChanged;
    public async void Execute(object? parameter)
    {
        var key=parameter??NullParameter;
        if(!_running.Add(key))return;
        CanExecuteChanged?.Invoke(this,EventArgs.Empty);
        try { await _execute(parameter); }
        catch(Exception e) { _error(e); }
        finally { _running.Remove(key); CanExecuteChanged?.Invoke(this,EventArgs.Empty); }
    }
}
