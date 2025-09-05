using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace CodeSharp.Tools;

public class BashOutputTool : ITool
{
    public string Name => "BashOutput";

    [KernelFunction("BashOutput"), Description(
        "\n- Retrieves output from a running or completed background bash shell\n- Takes a shell_id parameter identifying the shell\n- Always returns only new output since the last check\n- Returns stdout and stderr output along with shell status\n- Supports optional regex filtering to show only lines matching a pattern\n- Use this tool when you need to monitor or check the output of a long-running shell\n- Shell IDs can be found using the /bashes command\n")]
    public async Task<string> ExecuteAsync(
        [Description("The ID of the background shell to retrieve output from")]
        string bash_id,
        [Description("Optional regular expression to filter the output lines. Only lines matching this regex will be included in the result. Any lines that do not match will no longer be available to read.")]
        string? filter = null
    )
    {
        await Task.CompletedTask;

        try
        {
            var output = BashTool.GetBackgroundProcessOutput(bash_id);
            
            if (output == null)
            {
                return $"Background process with ID '{bash_id}' not found.";
            }

            if (string.IsNullOrWhiteSpace(filter))
            {
                return output;
            }

            // Apply regex filter if provided
            var lines = output.Split('\n');
            var filteredLines = new List<string>();
            
            try
            {
                var regex = new System.Text.RegularExpressions.Regex(filter);
                foreach (var line in lines)
                {
                    if (regex.IsMatch(line))
                    {
                        filteredLines.Add(line);
                    }
                }
                
                return string.Join('\n', filteredLines);
            }
            catch (System.Text.RegularExpressions.RegexException ex)
            {
                return $"Invalid regex pattern: {ex.Message}";
            }
        }
        catch (Exception ex)
        {
            return $"Error retrieving background process output: {ex.Message}";
        }
    }
}