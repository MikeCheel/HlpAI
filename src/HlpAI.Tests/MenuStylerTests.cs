using System;
using System.IO;
using System.Threading.Tasks;
using TUnit.Core;
using TUnit.Assertions;
using HlpAI;

namespace HlpAI.Tests
{
    public class MenuStylerTests
    {
        [Test]
        public async Task CreateStyledHeader_WithDefaultWidth_ReturnsFormattedHeader()
        {
            // Arrange
            var title = "Test Header";
            
            // Act
            var result = MenuStyler.CreateStyledHeader(title);
            
            // Assert
            await Assert.That(result).Contains("╭"); // Top left corner
            await Assert.That(result).Contains("╮"); // Top right corner
            await Assert.That(result).Contains("╰"); // Bottom left corner
            await Assert.That(result).Contains("╯"); // Bottom right corner
            await Assert.That(result).Contains(title);
        }
        
        [Test]
        public async Task CreateStyledHeader_WithCustomWidth_ReturnsCorrectWidth()
        {
            // Arrange
            var title = "Test";
            var width = 40;
            
            // Act
            var result = MenuStyler.CreateStyledHeader(title, width);
            var lines = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Assert
            await Assert.That(lines.Length >= 3).IsTrue(); // Should have at least top, content, bottom lines
            // Check that all lines are within reasonable width bounds (accounting for box drawing characters)
            foreach (var line in lines)
            {
                await Assert.That(line.Length >= width - 2).IsTrue(); // Allow some flexibility for box drawing
                await Assert.That(line.Length <= width + 2).IsTrue(); // Allow some flexibility for box drawing
            }
        }
        
        [Test]
        public async Task CreateSectionSeparator_WithTitle_ReturnsFormattedSeparator()
        {
            // Arrange
            var sectionTitle = "Test Section";
            
            // Act
            var result = MenuStyler.CreateSectionSeparator(sectionTitle);
            
            // Assert
            await Assert.That(result).Contains(sectionTitle);
            await Assert.That(result).Contains("├");
            await Assert.That(result).Contains("┤");
            await Assert.That(result).Contains("─");
        }
        
        [Test]
        public async Task FormatMenuOption_WithIntegerNumber_ReturnsFormattedOption()
        {
            // Arrange
            var number = 5;
            var description = "Test Option";
            var icon = "🔧";
            
            // Act
            var result = MenuStyler.FormatMenuOption(number, description, icon);
            
            // Assert
            await Assert.That(result).Contains("05"); // Should be zero-padded
            await Assert.That(result).Contains(description);
            await Assert.That(result).Contains(icon);
        }
        
        [Test]
        public async Task FormatMenuOption_WithStringKey_ReturnsFormattedOption()
        {
            // Arrange
            var key = "c";
            var description = "Clear screen";
            var icon = "🖥️";
            
            // Act
            var result = MenuStyler.FormatMenuOption(key, description, icon);
            
            // Assert
            await Assert.That(result).Contains(key);
            await Assert.That(result).Contains(description);
            await Assert.That(result).Contains(icon);
        }
        
        [Test]
        public async Task FormatMenuOption_WithoutIcon_ReturnsFormattedOptionWithoutIcon()
        {
            // Arrange
            var number = 1;
            var description = "Test Option";
            
            // Act
            var result = MenuStyler.FormatMenuOption(number, description);
            
            // Assert
            await Assert.That(result).Contains("01");
            await Assert.That(result).Contains(description);
            await Assert.That(result).DoesNotContain("🔧"); // Should not contain any random icon
        }
        
        [Test]
        public async Task FormatStatusLine_WithSuccessStatus_ReturnsFormattedStatus()
        {
            // Arrange
            var label = "Connection";
            var value = "Active";
            
            // Act
            var result = MenuStyler.FormatStatusLine(label, value, true);
            
            // Assert
            await Assert.That(result).Contains(label);
            await Assert.That(result).Contains(value);
            await Assert.That(result).Contains(MenuStyler.CheckMark);
        }
        
        [Test]
        public async Task FormatStatusLine_WithFailureStatus_ReturnsFormattedStatus()
        {
            // Arrange
            var label = "Connection";
            var value = "Failed";
            
            // Act
            var result = MenuStyler.FormatStatusLine(label, value, false);
            
            // Assert
            await Assert.That(result).Contains(label);
            await Assert.That(result).Contains(value);
            await Assert.That(result).Contains(MenuStyler.CrossMark);
        }
        
