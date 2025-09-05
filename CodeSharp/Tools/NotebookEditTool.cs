using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace CodeSharp.Tools;

public class NotebookEditTool: ITool
{
    public string Name => "NotebookEdit";
    
    [KernelFunction("NotebookEdit"), Description(
         "Completely replaces the contents of a specific cell in a Jupyter notebook (.ipynb file) with new source. Jupyter notebooks are interactive documents that combine code, text, and visualizations, commonly used for data analysis and scientific computing. The notebook_path parameter must be an absolute path, not a relative path. The cell_number is 0-indexed. Use edit_mode=insert to add a new cell at the index specified by cell_number. Use edit_mode=delete to delete the cell at the index specified by cell_number.")]
    public async Task<string> ExecuteAsync(
        [Description("The absolute path to the Jupyter notebook file to edit (must be absolute, not relative)")]
        string notebook_path,
        [Description("The new source for the cell")]
        string new_source,
        [Description(
            "The ID of the cell to edit. When inserting a new cell, the new cell will be inserted after the cell with this ID, or at the beginning if not specified.")]
        string? cell_id,
        [Description(
            "The type of the cell (code or markdown). If not specified, it defaults to the current cell type. If using edit_mode=insert, this is required.")]
        string? cell_type,
        [Description("The type of edit to make (replace, insert, delete). Defaults to replace.")]
        string? edit_mode
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(notebook_path))
                return "Error: Notebook path cannot be empty";

            if (!File.Exists(notebook_path))
                return $"Error: Notebook file '{notebook_path}' does not exist";

            if (!notebook_path.EndsWith(".ipynb", StringComparison.OrdinalIgnoreCase))
                return "Error: File must be a Jupyter notebook (.ipynb)";

            var mode = edit_mode?.ToLower() ?? "replace";
            
            if (mode == "delete" && string.IsNullOrWhiteSpace(cell_id))
                return "Error: Cell ID is required for delete operation";

            if (mode == "insert" && string.IsNullOrWhiteSpace(cell_type))
                return "Error: Cell type is required for insert operation";

            var jsonContent = await File.ReadAllTextAsync(notebook_path);
            var notebook = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(jsonContent);

            if (!notebook.TryGetProperty("cells", out var cellsProperty) || cellsProperty.ValueKind != JsonValueKind.Array)
                return "Error: Invalid notebook format - missing or invalid 'cells' array";

            var cells = cellsProperty.EnumerateArray().ToList();
            var targetCellIndex = -1;

            // Find target cell if cell_id is provided
            if (!string.IsNullOrWhiteSpace(cell_id))
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].TryGetProperty("id", out var idProp) && idProp.GetString() == cell_id)
                    {
                        targetCellIndex = i;
                        break;
                    }
                }

                if (targetCellIndex == -1)
                    return $"Error: Cell with ID '{cell_id}' not found";
            }

            var modifiedNotebook = new Dictionary<string, object>();
            
            // Copy all properties except cells
            foreach (var prop in notebook.EnumerateObject())
            {
                if (prop.Name != "cells")
                {
                    modifiedNotebook[prop.Name] = System.Text.Json.JsonSerializer.Deserialize<object>(prop.Value.GetRawText()) ?? new object();
                }
            }

            var newCells = new List<object>();

            switch (mode)
            {
                case "replace":
                    if (targetCellIndex == -1)
                    {
                        if (cells.Count == 0)
                            return "Error: No cells found to replace";
                        targetCellIndex = 0; // Replace first cell if no ID specified
                    }

                    for (int i = 0; i < cells.Count; i++)
                    {
                        if (i == targetCellIndex)
                        {
                            var cellDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(cells[i].GetRawText()) ?? new Dictionary<string, object>();
                            cellDict["source"] = new_source.Split('\n');
                            if (!string.IsNullOrWhiteSpace(cell_type))
                                cellDict["cell_type"] = cell_type;
                            newCells.Add(cellDict);
                        }
                        else
                        {
                            newCells.Add(System.Text.Json.JsonSerializer.Deserialize<object>(cells[i].GetRawText()) ?? new object());
                        }
                    }
                    break;

                case "insert":
                    var newCell = new Dictionary<string, object>
                    {
                        ["cell_type"] = cell_type!,
                        ["source"] = new_source.Split('\n'),
                        ["metadata"] = new Dictionary<string, object>(),
                        ["id"] = Guid.NewGuid().ToString()
                    };

                    if (cell_type == "code")
                    {
                        newCell["execution_count"] = default(object);
                        newCell["outputs"] = new object[0];
                    }

                    var insertIndex = targetCellIndex == -1 ? 0 : targetCellIndex + 1;
                    
                    for (int i = 0; i < cells.Count; i++)
                    {
                        if (i == insertIndex)
                        {
                            newCells.Add(newCell);
                        }
                        newCells.Add(System.Text.Json.JsonSerializer.Deserialize<object>(cells[i].GetRawText()) ?? new object());
                    }

                    if (insertIndex >= cells.Count)
                        newCells.Add(newCell);
                    break;

                case "delete":
                    for (int i = 0; i < cells.Count; i++)
                    {
                        if (i != targetCellIndex)
                        {
                            newCells.Add(System.Text.Json.JsonSerializer.Deserialize<object>(cells[i].GetRawText()) ?? new object());
                        }
                    }
                    break;

                default:
                    return $"Error: Invalid edit mode '{mode}'. Use 'replace', 'insert', or 'delete'";
            }

            modifiedNotebook["cells"] = newCells;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var newJsonContent = System.Text.Json.JsonSerializer.Serialize(modifiedNotebook, options);
            await File.WriteAllTextAsync(notebook_path, newJsonContent);

            return $"Successfully {mode}d cell in notebook '{notebook_path}'";
        }
        catch (JsonException ex)
        {
            return $"Error: Invalid JSON in notebook file - {ex.Message}";
        }
        catch (UnauthorizedAccessException)
        {
            return $"Error: Access denied to notebook file '{notebook_path}'";
        }
        catch (IOException ex)
        {
            return $"Error accessing notebook file '{notebook_path}': {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}