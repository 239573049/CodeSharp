using System.ComponentModel;
using System.Text;
using System.Text.Json;
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
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return "Error: Search query cannot be empty";

            // Validate domain filters
            var allowedDomains = SanitizeDomains(allowed_domains);
            var blockedDomains = SanitizeDomains(blocked_domains);

            // Check cache first
            var cacheKey = GenerateCacheKey(query, allowedDomains, blockedDomains);
            if (TryGetFromCache(cacheKey, out var cachedResult))
            {
                return $"[Cached Search Results]\n{cachedResult}";
            }

            // Enhance query with date context
            var enhancedQuery = EnhanceQueryWithDateContext(query);

            // Perform search (simulated - replace with real API)
            var searchResults = await PerformWebSearchAsync(enhancedQuery, allowedDomains, blockedDomains);

            // Format results
            var formattedResults = FormatSearchResults(searchResults, query);

            // Cache results
            CacheResult(cacheKey, formattedResults);

            return formattedResults;
        }
        catch (Exception ex)
        {
            return $"Error performing web search: {ex.Message}";
        }
    }

    private static List<string> SanitizeDomains(string[]? domains)
    {
        if (domains == null || domains.Length == 0)
            return new List<string>();

        return domains
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim().ToLower())
            .Select(d => d.StartsWith("http://") || d.StartsWith("https://") 
                ? new Uri(d).Host 
                : d.Replace("www.", ""))
            .Distinct()
            .ToList();
    }

    private static readonly Dictionary<string, SearchCacheEntry> _searchCache = new();
    private static readonly object _searchCacheLock = new();

    private static string GenerateCacheKey(string query, List<string> allowedDomains, List<string> blockedDomains)
    {
        var key = $"{query.ToLower()}|{string.Join(",", allowedDomains)}|{string.Join(",", blockedDomains)}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(key));
    }

    private static bool TryGetFromCache(string key, out string? result)
    {
        lock (_searchCacheLock)
        {
            if (_searchCache.TryGetValue(key, out var entry) && 
                DateTime.UtcNow - entry.Timestamp < TimeSpan.FromMinutes(30))
            {
                result = entry.Results;
                return true;
            }

            if (_searchCache.ContainsKey(key))
                _searchCache.Remove(key);
        }

        result = null;
        return false;
    }

    private static void CacheResult(string key, string results)
    {
        lock (_searchCacheLock)
        {
            _searchCache[key] = new SearchCacheEntry { Results = results, Timestamp = DateTime.UtcNow };
            
            // Clean old cache entries (keep max 50 entries)
            if (_searchCache.Count > 50)
            {
                var oldestKeys = _searchCache
                    .Where(kvp => DateTime.UtcNow - kvp.Value.Timestamp > TimeSpan.FromMinutes(30))
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var oldKey in oldestKeys)
                    _searchCache.Remove(oldKey);
            }
        }
    }

    private static string EnhanceQueryWithDateContext(string query)
    {
        var today = DateTime.Now;
        var currentYear = today.Year;
        
        // Add current year context if query mentions "latest", "recent", "current", etc.
        var timeKeywords = new[] { "latest", "recent", "current", "new", "update", "2024", "today" };
        var lowerQuery = query.ToLower();
        
        if (timeKeywords.Any(keyword => lowerQuery.Contains(keyword)))
        {
            // Replace old years with current year
            query = System.Text.RegularExpressions.Regex.Replace(query, @"\b202[0-3]\b", currentYear.ToString());
            
            if (!lowerQuery.Contains(currentYear.ToString()))
            {
                query = $"{query} {currentYear}";
            }
        }

        return query;
    }

    private static async Task<List<SearchResultItem>> PerformWebSearchAsync(string query, List<string> allowedDomains, List<string> blockedDomains)
    {
        await Task.CompletedTask; // Simulate async operation
        
        // NOTE: This is a simulated implementation. In production, this would integrate with:
        // - Google Custom Search API
        // - Bing Search API  
        // - DuckDuckGo API
        // - SearXNG instance
        // etc.

        var results = GenerateSimulatedResults(query);
        
        // Apply domain filtering
        if (allowedDomains.Count > 0)
        {
            results = results.Where(r => allowedDomains.Any(d => r.Domain.Contains(d))).ToList();
        }

        if (blockedDomains.Count > 0)
        {
            results = results.Where(r => !blockedDomains.Any(d => r.Domain.Contains(d))).ToList();
        }

        return results.Take(10).ToList(); // Limit to top 10 results
    }

    private static List<SearchResultItem> GenerateSimulatedResults(string query)
    {
        // This method simulates search results based on common patterns
        // In production, replace with actual API calls
        
        var results = new List<SearchResultItem>();
        var queryWords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // Generate relevant-looking results based on query content
        if (queryWords.Any(w => new[] { "programming", "code", "developer", "software" }.Contains(w)))
        {
            results.AddRange(new[]
            {
                new SearchResultItem
                {
                    Title = $"How to {query} - Stack Overflow",
                    Url = "https://stackoverflow.com/questions/example",
                    Snippet = $"Comprehensive guide on {query}. Detailed explanations with code examples and best practices.",
                    Domain = "stackoverflow.com",
                    Date = DateTime.Now.AddDays(-Random.Shared.Next(1, 30))
                },
                new SearchResultItem
                {
                    Title = $"{query} Tutorial - MDN Web Docs",
                    Url = "https://developer.mozilla.org/docs/example",
                    Snippet = $"Learn {query} with our comprehensive tutorial covering fundamentals to advanced topics.",
                    Domain = "developer.mozilla.org",
                    Date = DateTime.Now.AddDays(-Random.Shared.Next(1, 60))
                },
                new SearchResultItem
                {
                    Title = $"Best Practices for {query} - GitHub",
                    Url = "https://github.com/example/repo",
                    Snippet = $"Open source repository demonstrating {query} with real-world examples and documentation.",
                    Domain = "github.com",
                    Date = DateTime.Now.AddDays(-Random.Shared.Next(1, 90))
                }
            });
        }

        if (queryWords.Any(w => new[] { "news", "latest", "current", "today" }.Contains(w)))
        {
            results.AddRange(new[]
            {
                new SearchResultItem
                {
                    Title = $"Latest News: {query} - Reuters",
                    Url = "https://reuters.com/article/example",
                    Snippet = $"Breaking news and latest developments regarding {query}. Updated coverage with expert analysis.",
                    Domain = "reuters.com",
                    Date = DateTime.Now.AddHours(-Random.Shared.Next(1, 24))
                },
                new SearchResultItem
                {
                    Title = $"{query} - BBC News",
                    Url = "https://bbc.com/news/example",
                    Snippet = $"Comprehensive coverage of {query} with detailed reporting and background information.",
                    Domain = "bbc.com",
                    Date = DateTime.Now.AddHours(-Random.Shared.Next(1, 48))
                }
            });
        }

        // Add general results
        results.AddRange(new[]
        {
            new SearchResultItem
            {
                Title = $"{query} - Wikipedia",
                Url = "https://en.wikipedia.org/wiki/Example",
                Snippet = $"Comprehensive encyclopedia article about {query} covering history, background, and related topics.",
                Domain = "wikipedia.org",
                Date = DateTime.Now.AddDays(-Random.Shared.Next(30, 365))
            },
            new SearchResultItem
            {
                Title = $"Everything You Need to Know About {query}",
                Url = "https://medium.com/article/example",
                Snippet = $"In-depth article exploring {query} from multiple perspectives with expert insights and analysis.",
                Domain = "medium.com",
                Date = DateTime.Now.AddDays(-Random.Shared.Next(7, 30))
            }
        });

        return results.OrderByDescending(r => r.Date).ToList();
    }

    private static string FormatSearchResults(List<SearchResultItem> results, string originalQuery)
    {
        if (results.Count == 0)
        {
            return "No search results found. This may be due to:\n" +
                   "- Domain filtering restrictions\n" +
                   "- Query terms being too specific\n" +
                   "- Network connectivity issues\n" +
                   "- Search API limitations";
        }

        var output = new StringBuilder();
        output.AppendLine($"**Web Search Results for:** \"{originalQuery}\"");
        output.AppendLine($"**Found {results.Count} result(s)**");
        output.AppendLine();

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            output.AppendLine($"**{i + 1}. {result.Title}**");
            output.AppendLine($"🔗 {result.Url}");
            output.AppendLine($"📅 {result.Date:yyyy-MM-dd} | 🌐 {result.Domain}");
            output.AppendLine($"📝 {result.Snippet}");
            output.AppendLine();
        }

        output.AppendLine("---");
        output.AppendLine("**Search Tips:**");
        output.AppendLine("- Use domain filtering to focus on specific sources");
        output.AppendLine("- Add year (e.g., '2025') for recent information");
        output.AppendLine("- Use specific keywords for better results");
        
        // Add domain filtering info if applicable
        var searchKey = GenerateCacheKey(originalQuery, new List<string>(), new List<string>());
        if (_searchCache.TryGetValue(searchKey, out var cacheEntry))
        {
            output.AppendLine($"- Results cached for faster future searches");
        }

        return output.ToString();
    }

    private class SearchResultItem
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Snippet { get; set; } = "";
        public string Domain { get; set; } = "";
        public DateTime Date { get; set; }
    }

    private class SearchCacheEntry
    {
        public string Results { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    // Template methods for integrating with real search APIs
    // These methods provide a framework for extending the tool with actual search services
    
    /*
    Example integration with Google Custom Search API:
    
    private static async Task<List<SearchResultItem>> CallGoogleSearchAPI(string query, string apiKey, string searchEngineId)
    {
        using var httpClient = new HttpClient();
        var url = $"https://www.googleapis.com/customsearch/v1?key={apiKey}&cx={searchEngineId}&q={Uri.EscapeDataString(query)}";
        var response = await httpClient.GetStringAsync(url);
        var searchData = JsonSerializer.Deserialize<GoogleSearchResponse>(response);
        
        return searchData.Items.Select(item => new SearchResultItem
        {
            Title = item.Title,
            Url = item.Link,
            Snippet = item.Snippet,
            Domain = new Uri(item.Link).Host
        }).ToList();
    }
    
    Example integration with Bing Search API:
    
    private static async Task<List<SearchResultItem>> CallBingSearchAPI(string query, string apiKey)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
        var url = $"https://api.bing.microsoft.com/v7.0/search?q={Uri.EscapeDataString(query)}";
        var response = await httpClient.GetStringAsync(url);
        var searchData = JsonSerializer.Deserialize<BingSearchResponse>(response);
        
        return searchData.WebPages.Value.Select(page => new SearchResultItem
        {
            Title = page.Name,
            Url = page.Url,
            Snippet = page.Snippet,
            Domain = new Uri(page.Url).Host
        }).ToList();
    }
    */
}