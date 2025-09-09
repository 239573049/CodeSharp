using Spectre.Console;

namespace CodeSharp.UI.Interfaces;

public interface IUIRenderer : IDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
    void Stop();
    void UpdateMessages();
    void UpdateInput();
    void SetInputText(string text);
    bool IsSupported { get; }
}

public interface IUIState
{
    event Action<string>? InputSubmitted;
    event Action? StateChanged;
    
    string CurrentInput { get; set; }
    IReadOnlyList<ChatMessageInfo> Messages { get; }
    bool IsAssistantTyping { get; set; }
    
    void AddMessage(ChatMessageInfo message);
    void UpdateCurrentMessage(string content);
    void ClearInput();
}

public record ChatMessageInfo(
    string Role,
    string Content,
    string ColorTag,
    DateTime Timestamp)
{
    public string Content { get; set; } = Content;
}