using System.IO;
using System.Text;
using CRMS.Services;
using Microsoft.Win32;
namespace CRMS.Helpers;

public sealed class WpfFileExportService : IFileExportService
{
    public string? ChooseCsvPath(string suggestedName)
    {
        var dialog = new SaveFileDialog { Title = "Xuất báo cáo doanh thu", Filter = "CSV files (*.csv)|*.csv", DefaultExt = ".csv", AddExtension = true, OverwritePrompt = true, FileName = suggestedName };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    public Task WriteCsvAsync(string path, string content) => File.WriteAllTextAsync(path, content, new UTF8Encoding(true));
}
