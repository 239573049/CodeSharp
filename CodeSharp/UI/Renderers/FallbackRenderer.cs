using CodeSharp.UI.Core;
using CodeSharp.UI.Interfaces;

namespace CodeSharp.UI.Renderers;

public class FallbackRenderer : IUIRenderer
{
    private readonly IUIState _uiState;
    private readonly IUITheme _theme;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isDisposed;
    private int _lastMessageCount;

    public bool IsSupported => true;

    public FallbackRenderer(IUIState uiState, IUITheme? theme = null)
    {
        _uiState = uiState ?? throw new ArgumentNullException(nameof(uiState));
        _theme = theme ?? new DefaultUITheme();
        
        _uiState.StateChanged += OnStateChanged;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("CodeSharp - Fallback Mode");
        Console.WriteLine("=========================");
        Console.WriteLine($"Working Directory: {Environment.CurrentDirectory}");
        Console.WriteLine("Note: Limited display capabilities detected. Using basic text mode.");
        Console.WriteLine();

        _lastMessageCount = _uiState.Messages.Count;

        var combinedToken = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token).Token;

        try
        {
            await Task.Delay(Timeout.Infinite, combinedToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
    }

    public void UpdateMessages()
    {
        var messages = _uiState.Messages;
        if (messages.Count > _lastMessageCount)
        {
            for (int i = _lastMessageCount; i < messages.Count; i++)
            {
                var message = messages[i];
                Console.WriteLine($"{message.Role}: {message.Content}");
            }
            _lastMessageCount = messages.Count;
        }
        else if (messages.Count > 0)
        {
            var lastMessage = messages[^1];
            Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
            Console.Write($"{lastMessage.Role}: {lastMessage.Content}");
        }
    }

    public void UpdateInput()
    {
    }

    public void SetInputText(string text)
    {
        _uiState.CurrentInput = text;
    }

    private void OnStateChanged()
    {
        UpdateMessages();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _uiState.StateChanged -= OnStateChanged;
        Stop();
        _cancellationTokenSource.Dispose();
        _isDisposed = true;
    }
}