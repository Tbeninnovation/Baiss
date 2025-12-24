
using System.Text.Json.Serialization;


namespace Baiss.Application.DTOs;


// ! Available Models DTOs
public class AvailableModelsResponse
{
	[JsonPropertyName("status")]
	public int Status { get; set; }

	[JsonPropertyName("success")]
	public bool Success { get; set; }

	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;

	[JsonPropertyName("error")]
	public string Error { get; set; } = string.Empty;

	[JsonPropertyName("data")]
	public AvailableModelsData Data { get; set; } = new();
}

public class AvailableModelsData
{
	[JsonPropertyName("models")]
	public List<ModelInfo> Models { get; set; } = new();

	[JsonPropertyName("total")]
	public int Total { get; set; }
}

public class ModelInfo
{
	[JsonPropertyName("model_id")]
	public string ModelId { get; set; } = string.Empty;

	[JsonPropertyName("author")]
	public string Author { get; set; } = string.Empty;

	[JsonPropertyName("model_name")]
	public string ModelName { get; set; } = string.Empty;

	[JsonPropertyName("downloads")]
	public int Downloads { get; set; }

	[JsonPropertyName("likes")]
	public int Likes { get; set; }

	[JsonPropertyName("description")]
	public string Description { get; set; } = string.Empty;

	[JsonPropertyName("gguf_files")]
	public List<GgufFileInfo> GgufFiles { get; set; } = new();

	[JsonPropertyName("gguf_count")]
	public int GgufCount { get; set; }

	// [JsonPropertyName("model_url")]
	// public string ModelUrl { get; set; } = string.Empty;

	[JsonPropertyName("purpose")]
	public string Purpose { get; set; } = string.Empty;
}

public class GgufFileInfo
{
	[JsonPropertyName("filename")]
	public string Filename { get; set; } = string.Empty;

	[JsonPropertyName("size")]
	public long? Size { get; set; }

	[JsonPropertyName("size_formatted")]
	public string SizeFormatted { get; set; } = string.Empty;

	[JsonPropertyName("download_url")]
	public string DownloadUrl { get; set; } = string.Empty;

	[JsonPropertyName("default")]
	public bool Default { get; set; }
}


// ! Start a new model download
public class StartModelsDownloadResponse
{
	[JsonPropertyName("status")]
	public int Status { get; set; }

	[JsonPropertyName("success")]
	public bool Success { get; set; }

	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;

	[JsonPropertyName("error")]
	public string Error { get; set; } = string.Empty;

	[JsonPropertyName("data")]
	public StartModelDownloadData Data { get; set; } = new();
}

public class StartModelDownloadData
{
	[JsonPropertyName("model_id")]
	public string ModelId { get; set; } = string.Empty;

	[JsonPropertyName("models_dir")]
	public string ModelsDir { get; set; } = string.Empty;

	[JsonPropertyName("process_id")]
	public string ProcessId { get; set; } = string.Empty;
}



// ! List of model downloads
public class ModelDownloadListResponse
{
	[JsonPropertyName("status")]
	public int Status { get; set; }

	[JsonPropertyName("success")]
	public bool Success { get; set; }

	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;

	[JsonPropertyName("error")]
	public string Error { get; set; } = string.Empty;

	[JsonPropertyName("data")]
	public ModelDownloadListData Data { get; set; } = new();
}

public class ModelDownloadListData
{
	[JsonPropertyName("downloads")]
	public List<ModelDownloadInfo> Downloads { get; set; } = new();

	[JsonPropertyName("total_active")]
	public int TotalActive { get; set; }
}

public class ModelDownloadInfo
{
	[JsonPropertyName("process_id")]
	public string ProcessId { get; set; } = string.Empty;

	[JsonPropertyName("model_id")]
	public string ModelId { get; set; } = string.Empty;

	[JsonPropertyName("status")]
	public string Status { get; set; } = string.Empty;

	[JsonPropertyName("progress")]
	public double Progress { get; set; }

	[JsonPropertyName("current_file")]
	public string CurrentFile { get; set; } = string.Empty;

	[JsonPropertyName("elapsed_time")]
	public double ElapsedTime { get; set; }
}


// ! Stop an ongoing model download
public class StopModelDownloadResponse
{
	[JsonPropertyName("status")]
	public int Status { get; set; }

	[JsonPropertyName("success")]
	public bool Success { get; set; }

	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;

