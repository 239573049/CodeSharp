using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace CodeSharp.Tools;

public class GrepTool: ITool
{
    public string Name => "Grep";

    [KernelFunction("Grep"), Description(
         "A powerful search tool built on ripgrep\n\n  Usage:\n  - ALWAYS use Grep for search tasks. NEVER invoke `grep` or `rg` as a Bash command. The Grep tool has been optimized for correct permissions and access.\n  - Supports full regex syntax (e.g., \"log.*Error\", \"function\\s+\\w+\")\n  - Filter files with glob parameter (e.g., \"*.js\", \"**/*.tsx\") or type parameter (e.g., \"js\", \"py\", \"rust\")\n  - Output modes: \"content\" shows matching lines, \"files_with_matches\" shows only file paths (default), \"count\" shows match counts\n  - Use Task tool for open-ended searches requiring multiple rounds\n  - Pattern syntax: Uses ripgrep (not grep) - literal braces need escaping (use `interface\\{\\}` to find `interface{}` in Go code)\n  - Multiline matching: By default patterns match within single lines only. For cross-line patterns like `struct \\{[\\s\\S]*?field`, use `multiline: true`\n")]
    public async Task<string> ExecuteAsync(
        [Description("The regular expression pattern to search for in file contents")]
        string pattern,
        [Description("File or directory to search in (rg PATH). Defaults to current working directory.")]
        string path,
        [Description("Glob pattern to filter files (e.g. \"*.js\", \"*.{ts,tsx}\") - maps to rg --glob")]
        string glob,
        [Description(
            "Output mode: \"content\" shows matching lines (supports -A/-B/-C context, -n line numbers, head_limit), \"files_with_matches\" shows file paths (supports head_limit), \"count\" shows match counts (supports head_limit). Defaults to \"files_with_matches\".")]
        string output_mode,
        [Description(
            "Number of lines to show before each match (rg -B). Requires output_mode: \"content\", ignored otherwise.")]
        int B,
        [Description(
            "Number of lines to show after each match (rg -A). Requires output_mode: \"content\", ignored otherwise.")]
        int A,
        [Description(
            "Number of lines to show before and after each match (rg -C). Requires output_mode: \"content\", ignored otherwise.")]
        int C,
        [Description("Show line numbers in output (rg -n). Requires output_mode: \"content\", ignored otherwise.")]
        bool n,
        [Description("Case insensitive search (rg -i)")]
        bool i,
        [Description(
            "File type to search (rg --type). Common types: js, py, rust, go, java, etc. More efficient than include for standard file types.")]
        string type,
        [Description(
            "Limit output to first N lines/entries, equivalent to \"| head -N\". Works across all output modes: content (limits output lines), files_with_matches (limits file paths), count (limits count entries). When unspecified, shows all results from ripgrep.")]
        int head_limit,
        [Description(
            "Enable multiline mode where . matches newlines and patterns can span lines (rg -U --multiline-dotall). Default: false.")]
        bool multiline = false
    )
    {
        await Task.CompletedTask;
    }
}