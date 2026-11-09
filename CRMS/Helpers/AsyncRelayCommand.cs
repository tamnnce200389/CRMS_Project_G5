using System.Windows.Input;
namespace CRMS.Helpers;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    public bool IsRunning { get; private set; }
    public static Action<Exception> ReportError { get; set; } = ex => System.Diagnostics.Trace.TraceError(ex.ToString());
    public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public bool CanExecute(object? parameter) => !IsRunning && (_canExecute?.Invoke(parameter) ?? true);
    public async Task ExecuteAsync(object? parameter = null)
    {
        if (!CanExecute(parameter)) return;
        IsRunning = true; CommandManager.InvalidateRequerySuggested();
        try { await _execute(parameter); }
        finally { IsRunning = false; CommandManager.InvalidateRequerySuggested(); }
    }
    public async void Execute(object? parameter) { try { await ExecuteAsync(parameter); } catch (Exception ex) { ReportError(ex); } }
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
}
