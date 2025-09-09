using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;

namespace CodeSharp.Tools;

public class GrepTool : ITool
{
    public string Name => "Grep";

    [KernelFunction("Grep"), Description(
         "A powerful search tool built on ripgrep\n\n  Usage:\n  - ALWAYS use Grep for search tasks. NEVER invoke `grep` or `rg` as a Bash command. The Grep tool has been optimized for correct permissions and access.\n  - Supports full regex syntax (e.g., \"log.*Error\", \"function\\s+\\w+\")\n  - Filter files with glob parameter (e.g., \"*.js\", \"**/*.tsx\") or type parameter (e.g., \"js\", \"py\", \"rust\")\n  - Output modes: \"content\" shows matching lines, \"files_with_matches\" shows only file paths (default), \"count\" shows match counts\n  - Use Task tool for open-ended searches requiring multiple rounds\n  - Pattern syntax: Uses ripgrep (not grep) - literal braces need escaping (use `interface\\{\\}` to find `interface{}` in Go code)\n  - Multiline matching: By default patterns match within single lines only. For cross-line patterns like `struct \\{[\\s\\S]*?field`, use `multiline: true`\n")]
    public async Task<string> ExecuteAsync(
        [Description("The regular expression pattern to search for in file contents")]
        string pattern,
        [Description("File or directory to search in (rg PATH). Defaults to current working directory.")]
        string? path,
        [Description("Glob pattern to filter files (e.g. \"*.js\", \"*.{ts,tsx}\") - maps to rg --glob")]
        string? glob,
        [Description(
            "Output mode: \"content\" shows matching lines (supports -A/-B/-C context, -n line numbers, head_limit), \"files_with_matches\" shows file paths (supports head_limit), \"count\" shows match counts (supports head_limit). Defaults to \"files_with_matches\".")]
        string? output_mode
        // [Description(
        //     "Number of lines to show before each match (rg -B). Requires output_mode: \"content\", ignored otherwise.")]
        // int? B,
        // [Description(
        //     "Number of lines to show after each match (rg -A). Requires output_mode: \"content\", ignored otherwise.")]
        // int? A,
        // [Description(
        //     "Limit output to first N lines/entries, equivalent to \"| head -N\". Works across all output modes: content (limits output lines), files_with_matches (limits file paths), count (limits count entries). When unspecified, shows all results from ripgrep.")]
        // int? head_limit,
        // [Description(
        //     "Number of lines to show before and after each match (rg -C). Requires output_mode: \"content\", ignored otherwise.")]
        // int? C,
        // [Description("Show line numbers in output (rg -n). Requires output_mode: \"content\", ignored otherwise.")]
        // bool n = false,
        // [Description("Case insensitive search (rg -i)")]
        // bool i = false,
        // [Description(
        //     "File type to search (rg --type). Common types: js, py, rust, go, java, etc. More efficient than include for standard file types.")]
        // string? type = null,
        // [Description(
        //     "Enable multiline mode where . matches newlines and patterns can span lines (rg -U --multiline-dotall). Default: false.")]
        // bool multiline = false
    )
    {
        try
        {
            // if (string.IsNullOrWhiteSpace(pattern))
            //     return "Error: Pattern cannot be empty";
            //
            // // Set defaults
            // var searchPath = string.IsNullOrWhiteSpace(path) ? Environment.CurrentDirectory : path;
            // var mode = string.IsNullOrWhiteSpace(output_mode) ? "files_with_matches" : output_mode.ToLower();
            //
            // // Validate output mode
            // if (!new[] { "content", "files_with_matches", "count" }.Contains(mode))
            //     return "Error: Invalid output_mode. Use 'content', 'files_with_matches', or 'count'";
            //
            // // Get files to search
            // var filesToSearch = await GetFilesToSearchAsync(searchPath, glob, type);
            //
            // if (filesToSearch.Count == 0)
            //     return "No files found matching the specified criteria";
            //
            // // Prepare regex
            // var regexOptions = RegexOptions.Compiled;
            // if (i) regexOptions |= RegexOptions.IgnoreCase;
            // if (multiline) regexOptions |= RegexOptions.Singleline | RegexOptions.Multiline;
            //
            // Regex regex;
            // try
            // {
            //     regex = new Regex(pattern, regexOptions);
            // }
            // catch (ArgumentException ex)
            // {
            //     return $"Error: Invalid regex pattern - {ex.Message}";
            // }
            //
            // // Search files
            // var results = new List<SearchResult>();
            //
            // foreach (var file in filesToSearch)
            // {
            //     try
            //     {
            //         var searchResult = await SearchFileAsync(file, regex, mode, A ?? 0, B ?? 0, C ?? 0, n, multiline);
            //         if (searchResult != null && searchResult.HasMatches)
            //         {
            //             results.Add(searchResult);
            //         }
            //     }
            //     catch (Exception)
            //     {
            //         // Skip files that can't be read (permissions, binary, etc.)
            //         continue;
            //     }
            // }
            //
            // // Format output
            // return FormatResults(results, mode, head_limit);

            return "";
        }
        catch (Exception ex)
        {
            return $"Error during search: {ex.Message}";
        }
    }

