using Microsoft.SemanticKernel;

namespace CodeSharp.Tools;

public static class ToolContext
{
    private static readonly string[] AllToolNames =
    [
        "Bash",
        "BashOutput", 
        "Edit",
        "ExitPlanMode",
        "Glob",
        "Grep",
        "KillBash",
        "MultiEdit",
        "NotebookEdit",
        "Read",
        "Task",
        "TodoWrite",
        "WebFetch",
        "WebSearch",
        "Write"
    ];

    private static ITool CreateToolInstance(string toolName)
    {
        // 根据工具名称创建新的工具实例
        return toolName switch
        {
            "Bash" => new BashTool(),
            "BashOutput" => new BashOutputTool(),
            "Edit" => new EditTool(),
            "ExitPlanMode" => new ExitPlanModeTool(),
            "Glob" => new GlobTool(),
            "Grep" => new GrepTool(),
            "KillBash" => new KillBashTool(),
            "MultiEdit" => new MultiEditTool(),
            "NotebookEdit" => new NotebookEditTool(),
            "Read" => new ReadTool(),
            "Task" => new TaskTool(),
            "TodoWrite" => new TodoWriteTool(),
            "WebFetch" => new WebFetchTool(),
            "WebSearch" => new WebSearchTool(),
            "Write" => new WriteTool(),
            _ => throw new ArgumentException($"Unknown tool name: {toolName}"),
        };
    }

    public static IKernelBuilder AddToolContext(
        this IKernelBuilder builder,
        string[]? functionNames = null)
    {
        var plugins = GetFunctions(functionNames);

        foreach (var plugin in plugins)
        {
            builder.Plugins.Add(plugin);
        }

        return builder;
    }

    public static List<KernelPlugin> GetFunctions(string[]? functionNames = null)
    {
        var functions = new List<KernelPlugin>();

        // 如果没有指定工具名称，则使用所有可用的工具
        var toolNames = functionNames ?? AllToolNames;

        foreach (var functionName in toolNames)
        {
            var tool = CreateToolInstance(functionName);
            if (tool != null)
            {
                // 将新创建的工具实例转换为KernelPlugin并添加到函数列表
                var plugin = KernelPluginFactory.CreateFromObject(tool, tool.Name);
                functions.Add(plugin);
            }
        }

        return functions;
    }
}