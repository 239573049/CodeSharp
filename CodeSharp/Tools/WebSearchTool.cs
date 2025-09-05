using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace CodeSharp.Tools;

public class WebSearchTool: ITool
{
    public string Name => "WebSearch";

    [KernelFunction("WebSearch"), Description(
         "\n- Allows Claude to search the web and use the results to inform responses\n- Provides up-to-date information for current events and recent data\n- Returns search result information formatted as search result blocks\n- Use this tool for accessing information beyond Claude's knowledge cutoff\n- Searches are performed automatically within a single API call\n\nUsage notes:\n  - Domain filtering is supported to include or block specific websites\n  - Web search is only available in the US\n  - Account for \"Today's date\" in <env>. For example, if <env> says \"Today's date: 2025-09-05\", and the user wants the latest docs, do not use 2024 in the search query. Use 2025.\n")]
    public async Task<string> ExecuteAsync(
        [Description("The search query to use")]
        string query,
        [Description("Only include search results from these domains")]
        string[]? allowed_domains,
        [Description("Never include search results from these domains")]
        string[]? blocked_domains
    )
    {
        await Task.CompletedTask;
    }
}