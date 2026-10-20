
using System;
using System.Windows;
using System.Windows.Controls;
using CRMS.ViewModels;
namespace CRMS.Views.Guest
{
    public partial class CarSearchPage : Page
    {
        // ViewModel xu ly tim kiem xe
        private readonly CarSearchViewModel _viewModel = new();

        // Kiem tra trang da tai du lieu chua
        private bool _isLoaded = false;

        // =====================================
        // 1. CONSTRUCTOR MAC DINH
        // =====================================
        public CarSearchPage() : this("SelfDrive")
        {
        }

        // =====================================
        // 2. CONSTRUCTOR NHAN LOAI DICH VU
        // =====================================
        public CarSearchPage(string rentalMode)
        {
            InitializeComponent();

            // Kiem tra loai dich vu hop le
            if (rentalMode != "SelfDrive" &&
                rentalMode != "WithDriver" &&
                rentalMode != "LongTerm")
            {
                rentalMode = "SelfDrive";
            }

            // Luu loai dich vu vao ViewModel
            _viewModel.RentalMode = rentalMode;

            // Ket noi View voi ViewModel
            DataContext = _viewModel;

            // Tai du lieu khi trang duoc mo
            Loaded += CarSearchPage_Loaded;
        }

        // =====================================
        // 3. LOAD DU LIEU TU SQL SERVER
        // =====================================
        private async void CarSearchPage_Loaded(
            object sender, RoutedEventArgs e)
        {
            // Chi tai du lieu lan dau
            if (_isLoaded)
                return;

            _isLoaded = true;

            try
            {
                // Lay danh sach loai xe
                await _viewModel.LoadCategoriesAsync();

                // Tim kiem va lay danh sach xe
                await _viewModel.SearchAsync();
            }
            catch (Exception ex)
            {
                // Cho phep thu tai lai neu bi loi
                _isLoaded = false;

                MessageBox.Show(
                    ex.Message,
                    "Lỗi tải danh sách xe",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BackToHome_Click(
    object sender, RoutedEventArgs e)
        {
            Window? mainWindow = Window.GetWindow(this);

            if (mainWindow?.DataContext is MainViewModel vm)
            {
                vm.NavigateCommand.Execute("Home");
            }
        }
    }
}
