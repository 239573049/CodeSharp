using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace CodeSharp.Tools;

public class KillBashTool : ITool
{
    public string Name => "KillBash";

    [KernelFunction("KillBash"), Description(
        "\n- Kills a running background bash shell by its ID\n- Takes a shell_id parameter identifying the shell to kill\n- Returns a success or failure status \n- Use this tool when you need to terminate a long-running shell\n- Shell IDs can be found using the /bashes command\n")]
    public async Task<string> ExecuteAsync(
        [Description("The ID of the background shell to kill")]
        string shell_id
    )
    {
        await Task.CompletedTask;

        try
        {
            var success = BashTool.KillBackgroundProcess(shell_id);
            
            if (success)
            {
                return $"Successfully killed background process with ID '{shell_id}'.";
            }
            else
            {
                return $"Background process with ID '{shell_id}' not found or could not be killed.";
            }
        }
        catch (Exception ex)
        {
            return $"Error killing background process: {ex.Message}";
        }
    }
}