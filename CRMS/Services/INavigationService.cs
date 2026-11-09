namespace CRMS.Services;

public interface INavigationService
{
    object? Current { get; set; }
    string CurrentRoute { get; }
    event Action? Changed;
    void Navigate(string route, object? argument = null);
    void Reset();
}
public sealed record SearchNavigationRequest(string Mode, DateTime? Start, DateTime? End, CarSearchContext Context);
