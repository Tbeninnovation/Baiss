using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Baiss.Application.DTOs;

namespace Baiss.UI.Models
{
    public class ChatMessage : INotifyPropertyChanged
    {
        private static readonly Regex ThinkingBlockRegex = new(
            @"<\s*search_tool\s*>(?<tagged>.*?)<\s*/\s*search_tool\s*>|(?<json>\{\s*""tool""\s*:\s*""[^""]+""[^}]*\})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

        // Regex to detect Python code blocks: ```python ... ```
        private static readonly Regex PythonCodeBlockRegex = new(
            @"```python\s*(?<code>[\s\S]*?)```",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Regex to filter out <code_execution> and </code_execution> tags (and partial tags during streaming)
        private static readonly Regex CodeExecutionTagRegex = new(
            @"<\s*/?\s*code_execution\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Regex to filter out [CODE_EXEC:status:error] markers
        private static readonly Regex CodeExecMarkerRegex = new(
            @"\[CODE_EXEC:[^\]]*\]",
            RegexOptions.Compiled);

        private string _content = string.Empty;
        public required string Content
        {
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value;
                    OnPropertyChanged(nameof(Content));
                    OnPropertyChanged(nameof(MessageSegments));
                }
            }
        }

        public DateTime Timestamp { get; set; }
        public bool IsMine { get; set; }

        public IReadOnlyList<MessageSegment> MessageSegments => ParseSegments(Content, IsMine);

        private bool _isLoadingMessage;
        public bool IsLoadingMessage
        {
            get => _isLoadingMessage;
            set
            {
                if (_isLoadingMessage != value)
                {
                    _isLoadingMessage = value;
                    OnPropertyChanged(nameof(IsLoadingMessage));
                }
            }
        }

        private bool _isStreaming = false;
        public bool IsStreaming
        {
            get => _isStreaming;
            set
            {
                if (_isStreaming != value)
                {
                    _isStreaming = value;
                    OnPropertyChanged(nameof(IsStreaming));
                    OnPropertyChanged(nameof(IsComplete));
                    OnPropertyChanged(nameof(IsWaitingForData));
                }
            }
        }

        private bool _isReceivingData = false;
        /// <summary>
        /// True when actively receiving data chunks from the WebSocket.
        /// </summary>
        public bool IsReceivingData
        {
            get => _isReceivingData;
            set
            {
                if (_isReceivingData != value)
                {
                    _isReceivingData = value;
                    OnPropertyChanged(nameof(IsReceivingData));
                    OnPropertyChanged(nameof(IsWaitingForData));
                }
            }
        }

        /// <summary>
        /// True when streaming is active but no data is currently being received (e.g., during code execution).
        /// This is when we show the "Answering..." indicator.
        /// </summary>
        public bool IsWaitingForData => IsStreaming && !IsReceivingData;

        /// <summary>
        /// Returns true when the message is complete (not streaming) - used to show copy button
        /// </summary>
        public bool IsComplete => !IsStreaming && !IsMine;

        private List<SourceItem> _sources = new List<SourceItem>();
        public List<SourceItem> Sources
        {
            get => _sources;
            set
            {
                var normalized = value ?? new List<SourceItem>();
                if (!ReferenceEquals(_sources, normalized))
                {
                    _sources = normalized;
                    OnPropertyChanged(nameof(Sources));
                    OnPropertyChanged(nameof(HasSources));
                    OnPropertyChanged(nameof(SourcesCount));
                    OnPropertyChanged(nameof(SourcesSummary));
                }
            }
        }

        // Helper property to check if this message has sources
        public bool HasSources => Sources != null && Sources.Any();

        // Helper property to get sources count
        public int SourcesCount => Sources?.Count ?? 0;

        // Helper property for sources summary text
        public string SourcesSummary => HasSources ? $"Answer ready• {SourcesCount} file{(SourcesCount > 1 ? "s" : "")} analyzed" : "";

        private bool _isSourcesExpanded = false;
        public bool IsSourcesExpanded
        {
            get => _isSourcesExpanded;
            set
            {
                if (_isSourcesExpanded != value)
                {
                    _isSourcesExpanded = value;
                    OnPropertyChanged(nameof(IsSourcesExpanded));
                }
            }
        }

        private List<PathScore> _paths = new List<PathScore>();
        public List<PathScore> Paths
        {
            get => _paths;
            set
            {
                var normalized = value ?? new List<PathScore>();
                if (!ReferenceEquals(_paths, normalized))
                {
                    _paths = normalized;
                    OnPropertyChanged(nameof(Paths));
                    OnPropertyChanged(nameof(HasPaths));
                    OnPropertyChanged(nameof(PathsCount));
                }
            }
        }

        // Helper property to check if this message has paths
        public bool HasPaths => Paths != null && Paths.Any();

        // Helper property to get paths count
        public int PathsCount => Paths?.Count ?? 0;

        private bool _isPathsExpanded = false;
        public bool IsPathsExpanded
        {
            get => _isPathsExpanded;
            set
            {
                if (_isPathsExpanded != value)
                {
                    _isPathsExpanded = value;
                    OnPropertyChanged(nameof(IsPathsExpanded));
                }
            }
        }

        // Code execution results - persisted across MessageSegments regeneration
        private List<CodeExecutionResult> _codeExecutionResults = new();
        public List<CodeExecutionResult> CodeExecutionResults => _codeExecutionResults;

        /// <summary>
        /// Updates the execution status for a code block at the given index.
        /// </summary>
        public void UpdateCodeExecutionResult(int index, bool isSuccess, string? error = null)
        {
            while (_codeExecutionResults.Count <= index)
            {
                _codeExecutionResults.Add(new CodeExecutionResult());
            }
            _codeExecutionResults[index].IsSuccess = isSuccess;
            _codeExecutionResults[index].Error = error;
            OnPropertyChanged(nameof(MessageSegments)); // Trigger UI refresh
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Appends text to the content (used for streaming)
        /// </summary>
        /// <param name="text">Text to append</param>
        public void AppendContent(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (string.IsNullOrEmpty(Content))
            {
                text = text.TrimStart('\r', '\n');
            }

            if (text.Length == 0)
            {
                return;
            }

            Content += text;
        }

        private IReadOnlyList<MessageSegment> ParseSegments(string content, bool isMine)
        {
            if (string.IsNullOrEmpty(content))
            {
                return Array.Empty<MessageSegment>();
            }

            // Remove <code_execution> and </code_execution> tags from the content
            content = CodeExecutionTagRegex.Replace(content, string.Empty);

            // Remove [CODE_EXEC:...] markers from the content
            content = CodeExecMarkerRegex.Replace(content, string.Empty);

            // Combine all matches from both regexes with their types
            var allMatches = new List<(Match Match, string Type)>();
            
            foreach (Match match in ThinkingBlockRegex.Matches(content))
            {
                allMatches.Add((match, "thinking"));
            }
            
            foreach (Match match in PythonCodeBlockRegex.Matches(content))
            {
                allMatches.Add((match, "code"));
            }

            if (allMatches.Count == 0)
            {
                return new List<MessageSegment> { new TextMessageSegment(content) { IsMine = isMine } };
            }

            // Sort by position in content
            allMatches.Sort((a, b) => a.Match.Index.CompareTo(b.Match.Index));

            var segments = new List<MessageSegment>();
            var currentIndex = 0;

            foreach (var (match, type) in allMatches)
            {
                if (!match.Success)
                {
                    continue;
                }

                // Add any text before this match
                if (match.Index > currentIndex)
                {
                    var textSegment = content.Substring(currentIndex, match.Index - currentIndex);
                    if (!string.IsNullOrWhiteSpace(textSegment))
                    {
                        segments.Add(new TextMessageSegment(textSegment) { IsMine = isMine });
                    }
                }

                if (type == "thinking")
                {
                    var payload = match.Groups["tagged"].Success
                        ? match.Groups["tagged"].Value
                        : match.Groups["json"].Value;

                    if (!string.IsNullOrWhiteSpace(payload))
                    {
                        segments.Add(new ThinkingMessageSegment(payload.Trim()) { IsMine = isMine });
                    }
                }
                else if (type == "code")
                {
                    var code = match.Groups["code"].Value;
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        var codeBlockIndex = segments.OfType<CodeExecutionMessageSegment>().Count();
                        segments.Add(new CodeExecutionMessageSegment(code.Trim(), codeBlockIndex, this) { IsMine = isMine });
                    }
                }

                currentIndex = match.Index + match.Length;
            }

            // Add any remaining text after the last match
            if (currentIndex < content.Length)
            {
                var textSegment = content.Substring(currentIndex);
                if (!string.IsNullOrWhiteSpace(textSegment))
                {
                    segments.Add(new TextMessageSegment(textSegment) { IsMine = isMine });
                }
            }

            return segments;
        }
    }

