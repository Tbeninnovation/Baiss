public class OpenWindowInfo
{
    public string Title { get; set; } = "";
    public string? Filename { get; set; }
    public string? MatchedFullPath { get; set; }
    public string? ProcessName { get; set; }
    public string? ExecutablePath { get; set; }
    public bool IsActive { get; set; }
}
