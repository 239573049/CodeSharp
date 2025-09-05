using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace CodeSharp.Tools;

public class WriteTool : ITool
{
    public string Name => "Write";

    [KernelFunction("Write"), Description(
         "Writes a file to the local filesystem.\n\nUsage:\n- This tool will overwrite the existing file if there is one at the provided path.\n- If this is an existing file, you MUST use the Read tool first to read the file's contents. This tool will fail if you did not read the file first.\n- ALWAYS prefer editing existing files in the codebase. NEVER write new files unless explicitly required.\n- NEVER proactively create documentation files (*.md) or README files. Only create documentation files if explicitly requested by the User.\n- Only use emojis if the user explicitly requests it. Avoid writing emojis to files unless asked.")]
    public async Task<string> ExecuteAsync(
        [Description("The absolute path to the file to write (must be absolute, not relative)")]
        string file_path,
        [Description("The content to write to the file")]
        string content
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(file_path))
                return "Error: File path cannot be empty";

            if (!Path.IsPathRooted(file_path))
                return "Error: File path must be absolute, not relative";

            // Create directory if it doesn't exist
            var directory = Path.GetDirectoryName(file_path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Check if file exists and warn about overwrite
            bool fileExists = File.Exists(file_path);
            
            // Write the file
            await File.WriteAllTextAsync(file_path, content ?? string.Empty);

            if (fileExists)
                return $"File '{file_path}' overwritten successfully";
            else
                return $"File '{file_path}' created successfully";
        }
        catch (UnauthorizedAccessException)
        {
            return $"Error: Access denied to file '{file_path}'";
        }
        catch (DirectoryNotFoundException)
        {
            return $"Error: Directory not found for file '{file_path}'";
        }
        catch (IOException ex)
        {
            return $"Error writing file '{file_path}': {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}