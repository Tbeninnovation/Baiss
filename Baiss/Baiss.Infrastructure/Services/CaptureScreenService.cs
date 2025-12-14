using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Controls.ApplicationLifetimes;
using Baiss.Application.Interfaces;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;


public class CaptureScreenService : ICaptureScreenService
{
    private readonly ILogger<CaptureScreenService> _logger;

    public CaptureScreenService(ILogger<CaptureScreenService> logger)
    {
        _logger = logger;
    }


    // ! -------------------------------------------------------------------------------------

    // Obtenir le nombre total de moniteurs
    public int GetMonitorCount()
    {
        var screens = GetScreens();
        return screens?.Count ?? 0;
    }

    // Obtenir tous les écrans disponibles
    public static IReadOnlyList<Screen> GetScreens()
    {
        var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        var screenHelper = mainWindow != null ? TopLevel.GetTopLevel(mainWindow)?.Screens : null;
        return screenHelper?.All ?? new List<Screen>();
    }

    // Obtenir l'écran sur lequel la fenêtre est actuellement affichée
    public static Screen? GetCurrentScreen(Window window)
    {
        var screenHelper = TopLevel.GetTopLevel(window)?.Screens;
        var centerPoint = new PixelPoint(
            window.Position.X + (int)(window.Width / 2),
            window.Position.Y + (int)(window.Height / 2)
        );
        return screenHelper?.ScreenFromPoint(centerPoint);
    }

    // Obtenir l'écran principal
    public static Screen? GetPrimaryScreen()
    {
        var screens = GetScreens();
        return screens?.FirstOrDefault(s => s.IsPrimary) ?? screens?.FirstOrDefault();
    }

    // Obtenir les noms de tous les écrans disponibles
    public async Task<List<string>> GetScreenNames()
    {
        return await Task.Run(() =>
        {
            var screens = GetScreens();
            var screenNames = new List<string>();

            if (screens != null)
            {
                for (int i = 0; i < screens.Count; i++)
                {
                    var screen = screens[i];
                    var name = screen.IsPrimary ? $"Screen {i + 1} (Primary)" : $"Screen {i + 1}";
                    //Console.WriteLine($" - - - -> >> > > >  Detected Screen: {name}, Bounds: {screen.Bounds}, IsPrimary: {screen.IsPrimary}");
                    screenNames.Add(name);
                }
            }
            return screenNames;
        });
    }

    // Obtenir un écran spécifique par son index
    public static Screen? GetScreenByIndex(int screenIndex)
    {
        var screens = GetScreens();
        if (screens != null && screenIndex >= 0 && screenIndex < screens.Count)
        {
            return screens[screenIndex];
        }
        return null;
    }

    // ! -------------------------------------------------------------------------------------

    /// <summary>
    /// Captures a screenshot of the entire screen using native APIs
    /// </summary>
    /// <param name="filePath">Path where to save the screenshot</param>
    /// <param name="screenIndex">Index of the screen to capture (0-based). If null, captures primary screen</param>
    public async Task<bool> CaptureScreenshotAsync(string filePath, int? screenIndex = null)
    {
        try
        {
#if WINDOWS
            // For Windows, use P/Invoke to capture the entire screen
            await CaptureScreenWindowsAsync(filePath, screenIndex);
            return File.Exists(filePath);
#elif OSX
            // For macOS, use CoreGraphics APIs
            await CaptureScreenMacOSAsync(filePath, screenIndex);
            return File.Exists(filePath);
#else
            // Fallback using runtime detection for cross-platform builds
            if (OperatingSystem.IsWindows())
            {
                _logger.LogInformation("Using Windows screen capture with runtime detection fallback");
                // Use the tools method for Windows fallback
                return await CaptureScreenshotWithToolsAsync(filePath, screenIndex);
            }
            else if (OperatingSystem.IsMacOS())
            {
                _logger.LogInformation("Using macOS screen capture with runtime detection fallback");
                // Use the tools method for macOS fallback
                return await CaptureScreenshotWithToolsAsync(filePath, screenIndex);
            }
            else
            {
                await Task.CompletedTask; // Satisfy async method requirement
                _logger.LogWarning("Screen capture is currently only supported on Windows and macOS. Use window capture instead.");
                return false;
            }
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture screen screenshot");
            return false;
        }
    }


