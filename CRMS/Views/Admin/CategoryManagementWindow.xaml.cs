using System;
using System.Windows;
using CRMS.ViewModels;

namespace CRMS.Views.Admin
{
    public partial class CategoryManagementWindow : Window
    {
        public CategoryManagementWindow()
            : this(new CategoryManagementViewModel())
        {
        }

        public CategoryManagementWindow(CategoryManagementViewModel viewModel)
        {
            if (viewModel == null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }

            InitializeComponent();
            DataContext = viewModel;
            viewModel.RequestClose += Close;
        }
    }
}
