using Baiss.Application.Interfaces;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WindowsShortcutFactory;

namespace Baiss.Infrastructure.Services;

public class WindowInspectorService : IWindowInspectorService
{
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public Task<List<OpenWindowInfo>> GetOpenWindowsAsync()
    {
        var windows = new List<OpenWindowInfo>();
        var foreground = GetForegroundWindow();

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            StringBuilder title = new(256);
            GetWindowText(hWnd, title, title.Capacity);
            var windowTitle = title.ToString().Trim();
            if (string.IsNullOrWhiteSpace(windowTitle)) return true;

            GetWindowThreadProcessId(hWnd, out uint pid);

            string processName = "(unknown)";
            string exePath = "(unknown)";
            try
            {
                var proc = Process.GetProcessById((int)pid);
                processName = proc.ProcessName;
                exePath = proc.MainModule?.FileName ?? "(no path)";
            }
            catch { }

            var filename = ExtractFilenameFromTitle(windowTitle);
            var matchedPath = TryFindRecentFileByName(filename);

            windows.Add(new OpenWindowInfo
            {
                Title = windowTitle,
                Filename = filename,
                MatchedFullPath = matchedPath,
                ProcessName = processName,
                ExecutablePath = exePath,
                IsActive = hWnd == foreground
            });

            return true;
        }, IntPtr.Zero);

        return Task.FromResult(windows);
    }

    private static string ExtractFilenameFromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        int lastDash = title.LastIndexOf(" - ");
        var candidate = lastDash >= 0 ? title[..lastDash].Trim() : title;
        return Path.HasExtension(candidate) ? candidate : "";
    }

    private static string? TryFindRecentFileByName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        var recent = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
        var recentFiles = Directory.EnumerateFiles(recent, "*.lnk")
            .Select(path => new FileInfo(path))
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();

        foreach (var file in recentFiles)
        {
            try
            {
                var shortcut = WindowsShortcut.Load(file.FullName);
                var target = shortcut?.Path;

                if (!string.IsNullOrWhiteSpace(target) &&
                    string.Equals(Path.GetFileName(target), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return target;
                }
            }
            catch { }
        }

        return null;
    }
}
