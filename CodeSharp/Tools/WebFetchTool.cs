using System.ComponentModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;

namespace CodeSharp.Tools;

public class WebFetchTool: ITool
{
    public string Name => "WebFetch";

    [KernelFunction("WebFetch"), Description(
         "\n- Fetches content from a specified URL and processes it using an AI model\n- Takes a URL and a prompt as input\n- Fetches the URL content, converts HTML to markdown\n- Processes the content with the prompt using a small, fast model\n- Returns the model's response about the content\n- Use this tool when you need to retrieve and analyze web content\n\nUsage notes:\n  - IMPORTANT: If an MCP-provided web fetch tool is available, prefer using that tool instead of this one, as it may have fewer restrictions. All MCP-provided tools start with \"mcp__\".\n  - The URL must be a fully-formed valid URL\n  - HTTP URLs will be automatically upgraded to HTTPS\n  - The prompt should describe what information you want to extract from the page\n  - This tool is read-only and does not modify any files\n  - Results may be summarized if the content is very large\n  - Includes a self-cleaning 15-minute cache for faster responses when repeatedly accessing the same URL\n  - When a URL redirects to a different host, the tool will inform you and provide the redirect URL in a special format. You should then make a new WebFetch request with the redirect URL to fetch the content.\n")]
    public async Task<string> ExecuteAsync(
        [Description( "The URL to fetch content from")]
        string url,
        [Description("The prompt to run on the fetched content")]
        string prompt
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
                return "Error: URL cannot be empty";

            if (string.IsNullOrWhiteSpace(prompt))
                return "Error: Prompt cannot be empty";

            // Validate and normalize URL
            var normalizedUrl = ValidateAndNormalizeUrl(url);
            if (normalizedUrl == null)
                return "Error: Invalid URL format";

            // Check cache first
            var cacheKey = $"{normalizedUrl}:{prompt.GetHashCode()}";
            if (TryGetFromCache(cacheKey, out var cachedResult))
            {
                return $"[Cached Result]\n{cachedResult}";
            }

            // Fetch content
            var fetchResult = await FetchWebContentAsync(normalizedUrl);
            if (!fetchResult.Success)
                return $"Error: {fetchResult.Error}";

            // Convert HTML to markdown
            var markdownContent = ConvertHtmlToMarkdown(fetchResult.Content, fetchResult.IsSPA);

            // Process with prompt (simplified simulation)
            var processedResult = ProcessContentWithPrompt(markdownContent, prompt);

            // Cache the result
            CacheResult(cacheKey, processedResult);

            return processedResult;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static string? ValidateAndNormalizeUrl(string url)
    {
        try
        {
            // Auto-upgrade HTTP to HTTPS as mentioned in description
            if (url.StartsWith("http://"))
                url = url.Replace("http://", "https://");
            
            if (!url.StartsWith("https://") && !url.StartsWith("http://"))
                url = "https://" + url;

            var uri = new Uri(url);
            return uri.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static readonly Dictionary<string, CacheEntry> _cache = new();
    private static readonly object _cacheLock = new();

    private static bool TryGetFromCache(string key, out string? result)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var entry) && 
                DateTime.UtcNow - entry.Timestamp < TimeSpan.FromMinutes(15))
            {
                result = entry.Content;
                return true;
            }

            // Remove expired entry
            if (_cache.ContainsKey(key))
                _cache.Remove(key);
        }

