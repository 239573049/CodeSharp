using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

namespace CodeSharp.Tools;

public class MultiEditTool: ITool
{
    public string Name => "MultiEdit";
    
    [KernelFunction("MultiEdit"), Description(
         "This is a tool for making multiple edits to a single file in one operation. It is built on top of the Edit tool and allows you to perform multiple find-and-replace operations efficiently. Prefer this tool over the Edit tool when you need to make multiple edits to the same file.\n\nBefore using this tool:\n\n1. Use the Read tool to understand the file's contents and context\n2. Verify the directory path is correct\n\nTo make multiple file edits, provide the following:\n1. file_path: The absolute path to the file to modify (must be absolute, not relative)\n2. edits: An array of edit operations to perform, where each edit contains:\n   - old_string: The text to replace (must match the file contents exactly, including all whitespace and indentation)\n   - new_string: The edited text to replace the old_string\n   - replace_all: Replace all occurences of old_string. This parameter is optional and defaults to false.\n\nIMPORTANT:\n- All edits are applied in sequence, in the order they are provided\n- Each edit operates on the result of the previous edit\n- All edits must be valid for the operation to succeed - if any edit fails, none will be applied\n- This tool is ideal when you need to make several changes to different parts of the same file\n- For Jupyter notebooks (.ipynb files), use the NotebookEdit instead\n\nCRITICAL REQUIREMENTS:\n1. All edits follow the same requirements as the single Edit tool\n2. The edits are atomic - either all succeed or none are applied\n3. Plan your edits carefully to avoid conflicts between sequential operations\n\nWARNING:\n- The tool will fail if edits.old_string doesn't match the file contents exactly (including whitespace)\n- The tool will fail if edits.old_string and edits.new_string are the same\n- Since edits are applied in sequence, ensure that earlier edits don't affect the text that later edits are trying to find\n\nWhen making edits:\n- Ensure all edits result in idiomatic, correct code\n- Do not leave the code in a broken state\n- Always use absolute file paths (starting with /)\n- Only use emojis if the user explicitly requests it. Avoid adding emojis to files unless asked.\n- Use replace_all for replacing and renaming strings across the file. This parameter is useful if you want to rename a variable for instance.\n\nIf you want to create a new file, use:\n- A new file path, including dir name if needed\n- First edit: empty old_string and the new file's contents as new_string\n- Subsequent edits: normal edit operations on the created content")]
    public async Task<string> ExecuteAsync(
        [Description("The absolute path to the file to modify")]
        string file_path,
        [Description("Array of edit operations to perform sequentially on the file")]
        MultiEditInput[] edits
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(file_path))
                return "Error: File path cannot be empty";

            if (edits == null || edits.Length == 0)
                return "Error: No edits provided";

            if (!File.Exists(file_path))
                return $"Error: File '{file_path}' does not exist";

            var content = await File.ReadAllTextAsync(file_path);
            var originalContent = content;
            var totalReplacements = 0;
            var editResults = new List<string>();

            // Validate all edits first
            for (int i = 0; i < edits.Length; i++)
            {
                var edit = edits[i];
                if (string.IsNullOrEmpty(edit.OldString))
                    return $"Error: Edit {i + 1}: Old string cannot be empty";

                if (edit.OldString == edit.NewString)
                    return $"Error: Edit {i + 1}: Old string and new string must be different";
            }

            // Apply edits sequentially
            for (int i = 0; i < edits.Length; i++)
            {
                var edit = edits[i];
                
                if (!content.Contains(edit.OldString))
                {
                    return $"Error: Edit {i + 1}: Old string not found in current file content";
                }

                int replacementCount = 0;

                if (edit.ReplaceAll)
                {
                    // Count occurrences
                    int index = 0;
                    while ((index = content.IndexOf(edit.OldString, index)) != -1)
                    {
                        replacementCount++;
                        index += edit.OldString.Length;
                    }
                    
                    content = content.Replace(edit.OldString, edit.NewString);
                }
                else
                {
                    // Check if old_string appears more than once
                    var firstIndex = content.IndexOf(edit.OldString);
                    var lastIndex = content.LastIndexOf(edit.OldString);
                    
                    if (firstIndex != lastIndex)
                    {
                        return $"Error: Edit {i + 1}: Old string appears multiple times in file. Set replace_all=true to replace all occurrences or provide more specific context.";
                    }

                    content = content.Replace(edit.OldString, edit.NewString);
                    replacementCount = 1;
                }

                totalReplacements += replacementCount;
                editResults.Add($"Edit {i + 1}: Replaced {replacementCount} occurrence(s)");
            }

            // Write the final content
            await File.WriteAllTextAsync(file_path, content);

            var result = new StringBuilder();
            result.AppendLine($"Successfully applied {edits.Length} edit(s) to file '{file_path}':");
            foreach (var editResult in editResults)
            {
                result.AppendLine($"  - {editResult}");
            }
            result.AppendLine($"Total replacements: {totalReplacements}");

            return result.ToString().Trim();
        }
        catch (UnauthorizedAccessException)
        {
            return $"Error: Access denied to file '{file_path}'";
        }
        catch (IOException ex)
        {
            return $"Error accessing file '{file_path}': {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public class MultiEditInput
    {
        [JsonPropertyName("old_string"), Description("The text to replace (must match the file contents exactly, including all whitespace and indentation)")]
        public string OldString { get; set; } = string.Empty;

        [JsonPropertyName("new_string"),
         Description(
             "The edited text to replace the old_string")]
        public string NewString { get; set; } = string.Empty;

        [JsonPropertyName("replace_all"),
         Description("Replace all occurences of old_string. This parameter is optional and defaults to false.")]
        [DefaultValue(false)]
        public bool ReplaceAll { get; set; } = false;
    }
}