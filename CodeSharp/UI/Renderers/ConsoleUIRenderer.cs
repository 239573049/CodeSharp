using System.Collections.Concurrent;
using CodeSharp.UI.Core;
using CodeSharp.UI.Interfaces;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace CodeSharp.UI.Renderers;

public class ConsoleUIRenderer : IUIRenderer
{
    private readonly IUIState _uiState;
    private readonly IUITheme _theme;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Layout? _layout;
    private LiveDisplayContext? _liveContext;
    private Task? _renderTask;
    private bool _isDisposed;
    private DateTime _lastUpdate = DateTime.MinValue;
    private readonly TimeSpan _minUpdateInterval = TimeSpan.FromMilliseconds(50);
    private readonly ConcurrentQueue<Action> _renderQueue = new();

    public bool IsSupported => AnsiConsole.Profile.Capabilities.Ansi;

    public ConsoleUIRenderer(IUIState uiState, IUITheme? theme = null)
    {
        _uiState = uiState ?? throw new ArgumentNullException(nameof(uiState));
        _theme = theme ?? new DefaultUITheme();
        
        _uiState.StateChanged += OnStateChanged;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            throw new InvalidOperationException("Console does not support ANSI sequences");

        _layout = CreateLayout();
        UpdateLayout();

        _renderTask = Task.Run(ProcessRenderQueue, _cancellationTokenSource.Token);

        var combinedToken = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token).Token;

        var liveDisplay = AnsiConsole.Live(_layout);
        _ = Task.Run(async () =>
        {
            await liveDisplay.StartAsync(ctx =>
            {
                _liveContext = ctx;
                ctx.Refresh();
                return Task.CompletedTask;
            });
        }, combinedToken);

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
        _renderTask?.Wait(TimeSpan.FromSeconds(1));
    }

    public void UpdateMessages()
    {
        QueueRender(UpdateLayout);
    }

    public void UpdateInput()
    {
        QueueRender(UpdateLayout);
    }

    public void SetInputText(string text)
    {
        _uiState.CurrentInput = text;
    }

    private void OnStateChanged()
    {
        QueueRender(UpdateLayout);
    }

    private void QueueRender(Action renderAction)
    {
        _renderQueue.Enqueue(renderAction);
    }

    private async Task ProcessRenderQueue()
    {
        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                var hasWork = false;
                var actions = new List<Action>();

                while (_renderQueue.TryDequeue(out var action))
                {
                    actions.Add(action);
                    hasWork = true;
                }

                if (hasWork)
                {
                    var now = DateTime.UtcNow;
                    if (now - _lastUpdate >= _minUpdateInterval)
                    {
                        foreach (var action in actions)
                        {
                            action?.Invoke();
                        }
                        
                        _liveContext?.Refresh();
                        _lastUpdate = now;
                    }
                    else
                    {
                        var delay = _minUpdateInterval - (now - _lastUpdate);
                        await Task.Delay(delay, _cancellationTokenSource.Token);
                        
                        foreach (var action in actions)
                        {
                            action?.Invoke();
                        }
                        
                        _liveContext?.Refresh();
                        _lastUpdate = DateTime.UtcNow;
                    }
                }
                else
                {
                    await Task.Delay(16, _cancellationTokenSource.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private Layout CreateLayout()
    {
        return new Layout("root")
            .SplitRows(
                new Layout("messages"),
                new Layout("input") { Size = 3 }
            );
    }

    private void UpdateLayout()
    {
        if (_layout == null)
            return;

        UpdateMessagesPanel();
        UpdateInputPanel();
    }

    private void UpdateMessagesPanel()
    {
        var messages = _uiState.Messages;
        var items = new List<IRenderable>();

        foreach (var message in messages)
        {
            var content = string.IsNullOrEmpty(message.Content) ? "[dim]...[/]" : message.Content;
            var body = new Markup(content);
            var panel = new Panel(body)
            {
                Border = BoxBorder.Rounded,
                Expand = true
            };
            
            var roleColor = GetRoleColor(message.Role);
            panel.Header = new PanelHeader($"[{roleColor}]{Markup.Escape(message.Role)}[/]");
            items.Add(panel);
        }

        var messagesView = items.Count > 0 
            ? new Rows(items) 
            : new Rows(new Markup("[dim]No messages yet[/]"));
            
        var messagesPanel = new Panel(messagesView)
        {
            Border = BoxBorder.Square,
            Expand = true
        };
        messagesPanel.Header = new PanelHeader("Chat");

        _layout!["messages"].Update(messagesPanel);
    }

    private void UpdateInputPanel()
    {
        var input = _uiState.CurrentInput;
        var prompt = new Markup($"{_theme.InputPrompt}{Markup.Escape(input)}");
        var help = new Markup(_theme.HelpText);
        
        var inputPanel = new Panel(new Rows(prompt, help))
        {
            Border = BoxBorder.Rounded,
            Expand = true
        };
        inputPanel.Header = new PanelHeader("Input");

        _layout!["input"].Update(inputPanel);
    }

    private string GetRoleColor(string role)
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