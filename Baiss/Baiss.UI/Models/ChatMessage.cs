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
                }
            }
        }

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

        private static IReadOnlyList<MessageSegment> ParseSegments(string content, bool isMine)
        {
            if (string.IsNullOrEmpty(content))
            {
                return Array.Empty<MessageSegment>();
            }

            var matches = ThinkingBlockRegex.Matches(content);
            if (matches.Count == 0)
            {
                return new List<MessageSegment> { new TextMessageSegment(content) { IsMine = isMine } };
            }

            var segments = new List<MessageSegment>();
            var currentIndex = 0;

            foreach (Match match in matches)
            {
                if (!match.Success)
                {
                    continue;
                }

                if (match.Index > currentIndex)
                {
                    var textSegment = content.Substring(currentIndex, match.Index - currentIndex);
                    if (!string.IsNullOrWhiteSpace(textSegment))
                    {
                        segments.Add(new TextMessageSegment(textSegment) { IsMine = isMine });
                    }
                }

                var payload = match.Groups["tagged"].Success
                    ? match.Groups["tagged"].Value
                    : match.Groups["json"].Value;

                if (!string.IsNullOrWhiteSpace(payload))
                {
                    segments.Add(new ThinkingMessageSegment(payload.Trim()) { IsMine = isMine });
                }

                currentIndex = match.Index + match.Length;
            }

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
}