    public async Task<bool> CaptureScreenshotWithToolsAsync(string filePath, int? screenIndex = null)
    {
        try
        {

#if WINDOWS
            // Use PowerShell on Windows
            return await CaptureScreenshotWindowsPowerShell(filePath, screenIndex);
#elif OSX
            // Use screencapture command on macOS
            return await CaptureScreenshotMacOSCommand(filePath, screenIndex);
#else
            // Fallback using runtime detection for cross-platform builds
            if (OperatingSystem.IsWindows())
            {
                _logger.LogInformation("Using PowerShell screenshot with runtime detection fallback");
                // Use PowerShell method as fallback for Windows
                return await CaptureScreenshotWindowsPowerShellFallback(filePath, screenIndex);
            }
            else if (OperatingSystem.IsMacOS())
            {
                _logger.LogInformation("Using screencapture command with runtime detection fallback");
                // Use command-line tool as fallback for macOS
                return await CaptureScreenshotMacOSCommandFallback(filePath, screenIndex);
            }
            else
            {
                await Task.CompletedTask;
                _logger.LogWarning("Screenshot tools are only supported on Windows and macOS");
                return false;
            }
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture screenshot using tools");
            return false;
        }
    }

    // /// <summary>
    // /// Captures a screenshot of a specific window
    // /// </summary>
    // public async Task<bool> CaptureWindowScreenshotAsync(object windowHandle, string filePath)
    // {
    //     try
    //     {
    //         if (windowHandle is not Window window)
    //         {
    //             _logger.LogError("Invalid window handle provided. Expected Avalonia Window.");
    //             return false;
    //         }

    //         await Task.Run(() =>
    //         {
    //             // Capture the current window using Avalonia's native API
    //             var pixelSize = new PixelSize((int)window.Width, (int)window.Height);
    //             var renderTargetBitmap = new RenderTargetBitmap(pixelSize, new Vector(96, 96));

    //             renderTargetBitmap.Render(window);

    //             // Save the bitmap to file
    //             using var fileStream = File.OpenWrite(filePath);
    //             renderTargetBitmap.Save(fileStream);
    //         });

