namespace CRMS.Services;

public interface IFileExportService
{
    string? ChooseCsvPath(string suggestedName);
    Task WriteCsvAsync(string path, string content);
}
