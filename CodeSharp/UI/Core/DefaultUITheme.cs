using CodeSharp.UI.Interfaces;

namespace CodeSharp.UI.Core;

public class DefaultUITheme : IUITheme
{
    public string UserColor => "deepskyblue2";
    public string AssistantColor => "yellow";
    public string SystemColor => "green";
    public string InfoColor => "grey";
    public string ErrorColor => "red";
    public string InputPrompt => "> ";
    public string HelpText => "[dim]Enter=Send • Shift+Enter=New line • Ctrl+C=Exit[/]";
}