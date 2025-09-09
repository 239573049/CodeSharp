using CodeSharp.UI.Interfaces;

namespace CodeSharp.UI.Core;

public class MessageStore : IMessageStore
{
    private readonly object _lock = new();
    private readonly List<ChatMessageInfo> _messages = new();
    private int _currentAssistantMessageIndex = -1;
    private readonly int _maxMessages;

    public event Action<ChatMessageInfo>? MessageAdded;
    public event Action<int, string>? MessageUpdated;
    public event Action? MessagesCleared;

    public MessageStore(int maxMessages = 1000)
    {
        _maxMessages = maxMessages;
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

    public int CurrentAssistantMessageIndex
    {
        get
        {
            lock (_lock)
            {
                return _currentAssistantMessageIndex;
            }
        }
    }

    public void AddMessage(ChatMessageInfo message)
    {
        lock (_lock)
        {
            _messages.Add(message);
            
            if (message.Role.Equals("Assistant", StringComparison.OrdinalIgnoreCase))
            {
                _currentAssistantMessageIndex = _messages.Count - 1;
            }

            CleanupOldMessages();
        }
        
        MessageAdded?.Invoke(message);
    }

    public void UpdateMessage(int index, string content)
    {
        bool updated = false;
        lock (_lock)
        {
            if (index >= 0 && index < _messages.Count)
            {
                _messages[index].Content = content;
                updated = true;
            }
        }
        
        if (updated)
        {
            MessageUpdated?.Invoke(index, content);
        }
    }

    public void UpdateCurrentAssistantMessage(string content)
    {
        lock (_lock)
        {
            if (_currentAssistantMessageIndex >= 0 && _currentAssistantMessageIndex < _messages.Count)
            {
                _messages[_currentAssistantMessageIndex].Content = content;
                MessageUpdated?.Invoke(_currentAssistantMessageIndex, content);
            }
        }
    }

    public void StartNewAssistantMessage()
    {
        var message = new ChatMessageInfo("Assistant", "", "yellow", DateTime.Now);
        AddMessage(message);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
            _currentAssistantMessageIndex = -1;
        }
        
        MessagesCleared?.Invoke();
    }

    private void CleanupOldMessages()
    {
        if (_messages.Count > _maxMessages)
        {
            var toRemove = _messages.Count - _maxMessages;
            _messages.RemoveRange(0, toRemove);
            
            _currentAssistantMessageIndex -= toRemove;
            if (_currentAssistantMessageIndex < 0)
            {
                _currentAssistantMessageIndex = -1;
            }
        }
    }
}