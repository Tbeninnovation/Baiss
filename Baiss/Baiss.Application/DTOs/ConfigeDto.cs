using System.Text.Json.Serialization;

using Baiss.Application.DTOs;

public class ConfigeDto
{
	public string PythonPath { get; set; } = string.Empty;

	public string LlamaCppServerPath { get; set; } = string.Empty;

	public string BaissPythonCorePath { get; set; } = string.Empty;
	public object? CpuInfo { get; set; }
	public object? GpuInfo { get; set; }
	public object? RamInfo { get; set; }
}