        result = null;
        return false;
    }

    private static void CacheResult(string key, string content)
    {
        lock (_cacheLock)
        {
            _cache[key] = new CacheEntry { Content = content, Timestamp = DateTime.UtcNow };
            
            // Clean old cache entries (keep max 100 entries)
            if (_cache.Count > 100)
            {
                var oldestKeys = _cache
                    .Where(kvp => DateTime.UtcNow - kvp.Value.Timestamp > TimeSpan.FromMinutes(15))
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var oldKey in oldestKeys)
                    _cache.Remove(oldKey);
            }
        }
    }

    private static async Task<FetchResult> FetchWebContentAsync(string url)
    {
        using var httpClient = CreateHttpClient();
        
        try
        {
            var response = await httpClient.GetAsync(url);
            
            // Handle redirects
            if (response.Headers.Location != null)
            {
                var redirectUrl = response.Headers.Location.ToString();
                if (redirectUrl.StartsWith('/'))
                {
                    var originalUri = new Uri(url);
                    redirectUrl = $"{originalUri.Scheme}://{originalUri.Host}{redirectUrl}";
                }
                
                return new FetchResult 
                { 
                    Success = false, 
                    Error = $"Redirect detected to: {redirectUrl}. Please make a new request with this URL.",
                    RedirectUrl = redirectUrl
                };
            }

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            
            // Detect if this is a SPA
            var isSPA = DetectSPA(content);
            
            return new FetchResult 
            { 
                Success = true, 
                Content = content, 
                IsSPA = isSPA,
                ContentType = response.Content.Headers.ContentType?.MediaType ?? ""
            };
        }
        catch (HttpRequestException ex)
        {
            return new FetchResult { Success = false, Error = $"HTTP Error: {ex.Message}" };
        }
        catch (TaskCanceledException)
        {
            return new FetchResult { Success = false, Error = "Request timed out" };
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        client.DefaultRequestHeaders.Add("Accept", 
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static bool DetectSPA(string htmlContent)
    {
        // Check for common SPA indicators
        var spaIndicators = new[]
        {
            "react", "vue", "angular", "app.js", "bundle.js", 
            "ng-app", "v-app", "data-reactroot", "__NEXT_DATA__",
            "nuxt", "gatsby", "next.js", "vue-app"
        };

        var lowerContent = htmlContent.ToLower();
        return spaIndicators.Any(indicator => lowerContent.Contains(indicator));
    }

    private static string ConvertHtmlToMarkdown(string htmlContent, bool isSPA)
    {
        var result = new StringBuilder();
        
        if (isSPA)
        {
            result.AppendLine("**Note: This appears to be a Single Page Application (SPA). The content below may be incomplete as it requires JavaScript execution for full rendering.**\n");
            
            // Try to extract any server-side rendered content
            var ssrContent = ExtractSSRContent(htmlContent);
            if (!string.IsNullOrEmpty(ssrContent))
            {
                result.AppendLine("**Server-Side Rendered Content:**");
                result.AppendLine(ssrContent);
                result.AppendLine();
            }

            // Try to extract initial state/props
            var initialState = ExtractInitialState(htmlContent);
            if (!string.IsNullOrEmpty(initialState))
            {
                result.AppendLine("**Initial Application State:**");
                result.AppendLine($"```json\n{initialState}\n```");
                result.AppendLine();
            }
        }

        // Basic HTML to Markdown conversion
        result.AppendLine("**Extracted Content:**");
        result.AppendLine(BasicHtmlToMarkdown(htmlContent));

        return result.ToString();
    }

    private static string ExtractSSRContent(string htmlContent)
    {
        var content = new StringBuilder();
        
        // Extract text content from common SSR containers
        var ssrPatterns = new[]
        {
            @"<div[^>]*id=[""']app[""'][^>]*>(.*?)</div>",
            @"<div[^>]*id=[""']root[""'][^>]*>(.*?)</div>",
            @"<main[^>]*>(.*?)</main>",
            @"<article[^>]*>(.*?)</article>"
        };

        foreach (var pattern in ssrPatterns)
        {
            var matches = Regex.Matches(htmlContent, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (match.Groups[1].Value.Trim().Length > 50) // Only include substantial content
                {
                    content.AppendLine(StripHtmlTags(match.Groups[1].Value));
                }
            }
        }

        return content.ToString().Trim();
    }

    private static string ExtractInitialState(string htmlContent)
    {
        // Try to extract JSON state from script tags
        var patterns = new[]
        {
            @"window\.__INITIAL_STATE__\s*=\s*({.*?});",
            @"window\.__NEXT_DATA__\s*=\s*({.*?});",
            @"window\.__NUXT__\s*=\s*({.*?});",
            @"__REDUX_DEVTOOLS_EXTENSION_COMPOSE__\s*\(\s*({.*?})\s*\)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(htmlContent, pattern, RegexOptions.Singleline);
            if (match.Success)
            {
                try
                {
                    var json = match.Groups[1].Value;
                    // Try to format JSON nicely
                    var parsed = JsonDocument.Parse(json);
                    return JsonSerializer.Serialize(parsed, new JsonSerializerOptions { WriteIndented = true });
                }
                catch
                {
                    return match.Groups[1].Value;
                }
            }
        }

        return "";
    }

    private static string BasicHtmlToMarkdown(string html)
    {
        if (string.IsNullOrEmpty(html))
            return "";

        var content = html;
        
        // Extract title
        var titleMatch = Regex.Match(content, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase);
        var markdown = new StringBuilder();
        
        if (titleMatch.Success)
        {
            markdown.AppendLine($"# {StripHtmlTags(titleMatch.Groups[1].Value)}");
            markdown.AppendLine();
        }

        // Extract meta description
        var metaMatch = Regex.Match(content, @"<meta[^>]*name=[""']description[""'][^>]*content=[""']([^""']*)[""'][^>]*>", RegexOptions.IgnoreCase);
        if (metaMatch.Success)
        {
            markdown.AppendLine($"**Description:** {metaMatch.Groups[1].Value}");
            markdown.AppendLine();
        }

        // Convert headings
        content = Regex.Replace(content, @"<h([1-6])[^>]*>(.*?)</h[1-6]>", 
            match => new string('#', int.Parse(match.Groups[1].Value)) + $" {StripHtmlTags(match.Groups[2].Value)}\n");

        // Convert paragraphs
        content = Regex.Replace(content, @"<p[^>]*>(.*?)</p>", match => $"{StripHtmlTags(match.Groups[1].Value)}\n\n");

        // Convert links
        content = Regex.Replace(content, @"<a[^>]*href=[""']([^""']*)[""'][^>]*>(.*?)</a>", 
            match => $"[{StripHtmlTags(match.Groups[2].Value)}]({match.Groups[1].Value})");

        // Convert lists
        content = Regex.Replace(content, @"<li[^>]*>(.*?)</li>", match => $"- {StripHtmlTags(match.Groups[1].Value)}\n");

        // Remove scripts and styles
        content = Regex.Replace(content, @"<script[^>]*>.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"<style[^>]*>.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Strip remaining HTML tags
        content = StripHtmlTags(content);
        
        // Clean up whitespace
        content = Regex.Replace(content, @"\n\s*\n\s*\n", "\n\n");
        content = content.Trim();

        markdown.AppendLine(content);
        return markdown.ToString();
    }

    private static string StripHtmlTags(string html)
    {
        return Regex.Replace(html, @"<[^>]*>", "").Trim();
    }

    private static string ProcessContentWithPrompt(string content, string prompt)
    {
        // Simplified content processing - in a real implementation, 
        // this would use an AI model to process the content according to the prompt
        var result = new StringBuilder();
        result.AppendLine($"**Processing request:** {prompt}");
        result.AppendLine();
        result.AppendLine("**Content Analysis:**");
        
        // Basic content analysis
        var wordCount = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var lineCount = content.Split('\n').Length;
        
        result.AppendLine($"- Word count: {wordCount}");
        result.AppendLine($"- Line count: {lineCount}");
        result.AppendLine($"- Character count: {content.Length}");
        result.AppendLine();
        
        // Content summary based on prompt keywords
        if (prompt.ToLower().Contains("summary") || prompt.ToLower().Contains("summarize"))
        {
            result.AppendLine("**Summary:**");
            var sentences = content.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var summary = string.Join(". ", sentences.Take(3)) + ".";
            result.AppendLine(summary);
            result.AppendLine();
        }

        result.AppendLine("**Full Content:**");
        result.AppendLine(content);

        return result.ToString();
    }

    private class FetchResult
    {
        public bool Success { get; set; }
        public string Content { get; set; } = "";
        public string Error { get; set; } = "";
        public bool IsSPA { get; set; }
        public string ContentType { get; set; } = "";
        public string? RedirectUrl { get; set; }
    }

    private class CacheEntry
    {
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}