    public abstract class MessageSegment : INotifyPropertyChanged
    {
        public bool IsMine { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TextMessageSegment : MessageSegment
    {
        public TextMessageSegment(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }

    public class ThinkingMessageSegment : MessageSegment
    {
        public ThinkingMessageSegment(string content)
        {
            DisplayText = content;
        }

        public string DisplayText { get; }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }
    }

    /// <summary>
    /// Represents a code execution segment in a message (Python code block).
    /// </summary>
    public class CodeExecutionMessageSegment : MessageSegment
    {
        public CodeExecutionMessageSegment(string code, int codeBlockIndex, ChatMessage? parentMessage = null)
        {
            Code = code;
            CodeBlockIndex = codeBlockIndex;
            ParentMessage = parentMessage;
            _isExpanded = true; // Default to expanded
        }

        /// <summary>
        /// The Python code to be executed.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Index of this code block in the message (for looking up execution result).
        /// </summary>
        public int CodeBlockIndex { get; }

        /// <summary>
        /// Reference to parent message for looking up execution results.
        /// </summary>
        private ChatMessage? ParentMessage { get; }

        /// <summary>
        /// Gets the execution result from the parent message.
        /// </summary>
        private CodeExecutionResult? ExecutionResult =>
            ParentMessage?.CodeExecutionResults.Count > CodeBlockIndex
                ? ParentMessage.CodeExecutionResults[CodeBlockIndex]
                : null;

        /// <summary>
        /// Execution status: null = pending/running, true = success, false = error.
        /// </summary>
        public bool? IsSuccess => ExecutionResult?.IsSuccess;

        /// <summary>
        /// Returns true if the parent message is still streaming.
        /// </summary>
        private bool IsParentStreaming => ParentMessage?.IsStreaming ?? false;

        /// <summary>
        /// Output from the code execution (stdout or error message).
        /// </summary>
        public string? Output => ExecutionResult?.Error;

        public bool HasOutput => !string.IsNullOrEmpty(Output);

        public string StatusText => IsSuccess switch
        {
            null => IsParentStreaming ? "Running..." : "Completed",
            true => "Success",
            false => "Error"
        };

        public string StatusColor => IsSuccess switch
        {
            null => IsParentStreaming ? "#F59E0B" : "#10B981", // amber/yellow for running, green when completed
            true => "#10B981", // green for success
            false => "#EF4444"  // red for error
        };

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }
    }

    // Extension class to add helper methods for SourceItem
    public static class SourceItemExtensions
    {
        public static string GetDisplayFileName(this SourceItem source)
        {
            if (string.IsNullOrEmpty(source.FileName))
                return "Unknown File";

            var parts = source.FileName.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[parts.Length - 1] : source.FileName;
        }
    }

    public class PathScore
    {
        public string Path { get; set; } = string.Empty;
        public double Score { get; set; }

        public string DisplayFileName
        {
            get
            {
                if (string.IsNullOrEmpty(Path))
                    return "Unknown File";

                var parts = Path.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                return parts.Length > 0 ? parts[parts.Length - 1] : Path;
            }
        }

        public string FormattedScore => string.Format(CultureInfo.InvariantCulture, "score: {0:F2}%", Score * 100);
    }

    /// <summary>
    /// Stores the result of a code execution.
    /// </summary>
    public class CodeExecutionResult
    {
        public bool? IsSuccess { get; set; }
        public string? Error { get; set; }
    }
}
