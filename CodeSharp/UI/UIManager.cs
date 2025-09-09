using CodeSharp.UI.Core;
using CodeSharp.UI.Interfaces;
using CodeSharp.UI.Renderers;
using Spectre.Console;

namespace CodeSharp.UI;

public class UIManager : IDisposable
{
    private readonly IUIState _uiState;
    private readonly IInputHandler _inputHandler;
    private readonly IUIRenderer _renderer;
    private readonly IUITheme _theme;
    private bool _isDisposed;
    private bool _isStarted;

    public event Action<string>? InputReceived;

    public UIManager(IUITheme? theme = null)
    {
        _theme = theme ?? new DefaultUITheme();
        _uiState = new UIState();
        _inputHandler = new InputProcessor();
        
        if (AnsiConsole.Profile.Capabilities.Ansi)
        {
            _renderer = new ConsoleUIRenderer(_uiState, _theme);
        }
        else
        {
            _renderer = new FallbackRenderer(_uiState, _theme);
        }

        WireUpEvents();
    }

    public UIManager(IUIState uiState, IInputHandler inputHandler, IUIRenderer renderer, IUITheme? theme = null)
    {
        _uiState = uiState ?? throw new ArgumentNullException(nameof(uiState));
        _inputHandler = inputHandler ?? throw new ArgumentNullException(nameof(inputHandler));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _theme = theme ?? new DefaultUITheme();

        WireUpEvents();
    }

    private void WireUpEvents()
    {
        _uiState.InputSubmitted += OnInputSubmitted;
        _inputHandler.InputSubmitted += OnInputSubmitted;
        _inputHandler.InputChanged += OnInputChanged;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isStarted)
            return;

        try
        {
            _inputHandler.Start();
            
            AddWelcomeMessages();
            
            await _renderer.StartAsync(cancellationToken);
            _isStarted = true;
        }
        catch (Exception ex)
        {
            AddErrorMessage($"Failed to start UI: {ex.Message}");
            throw;
        }
    }

    public void Stop()
    {
        if (!_isStarted)
            return;

        _inputHandler.Stop();
        _renderer.Stop();
        _isStarted = false;
    }

    public void AddMessage(string role, string content, string? colorTag = null)
    {
        var color = colorTag ?? GetDefaultColorForRole(role);
        var message = new ChatMessageInfo(role, content, color, DateTime.Now);
        _uiState.AddMessage(message);
    }

    public void AddUserMessage(string content)
    {
        AddMessage("You", content, _theme.UserColor);
    }

    public void AddAssistantMessage(string content = "")
    {
        AddMessage("Assistant", content, _theme.AssistantColor);
    }

    public void AddSystemMessage(string content)
    {
        AddMessage("System", content, _theme.SystemColor);
    }

    public void AddInfoMessage(string content)
    {
        AddMessage("Info", content, _theme.InfoColor);
    }

    public void AddErrorMessage(string content)
    {
        AddMessage("Error", content, _theme.ErrorColor);
    }

    public void UpdateCurrentMessage(string content)
    {
        _uiState.UpdateCurrentMessage(content);
    }

    public void AppendToCurrentMessage(string content)
    {
        var messages = _uiState.Messages;
        if (messages.Count > 0)
        {
            var currentContent = messages[^1].Content;
            _uiState.UpdateCurrentMessage(currentContent + content);
        }
    }

    public void SetAssistantTyping(bool isTyping)
    {
        _uiState.IsAssistantTyping = isTyping;
    }

    private void AddWelcomeMessages()
    {
        AddSystemMessage("✨ Welcome to CodeSharp!");
        AddInfoMessage($"cwd: {Environment.CurrentDirectory}");
    }

    private string GetDefaultColorForRole(string role)
    {
        return role.ToLowerInvariant() switch
        {
            "user" or "you" => _theme.UserColor,
            "assistant" => _theme.AssistantColor,
            "system" => _theme.SystemColor,
            "info" => _theme.InfoColor,
            "error" => _theme.ErrorColor,
            _ => _theme.InfoColor
        };
    }

    private void OnInputSubmitted(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return;

        AddUserMessage(input);
        InputReceived?.Invoke(input);
    }

    private void OnInputChanged(string input)
    {
        _uiState.CurrentInput = input;
        _renderer.UpdateInput();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        Stop();
        
        _uiState.InputSubmitted -= OnInputSubmitted;
        _inputHandler.InputSubmitted -= OnInputSubmitted;
        _inputHandler.InputChanged -= OnInputChanged;
        
        _inputHandler.Dispose();
        _renderer.Dispose();
        _isDisposed = true;
    }
}