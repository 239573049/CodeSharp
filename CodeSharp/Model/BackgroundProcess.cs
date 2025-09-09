using System.Diagnostics;
using System.Text;

namespace CodeSharp.Model;

public class BackgroundProcess
{
    public Process Process { get; set; }
    
    public StringBuilder Output { get; set; } = new();
    
    public StringBuilder Error { get; set; } = new();
    
    public DateTime StartTime { get; set; } = DateTime.Now;
    
    public string Description { get; set; } = "";
    
    public bool IsCompleted { get; set; } = false;
}