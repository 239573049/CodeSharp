using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;

namespace CodeSharp.Tools;

public class ExitPlanModeTool: ITool
{
    public string Name => "ExitPlanMode";

    [KernelFunction("ExitPlanMode"), Description(
         "Use this tool when you are in plan mode and have finished presenting your plan and are ready to code. This will prompt the user to exit plan mode. \nIMPORTANT: Only use this tool when the task requires planning the implementation steps of a task that requires writing code. For research tasks where you're gathering information, searching files, reading files or in general trying to understand the codebase - do NOT use this tool.\n\nEg. \n1. Initial task: \"Search for and understand the implementation of vim mode in the codebase\" - Do not use the exit plan mode tool because you are not planning the implementation steps of a task.\n2. Initial task: \"Help me implement yank mode for vim\" - Use the exit plan mode tool after you have finished planning the implementation steps of the task.\n")]
    public async Task<string> ExecuteAsync(
        [Description("The plan you came up with, that you want to run by the user for approval. Supports markdown. The plan should be pretty concise.")]
        string plan
    )
    {
        await Task.CompletedTask;
        
        if (string.IsNullOrWhiteSpace(plan))
            return "Error: Plan cannot be empty";

        // Format the plan for user approval
        var result = new StringBuilder();
        result.AppendLine("## Implementation Plan");
        result.AppendLine();
        result.AppendLine(plan);
        result.AppendLine();
        result.AppendLine("**Ready to proceed with implementation. Please confirm if you'd like me to proceed with this plan.**");
        
        return result.ToString();
    }
}