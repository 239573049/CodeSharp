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

            // Sanitize domain filters
            var allowedDomains = SanitizeDomains(allowed_domains);
            var blockedDomains = SanitizeDomains(blocked_domains);

            // Check cache first
            var cacheKey = GenerateCacheKey(query, allowedDomains, blockedDomains);
            if (TryGetFromCache(cacheKey, out var cachedResult))
            {
                return cachedResult!;
            }

            // Perform search using available APIs
            var searchResults = await CallSearchAPI(query, allowedDomains, blockedDomains);
            
            // Format and return results
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

    private static async Task<List<SearchResultItem>> CallSearchAPI(string query, List<string> allowedDomains, List<string> blockedDomains)
    {
        try
        {
            // Add current year context if query contains "latest", "recent", etc.
            var enhancedQuery = AddDateContextIfNeeded(query);
            
            // Try real search APIs first
            var results = new List<SearchResultItem>();
            
            // Google Search API
            if (!string.IsNullOrEmpty(SearchAPIConfig.GoogleAPIKey))
            {
                var googleResults = await TryGoogleSearch(enhancedQuery);
                results.AddRange(googleResults);
            }
            
            // Bing Search API  
            if (!string.IsNullOrEmpty(SearchAPIConfig.BingAPIKey))
            {
                var bingResults = await TryBingSearch(enhancedQuery);
                results.AddRange(bingResults);
            }
            
            // DuckDuckGo as fallback
            if (results.Count == 0)
            {
                var duckResults = await TryDuckDuckGoSearch(enhancedQuery);
                results.AddRange(duckResults);
            }
            
            // Apply domain filtering
            results = ApplyDomainFiltering(results, allowedDomains, blockedDomains);
            
            return results.Take(10).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SearchAPI] Error: {ex.Message}");
            return new List<SearchResultItem>();
        }
    }
    
    private static string AddDateContextIfNeeded(string query)
    {
        var lowerQuery = query.ToLower();
        var currentYear = DateTime.Now.Year;
        
        // Add current year if query contains time-sensitive keywords
        if (lowerQuery.Contains("latest") || lowerQuery.Contains("recent") || 
            lowerQuery.Contains("current") || lowerQuery.Contains("new"))
        {
            // Replace old years with current year
            query = System.Text.RegularExpressions.Regex.Replace(query, @"\b20(1[0-9]|2[0-4])\b", currentYear.ToString());
            
            // Add current year if not already present
            if (!lowerQuery.Contains(currentYear.ToString()))
            {
                query = $"{query} {currentYear}";
            }
        }
        
        return query;
    }
    
    private static async Task<List<SearchResultItem>> TryGoogleSearch(string query)
    {
        try
        {
            var url = $"https://www.googleapis.com/customsearch/v1?" +
                     $"key={SearchAPIConfig.GoogleAPIKey}" +
                     $"&cx={SearchAPIConfig.GoogleSearchEngineId}" +
                     $"&q={Uri.EscapeDataString(query)}" +
                     $"&num=10";
            
            var response = await HttpClientManager.GoogleClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<JsonElement>(response);
            
            var results = new List<SearchResultItem>();
            
            if (data.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("title", out var title) &&
                        item.TryGetProperty("link", out var link) &&
                        item.TryGetProperty("snippet", out var snippet))
                    {
                        results.Add(new SearchResultItem
                        {
                            Title = title.GetString() ?? "",
                            Url = link.GetString() ?? "",
                            Snippet = snippet.GetString() ?? "",
                            Domain = ExtractDomain(link.GetString()),
                            Date = DateTime.Now
                        });
                    }
                }
            }
            
            return results;
        }
        catch
        {
            return new List<SearchResultItem>();
        }
    }
    
    private static async Task<List<SearchResultItem>> TryBingSearch(string query)
    {
        try
        {
            var url = $"https://api.bing.microsoft.com/v7.0/search?" +
                     $"q={Uri.EscapeDataString(query)}" +
                     $"&count=10";
            
            var response = await HttpClientManager.BingClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<JsonElement>(response);
            
            var results = new List<SearchResultItem>();
            
            if (data.TryGetProperty("webPages", out var webPages) &&
                webPages.TryGetProperty("value", out var pages))
            {
                foreach (var page in pages.EnumerateArray())
                {
                    if (page.TryGetProperty("name", out var name) &&
                        page.TryGetProperty("url", out var url_prop) &&
                        page.TryGetProperty("snippet", out var snippet))
                    {
                        results.Add(new SearchResultItem
                        {
                            Title = name.GetString() ?? "",
                            Url = url_prop.GetString() ?? "",
                            Snippet = snippet.GetString() ?? "",
                            Domain = ExtractDomain(url_prop.GetString()),
                            Date = DateTime.Now
                        });
                    }
                }
            }
            
            return results;
        }
        catch
        {
            return new List<SearchResultItem>();
        }
    }
    
    private static string ExtractDomain(string? url)
    {
        try
        {
            return string.IsNullOrEmpty(url) ? "" : new Uri(url).Host;
        }
        catch
        {
            return "";
        }
    }

    private static List<SearchResultItem> ApplyDomainFiltering(List<SearchResultItem> results, List<string> allowedDomains, List<string> blockedDomains)
    {
        var filteredResults = results.AsEnumerable();
        
        // Apply allowed domains filter
        if (allowedDomains.Count > 0)
        {
            filteredResults = filteredResults.Where(r => 
                allowedDomains.Any(domain => 
                    r.Domain.Contains(domain, StringComparison.OrdinalIgnoreCase) ||
                    r.Url.Contains(domain, StringComparison.OrdinalIgnoreCase)));
        }

        // Apply blocked domains filter
        if (blockedDomains.Count > 0)
        {
            filteredResults = filteredResults.Where(r => 
                !blockedDomains.Any(domain => 
                    r.Domain.Contains(domain, StringComparison.OrdinalIgnoreCase) ||
                    r.Url.Contains(domain, StringComparison.OrdinalIgnoreCase)));
        }
        
        return filteredResults.ToList();
    }

    private static async Task<List<SearchResultItem>> TryDuckDuckGoSearch(string query)
    {
        try
        {
            var url = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(query)}&format=json&no_html=1&skip_disambig=1";
            var response = await HttpClientManager.SharedClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<JsonElement>(response);
            
            var results = new List<SearchResultItem>();
            
            if (data.TryGetProperty("RelatedTopics", out var topics))
            {
                foreach (var topic in topics.EnumerateArray().Take(10))
                {
                    if (topic.TryGetProperty("Text", out var text) &&
                        topic.TryGetProperty("FirstURL", out var firstUrl))
                    {
                        results.Add(new SearchResultItem
                        {
                            Title = ExtractTitleFromText(text.GetString() ?? ""),
                            Url = firstUrl.GetString() ?? "",
                            Snippet = text.GetString() ?? "",
                            Domain = ExtractDomain(firstUrl.GetString()),
                            Date = DateTime.Now
                        });
                    }
                }
            }
            
            return results;
        }
        catch
        {
            return new List<SearchResultItem>();
        }
    }

    private static string ExtractTitleFromText(string text)
    {
        // Extract title from DuckDuckGo text format
        var parts = text.Split('-', 2);
        return parts.Length > 0 ? parts[0].Trim() : text;
    }


    public class SearchResultItem
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Snippet { get; set; } = "";
        public string Domain { get; set; } = "";
        public DateTime Date { get; set; }
        public float Relevance { get; set; } = 0.5f;
    }

    private static string FormatSearchResults(List<SearchResultItem> results, string originalQuery)
    {
        if (results.Count == 0)
        {
            return "No search results found";
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

        return output.ToString();
    }

    private class SearchCacheEntry
    {
        public string Results { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Framework for integrating with real search APIs
    /// This section provides templates and interfaces for extending the tool with actual search services
    /// </summary>
    
    // Configuration for API integration
    public static class SearchAPIConfig
    {
        public static string? GoogleAPIKey { get; set; }
        public static string? GoogleSearchEngineId { get; set; }
        public static string? BingAPIKey { get; set; }
        public static string? DuckDuckGoEndpoint { get; set; }
        public static bool UseRealAPIs { get; set; } = false;
    }

    // Interface for real API integration
    public interface ISearchAPI
    {
        Task<List<SearchResultItem>> SearchAsync(string query, int maxResults = 10);
        string Name { get; }
        bool IsAvailable { get; }
    }

    // Shared HttpClient instances for better performance
    public static class HttpClientManager
    {
        private static readonly Lazy<HttpClient> _sharedClient = new(() =>
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", 
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        });

        private static readonly Lazy<HttpClient> _bingClient = new(() =>
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "CodeSharp/1.0");
            if (!string.IsNullOrEmpty(SearchAPIConfig.BingAPIKey))
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", SearchAPIConfig.BingAPIKey);
            }
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        });

        private static readonly Lazy<HttpClient> _googleClient = new(() =>
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "CodeSharp/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        });

        public static HttpClient SharedClient => _sharedClient.Value;
        public static HttpClient BingClient => _bingClient.Value;
        public static HttpClient GoogleClient => _googleClient.Value;
    }

    // Google Custom Search API implementation template
    public class GoogleSearchAPI : ISearchAPI
    {
        public string Name => "Google Custom Search";
        public bool IsAvailable => !string.IsNullOrEmpty(SearchAPIConfig.GoogleAPIKey) && 
                                   !string.IsNullOrEmpty(SearchAPIConfig.GoogleSearchEngineId);

        public async Task<List<SearchResultItem>> SearchAsync(string query, int maxResults = 10)
        {
            if (!IsAvailable) return new List<SearchResultItem>();
            
            try
            {
                var url = $"https://www.googleapis.com/customsearch/v1?" +
                         $"key={SearchAPIConfig.GoogleAPIKey}" +
                         $"&cx={SearchAPIConfig.GoogleSearchEngineId}" +
                         $"&q={Uri.EscapeDataString(query)}" +
                         $"&num={Math.Min(maxResults, 10)}";
                
                var response = await HttpClientManager.GoogleClient.GetStringAsync(url);
                var searchData = JsonSerializer.Deserialize<JsonElement>(response);
                
                var results = new List<SearchResultItem>();
                
                if (searchData.TryGetProperty("items", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        if (item.TryGetProperty("title", out var title) &&
                            item.TryGetProperty("link", out var link) &&
                            item.TryGetProperty("snippet", out var snippet))
                        {
                            results.Add(new SearchResultItem
                            {
                                Title = title.GetString() ?? "",
                                Url = link.GetString() ?? "",
                                Snippet = snippet.GetString() ?? "",
                                Domain = ExtractDomain(link.GetString()),
                                Date = DateTime.Now,
                                Relevance = 0.9f
                            });
                        }
                    }
                }
                
                return results;
            }
            catch (Exception)
            {
                // Log error and fallback to simulated results
                return new List<SearchResultItem>();
            }
        }
        
        private static string ExtractDomain(string? url)
        {
            try
            {
                return string.IsNullOrEmpty(url) ? "" : new Uri(url).Host;
            }
            catch
            {
                return "";
            }
        }
    }

    // Bing Search API implementation template
    public class BingSearchAPI : ISearchAPI
    {
        public string Name => "Bing Web Search";
        public bool IsAvailable => !string.IsNullOrEmpty(SearchAPIConfig.BingAPIKey);

        public async Task<List<SearchResultItem>> SearchAsync(string query, int maxResults = 10)
        {
            if (!IsAvailable) return new List<SearchResultItem>();
            
            try
            {
                var url = $"https://api.bing.microsoft.com/v7.0/search?" +
                         $"q={Uri.EscapeDataString(query)}" +
                         $"&count={Math.Min(maxResults, 20)}";
                
                var response = await HttpClientManager.BingClient.GetStringAsync(url);
                var searchData = JsonSerializer.Deserialize<JsonElement>(response);
                
                var results = new List<SearchResultItem>();
                
                if (searchData.TryGetProperty("webPages", out var webPages) &&
                    webPages.TryGetProperty("value", out var pages))
                {
                    foreach (var page in pages.EnumerateArray())
                    {
                        if (page.TryGetProperty("name", out var name) &&
                            page.TryGetProperty("url", out var url_prop) &&
                            page.TryGetProperty("snippet", out var snippet))
                        {
                            results.Add(new SearchResultItem
                            {
                                Title = name.GetString() ?? "",
                                Url = url_prop.GetString() ?? "",
                                Snippet = snippet.GetString() ?? "",
                                Domain = ExtractDomain(url_prop.GetString()),
                                Date = DateTime.Now,
                                Relevance = 0.85f
                            });
                        }
                    }
                }
                
                return results;
            }
            catch (Exception)
            {
                return new List<SearchResultItem>();
            }
        }
        
        private static string ExtractDomain(string? url)
        {
            try
            {
                return string.IsNullOrEmpty(url) ? "" : new Uri(url).Host;
            }
            catch
            {
                return "";
            }
        }
    }

    // API Manager to coordinate different search services
    public static class SearchAPIManager
    {
        private static readonly List<ISearchAPI> _apis = new()
        {
            new GoogleSearchAPI(),
            new BingSearchAPI()
        };

        public static async Task<List<SearchResultItem>> GetRealSearchResults(string query, int maxResults = 10)
        {
            var availableAPI = _apis.FirstOrDefault(api => api.IsAvailable);
            
            if (availableAPI != null)
            {
                return await availableAPI.SearchAsync(query, maxResults);
            }
            
            // Fallback to simulated results if no real APIs are available
            return new List<SearchResultItem>();
        }

        public static bool HasRealAPIAvailable => _apis.Any(api => api.IsAvailable);
    }
}