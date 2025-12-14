

namespace Baiss.Application.Interfaces;



public interface ICaptureScreenService
{
	/// <summary>
	/// Captures a screenshot of the entire screen using native APIs
	/// </summary>
	/// <param name="filePath">Path where to save the screenshot</param>
	/// <param name="screenIndex">Index of the screen to capture (0-based). If null, captures primary screen</param>
	/// <returns>True if successful, false otherwise</returns>
	Task<bool> CaptureScreenshotAsync(string filePath, int? screenIndex = null);


	/// <summary>
	/// Retrieves the names or identifiers of available screens/monitors
	/// </summary>
	/// <returns>A list of screen names</returns>
	Task<List<string>> GetScreenNames();

	/// <summary>
	/// Captures a screenshot using platform-specific command line tools
	/// </summary>
	/// <param name="filePath">Path where to save the screenshot</param>
	/// <param name="screenIndex">Index of the screen to capture (0-based). If null, captures primary screen</param>
	/// <returns>True if successful, false otherwise</returns>
	Task<bool> CaptureScreenshotWithToolsAsync(string filePath, int? screenIndex = null);

	/// <summary>
	/// Captures a screenshot of a specific window
	/// </summary>
	/// <param name="windowHandle">Handle or reference to the window to capture</param>
	/// <param name="filePath">Path where to save the screenshot</param>
	/// <returns>True if successful, false otherwise</returns>
	// Task<bool> CaptureWindowScreenshotAsync(object windowHandle, string filePath);

	int GetMonitorCount();
}
