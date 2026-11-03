
using CRMS.Models;
using CRMS.ViewModels;
using System.Globalization;

namespace CRMS.Services
{
    public class CurrentUserService : BaseViewModel
    {
        // Dung chung mot phien nguoi dung trong ung dung
        public static CurrentUserService Instance { get; } = new();

        private CurrentUserService()
        {
        }

        public long? UserId { get; private set; }

        public string FullName { get; private set; } = "Guest";

        public string AvatarInitials
        {
            get
            {
                var words = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return words.Length == 0 ? "KH" : string.Concat(words.TakeLast(2)
                    .Select(word => StringInfo.GetNextTextElement(word))).ToUpperInvariant();
            }
        }

        public string Role { get; private set; } = "Guest";

        public bool IsLoggedIn => UserId.HasValue;
        public bool IsGuest => !IsLoggedIn;

        public bool IsAdmin => Role == "Admin";

        public bool IsStaff => Role == "Staff";

        public bool IsCustomer => Role == "Customer";

        // Chi goi sau khi AuthService xac thuc thanh cong
        public void SetUser(User user)
        {
            UserId = user.UserId;
            FullName = user.FullName;
            Role = user.UserRole;

            NotifyChanges();
        }

        // Dang xuat ve Guest
        public void Logout()
        {
            UserId = null;
            FullName = "Guest";
            Role = "Guest";

            NotifyChanges();
        }

        private void NotifyChanges()
        {
            OnPropertyChanged(nameof(UserId));
            OnPropertyChanged(nameof(FullName));
            OnPropertyChanged(nameof(AvatarInitials));
            OnPropertyChanged(nameof(Role));
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(IsStaff));
            OnPropertyChanged(nameof(IsCustomer));
            OnPropertyChanged(nameof(IsGuest));
        }
    }
}