    //         _logger.LogInformation($"Window screenshot saved to: {filePath}");
    //         return File.Exists(filePath);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Failed to capture window screenshot");
    //         return false;
    //     }
    // }

#if WINDOWS
        // Windows P/Invoke declarations for window management
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern int BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSrc, int xSrc, int ySrc, int rop);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern int DeleteDC(IntPtr hdc);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern int DeleteObject(IntPtr obj);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        // Additional APIs for multi-monitor support
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;
        private const int DESKTOPHORZRES = 118;
        private const int DESKTOPVERTRES = 117;

        private List<IntPtr> HideApplicationWindows()
        {
            var hiddenWindows = new List<IntPtr>();
            var currentProcessId = (uint)Process.GetCurrentProcess().Id;

            bool EnumWindowCallback(IntPtr hWnd, IntPtr lParam)
            {
                GetWindowThreadProcessId(hWnd, out uint windowProcessId);

                if (windowProcessId == currentProcessId && IsWindowVisible(hWnd))
                {
                    ShowWindow(hWnd, SW_HIDE);
                    hiddenWindows.Add(hWnd);
                }
                return true;
            }

            EnumWindows(EnumWindowCallback, IntPtr.Zero);
            return hiddenWindows;
        }

        private void RestoreApplicationWindows(List<IntPtr> hiddenWindows)
        {
            foreach (var hWnd in hiddenWindows)
            {
                ShowWindow(hWnd, SW_SHOW);
            }
        }

        private async Task CaptureScreenWindowsAsync(string filePath, int? screenIndex = null)
        {
            await Task.Run(() =>
            {
                const int SRCCOPY = 0x00CC0020;

                int screenWidth, screenHeight, screenX = 0, screenY = 0;

                if (screenIndex.HasValue)
                {
                    // Get specific screen dimensions
                    var screen = GetScreenByIndex(screenIndex.Value);
                    if (screen == null)
                    {
                        _logger.LogWarning($"Screen index {screenIndex} not found, using primary screen");
                        screenX = 0;
                        screenY = 0;
                        screenWidth = GetSystemMetrics(0); // SM_CXSCREEN
                        screenHeight = GetSystemMetrics(1); // SM_CYSCREEN
                    }
                    else
                    {
                        screenWidth = screen.Bounds.Width;
                        screenHeight = screen.Bounds.Height;
                        screenX = screen.Bounds.X;
                        screenY = screen.Bounds.Y;
                        _logger.LogInformation($"Capturing screen {screenIndex}: {screenWidth}x{screenHeight} at ({screenX},{screenY})");
                    }
                }
                else
                {
                    // Use primary screen
                    screenWidth = GetSystemMetrics(0); // SM_CXSCREEN
                    screenHeight = GetSystemMetrics(1); // SM_CYSCREEN
                    _logger.LogInformation($"Capturing primary screen: {screenWidth}x{screenHeight}");
                }

                // Get desktop DC - this gives us access to the entire virtual desktop
                IntPtr hdcScreen = GetDC(IntPtr.Zero);

                // Create memory DC and bitmap
                IntPtr hdcMemDC = CreateCompatibleDC(hdcScreen);
                IntPtr hBitmap = CreateCompatibleBitmap(hdcScreen, screenWidth, screenHeight);
                IntPtr hOld = SelectObject(hdcMemDC, hBitmap);

                // Copy the specified screen area from the virtual desktop
                // The key is to use screenX, screenY as source coordinates
                bool success = BitBlt(hdcMemDC, 0, 0, screenWidth, screenHeight, hdcScreen, screenX, screenY, SRCCOPY) != 0;

                if (!success)
                {
                    var error = Marshal.GetLastWin32Error();
                    _logger.LogError($"BitBlt failed with error: {error}");
                    // Console.WriteLine($"=== BitBlt FAILED with error: {error} ===");
                }
                else
                {
                    // Console.WriteLine($"=== BitBlt SUCCESS: copied {screenWidth}x{screenHeight} from ({screenX},{screenY}) ===");
                }

                SelectObject(hdcMemDC, hOld);
                DeleteDC(hdcMemDC);
                ReleaseDC(IntPtr.Zero, hdcScreen);

                // Convert to bitmap and save
                using var bitmap = System.Drawing.Image.FromHbitmap(hBitmap);
                bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

                DeleteObject(hBitmap);

                _logger.LogInformation($"Windows screenshot saved to: {filePath}");
            });
        }

        private static async Task<bool> CaptureScreenshotWindowsPowerShell(string filePath, int? screenIndex = null)
        {
            try
            {
                // First, validate the screen index if provided
                if (screenIndex.HasValue)
                {
                    var validateInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $@"-Command ""Add-Type -AssemblyName System.Windows.Forms; Write-Host 'Total screens:' [System.Windows.Forms.Screen]::AllScreens.Count; if ({screenIndex.Value} -ge [System.Windows.Forms.Screen]::AllScreens.Count) {{ Write-Error 'Screen index out of range'; exit 1; }}""",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var validateProcess = Process.Start(validateInfo);
                    if (validateProcess != null)
                    {
                        await validateProcess.WaitForExitAsync();
                        var validateOutput = await validateProcess.StandardOutput.ReadToEndAsync();
                        // Console.WriteLine($"Screen validation: {validateOutput}");

                        if (validateProcess.ExitCode != 0)
                        {
                            // Console.WriteLine($"Screen index {screenIndex.Value} is out of range");
                            return false;
                        }
                    }
                }

                string screenSelection = screenIndex.HasValue
                    ? $"[System.Windows.Forms.Screen]::AllScreens[{screenIndex.Value}].Bounds"
                    : "[System.Windows.Forms.Screen]::PrimaryScreen.Bounds";

                // Console.WriteLine($"PowerShell screenshot: screenIndex={screenIndex}, selection={screenSelection}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $@"-Command ""Add-Type -AssemblyName System.Windows.Forms; try {{ $screen = {screenSelection}; Write-Host 'Screen bounds:' $screen.X $screen.Y $screen.Width $screen.Height; $bitmap = New-Object System.Drawing.Bitmap $screen.Width, $screen.Height; $graphics = [System.Drawing.Graphics]::FromImage($bitmap); $graphics.CopyFromScreen($screen.X, $screen.Y, 0, 0, $bitmap.Size); $bitmap.Save('{filePath}', [System.Drawing.Imaging.ImageFormat]::Png); $graphics.Dispose(); $bitmap.Dispose(); Write-Host 'Screenshot saved successfully'; }} catch {{ Write-Error $_.Exception.Message; exit 1; }}""",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();

                    // Console.WriteLine($"PowerShell output: {output}");
                    if (!string.IsNullOrEmpty(error))
                    {
                        // Console.WriteLine($"PowerShell error: {error}");
                    }

                    bool success = process.ExitCode == 0 && File.Exists(filePath);
                    // Console.WriteLine($"PowerShell screenshot result: ExitCode={process.ExitCode}, FileExists={File.Exists(filePath)}, Success={success}");
                    return success;
                }
                return false;
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"PowerShell screenshot exception: {ex.Message}");
                return false;
            }
        }