	[JsonPropertyName("error")]
	public string Error { get; set; } = string.Empty;

	[JsonPropertyName("data")]
	public StopModelDownloadData Data { get; set; } = new();
}

public class StopModelDownloadData
{
	[JsonPropertyName("process_id")]
	public string ProcessId { get; set; } = string.Empty;

	[JsonPropertyName("stopped")]
	public bool Stopped { get; set; }
}


// ! progress of a model download
public class ModelDownloadProgressResponse
{
	[JsonPropertyName("status")]
	public int Status { get; set; }

	[JsonPropertyName("success")]
	public bool Success { get; set; }

	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;

	[JsonPropertyName("error")]
	public string Error { get; set; } = string.Empty;

	[JsonPropertyName("data")]
	public ModelDownloadProgressData Data { get; set; } = new();
}

public class ModelDownloadProgressData
{
	[JsonPropertyName("model_id")]
	public string ModelId { get; set; } = string.Empty;

	[JsonPropertyName("model_dir")]
	public string ModelDir { get; set; } = string.Empty;

	[JsonPropertyName("process_id")]
	public string ProcessId { get; set; } = string.Empty;

	[JsonPropertyName("current_size")]
	public long CurrentSize { get; set; }

	[JsonPropertyName("status")]
	public string Status { get; set; } = string.Empty;

	[JsonPropertyName("total_size")]
	public long TotalSize { get; set; }

	[JsonPropertyName("percentage")]
	public double Percentage { get; set; }
}




// ! Download Status Dictionary Response
public class ModelsListResponse
{
	[JsonPropertyName("status")]
	public int Status { get; set; }

	[JsonPropertyName("success")]
	public bool Success { get; set; }

	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;

	[JsonPropertyName("error")]
	public string Error { get; set; } = string.Empty;

	[JsonPropertyName("data")]
	public Dictionary<string, ModelsListStatus> Data { get; set; } = new();
}

public class ModelsListStatus
{
	[JsonPropertyName("model_id")]
	public string ModelId { get; set; } = string.Empty;

	[JsonPropertyName("model_dir")]
	public string ModelDir { get; set; } = string.Empty;

	[JsonPropertyName("models_dir")]
	public string ModelsDir { get; set; } = string.Empty;

	[JsonPropertyName("process_id")]
	public string ProcessId { get; set; } = string.Empty;

	[JsonPropertyName("current_size")]
	public long CurrentSize { get; set; }

	[JsonPropertyName("status")]
	public string Status { get; set; } = string.Empty;

	[JsonPropertyName("total_size")]
	public long TotalSize { get; set; }

	[JsonPropertyName("files")]
	public Dictionary<string, long> Files { get; set; } = new();

	[JsonPropertyName("entypoint")]
	public string Entypoint { get; set; } = string.Empty;

	[JsonPropertyName("info_file")]
	public string InfoFile { get; set; } = string.Empty;

	[JsonPropertyName("percentage")]
	public double Percentage { get; set; }
}

// ! Single Model Info Response
public class ModelInfoResponse
{
	[JsonPropertyName("status")]
	public int Status { get; set; }

	[JsonPropertyName("success")]
	public bool Success { get; set; }

	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;

	[JsonPropertyName("error")]
	public string Error { get; set; } = string.Empty;

	[JsonPropertyName("data")]
	public ModelInfoData Data { get; set; } = new();
}

public class ModelInfoData
{
	[JsonPropertyName("model_id")]
	public string ModelId { get; set; } = string.Empty;

	[JsonPropertyName("model_dir")]
	public string ModelDir { get; set; } = string.Empty;

	[JsonPropertyName("models_dir")]
	public string ModelsDir { get; set; } = string.Empty;

	[JsonPropertyName("process_id")]
	public string ProcessId { get; set; } = string.Empty;

	[JsonPropertyName("current_size")]
	public long CurrentSize { get; set; }

	[JsonPropertyName("status")]
	public string Status { get; set; } = string.Empty;

	[JsonPropertyName("total_size")]
	public long TotalSize { get; set; }

	[JsonPropertyName("files")]
	public Dictionary<string, long> Files { get; set; } = new();

	[JsonPropertyName("entypoint")]
	public string Entypoint { get; set; } = string.Empty;

	[JsonPropertyName("info_file")]
	public string InfoFile { get; set; } = string.Empty;

	[JsonPropertyName("percentage")]
	public double Percentage { get; set; }
}

