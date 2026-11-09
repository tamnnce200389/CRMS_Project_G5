using System;
using System.Collections.Generic;
using System.Windows;
using CRMS.ViewModels;

namespace CRMS.Views.Guest
{
    public partial class LocationSearchWindow : Window
    {
        public LocationSearchWindow(int? currentCityId, List<string>? currentNeeds, List<string>? currentTrends, List<string>? currentBudgets, string? currentCity)
        {
            InitializeComponent();

            // Khởi tạo ViewModel đúng với cấu trúc dữ liệu của bạn
            var viewModel = new LocationFilterViewModel(currentCityId, currentNeeds, currentTrends, currentBudgets, currentCityName: currentCity);

            // Khi ViewModel báo hoàn tất (Applied), đóng cửa sổ và trả về true
            viewModel.Applied += () => DialogResult = true;

            DataContext = viewModel;

            // Load dữ liệu khi cửa sổ mở lên
            Loaded += (_, _) => viewModel.LoadCommand.Execute(null);
        }
    }
}