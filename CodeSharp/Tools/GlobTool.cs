using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace CodeSharp.Tools;

public class GlobTool : ITool
{
    public string Name => "Glob";

    [KernelFunction("Glob"), Description(
         "- Fast file pattern matching tool that works with any codebase size\n- Supports glob patterns like \"**/*.js\" or \"src/**/*.ts\"\n- Returns matching file paths sorted by modification time\n- Use this tool when you need to find files by name patterns\n- When you are doing an open ended search that may require multiple rounds of globbing and grepping, use the Agent tool instead\n- You have the capability to call multiple tools in a single response. It is always better to speculatively perform multiple searches as a batch that are potentially useful.")]
    public async Task<string> ExecuteAsync(
        [Description("The glob pattern to match files against")]
        string pattern,
        [Description(
            "The directory to search in. If not specified, the current working directory will be used. IMPORTANT: Omit this field to use the default directory. DO NOT enter \"undefined\" or \"null\" - simply omit it for the default behavior. Must be a valid directory path if provided.")]
        string? path
    )
    {
        await Task.CompletedTask;
        
        try
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return "Error: Pattern cannot be empty";

            var searchPath = string.IsNullOrWhiteSpace(path) ? Environment.CurrentDirectory : path;
            
            if (!Directory.Exists(searchPath))
                return $"Error: Directory '{searchPath}' does not exist";

            var files = new List<string>();
            
            // Support different glob patterns
            if (pattern.Contains("**"))
            {
                // Recursive pattern
                files.AddRange(GetFilesRecursively(searchPath, pattern));
            }
            else
            {
                // Simple pattern
                var searchPattern = pattern.Replace("*", "*");
                try
                {
                    files.AddRange(Directory.GetFiles(searchPath, searchPattern, SearchOption.TopDirectoryOnly));
                }
                catch (ArgumentException)
                {
                    // Fallback for complex patterns
                    files.AddRange(GetFilesRecursively(searchPath, pattern));
                }
            }

            if (files.Count == 0)
                return $"No files found matching pattern '{pattern}' in directory '{searchPath}'";

            // Sort by modification time (most recent first)
            var sortedFiles = files
                .Where(File.Exists)
                .Select(f => new { File = f, Modified = File.GetLastWriteTime(f) })
                .OrderByDescending(x => x.Modified)
                .Select(x => x.File)
                .ToList();

            return string.Join("\n", sortedFiles);
        }
        catch (UnauthorizedAccessException ex)
        {
            return $"Error: Access denied - {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static List<string> GetFilesRecursively(string directory, string pattern)
    {
        var files = new List<string>();
        var simplePattern = ExtractSimplePattern(pattern);
        
        try
        {
            // Search in current directory
            if (MatchesPattern(directory, pattern, isDirectory: true))
            {
                files.AddRange(Directory.GetFiles(directory, simplePattern, SearchOption.TopDirectoryOnly));
            }

            // Search in subdirectories
            foreach (var subDir in Directory.GetDirectories(directory))
            {
                if (pattern.StartsWith("**/") || pattern.Contains("**/"))
                {
                    files.AddRange(GetFilesRecursively(subDir, pattern));
                }
                else if (MatchesPattern(Path.GetFileName(subDir), ExtractDirectoryPattern(pattern), isDirectory: true))
                {
                    files.AddRange(GetFilesRecursively(subDir, pattern));
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }
        catch (DirectoryNotFoundException)
        {
            // Skip missing directories
        }

        return files.Where(f => MatchesPattern(Path.GetFileName(f), simplePattern, isDirectory: false)).ToList();
    }

    private static string ExtractSimplePattern(string globPattern)
    {
        // Extract file pattern from glob pattern
        var parts = globPattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.LastOrDefault() ?? "*";
    }

    private static string ExtractDirectoryPattern(string globPattern)
    {
        // Extract directory pattern from glob pattern
        var parts = globPattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[parts.Length - 2] : "*";
    }

    private static bool MatchesPattern(string text, string pattern, bool isDirectory)
    {
        if (string.IsNullOrEmpty(pattern) || pattern == "*")
            return true;

        if (pattern == "**")
            return isDirectory;

        // Simple wildcard matching
        return IsMatch(text, pattern);
    }

    private static bool IsMatch(string text, string pattern)
    {
        // Simple glob pattern matching
        if (pattern == "*") return true;
        if (!pattern.Contains('*')) return text.Equals(pattern, StringComparison.OrdinalIgnoreCase);

        var regex = "^" + pattern.Replace(".", @"\.").Replace("*", ".*").Replace("?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(text, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}