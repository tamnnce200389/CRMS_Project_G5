
using CRMS.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace CRMS.Views
{
    public partial class DriverPage : Page
    {
        private bool _isLoaded = false;

        public DriverPage()
        {
            InitializeComponent();
            Loaded += DriverPage_Loaded;
        }

        private async void DriverPage_Loaded(
            object sender, RoutedEventArgs e)
        {
            if (_isLoaded)
                return;

            if (DataContext is DriverViewModel vm)
            {
                try
                {
                    await vm.LoadDriversAsync();
                    _isLoaded = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        ex.Message,
                        "Lỗi tải danh sách tài xế",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
    }
}

