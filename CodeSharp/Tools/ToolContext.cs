using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

#pragma warning disable SKEXP0120

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
        var toolNames = functionNames ?? AllToolNames;

        var jsonOptions = new JsonSerializerOptions(JsonSerializerOptions.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // 中文乱码
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            // 枚举
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        foreach (var plugin in toolNames)
        {
            var tool = CreateToolInstance(plugin);
            builder.Plugins.AddFromObject(tool, jsonOptions, plugin);
        }

        return builder;
    }
}