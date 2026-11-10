using System.Windows;
using CRMS.ViewModels;

namespace CRMS.Views.Admin;

public partial class CategoryManagementWindow : Window
{
    public CategoryManagementWindow()
        : this(new CategoryManagementViewModel())
    {
    }

    public CategoryManagementWindow(CategoryManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        viewModel.RequestClose += Close;
    }
}
