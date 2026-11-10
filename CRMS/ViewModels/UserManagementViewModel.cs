using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using CRMS.Dtos;
using CRMS.Helpers;
using CRMS.Services;
namespace CRMS.ViewModels;

public sealed class UserManagementViewModel : SessionViewModel
{
    private readonly IUserAccountService _service;
    private readonly IConfirmationService _dialogs;
    private readonly ObservableCollection<UserAccountDto> _users = new();
    public ICollectionView Users { get; }
    public string[] Roles { get; } = ["Customer", "Staff", "Admin"];
    private string _search = "", _role = "Customer";
    private UserAccountDto? _selected;
    public string SearchText { get => _search; set { _search = value; OnPropertyChanged(); Users.Refresh(); } }
    public UserAccountDto? SelectedUser { get => _selected; set { _selected = value; if (value != null) SelectedRole = value.UserRole; OnPropertyChanged(); OnPropertyChanged(nameof(SelectionMessage)); OnPropertyChanged(nameof(CanModify)); CommandManager.InvalidateRequerySuggested(); } }
    public string SelectedRole { get => _role; set { _role = value; OnPropertyChanged(); } }
    public bool CanModify => !IsBusy && SelectedUser != null && SelectedUser.UserId != Session.UserId && Session.Role == "Admin";
    protected override void OnBusyChanged() => OnPropertyChanged(nameof(CanModify));
    public string SelectionMessage => SelectedUser is { } u ? $"Đã chọn: #{u.UserId} — {u.FullName} ({u.Email})" : $"Tổng số tài khoản: {_users.Count}";
    public ICommand LoadCommand { get; }
    public ICommand ChangeRoleCommand { get; }
    public ICommand ToggleActiveCommand { get; }
    public UserManagementViewModel() : this(new UserAccountService(), CurrentUserService.Instance, ApplicationServices.Confirmation) { }
    public UserManagementViewModel(IUserAccountService service, IUserSession session, IConfirmationService dialogs) : base(session)
    {
        _service = service; _dialogs = dialogs; Users = CollectionViewSource.GetDefaultView(_users);
        Users.Filter = item => item is UserAccountDto u && ($"{u.UserId} {u.FullName} {u.Email} {u.Phone}").Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase);
        LoadCommand = new AsyncRelayCommand(_ => LoadAsync(), _ => !IsBusy);
        ChangeRoleCommand = new AsyncRelayCommand(_ => UpdateAsync(true), _ => CanModify);
        ToggleActiveCommand = new AsyncRelayCommand(_ => UpdateAsync(false), _ => CanModify);
    }
    protected override void ClearState() { SelectedUser = null; _users.Clear(); OnPropertyChanged(nameof(SelectionMessage)); OnPropertyChanged(nameof(CanModify)); }
    private async Task RefreshAsync(SessionStamp stamp, CancellationToken token)
    {
        var rows = await _service.GetUsersAsync(token); Ensure(stamp, token); ClearState(); foreach (var row in rows) _users.Add(row); OnPropertyChanged(nameof(SelectionMessage));
    }
    public Task LoadAsync() => RunAsync(async (stamp, token) => { ClearState(); await RefreshAsync(stamp, token); });
    private Task UpdateAsync(bool role)
    {
        if (!CanModify || SelectedUser is not { } target) return Task.CompletedTask;
        var selectedRole = SelectedRole; var stamp = SessionStamp.Capture(Session);
        if (role && selectedRole == target.UserRole) { Message = "Tài khoản đã có vai trò này."; return Task.CompletedTask; }
        if (!_dialogs.Confirm(role ? $"Đổi vai trò của {target.FullName} thành {selectedRole}?" : $"{(target.IsActive ? "Khóa" : "Mở khóa")} tài khoản {target.FullName}?", "Xác nhận")) return Task.CompletedTask;
        return RunAsync(async (_, token) => { Ensure(stamp, token); if (role) await _service.ChangeRoleAsync(target, selectedRole, token); else await _service.SetActiveAsync(target, !target.IsActive, token); Ensure(stamp, token); await RefreshAsync(stamp, token); Message = "Đã cập nhật tài khoản."; });
    }
}
