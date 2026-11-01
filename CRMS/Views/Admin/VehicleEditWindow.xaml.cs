using System.IO;
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
        DataContext = viewModel;
        viewModel.RequestClose += saved =>
        {
            DialogResult = saved;
            Close();
        };
    }

    private void BrowseLocalImage_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not VehicleEditViewModel vm)
            return;

        var dialog = new OpenFileDialog
        {
            Title = "Chọn ảnh phương tiện",
            Filter = "Tệp hình ảnh (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        var uploadDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VehicleUploads");
        Directory.CreateDirectory(uploadDir);

        foreach (var sourceFile in dialog.FileNames)
        {
            var ext = Path.GetExtension(sourceFile);
            var safeName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
            var destination = Path.Combine(uploadDir, safeName);
            File.Copy(sourceFile, destination, overwrite: true);
            vm.AddLocalImagePath(destination);
        }
    }
}