    private static Task<List<string>> GetFilesToSearchAsync(string searchPath, string? glob, string? type)
    {
        var files = new List<string>();

        if (File.Exists(searchPath))
        {
            files.Add(searchPath);
            return Task.FromResult(files);
        }

        if (!Directory.Exists(searchPath))
            return Task.FromResult(files);

        // Get all files recursively
        var allFiles = Directory.GetFiles(searchPath, "*", SearchOption.AllDirectories);

        // Apply type filter
        if (!string.IsNullOrWhiteSpace(type))
        {
            var extensions = GetExtensionsForType(type.ToLower());
            allFiles = allFiles.Where(f => extensions.Contains(Path.GetExtension(f).ToLower())).ToArray();
        }

        // Apply glob filter
        if (!string.IsNullOrWhiteSpace(glob))
        {
            var globRegex = ConvertGlobToRegex(glob);
            allFiles = allFiles.Where(f => globRegex.IsMatch(Path.GetFileName(f))).ToArray();
        }

        // Filter out likely binary files
        files.AddRange(allFiles.Where(IsTextFile));

        return Task.FromResult(files);
    }

    private static async Task<SearchResult?> SearchFileAsync(string filePath, Regex regex, string mode, int after,
        int before, int context, bool showLineNumbers, bool multiline)
    {
        var content = await File.ReadAllTextAsync(filePath);
        var lines = content.Split('\n');
        var matches = new List<MatchInfo>();

        if (context > 0)
        {
            after = Math.Max(after, context);
            before = Math.Max(before, context);
        }

        if (multiline)
        {
            // Search entire content for multiline patterns
            var multilineMatches = regex.Matches(content);
            if (multilineMatches.Count > 0)
            {
                return new SearchResult
                {
                    FilePath = filePath,
                    MatchCount = multilineMatches.Count,
                    HasMatches = true,
                    Matches = multilineMatches.Cast<Match>().Select((m, i) => new MatchInfo
                    {
                        LineNumber = GetLineNumber(content, m.Index),
                        Content = m.Value,
                        Context = new List<string>()
                    }).ToList()
                };
            }
        }
        else
        {
            // Search line by line
            for (int i = 0; i < lines.Length; i++)
            {
                if (regex.IsMatch(lines[i]))
                {
                    var match = new MatchInfo
                    {
                        LineNumber = i + 1,
                        Content = lines[i],
                        Context = new List<string>()
                    };

                    // Add context lines if needed
                    if (mode == "content")
                    {
                        for (int j = Math.Max(0, i - before); j <= Math.Min(lines.Length - 1, i + after); j++)
                        {
                            if (j != i)
                            {
                                match.Context.Add($"{j + 1}: {lines[j]}");
                            }
                        }
                    }

                    matches.Add(match);
                }
            }
        }

        if (matches.Count > 0)
        {
            return new SearchResult
            {
                FilePath = filePath,
                MatchCount = matches.Count,
                HasMatches = true,
                Matches = matches
            };
        }

        return null;
    }

