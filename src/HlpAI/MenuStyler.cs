using System;
using System.Text;

namespace HlpAI
{
    /// <summary>
    /// Provides enhanced styling and formatting for console menus
    /// </summary>
    public static class MenuStyler
    {
        // Color constants for consistent theming
        public static readonly ConsoleColor HeaderColor = ConsoleColor.Cyan;
        public static readonly ConsoleColor AccentColor = ConsoleColor.Yellow;
        public static readonly ConsoleColor SuccessColor = ConsoleColor.Green;
        public static readonly ConsoleColor ErrorColor = ConsoleColor.Red;
        public static readonly ConsoleColor InfoColor = ConsoleColor.Blue;
        public static readonly ConsoleColor MenuOptionColor = ConsoleColor.White;
        public static readonly ConsoleColor StatusColor = ConsoleColor.Gray;
        
        // Unicode characters for enhanced visual elements
        public const string BoxTopLeft = "╭";
        public const string BoxTopRight = "╮";
        public const string BoxBottomLeft = "╰";
        public const string BoxBottomRight = "╯";
        public const string BoxHorizontal = "─";
        public const string BoxVertical = "│";
        public const string Separator = "├─────────────────────────────────────────────────────────────────────────────╤";
        public const string BulletPoint = "●";
        public const string Arrow = "▶";
        public const string CheckMark = "✓";
        public const string CrossMark = "✗";
        public const string InfoIcon = "ℹ";
        public const string WarningIcon = "⚠";
        
        /// <summary>
        /// Creates a styled header with box border
        /// </summary>
        public static string CreateStyledHeader(string title, int width = 80)
        {
            var sb = new StringBuilder();
            var paddedTitle = $" {title} ";
            var padding = Math.Max(0, (width - paddedTitle.Length) / 2);
            var totalPadding = padding * 2 + paddedTitle.Length;
            var extraPadding = width - totalPadding;
            
            // Top border
            sb.AppendLine($"{BoxTopLeft}{new string(BoxHorizontal[0], width - 2)}{BoxTopRight}");
            
            // Title line
            sb.AppendLine($"{BoxVertical}{new string(' ', padding)}{paddedTitle}{new string(' ', padding + extraPadding)}{BoxVertical}");
            
            // Bottom border
            sb.AppendLine($"{BoxBottomLeft}{new string(BoxHorizontal[0], width - 2)}{BoxBottomRight}");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Creates a section separator with title
        /// </summary>
        public static string CreateSectionSeparator(string sectionTitle, int width = 80)
        {
            var titleWithSpaces = $" {sectionTitle} ";
            var remainingWidth = Math.Max(0, width - titleWithSpaces.Length - 2);
            var leftPadding = remainingWidth / 2;
            var rightPadding = remainingWidth - leftPadding;
            
            return $"├{new string(BoxHorizontal[0], leftPadding)}{titleWithSpaces}{new string(BoxHorizontal[0], rightPadding)}┤";
        }
        
        /// <summary>
        /// Formats a menu option with consistent styling
        /// </summary>
        public static string FormatMenuOption(int number, string description, string? icon = null)
        {
            var iconPart = string.IsNullOrEmpty(icon) ? "" : $"{icon} ";
            return $"  {number:D2}. {iconPart}{description}";
        }
        
        /// <summary>
        /// Formats a menu option with string key (for quick actions like 'c', 'm', 'q')
        /// </summary>
        public static string FormatMenuOption(string key, string description, string? icon = null)
        {
            var iconPart = string.IsNullOrEmpty(icon) ? "" : $"{icon} ";
            return $"  {key}. {iconPart}{description}";
        }
        
        /// <summary>
        /// Formats a status line with icon and color coding
        /// </summary>
        public static string FormatStatusLine(string label, string value, bool isSuccess = true, string? customIcon = null)
        {
            var icon = customIcon ?? (isSuccess ? CheckMark : CrossMark);
            return $"  {icon} {label}: {value}";
        }
        
        /// <summary>
        /// Creates a styled box around content
        /// </summary>
        public static string CreateContentBox(string content, int width = 80)
        {
            var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            
            // Top border
            sb.AppendLine($"{BoxTopLeft}{new string(BoxHorizontal[0], width - 2)}{BoxTopRight}");
            
            // Content lines
            foreach (var line in lines)
            {
                var paddedLine = line.PadRight(width - 4);
                if (paddedLine.Length > width - 4)
                {
                    paddedLine = paddedLine.Substring(0, width - 4);
                }
                sb.AppendLine($"{BoxVertical} {paddedLine} {BoxVertical}");
            }
            
            // Bottom border
            sb.AppendLine($"{BoxBottomLeft}{new string(BoxHorizontal[0], width - 2)}{BoxBottomRight}");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Writes colored text to console
        /// </summary>
        public static void WriteColored(string text, ConsoleColor color)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = originalColor;
        }
        
        /// <summary>
        /// Writes colored line to console
        /// </summary>
        public static void WriteColoredLine(string text, ConsoleColor color)
        {
            WriteColored(text + Environment.NewLine, color);
        }
        
        /// <summary>
        /// Creates a progress indicator
        /// </summary>
        public static string CreateProgressIndicator(int current, int total, int width = 30)
        {
            var percentage = (double)current / total;
            var filledWidth = (int)(percentage * width);
            var emptyWidth = width - filledWidth;
            
            return $"[{new string('█', filledWidth)}{new string('░', emptyWidth)}] {percentage:P0}";
        }
        
        /// <summary>
        /// Formats a breadcrumb path with styling
        /// </summary>
        public static string FormatBreadcrumb(string? breadcrumb)
        {
            if (string.IsNullOrEmpty(breadcrumb))
                return string.Empty;
                
            return $"  {Arrow} {breadcrumb}";
        }
    }
}