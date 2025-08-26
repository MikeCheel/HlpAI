using Microsoft.Extensions.Logging;
using HlpAI.FileExtractors;
using HlpAI.Models;

namespace HlpAI.Utilities
{
    // File Audit Utility
    public static class FileAuditUtility
    {
        public static void AuditDirectory(string rootPath, ILogger? logger = null, TextWriter? output = null, long maxFileSizeBytes = 100 * 1024 * 1024)
        {
            var writer = output ?? Console.Out;
            writer.WriteLine($"🔍 Auditing directory: {rootPath}");
            writer.WriteLine($"⏰ Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

            if (!Directory.Exists(rootPath))
            {
                writer.WriteLine("❌ Directory does not exist!");
                return;
            }

            var allFiles = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories);
            var extractors = new List<IFileExtractor>
            {
                new TextFileExtractor(),
                new HtmlFileExtractor(),
                new PdfFileExtractor(),
                new ChmFileExtractor(logger),
                new HhcFileExtractor()
            };

            var results = new
            {
                Total = allFiles.Length,
                Supported = new List<string>(),
                Unsupported = new List<(string file, string reason)>(),
                Skipped = new List<(string file, string reason)>(),
                TooLarge = new List<(string file, long size)>(),
                ByExtension = new Dictionary<string, int>()
            };

            foreach (var file in allFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    var extension = Path.GetExtension(file);

                    // Count by extension
                    results.ByExtension[extension] = results.ByExtension.GetValueOrDefault(extension, 0) + 1;

                    // Check if should skip
                    if (ShouldSkipFileAudit(file, fileInfo, maxFileSizeBytes, out string skipReason))
                    {
                        if (fileInfo.Length > maxFileSizeBytes)
                        {
                            results.TooLarge.Add((file, fileInfo.Length));
                        }
                        else
                        {
                            results.Skipped.Add((file, skipReason));
                        }
                        continue;
                    }

                    // Check if extractor available
                    var extractor = extractors.FirstOrDefault(e => e.CanHandle(file));
                    if (extractor != null)
                    {
                        results.Supported.Add(file);
                    }
                    else
                    {
                        results.Unsupported.Add((file, $"No extractor for {extension}"));
                    }
                }
                catch (Exception ex)
                {
                    results.Unsupported.Add((file, $"Error: {ex.Message}"));
                }
            }

            // Display results
            writer.WriteLine("📊 AUDIT SUMMARY");
            writer.WriteLine("================");
            writer.WriteLine($"Total Files: {results.Total}");
            writer.WriteLine($"✅ Indexable: {results.Supported.Count} ({results.Supported.Count * 100.0 / results.Total:F1}%)");
            writer.WriteLine($"❌ Not Indexable: {results.Unsupported.Count}");
            writer.WriteLine($"⭐️ Skipped: {results.Skipped.Count}");
            writer.WriteLine($"📦 Too Large: {results.TooLarge.Count}");

            writer.WriteLine("\n📈 BY FILE TYPE");
            writer.WriteLine("===============");
            foreach (var ext in results.ByExtension.OrderByDescending(x => x.Value))
            {
                var extName = string.IsNullOrEmpty(ext.Key) ? "(no extension)" : ext.Key;
                var supportedCount = results.Supported.Count(f => string.Equals(Path.GetExtension(f), ext.Key, StringComparison.OrdinalIgnoreCase));
                var supportStatus = supportedCount > 0 ? "✅" : "❌";
                writer.WriteLine($"{supportStatus} {extName}: {ext.Value} files ({supportedCount} indexable)");
            }

            if (results.Unsupported.Count > 0)
            {
                writer.WriteLine("\n❌ UNSUPPORTED FILES (sample)");
                writer.WriteLine("=============================");
                foreach (var (file, reason) in results.Unsupported.Take(10))
                {
                    writer.WriteLine($"📄 {Path.GetFileName(file)} - {reason}");
                }
                if (results.Unsupported.Count > 10)
                {
                    writer.WriteLine($"... and {results.Unsupported.Count - 10} more files");
                }
            }

            if (results.TooLarge.Count > 0)
            {
                writer.WriteLine($"\n📦 LARGE FILES (>{maxFileSizeBytes / (1024 * 1024)}MB)");
                writer.WriteLine("=======================");
                foreach (var (file, size) in results.TooLarge.OrderByDescending(x => x.size).Take(5))
                {
                    writer.WriteLine($"📄 {Path.GetFileName(file)} - {size / (1024 * 1024):F1} MB");
                }
            }

            // Recommendations
            writer.WriteLine("\n💡 RECOMMENDATIONS");
            writer.WriteLine("==================");

            var unsupportedExtensions = results.Unsupported
                .Where(x => x.reason.StartsWith("No extractor"))
                .GroupBy(x => Path.GetExtension(x.file))
                .Where(g => g.Count() > 1)
                .OrderByDescending(g => g.Count());

            foreach (var extGroup in unsupportedExtensions.Take(3))
            {
                var ext = extGroup.Key;
                writer.WriteLine($"• Consider adding support for {ext} files ({extGroup.Count()} files found)");
            }

            if (results.TooLarge.Count > 0)
            {
                writer.WriteLine($"• Consider splitting or excluding {results.TooLarge.Count} large files");
            }

            var lowValueExtensions = new[] { ".tmp", ".log", ".cache", ".bak" };
            var lowValueCount = results.ByExtension
                .Where(x => lowValueExtensions.Contains(x.Key, StringComparer.OrdinalIgnoreCase))
                .Sum(x => x.Value);

            if (lowValueCount > 0)
            {
                writer.WriteLine($"• Consider excluding {lowValueCount} temporary/log files to improve indexing speed");
            }

            writer.WriteLine($"\n✨ Audit completed in {DateTime.Now:HH:mm:ss}");
        }

        private static bool ShouldSkipFileAudit(string filePath, FileInfo fileInfo, long maxFileSizeBytes, out string reason)
        {
            var fileName = Path.GetFileName(filePath);
            var fileExtension = Path.GetExtension(filePath);

            if (fileName.StartsWith('.'))
            {
                reason = "Hidden file";
                return true;
            }

            if (fileInfo.Attributes.HasFlag(FileAttributes.System))
            {
                reason = "System file";
                return true;
            }

            if (fileInfo.Length > maxFileSizeBytes)
            {
                reason = $"Too large ({fileInfo.Length / (1024 * 1024):F1} MB)";
                return true;
            }

            if (fileName.EndsWith(".db") || fileName.EndsWith(".sqlite"))
            {
                reason = "Database file";
                return true;
            }

            var binaryExtensions = new[] { ".exe", ".dll", ".bin", ".so", ".dylib" };
            if (binaryExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
            {
                reason = "Binary executable";
                return true;
            }

            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" };
            if (imageExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
            {
                reason = "Image file";
                return true;
            }

            var mediaExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mp3", ".wav", ".flac" };
            if (mediaExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
            {
                reason = "Media file";
                return true;
            }

            var archiveExtensions = new[] { ".zip", ".rar", ".7z", ".tar", ".gz" };
            if (archiveExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
            {
                reason = "Archive file";
                return true;
            }

            reason = "";
            return false;
        }
    }
}