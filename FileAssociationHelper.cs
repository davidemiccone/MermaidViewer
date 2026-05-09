using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace MermaidViewer;

internal static class FileAssociationHelper
{
    // Per-user registration (HKCU) - doesn't require admin.
    private const string ProgId = "MermaidViewer.File";

    public static void Register(string exePath, IEnumerable<string> extensions)
    {
        if (string.IsNullOrWhiteSpace(exePath)) throw new ArgumentException("exePath mancante", nameof(exePath));

        using (var prog = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}", writable: true))
        {
            prog?.SetValue(null, "Mermaid diagram", RegistryValueKind.String);
            using var icon = prog?.CreateSubKey("DefaultIcon", writable: true);
            icon?.SetValue(null, $"\"{exePath}\",0", RegistryValueKind.String);
            using var cmd = prog?.CreateSubKey(@"shell\open\command", writable: true);
            cmd?.SetValue(null, $"\"{exePath}\" \"%1\"", RegistryValueKind.String);
        }

        foreach (var ext in extensions.Select(NormalizeExt).Where(e => !string.IsNullOrWhiteSpace(e)))
        {
            using var k = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}", writable: true);
            k?.SetValue(null, ProgId, RegistryValueKind.String);
        }

        NotifyShell();
    }

    public static void Unregister(IEnumerable<string> extensions)
    {
        // Remove extension default only if it points to us (be conservative).
        foreach (var ext in extensions.Select(NormalizeExt).Where(e => !string.IsNullOrWhiteSpace(e)))
        {
            using var k = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ext}", writable: true);
            if (k is null) continue;
            var cur = k.GetValue(null) as string;
            if (string.Equals(cur, ProgId, StringComparison.OrdinalIgnoreCase))
            {
                try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ext}", throwOnMissingSubKey: false); } catch { }
            }
        }

        try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", throwOnMissingSubKey: false); } catch { }

        NotifyShell();
    }

    public static bool IsRegisteredFor(string extension)
    {
        var ext = NormalizeExt(extension);
        using var k = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ext}");
        var cur = k?.GetValue(null) as string;
        return string.Equals(cur, ProgId, StringComparison.OrdinalIgnoreCase);
    }

    public static void OpenDefaultAppsSettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-settings:defaultapps",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private static string NormalizeExt(string ext)
    {
        ext = (ext ?? "").Trim();
        if (ext.Length == 0) return "";
        return ext.StartsWith('.') ? ext : "." + ext;
    }

    private static void NotifyShell()
    {
        // Refresh Explorer associations.
        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}

