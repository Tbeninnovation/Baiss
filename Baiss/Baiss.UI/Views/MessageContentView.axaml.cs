using Avalonia;
using Avalonia.Controls;
using Markdig;
using System;
using System.Text;
using Avalonia.Input;
using Avalonia.Interactivity;
using TheArtOfDev.HtmlRenderer.Avalonia;

namespace Baiss.UI.Views
{
    public partial class MessageContentView : UserControl
    {
        private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        public static readonly StyledProperty<string> MessageContentProperty =
            AvaloniaProperty.Register<MessageContentView, string>(nameof(MessageContent));

        public static readonly StyledProperty<string> HtmlContentProperty =
            AvaloniaProperty.Register<MessageContentView, string>(
                nameof(HtmlContent), 
                defaultValue: string.Empty);

        public string MessageContent
        {
            get => GetValue(MessageContentProperty);
            set => SetValue(MessageContentProperty, value);
        }

        public string HtmlContent
        {
            get => GetValue(HtmlContentProperty);
            private set => SetValue(HtmlContentProperty, value);
        }

        public static readonly StyledProperty<bool> IsMineProperty =
            AvaloniaProperty.Register<MessageContentView, bool>(
                nameof(IsMine),
                defaultValue: false);

        public bool IsMine
        {
            get => GetValue(IsMineProperty);
            set => SetValue(IsMineProperty, value);
        }

        public static event Action<MessageContentView?>? GlobalSelectionChanged;

        private HtmlLabel? _htmlLabel;

        public static void ClearAllSelections()
        {
            GlobalSelectionChanged?.Invoke(null);
        }

        public MessageContentView()
        {
            InitializeComponent();
            
            _htmlLabel = this.FindControl<HtmlLabel>("HtmlControl");
            if (_htmlLabel != null)
            {
                // Detect click/selection start to clear other selections
                _htmlLabel.AddHandler(PointerPressedEvent, (s, e) =>
                {
                    GlobalSelectionChanged?.Invoke(this);
                }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            GlobalSelectionChanged += OnGlobalSelectionChanged;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            GlobalSelectionChanged -= OnGlobalSelectionChanged;
            base.OnDetachedFromVisualTree(e);
        }

        private void OnGlobalSelectionChanged(MessageContentView? sender)
        {
            if (sender != this && _htmlLabel != null)
            {
                try
                {
                    // Attempt to clear selection. Using reflection to be safe if the API method is protected or missing in this version
                    var method = _htmlLabel.GetType().GetMethod("ClearSelection");
                    if (method != null)
                    {
                        method.Invoke(_htmlLabel, null);
                    }
                }
                catch (Exception)
                {
                    // Ignore errors during selection clearing
                }
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == MessageContentProperty)
            {
                UpdateHtmlContent(change.NewValue as string);
            }
        }

        private static string ConvertEmojisToHtmlEntities(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];
                
                // Check if this is a high surrogate (start of emoji)
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    // Get the full Unicode code point from surrogate pair
                    int codePoint = char.ConvertToUtf32(c, text[i + 1]);
                    sb.Append($"&#x{codePoint:X};");
                    i++; // Skip the low surrogate
                }
                // If character is outside basic ASCII range, convert to HTML entity
                else if (c > 127)
                {
                    sb.Append($"&#x{(int)c:X};");
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private void UpdateHtmlContent(string? markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                HtmlContent = string.Empty;
                return;
            }

            try 
            {
                // Ensure markdown is trimmed and has a newline to help parser with headers
                var cleanMarkdown = markdown.Trim() + "\n";
                
                // Convert markdown to HTML
                var html = Markdown.ToHtml(cleanMarkdown, _markdownPipeline);
                
                // Convert emojis and special characters to HTML entities
                html = ConvertEmojisToHtmlEntities(html);
                
                // Wrap in basic HTML with styling for dark theme
                var styledHtml = $@"
                    <html>
                    <head>
                        <meta charset=""UTF-8"">
                        <style>
                            body {{ 
                                color: #E0E0E0; 
                                font-family: 'Segoe UI', 'Segoe UI Emoji', 'Segoe UI Symbol', Arial, sans-serif; 
                                font-size: 16px;
                                background: #3b00c4ff;
                                margin: 0;
                                padding: 0;
                                word-wrap: break-word;
                                overflow-wrap: break-word;
                                white-space: normal;
                            }}
                            * {{
                                max-width: 100%;
                                box-sizing: border-box;
                            }}
                            h1, h2, h3, h4, h5, h6 {{
                                color: #FFFFFF;
                                font-weight: bold;
                                margin: 0.5em 0;
                                display: block !important;
                            }}
                            h1 {{ font-size: 2em; border-bottom: 1px solid #444; padding-bottom: 0.3em; }}
                            h2 {{ font-size: 1.5em; border-bottom: 1px solid #444; padding-bottom: 0.3em; }}
                            h3 {{ font-size: 1.25em; }}
                            p {{ 
                                margin: 0.5em 0;
                            }}
                            code {{ 
                                background-color: #2D2D30; 
                                padding: 2px 4px; 
                                border-radius: 3px;
                                color: #CE9178;
                                font-family: Consolas, 'Courier New', monospace;
                                font-size: 16px;
                            }}
                            pre {{ 
                                background-color: #1E1E1E; 
                                padding: 10px; 
                                border-radius: 5px; 
                                overflow-x: auto;
                                white-space: pre-wrap;
                                margin: 0.5em 0;
                            }}
                            pre code {{ 
                                background-color: transparent; 
                                padding: 0;
                                color: #D4D4D4;
                                font-size: 16px;
                            }}
                            a {{ 
                                color: #4EC9B0;
                                text-decoration: none;
                            }}
                            a:hover {{
                                text-decoration: underline;
                            }}
                            blockquote {{ 
                                border-left: 4px solid #4EC9B0; 
                                padding-left: 10px; 
                                margin-left: 0;
                                color: #B0B0B0;
                                background-color: rgba(78, 201, 176, 0.1);
                            }}
                            ul, ol {{
                                padding-left: 20px;
                                margin: 0.5em 0;
                            }}
                            li {{
                                margin: 0.2em 0;
                            }}
                        </style>
                    </head>
                    <body>{html}</body>
                    </html>";
                
                HtmlContent = styledHtml;
            }
            catch (Exception)
            {
                // Fallback to plain text if something goes wrong
                HtmlContent = markdown;
            }
        }
    }
}


