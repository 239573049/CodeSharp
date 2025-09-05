using Microsoft.SemanticKernel.ChatCompletion;

namespace CodeSharp.Tools;

public class ToolStore
{
    private static AsyncLocal<ToolHolder> Current { get; } = new();

    public static ToolHolder Store
    {
        get { return Current.Value ??= new ToolHolder(); }
        set { Current.Value = value; }
    }
}

public class ToolHolder
{
    public ChatHistory ChatHistory { get; set; } = new();

    public List<TodoWriteTool.TodoWriteInput> Todos { get; set; } = new();
}