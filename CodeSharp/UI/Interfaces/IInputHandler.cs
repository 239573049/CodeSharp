namespace CodeSharp.UI.Interfaces;

public interface IInputHandler : IDisposable
{
    event Action<string>? InputSubmitted;
    event Action<string>? InputChanged;
    
    void Start();
    void Stop();
    string CurrentInput { get; }
    void ClearInput();
}

public interface IMessageStore
{
    event Action<ChatMessageInfo>? MessageAdded;
    event Action<int, string>? MessageUpdated;
    event Action? MessagesCleared;
    
    IReadOnlyList<ChatMessageInfo> Messages { get; }
    int CurrentAssistantMessageIndex { get; }
    
    void AddMessage(ChatMessageInfo message);
    void UpdateMessage(int index, string content);
    void UpdateCurrentAssistantMessage(string content);
    void StartNewAssistantMessage();
    void Clear();
}

public interface IUITheme
{
    string UserColor { get; }
    string AssistantColor { get; }
    string SystemColor { get; }
    string InfoColor { get; }
    string ErrorColor { get; }
    string InputPrompt { get; }
    string HelpText { get; }
}