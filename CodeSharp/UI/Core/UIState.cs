using System.Collections.Concurrent;
using CodeSharp.UI.Interfaces;

namespace CodeSharp.UI.Core;

public class UIState : IUIState
{
    private readonly object _lock = new();
    private readonly List<ChatMessageInfo> _messages = new();
    private string _currentInput = string.Empty;
    private bool _isAssistantTyping;

    public event Action<string>? InputSubmitted;
    public event Action? StateChanged;

    public string CurrentInput
    {
        get
        {
            lock (_lock)
            {
                return _currentInput;
            }
        }
        set
        {
            bool changed = false;
            lock (_lock)
            {
                if (_currentInput != value)
                {
                    _currentInput = value;
                    changed = true;
                }
            }
            if (changed)
            {
                StateChanged?.Invoke();
            }
        }
    }

    public IReadOnlyList<ChatMessageInfo> Messages
    {
        get
        {
            lock (_lock)
            {
                return _messages.ToList();
            }
        }
    }

    public bool IsAssistantTyping
    {
        get
        {
            lock (_lock)
            {
                return _isAssistantTyping;
            }
        }
        set
        {
            bool changed = false;
            lock (_lock)
            {
                if (_isAssistantTyping != value)
                {
                    _isAssistantTyping = value;
                    changed = true;
                }
            }
            if (changed)
            {
                StateChanged?.Invoke();
            }
        }
    }

    public void AddMessage(ChatMessageInfo message)
    {
        lock (_lock)
        {
            _messages.Add(message);
        }
        StateChanged?.Invoke();
    }

    public void UpdateCurrentMessage(string content)
    {
        bool updated = false;
        lock (_lock)
        {
            if (_messages.Count > 0)
            {
                var lastMessage = _messages[^1];
                lastMessage.Content = content;
                updated = true;
            }
        }
        if (updated)
        {
            StateChanged?.Invoke();
        }
    }

    public void ClearInput()
    {
        lock (_lock)
        {
            _currentInput = string.Empty;
        }
        StateChanged?.Invoke();
    }

    public void SubmitInput()
    {
        string input;
        lock (_lock)
        {
            input = _currentInput;
            _currentInput = string.Empty;
        }
        
        if (!string.IsNullOrEmpty(input))
        {
            InputSubmitted?.Invoke(input);
            StateChanged?.Invoke();
        }
    }
}