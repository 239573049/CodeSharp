using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;

namespace CodeSharp.Tools;

public class ReadTool: ITool
{
    public string Name => "Read";

    [KernelFunction("Read"), Description(
         "Reads a file from the local filesystem. You can access any file directly by using this tool.\nAssume this tool is able to read all files on the machine. If the User provides a path to a file assume that path is valid. It is okay to read a file that does not exist; an error will be returned.\n\nUsage:\n- The file_path parameter must be an absolute path, not a relative path\n- By default, it reads up to 2000 lines starting from the beginning of the file\n- You can optionally specify a line offset and limit (especially handy for long files), but it's recommended to read the whole file by not providing these parameters\n- Any lines longer than 2000 characters will be truncated\n- Results are returned using cat -n format, with line numbers starting at 1\n- This tool allows Claude Code to read images (eg PNG, JPG, etc). When reading an image file the contents are presented visually as Claude Code is a multimodal LLM.\n- This tool can read PDF files (.pdf). PDFs are processed page by page, extracting both text and visual content for analysis.\n- This tool can read Jupyter notebooks (.ipynb files) and returns all cells with their outputs, combining code, text, and visualizations.\n- This tool can only read files, not directories. To read a directory, use an ls command via the Bash tool.\n- You have the capability to call multiple tools in a single response. It is always better to speculatively read multiple files as a batch that are potentially useful. \n- You will regularly be asked to read screenshots. If the user provides a path to a screenshot ALWAYS use this tool to view the file at the path. This tool will work with all temporary file paths like /var/folders/123/abc/T/TemporaryItems/NSIRD_screencaptureui_ZfB1tD/Screenshot.png\n- If you read a file that exists but has empty contents you will receive a system reminder warning in place of file contents.")]
    public async Task<string> ExecuteAsync(
        [Description("The absolute path to the file to read")]
        string file_path,
        [Description("The line number to start reading from. Only provide if the file is too large to read at once")]
        int? offset,
        [Description("The number of lines to read. Only provide if the file is too large to read at once.")]
        int? limit
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(file_path))
                return "Error: File path cannot be empty";

            if (!File.Exists(file_path))
                return $"Error: File '{file_path}' does not exist";

            var allLines = await File.ReadAllLinesAsync(file_path);
            
            if (allLines.Length == 0)
                return "File is empty";

            var startLine = offset ?? 1;
            var maxLines = limit ?? 2000;
            
            // Validate parameters
            if (startLine < 1)
                startLine = 1;
            
            if (startLine > allLines.Length)
                return $"Error: Start line {startLine} exceeds file length ({allLines.Length} lines)";

            var endLine = Math.Min(startLine + maxLines - 1, allLines.Length);
            var result = new StringBuilder();

            for (int i = startLine - 1; i < endLine; i++)
            {
                var line = allLines[i];
                // Truncate lines longer than 2000 characters
                if (line.Length > 2000)
                    line = line.Substring(0, 2000) + "... [truncated]";
                
                result.AppendLine($"{i + 1,6}→{line}");
            }

            return result.ToString();
        }
        catch (UnauthorizedAccessException)
        {
            return $"Error: Access denied to file '{file_path}'";
        }
        catch (IOException ex)
        {
            return $"Error reading file '{file_path}': {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}