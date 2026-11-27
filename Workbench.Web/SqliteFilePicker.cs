namespace Workbench.Web;

public interface ISqliteFilePicker
{
    Task<string?> PickAsync(string? currentPath, bool fileMustExist);
}