        [Test]
        public async Task FormatStatusLine_WithCustomIcon_ReturnsFormattedStatusWithCustomIcon()
        {
            // Arrange
            var label = "Status";
            var value = "Custom";
            var customIcon = "⚠";
            
            // Act
            var result = MenuStyler.FormatStatusLine(label, value, true, customIcon);
            
            // Assert
            await Assert.That(result).Contains(label);
            await Assert.That(result).Contains(value);
            await Assert.That(result).Contains(customIcon);
            await Assert.That(result).DoesNotContain(MenuStyler.CheckMark);
        }
        
        [Test]
        public async Task CreateContentBox_WithContent_ReturnsBoxedContent()
        {
            // Arrange
            var content = "Test content\nSecond line";
            
            // Act
            var result = MenuStyler.CreateContentBox(content);
            
            // Assert
            await Assert.That(result).Contains("╭");
            await Assert.That(result).Contains("╮");
            await Assert.That(result).Contains("╰");
            await Assert.That(result).Contains("╯");
            await Assert.That(result).Contains("│");
            await Assert.That(result).Contains("Test content");
            await Assert.That(result).Contains("Second line");
        }
        
        [Test]
        public async Task CreateProgressIndicator_WithValidValues_ReturnsProgressBar()
        {
            // Arrange
            var current = 7;
            var total = 10;
            
            // Act
            var result = MenuStyler.CreateProgressIndicator(current, total);
            
            // Assert
            await Assert.That(result).Contains("[");
            await Assert.That(result).Contains("]");
            await Assert.That(result).Contains("70%");
            await Assert.That(result).Contains("█"); // Filled portion
            await Assert.That(result).Contains("░"); // Empty portion
        }
        
        [Test]
        public async Task FormatBreadcrumb_WithValidBreadcrumb_ReturnsFormattedBreadcrumb()
        {
            // Arrange
            var breadcrumb = "Home > Settings > AI Provider";
            
            // Act
            var result = MenuStyler.FormatBreadcrumb(breadcrumb);
            
            // Assert
            await Assert.That(result).Contains(MenuStyler.Arrow);
            await Assert.That(result).Contains(breadcrumb);
        }
        
        [Test]
        public async Task FormatBreadcrumb_WithEmptyBreadcrumb_ReturnsEmptyString()
        {
            // Arrange
            var breadcrumb = "";
            
            // Act
            var result = MenuStyler.FormatBreadcrumb(breadcrumb);
            
            // Assert
            await Assert.That(result).IsEqualTo(string.Empty);
        }
        
        [Test]
        public async Task FormatBreadcrumb_WithNullBreadcrumb_ReturnsEmptyString()
        {
            // Arrange
            string? breadcrumb = null;
            
            // Act
            var result = MenuStyler.FormatBreadcrumb(breadcrumb);
            
            // Assert
            await Assert.That(result).IsEqualTo(string.Empty);
        }
        
        [Test]
        public async Task WriteColored_WritesToConsole_WithoutException()
        {
            // Arrange
            var originalOut = Console.Out;
            var stringWriter = new StringWriter();
#pragma warning disable TUnit0055 // Overwriting the Console writer can break TUnit logging
            Console.SetOut(stringWriter);
#pragma warning restore TUnit0055
            
            try
            {
                // Act
                MenuStyler.WriteColored("Test", ConsoleColor.Red);
                
                // Assert
                var output = stringWriter.ToString();
                await Assert.That(output).Contains("Test");
            }
            finally
            {
                // Cleanup
#pragma warning disable TUnit0055 // Overwriting the Console writer can break TUnit logging
                Console.SetOut(originalOut);
#pragma warning restore TUnit0055
                stringWriter.Dispose();
            }
        }
        
        [Test]
        public async Task WriteColoredLine_WritesToConsole_WithoutException()
        {
            // Arrange
            var originalOut = Console.Out;
            var stringWriter = new StringWriter();
#pragma warning disable TUnit0055 // Overwriting the Console writer can break TUnit logging
            Console.SetOut(stringWriter);
#pragma warning restore TUnit0055
            
            try
            {
                // Act
                MenuStyler.WriteColoredLine("Test Line", ConsoleColor.Blue);
                
                // Assert
                var output = stringWriter.ToString();
                await Assert.That(output).Contains("Test Line");
            }
            finally
            {
                // Cleanup
#pragma warning disable TUnit0055 // Overwriting the Console writer can break TUnit logging
                Console.SetOut(originalOut);
#pragma warning restore TUnit0055
                stringWriter.Dispose();
            }
        }
    }
}