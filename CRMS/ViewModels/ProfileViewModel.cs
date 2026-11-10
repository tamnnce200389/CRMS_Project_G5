using System.Windows.Input;
using CRMS.Dtos;
using CRMS.Helpers;
using CRMS.Models;
using CRMS.Services;
namespace CRMS.ViewModels;

public sealed class ProfileViewModel : SessionViewModel
{
    private readonly IUserAccountService _service;
    private UserAccountDto? _profile;
    private string _name = "", _phone = "";
    public long? UserId => _profile?.UserId;
    public string Email => _profile?.Email ?? "";
    public string RoleLabel => _profile?.UserRole switch { "Admin" => "Quản trị viên", "Staff" => "Nhân viên", "Customer" => "Khách hàng", _ => "" };
    public string StatusLabel => _profile is null ? "" : _profile.IsActive ? "Đang hoạt động" : "Ngừng hoạt động";
    public string FullName { get => _name; set { _name = value; OnPropertyChanged(); } }
    public string Phone { get => _phone; set { _phone = value; OnPropertyChanged(); } }
    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ProfileViewModel() : this(new UserAccountService(), CurrentUserService.Instance) { }
    public ProfileViewModel(IUserAccountService service, IUserSession session) : base(session)
    {
        _service = service; LoadCommand = new AsyncRelayCommand(_ => LoadAsync(), _ => !IsBusy);
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(), _ => !IsBusy && _profile != null && Session.UserId == _profile.UserId);
    }
    private void Publish(UserAccountDto? profile)
    {
        _profile = profile; FullName = profile?.FullName ?? ""; Phone = profile?.Phone ?? "";
        OnPropertyChanged(nameof(UserId)); OnPropertyChanged(nameof(Email)); OnPropertyChanged(nameof(RoleLabel)); OnPropertyChanged(nameof(StatusLabel));
    }
    protected override void ClearState() => Publish(null);
    public Task LoadAsync() => RunAsync(async (stamp, token) => { Publish(null); var profile = await _service.GetProfileAsync(token); Ensure(stamp, token); Publish(profile); });
    public Task SaveAsync()
    {
        var original = _profile; if (original is null) return Task.CompletedTask;
        var request = new ProfileUpdateRequest(FullName, Phone, original.RowVersion);
        return RunAsync(async (stamp, token) =>
        {
            if (original.UserId != stamp.UserId) throw new InvalidOperationException("Vui lòng tải lại hồ sơ.");
            var profile = await _service.SaveProfileAsync(request, token); Ensure(stamp, token); Publish(profile);
            if (ReferenceEquals(Session, CurrentUserService.Instance)) CurrentUserService.Instance.SetUser(new User { UserId = profile.UserId, FullName = profile.FullName, UserRole = profile.UserRole });
            Message = "Đã lưu thông tin hồ sơ.";
        });
    }
}
