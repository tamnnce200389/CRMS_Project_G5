
using CRMS.Helpers;
using System.Windows;
using System.Windows.Input;

namespace CRMS.ViewModels
{
    public partial class HomeViewModel : BaseViewModel
    {
        public string[] Seasons { get; } = ["Mùa xuân", "Mùa hè", "Mùa thu", "Mùa đông"];

        private string _selectedSeason = SeasonForMonth(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTime.UtcNow, "SE Asia Standard Time").Month);

        public string SelectedSeason
        {
            get => _selectedSeason;
            set
            {
                if (!Seasons.Contains(value) || value == _selectedSeason) return;
                _selectedSeason = value;
                SelectedDestination = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Destinations));
                OnPropertyChanged(nameof(SeasonDescription));
            }
        }

        public string SeasonDescription => SelectedSeason switch
        {
            "Mùa xuân" => "Tháng 3–5 · Những chuyến đi giữa thiên nhiên xanh và nắng nhẹ.",
            "Mùa hè" => "Tháng 6–8 · Tìm một bờ biển đẹp, dành thời gian cho gia đình.",
            "Mùa thu" => "Tháng 9–11 · Đường đèo, sắc lúa và những ngày trời dịu mát.",
            _ => "Tháng 12–2 · Đón nắng phương Nam hoặc tận hưởng không khí cao nguyên."
        };

        private SeasonalDestination? _selectedDestination;
        public SeasonalDestination? SelectedDestination
        {
            get => _selectedDestination;
            set
            {
                _selectedDestination = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDestination));
            }
        }
        public bool HasDestination => SelectedDestination is not null;

        public IReadOnlyList<SeasonalDestination> Destinations => SelectedSeason switch
        {
            "Mùa xuân" => [NinhBinh, DaLat, CanTho],
            "Mùa hè" => [DaNang, NinhBinh, CanTho],
            "Mùa thu" => [HaGiang, NinhBinh, DaLat],
            _ => [PhuQuoc, DaLat, CanTho]
        };

        public static string SeasonForMonth(int month) => month switch
        {
            >= 3 and <= 5 => "Mùa xuân",
            >= 6 and <= 8 => "Mùa hè",
            >= 9 and <= 11 => "Mùa thu",
            _ => "Mùa đông"
        };

        private static readonly SeasonalDestination NinhBinh = new("Ninh Bình", "MIỀN BẮC",
            "Chậm lại giữa non nước Tràng An", "2–3 ngày",
            "Dành một buổi đi thuyền Tràng An, ghé Hang Múa lúc sáng sớm. Kiểm tra thời tiết trước chuyến đi.", new Rect(0, 0, 1d / 3, .5));
        private static readonly SeasonalDestination DaNang = new("Đà Nẵng", "MIỀN TRUNG",
            "Đường ven biển, nắng và những điểm dừng", "3–4 ngày",
            "Kết hợp biển Mỹ Khê và Hội An. Ưu tiên sáng sớm hoặc chiều muộn cho các hoạt động ngoài trời.", new Rect(1d / 3, 0, 1d / 3, .5));
        private static readonly SeasonalDestination HaGiang = new("Hà Giang", "MIỀN BẮC",
            "Một hành trình giữa những tầng núi", "3–4 ngày",
            "Lên lịch Đồng Văn và Mã Pí Lèng vào ban ngày. Đường đèo phù hợp với người lái có kinh nghiệm hoặc thuê xe có tài xế.", new Rect(2d / 3, 0, 1d / 3, .5));
        private static readonly SeasonalDestination DaLat = new("Đà Lạt", "CAO NGUYÊN",
            "Hẹn nhau giữa đồi thông và sương sớm", "2–3 ngày",
            "Ghé hồ Tuyền Lâm, dạo đồi thông và thưởng thức cà phê. Mang áo ấm và kiểm tra mưa trước khi đi đường đèo.", new Rect(0, .5, 1d / 3, .5));
        private static readonly SeasonalDestination PhuQuoc = new("Phú Quốc", "BIỂN ĐẢO",
            "Đổi nhịp sống bằng một chút nắng biển", "3–4 ngày",
            "Mùa khô thường từ tháng 11 đến tháng 4. Nhận xe trên đảo để khám phá bãi biển và làng chài theo lịch trình riêng.", new Rect(1d / 3, .5, 1d / 3, .5));
        private static readonly SeasonalDestination CanTho = new("Cần Thơ", "MIỀN TÂY",
            "Buổi sáng trên sông, buổi chiều miệt vườn", "1–2 ngày",
            "Khởi hành sớm để ghé chợ nổi Cái Răng, dành buổi chiều khám phá miệt vườn. Theo dõi mưa khi lên lịch hoạt động ngoài trời.", new Rect(2d / 3, .5, 1d / 3, .5));

        private string _selectedMode = "SelfDrive";

        public string SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (_selectedMode == value)
                    return;

                _selectedMode = value;
                OnPropertyChanged();

                OnPropertyChanged(nameof(IsSelfDrive));
                OnPropertyChanged(nameof(IsWithDriver));
                OnPropertyChanged(nameof(IsLongTerm));
            }
        }

        public bool IsSelfDrive =>
            SelectedMode == "SelfDrive";

        public bool IsWithDriver =>
            SelectedMode == "WithDriver";

        public bool IsLongTerm =>
            SelectedMode == "LongTerm";

        public ICommand SelectModeCommand { get; }

        public HomeViewModel()
        {
            InitializeSearchState();
            SelectModeCommand = new RelayCommand(parameter =>
            {
                string? mode = parameter?.ToString();

                if (mode == "SelfDrive" ||
                    mode == "WithDriver" ||
                    mode == "LongTerm")
                {
                    SelectedMode = mode;
                }
            });
        }
    }
}
