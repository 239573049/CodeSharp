using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace CodeSharp.Tools;

public class EditTool : ITool
{
    public string Name => "Edit";

    [KernelFunction("Edit"), Description(
         "Performs exact string replacements in files. \n\nUsage:\n- You must use your `Read` tool at least once in the conversation before editing. This tool will error if you attempt an edit without reading the file. \n- When editing text from Read tool output, ensure you preserve the exact indentation (tabs/spaces) as it appears AFTER the line number prefix. The line number prefix format is: spaces + line number + tab. Everything after that tab is the actual file content to match. Never include any part of the line number prefix in the old_string or new_string.\n- ALWAYS prefer editing existing files in the codebase. NEVER write new files unless explicitly required.\n- Only use emojis if the user explicitly requests it. Avoid adding emojis to files unless asked.\n- The edit will FAIL if `old_string` is not unique in the file. Either provide a larger string with more surrounding context to make it unique or use `replace_all` to change every instance of `old_string`. \n- Use `replace_all` for replacing and renaming strings across the file. This parameter is useful if you want to rename a variable for instance.")]
    public async Task<string> ExecuteAsync(
        [Description("The absolute path to the file to modify")]
        string file_path,
        [Description("The text to replace")] string old_string,
        [Description("The text to replace it with (must be different from old_string)")]
        string new_string,
        [Description("Replace all occurences of old_string (default false)")]
        bool replace_all = false
    )
    {
    }
}