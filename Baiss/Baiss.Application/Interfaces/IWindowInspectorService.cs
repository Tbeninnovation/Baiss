namespace Baiss.Application.Interfaces;

public interface IWindowInspectorService
{
    Task<List<OpenWindowInfo>> GetOpenWindowsAsync();
}
