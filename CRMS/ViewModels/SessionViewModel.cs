using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CRMS.Services;
namespace CRMS.ViewModels;

public abstract class SessionViewModel : BaseViewModel
{
    protected readonly IUserSession Session;
    private CancellationTokenSource? _request;
    private int _generation;
    private bool _busy;
    public bool IsBusy { get => _busy; private set { _busy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanEdit)); OnBusyChanged(); CommandManager.InvalidateRequerySuggested(); } }
    protected virtual void OnBusyChanged() { }
    public bool CanEdit => !IsBusy;
    private string _message = "";
    public string Message { get => _message; protected set { _message = value; OnPropertyChanged(); } }
    protected SessionViewModel(IUserSession session)
    {
        Session = session;
        if (session is INotifyPropertyChanged changes) PropertyChangedEventManager.AddHandler(changes, SessionChanged, nameof(IUserSession.SessionVersion));
    }
    private void SessionChanged(object? sender, PropertyChangedEventArgs e) => Cancel();
    public void Cancel() { ++_generation; _request?.Cancel(); _request = null; IsBusy = false; Message = ""; ClearState(); }
    protected abstract void ClearState();
    protected async Task RunAsync(Func<SessionStamp, CancellationToken, Task> work)
    {
        if (IsBusy) return;
        var version = Session.SessionVersion; var generation = ++_generation;
        using var request = new CancellationTokenSource(); _request = request; IsBusy = true; Message = "";
        try { var stamp = SessionStamp.Capture(Session); await work(stamp, request.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (generation == _generation && version == Session.SessionVersion) Message = ex.Message; }
        finally { if (generation == _generation) { _request = null; IsBusy = false; } }
    }
    protected void Ensure(SessionStamp stamp, CancellationToken token) { token.ThrowIfCancellationRequested(); stamp.Ensure(Session); }
}
