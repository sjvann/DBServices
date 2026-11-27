using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Workbench.Web;

namespace Workbench;

[SupportedOSPlatform("windows")]
public sealed class WindowsSqliteFilePicker : ISqliteFilePicker
{
    public Task<string?> PickAsync(string? currentPath, bool fileMustExist)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult<string?>(null);

        var owner = GetForegroundWindow();
        return Task.Run(() => Show(owner, currentPath, fileMustExist));
    }

    private static string? Show(IntPtr owner, string? currentPath, bool fileMustExist)
    {
        string? chosen = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                chosen = Dialog(owner, currentPath, fileMustExist);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
            throw failure;
        return chosen;
    }

    private static string? Dialog(IntPtr owner, string? currentPath, bool fileMustExist)
    {
        var initial = "";
        string? directory = null;
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var trimmed = currentPath.Trim().Trim('"');
            var folder = Path.GetDirectoryName(trimmed);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                directory = folder;
            initial = Path.GetFileName(trimmed);
        }

        try
        {
            return Ask(owner, directory, initial, fileMustExist);
        }
        catch (InvalidOperationException) when (owner != IntPtr.Zero)
        {
            return Ask(IntPtr.Zero, directory, initial, fileMustExist);
        }
    }

    private static string? Ask(IntPtr owner, string? directory, string initial, bool fileMustExist)
    {
        const int chars = 8192;
        var buffer = Marshal.AllocHGlobal(chars * 2);
        try
        {
            for (var i = 0; i < chars * 2; i++)
                Marshal.WriteByte(buffer, i, 0);
            if (initial.Length > 0 && initial.Length < chars)
            {
                var data = Encoding.Unicode.GetBytes(initial);
                Marshal.Copy(data, 0, buffer, data.Length);
            }

            var filter = "SQLite 資料庫 (*.db;*.sqlite;*.sqlite3)\0*.db;*.sqlite;*.sqlite3\0所有檔案 (*.*)\0*.*\0\0";
            var filterBytes = Encoding.Unicode.GetBytes(filter);
            var filterBuffer = Marshal.AllocHGlobal(filterBytes.Length + 2);
            try
            {
                for (var i = 0; i < filterBytes.Length + 2; i++)
                    Marshal.WriteByte(filterBuffer, i, 0);
                Marshal.Copy(filterBytes, 0, filterBuffer, filterBytes.Length);
                var dialog = new OpenFileName
                {
                    hwndOwner = owner,
                    lpstrFilter = filterBuffer,
                    nFilterIndex = 1,
                    lpstrFile = buffer,
                    nMaxFile = chars,
                    lpstrInitialDir = directory,
                    lpstrTitle = fileMustExist ? "選擇 SQLite 資料庫" : "選擇或建立 SQLite 資料庫",
                    lpstrDefExt = "db",
                    Flags = 0x00080000 | 0x00000008 | 0x00000004 | 0x00000800 | 0x00800000 | (fileMustExist ? 0x00001000 : 0),
                };
                dialog.lStructSize = Marshal.SizeOf(dialog);
                if (GetOpenFileName(ref dialog))
                    return Marshal.PtrToStringUni(buffer);

            var code = CommDlgExtendedError();
            if (code == 0)
                return null;
            throw new InvalidOperationException("選擇檔案視窗沒有打開（" + code.ToString("X") + "）。");
            }
            finally
            {
                Marshal.FreeHGlobal(filterBuffer);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetOpenFileNameW")]
    private static extern bool GetOpenFileName(ref OpenFileName dialog);

    [DllImport("comdlg32.dll")]
    private static extern int CommDlgExtendedError();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string? lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }
}