#endif

#if OSX
        // macOS CoreGraphics P/Invoke declarations
        [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGDisplayCreateImage(uint displayID);

        [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern uint CGMainDisplayID();

        [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern int CGGetActiveDisplayList(uint maxDisplays, uint[] activeDisplays, out uint displayCount);

        [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/ImageIO.framework/ImageIO")]
        private static extern bool CGImageDestinationAddImage(IntPtr idst, IntPtr image, IntPtr properties);

        [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/ImageIO.framework/ImageIO")]
        private static extern bool CGImageDestinationFinalize(IntPtr idst);

        [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRelease(IntPtr obj);

        [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string cStr, uint encoding);

        [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern IntPtr CFURLCreateFromFileSystemRepresentation(IntPtr allocator, byte[] buffer, long bufLen, bool isDirectory);

        [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/ImageIO.framework/ImageIO")]
        private static extern IntPtr CGImageDestinationCreateWithURL(IntPtr url, IntPtr type, nuint count, IntPtr options);

        private static uint[] GetMacOSDisplayIDs()
        {
            uint[] displayIDs = new uint[32]; // Max 32 displays
            int result = CGGetActiveDisplayList((uint)displayIDs.Length, displayIDs, out uint displayCount);

            if (result != 0)
            {
                return new uint[] { CGMainDisplayID() }; // Fallback to main display
            }

            uint[] activeDisplays = new uint[displayCount];
            Array.Copy(displayIDs, activeDisplays, (int)displayCount);
            return activeDisplays;
        }


        private async Task CaptureScreenMacOSAsync(string filePath, int? screenIndex = null)
        {
            await Task.Run(() =>
            {
                try
                {
                    uint displayID;

                    if (screenIndex.HasValue)
                    {
                        // Get specific display ID
                        var displayIDs = GetMacOSDisplayIDs();
                        if (screenIndex.Value >= 0 && screenIndex.Value < displayIDs.Length)
                        {
                            displayID = displayIDs[screenIndex.Value];
                            _logger.LogInformation($"Capturing macOS screen {screenIndex.Value} with display ID: {displayID}");
                        }
                        else
                        {
                            _logger.LogWarning($"Screen index {screenIndex.Value} not found, using main display");
                            displayID = CGMainDisplayID();
                        }
                    }
                    else
                    {
                        // Get the main display ID
                        displayID = CGMainDisplayID();
                    }

                    // Create an image of the specified display
                    IntPtr image = CGDisplayCreateImage(displayID);
                    if (image == IntPtr.Zero)
                    {
                        _logger.LogError("Failed to create display image on macOS");
                        return;
                    }

                    // Convert file path to CFString and CFURL
                    var pathBytes = System.Text.Encoding.UTF8.GetBytes(filePath);
                    IntPtr fileUrl = CFURLCreateFromFileSystemRepresentation(IntPtr.Zero, pathBytes, pathBytes.Length, false);

                    if (fileUrl == IntPtr.Zero)
                    {
                        CFRelease(image);
                        _logger.LogError("Failed to create file URL on macOS");
                        return;
                    }

                    // Create PNG type string
                    IntPtr pngType = CFStringCreateWithCString(IntPtr.Zero, "public.png", 0x08000100); // kCFStringEncodingUTF8

                    if (pngType == IntPtr.Zero)
                    {
                        CFRelease(image);
                        CFRelease(fileUrl);
                        _logger.LogError("Failed to create PNG type string on macOS");
                        return;
                    }

                    // Create image destination
                    IntPtr destination = CGImageDestinationCreateWithURL(fileUrl, pngType, 1, IntPtr.Zero);

                    if (destination == IntPtr.Zero)
                    {
                        CFRelease(image);
                        CFRelease(fileUrl);
                        CFRelease(pngType);
                        _logger.LogError("Failed to create image destination on macOS");
                        return;
                    }

                    // Add the image to the destination
                    CGImageDestinationAddImage(destination, image, IntPtr.Zero);

                    // Finalize the image destination (this writes the file)
                    bool success = CGImageDestinationFinalize(destination);

                    // Clean up
                    CFRelease(destination);
                    CFRelease(pngType);
                    CFRelease(fileUrl);
                    CFRelease(image);

                    if (success)
                    {
                        _logger.LogInformation($"macOS screenshot saved successfully to: {filePath}");
                    }
                    else
                    {
                        _logger.LogError("Failed to finalize screenshot on macOS");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception during macOS screenshot capture");
                }
            });
        }

        private static async Task<bool> CaptureScreenshotMacOSCommand(string filePath, int? screenIndex = null)
        {
            try
            {
                string displayArgs = screenIndex.HasValue ? $"-D {screenIndex.Value + 1}" : ""; // macOS displays are 1-indexed

                var startInfo = new ProcessStartInfo
                {
                    FileName = "/usr/sbin/screencapture",
                    Arguments = $"-x {displayArgs} \"{filePath}\"", // -x suppresses screenshot sound, -D specifies display
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0 && File.Exists(filePath);
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
#endif

    // Fallback methods for cross-platform builds when compilation symbols aren't available
    private async Task<bool> CaptureScreenshotWindowsPowerShellFallback(string filePath, int? screenIndex = null)
    {
        try
        {
            string screenSelection = screenIndex.HasValue
                ? $"[System.Windows.Forms.Screen]::AllScreens[{screenIndex.Value}].Bounds"
                : "[System.Windows.Forms.Screen]::PrimaryScreen.Bounds";

            // Console.WriteLine($"PowerShell fallback screenshot: screenIndex={screenIndex}, selection={screenSelection}");

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $@"-Command ""Add-Type -AssemblyName System.Windows.Forms; try {{ $screen = {screenSelection}; Write-Host 'Screen bounds:' $screen.X $screen.Y $screen.Width $screen.Height; $bitmap = New-Object System.Drawing.Bitmap $screen.Width, $screen.Height; $graphics = [System.Drawing.Graphics]::FromImage($bitmap); $graphics.CopyFromScreen($screen.X, $screen.Y, 0, 0, $bitmap.Size); $bitmap.Save('{filePath}', [System.Drawing.Imaging.ImageFormat]::Png); $graphics.Dispose(); $bitmap.Dispose(); Write-Host 'Screenshot saved successfully'; }} catch {{ Write-Error $_.Exception.Message; exit 1; }}""",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                // Console.WriteLine($"PowerShell fallback output: {output}");
                if (!string.IsNullOrEmpty(error))
                {
                    // Console.WriteLine($"PowerShell fallback error: {error}");
                }

                bool success = process.ExitCode == 0 && File.Exists(filePath);
                // Console.WriteLine($"PowerShell fallback screenshot result: ExitCode={process.ExitCode}, FileExists={File.Exists(filePath)}, Success={success}");
                return success;
            }
            return false;
        }
        catch (Exception ex)
        {
            // Console.WriteLine($"PowerShell fallback screenshot exception: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> CaptureScreenshotMacOSCommandFallback(string filePath, int? screenIndex = null)
    {
        try
        {
            string displayArgs = screenIndex.HasValue ? $"-D {screenIndex.Value + 1}" : ""; // macOS displays are 1-indexed

            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/sbin/screencapture",
                Arguments = $"-x {displayArgs} \"{filePath}\"", // -x suppresses screenshot sound, -D specifies display
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                return process.ExitCode == 0 && File.Exists(filePath);
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
