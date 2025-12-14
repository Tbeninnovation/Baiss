
using Baiss.Application.Interfaces;



namespace Baiss.Application.UseCases;

public class CaptureScreenUseCase
{
	private readonly ICaptureScreenService _captureScreenService;
	public CaptureScreenUseCase(ICaptureScreenService captureScreenService)
	{
		_captureScreenService = captureScreenService ?? throw new ArgumentNullException(nameof(captureScreenService));
	}

	public async Task<bool> CaptureScreenshotAsync(string savePath, int screenIndex = 0)
	{   // Capture the screenshot using the service
		// Capture the screenshot using the service

		bool result = await _captureScreenService.CaptureScreenshotAsync(savePath, screenIndex);
		// Console.WriteLine($"CaptureScreenshotAsync result: -- >> {result}");
		return result;
	}

	public async Task<List<string>> GetAvailableScreensAsync()
	{
		return await _captureScreenService.GetScreenNames();
	}

	public Task<int> GetMonitorCountAsync()
	{
		return Task.FromResult(_captureScreenService.GetMonitorCount());
	}


}
