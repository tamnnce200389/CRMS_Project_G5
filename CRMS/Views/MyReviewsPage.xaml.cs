
using System;
using System.Windows;
using System.Windows.Controls;
using CRMS.ViewModels;

namespace CRMS.Views
{
    public partial class MyReviewsPage : Page
    {
        private readonly MyReviewsViewModel _viewModel;

        private bool _isReviewWindowOpen;

        public MyReviewsPage()
        {
            InitializeComponent();

            _viewModel = new MyReviewsViewModel();
            DataContext = _viewModel;

            Loaded += MyReviewsPage_Loaded;
        }

        // Tu dong tai danh sach khi vao trang
        private async void MyReviewsPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                await _viewModel.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Lỗi tải đánh giá",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // F22 - Mo cua so Viet danh gia
        private async void WriteReview_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isReviewWindowOpen)
                return;

            _isReviewWindowOpen = true;
            WriteReviewButton.IsEnabled = false;

            try
            {
                var reviewWindow = new WriteReviewWindow();

                // Gan cua so cha neu co
                Window? owner = Window.GetWindow(this);

                if (owner != null)
                {
                    reviewWindow.Owner = owner;
                }
                else
                {
                    reviewWindow.WindowStartupLocation =
                        WindowStartupLocation.CenterScreen;
                }

                bool? result = reviewWindow.ShowDialog();

                // Gui thanh cong thi lam moi danh sach
                if (result == true)
                {
                    await _viewModel.LoadAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Lỗi mở cửa sổ đánh giá",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isReviewWindowOpen = false;
                WriteReviewButton.IsEnabled = true;
            }
        }
    }
}
