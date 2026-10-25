
using System;
using System.Windows;
using System.Windows.Controls;
using CRMS.Services;
using CRMS.ViewModels;

namespace CRMS.Views.Guest
{
    public partial class CarSearchPage : Page
    {
        private readonly CarSearchViewModel _viewModel = new();

        private bool _isLoaded = false;

        // Mac dinh la xe tu lai
        public CarSearchPage() : this("SelfDrive")
        {
        }

        // Nhan loai dich vu tu HomePage
        public CarSearchPage(string rentalMode)
        {
            InitializeComponent();

            if (rentalMode != "SelfDrive" &&
                rentalMode != "WithDriver" &&
                rentalMode != "LongTerm")
            {
                rentalMode = "SelfDrive";
            }

            _viewModel.RentalMode = rentalMode;

            DataContext = _viewModel;

            Loaded += CarSearchPage_Loaded;
        }

        // =====================================
        // LOAD DANH SACH XE
        // =====================================
        private async void CarSearchPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_isLoaded)
                return;

            _isLoaded = true;

            try
            {
                await _viewModel.LoadCategoriesAsync();

                await _viewModel.SearchAsync();
            }
            catch (Exception ex)
            {
                _isLoaded = false;

                MessageBox.Show(
                    ex.Message,
                    "Lỗi tải danh sách xe",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =====================================
        // MO / DONG BO LOC GIA
        // =====================================
        private void ToggleLongTermFilter_Click(
            object sender,
            RoutedEventArgs e)
        {
            LongTermPriceFilter.Visibility =
                LongTermPriceFilter.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }

        // =====================================
        // MO CHI TIET XE DAI HAN
        // =====================================
        private void LongTermChoose_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.DataContext is not CarSearchItem car)
            {
                return;
            }

            var detailWindow = new LongTermDetailWindow(car);

            Window? mainWindow = Window.GetWindow(this);

            if (mainWindow != null)
            {
                detailWindow.Owner = mainWindow;
            }

            detailWindow.ShowDialog();
        }

        // =====================================
        // QUAY LAI TRANG CHU
        // =====================================
        private void BackToHome_Click(
            object sender,
            RoutedEventArgs e)
        {
            Window? mainWindow = Window.GetWindow(this);

            if (mainWindow?.DataContext is MainViewModel vm)
            {
                vm.NavigateCommand.Execute("Home");
            }
        }
    }
}