    private static string FormatResults(List<SearchResult> results, string mode, int? headLimit)
    {
        if (results.Count == 0)
            return "No matches found";

        var output = new StringBuilder();
        var totalOutputLines = 0;
        var limit = headLimit ?? int.MaxValue;

        switch (mode)
        {
            case "files_with_matches":
                foreach (var result in results.Take(limit))
                {
                    output.AppendLine(result.FilePath);
                }

                break;

            case "count":
                foreach (var result in results.Take(limit))
                {
                    output.AppendLine($"{result.FilePath}: {result.MatchCount}");
                }

                break;

            case "content":
                foreach (var result in results)
                {
                    if (totalOutputLines >= limit) break;

                    output.AppendLine($"== {result.FilePath} ==");
                    totalOutputLines++;

                    foreach (var match in result.Matches)
                    {
                        if (totalOutputLines >= limit) break;

                        output.AppendLine($"{match.LineNumber}: {match.Content}");
                        totalOutputLines++;

                        foreach (var contextLine in match.Context)
                        {
                            if (totalOutputLines >= limit) break;
                            output.AppendLine($"  {contextLine}");
                            totalOutputLines++;
                        }
                    }

                    output.AppendLine();
                    totalOutputLines++;
                }

                break;
        }

        return output.ToString().Trim();
    }

    private static string[] GetExtensionsForType(string type)
    {
        return type switch
        {
            "js" => new[] { ".js", ".jsx", ".mjs" },
            "ts" => new[] { ".ts", ".tsx" },
            "py" => new[] { ".py", ".pyx", ".pyi" },
            "cs" => new[] { ".cs" },
            "java" => new[] { ".java" },
            "cpp" => new[] { ".cpp", ".cxx", ".cc", ".c++", ".c", ".h", ".hpp" },
            "rust" => new[] { ".rs" },
            "go" => new[] { ".go" },
            "php" => new[] { ".php" },
            "rb" => new[] { ".rb" },
            "html" => new[] { ".html", ".htm" },
            "css" => new[] { ".css", ".scss", ".sass", ".less" },
            "json" => new[] { ".json" },
            "xml" => new[] { ".xml" },
            "yaml" => new[] { ".yaml", ".yml" },
            "md" => new[] { ".md", ".markdown" },
            _ => new[] { $".{type}" }
        };
    }

    private static Regex ConvertGlobToRegex(string glob)
    {
        var pattern = "^" + Regex.Escape(glob)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".")
            .Replace(@"\{", "(")
            .Replace(@"\}", ")")
            .Replace(",", "|") + "$";
        return new Regex(pattern, RegexOptions.IgnoreCase);
    }

    private static bool IsTextFile(string filePath)
    {
        try
        {
            var extension = Path.GetExtension(filePath).ToLower();
            if (string.IsNullOrEmpty(extension)) return false;

            // Common binary file extensions to skip
            var binaryExtensions = new[]
            {
                ".exe", ".dll", ".bin", ".zip", ".rar", ".7z", ".tar", ".gz",
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".svg", ".pdf", ".doc", ".docx",
                ".xls", ".xlsx", ".ppt", ".pptx", ".mp3", ".mp4", ".avi", ".mov", ".wmv"
            };

            return !binaryExtensions.Contains(extension);
        }
        catch
        {
            return false;
        }
    }

    private static int GetLineNumber(string content, int charIndex)
    {
        return content.Take(charIndex).Count(c => c == '\n') + 1;
    }

    private class SearchResult
    {
        public string FilePath { get; set; } = "";
        public int MatchCount { get; set; }
        public bool HasMatches { get; set; }
        public List<MatchInfo> Matches { get; set; } = new();
    }

    private class MatchInfo
    {
        public int LineNumber { get; set; }
        public string Content { get; set; } = "";
        public List<string> Context { get; set; } = new();
    }
}