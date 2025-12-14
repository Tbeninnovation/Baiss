using System.Text.Json.Serialization;
using Baiss.Domain.Entities;

namespace Baiss.Application.DTOs;

public class SendMessageDto
{
	public Guid? ConversationId { get; set; } // Null = nouvelle conversation
	public string Content { get; set; } = string.Empty;
	public List<string> FilePaths { get; set; } = new List<string>(); // Paths to attached files
}



public class SendMessageResponseDto
{
	public bool IsSuccessful { get; set; }
	public string? ErrorMessage { get; set; }
	public Guid ConversationId { get; set; }
	public string ConversationTitle { get; set; } = string.Empty;
	public Guid MessageId { get; set; }
	public bool IsNewConversation { get; set; }

	public ContentResponse? Content { get; set; } // Changed from string to ContentResponse
	public List<SourceItem> Sources { get; set; } = new(); // Direct access to sources
	public List<PathScoreDto> Paths { get; set; } = new(); // Paths with scores
}

public class ContentResponse
{

	[JsonPropertyName("status")]
	public int Status { get; set; }

	[JsonPropertyName("success")]
	public bool Success { get; set; }

	[JsonPropertyName("error")]
	public string? Error { get; set; }

	[JsonPropertyName("response")]
	public ResponseData Response { get; set; } = new();

	[JsonPropertyName("usage")]
	public UsageData Usage { get; set; } = new();
	// public List<object> Dashboard { get; set; } = new();

	[JsonPropertyName("sources")]
	public List<SourceItem> Sources { get; set; } = new();

    [JsonPropertyName("stop_reason")]
    public string StopReason { get; set; } = string.Empty;
}

public class ResponseData
{
	public List<MessageItem> Messages { get; set; } = new();
}

public class MessageItem
{
	public string Role { get; set; } = string.Empty;
	public List<ContentItem> Content { get; set; } = new();
}

public class ContentItem
{
	public string Text { get; set; } = string.Empty;
}

public class SourceItem
{
	[JsonPropertyName("file_name")]
	public string FileName { get; set; } = string.Empty;

	[JsonPropertyName("file_chunk")]
	public FileChunk FileChunk { get; set; } = new();
}

public class FileChunk
{
	[JsonPropertyName("full_text")]
	public string FullText { get; set; } = string.Empty;

	[JsonPropertyName("token_count")]
	public int TokenCount { get; set; }
}

public class PathScoreDto
{
	[JsonPropertyName("path")]
	public string Path { get; set; } = string.Empty;

	[JsonPropertyName("score")]
	public double Score { get; set; }
}

public class UsageData
{
    [JsonPropertyName("inputTokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("outputTokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("totalTokens")]
    public int TotalTokens { get; set; }
}

public class ConversationDto
{
	public Guid ConversationId { get; set; }
	public string Title { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
	public List<MessageDto> Messages { get; set; } = new();
}

public class MessageDto
{
	public Guid MessageId { get; set; }
	public string Content { get; set; } = string.Empty;
	public SenderType SenderType { get; set; } = SenderType.USER; // "user" or "assistant"
	public DateTime SentAt { get; set; }
	public string? Sources { get; set; } // JSON string containing source information
	public List<PathScoreDto> Paths { get; set; } = new(); // Paths with scores
}


    public class ContentRequestItem
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class MessageRequest
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public List<ContentRequestItem>? Content { get; set; }
    }

    public class InstructionRequest
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class SystemRequest
    {
        [JsonPropertyName("instructions")]
        public List<InstructionRequest>? Instructions { get; set; }
    }

    public class ConfigRequest
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("top_p")]
        public double TopP { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    public class ModelRequest
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;
    }

    public class ChatRequest
    {

        public List<object>? messages { get; set; }

        public string url { get; set; } = string.Empty;
        public string embedding_url { get; set; } = string.Empty;

        public List<string>? paths { get; set; }



    }

    // Streaming response DTOs
    public class StreamingResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("data")]
        public StreamingData? Data { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;
    }

    public class StreamingData
    {
        [JsonPropertyName("chunks")]
        public List<StreamingChunk> Chunks { get; set; } = new();
    }

    public class StreamingChunk
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("response")]
        public StreamingChunkResponse? Response { get; set; }
    }

    public class StreamingChunkResponse
    {
        [JsonPropertyName("choices")]
        public List<StreamingChoice> Choices { get; set; } = new();
    }

    public class StreamingChoice
    {
        [JsonPropertyName("messages")]
        public List<StreamingMessage> Messages { get; set; } = new();
    }

    public class StreamingMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public List<StreamingContentItem> Content { get; set; } = new();
    }

    public class StreamingContentItem
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }
