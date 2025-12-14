using System;
using System.Collections.Generic;
using System.Linq;

namespace Baiss.UI.Services
{
    /// <summary>
    /// Parses message content to identify and separate markdown titles (lines starting with '# ')
    /// </summary>
    public static class MarkdownTitleParser
    {
        public class ContentBlock
        {
            public string Text { get; set; } = string.Empty;
            public bool IsBigTitle { get; set; }
        }

        /// <summary>
        /// Parses content and identifies lines starting with a single '# ' as big titles
        /// </summary>
        /// <param name="content">The message content to parse</param>
        /// <returns>List of content blocks with title information</returns>
        public static List<ContentBlock> ParseContent(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return new List<ContentBlock>();
            }

            var blocks = new List<ContentBlock>();
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var currentBlock = new List<string>();
            bool isInCodeBlock = false;

            foreach (var line in lines)
            {
                // Track code blocks to avoid treating # inside code blocks as titles
                if (line.TrimStart().StartsWith("```"))
                {
                    isInCodeBlock = !isInCodeBlock;
                    currentBlock.Add(line);
                    continue;
                }

                // Check if this line is a big title (single # at the start, not inside code block)
                if (!isInCodeBlock && IsBigTitle(line))
                {
                    // Save any accumulated non-title content
                    if (currentBlock.Count > 0)
                    {
                        blocks.Add(new ContentBlock
                        {
                            Text = string.Join(Environment.NewLine, currentBlock),
                            IsBigTitle = false
                        });
                        currentBlock.Clear();
                    }

                    // Add the title block (without the # or ## markers)
                    var titleText = line.TrimStart();
                    if (titleText.StartsWith("## "))
                    {
                        titleText = titleText.Substring(3); // Remove "## "
                    }
                    else if (titleText.StartsWith("# "))
                    {
                        titleText = titleText.Substring(2); // Remove "# "
                    }
                    
                    blocks.Add(new ContentBlock
                    {
                        Text = titleText,
                        IsBigTitle = true
                    });
                }
                else
                {
                    currentBlock.Add(line);
                }
            }

            // Add any remaining content
            if (currentBlock.Count > 0)
            {
                blocks.Add(new ContentBlock
                {
                    Text = string.Join(Environment.NewLine, currentBlock),
                    IsBigTitle = false
                });
            }

            return blocks;
        }

        /// <summary>
        /// Checks if a line represents a big title (starts with exactly one '#' followed by space)
        /// </summary>
        private static bool IsBigTitle(string line)
        {
            if (string.IsNullOrEmpty(line))
                return false;

            var trimmed = line.TrimStart();
            
            // Must start with # followed by space (allows # or ##)
            if (!trimmed.StartsWith("#"))
                return false;

            // Check for # or ##
            if (trimmed.StartsWith("## "))
            {
                return true; // ## is also a big title
            }
            
            if (trimmed.StartsWith("# "))
            {
                return true; // # is a big title
            }

            return false;
        }
    }
}
