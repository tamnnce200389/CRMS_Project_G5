using System.Windows;
using CRMS.ViewModels;
using Microsoft.Win32;

namespace CRMS.Views.Admin;

public partial class VehicleEditWindow : Window
{
    public VehicleEditWindow()
        : this(new VehicleEditViewModel())
    {
    }

    public VehicleEditWindow(VehicleEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        viewModel.PickLocalImageFiles = PickFilesFromDialog;
        viewModel.RequestClose += saved =>
        {
            DialogResult = saved;
            Close();
        };
    }

    private IReadOnlyList<string>? PickFilesFromDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn ảnh phương tiện",
            Filter = "Tệp hình ảnh (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp",
            Multiselect = true
        };

        return dialog.ShowDialog(this) == true ? dialog.FileNames : null;
    }
}
