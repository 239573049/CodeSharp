using System.Collections.Concurrent;
using CodeSharp.UI.Interfaces;

namespace CodeSharp.UI.Core;

public class InputProcessor : IInputHandler
{
    private readonly BlockingCollection<ConsoleKeyInfo> _keyQueue = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly object _inputLock = new();
    private string _currentInput = string.Empty;
    private Task? _processingTask;
    private bool _isDisposed;

    public event Action<string>? InputSubmitted;
    public event Action<string>? InputChanged;

    public string CurrentInput
    {
        get
        {
            lock (_inputLock)
            {
                return _currentInput;
            }
        }
    }

    public void Start()
    {
        if (_processingTask != null)
            return;

        _processingTask = Task.Run(ProcessInput, _cancellationTokenSource.Token);
        Task.Run(CaptureKeys, _cancellationTokenSource.Token);
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
        _keyQueue.CompleteAdding();
        _processingTask?.Wait(TimeSpan.FromSeconds(1));
    }

    public void ClearInput()
    {
        lock (_inputLock)
        {
            _currentInput = string.Empty;
        }
        InputChanged?.Invoke(_currentInput);
    }

    private async Task CaptureKeys()
    {
        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    _keyQueue.Add(key, _cancellationTokenSource.Token);
                }
                else
                {
                    await Task.Delay(10, _cancellationTokenSource.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ProcessInput()
    {
        try
        {
            foreach (var key in _keyQueue.GetConsumingEnumerable(_cancellationTokenSource.Token))
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    break;

                await ProcessKey(key);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ProcessKey(ConsoleKeyInfo key)
    {
        string newInput;
        bool shouldSubmit = false;

        lock (_inputLock)
        {
            switch (key.Key)
            {
                case ConsoleKey.Enter when (key.Modifiers & ConsoleModifiers.Shift) != 0:
                    _currentInput += Environment.NewLine;
                    break;

                case ConsoleKey.Enter:
                    shouldSubmit = true;
                    break;

                case ConsoleKey.Backspace:
                    if (_currentInput.Length > 0)
                    {
                        _currentInput = _currentInput[..^1];
                    }
                    break;

                case ConsoleKey.Delete:
                    break;

                case ConsoleKey.LeftArrow:
                case ConsoleKey.RightArrow:
                case ConsoleKey.UpArrow:
                case ConsoleKey.DownArrow:
                case ConsoleKey.Home:
                case ConsoleKey.End:
                    break;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        _currentInput += key.KeyChar;
                    }
                    break;
            }

            newInput = _currentInput;
        }

        if (shouldSubmit && !string.IsNullOrWhiteSpace(newInput))
        {
            var submittedInput = newInput;
            lock (_inputLock)
            {
                _currentInput = string.Empty;
            }
            
            InputSubmitted?.Invoke(submittedInput);
            InputChanged?.Invoke(string.Empty);
        }
        else if (!shouldSubmit)
        {
            InputChanged?.Invoke(newInput);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        Stop();
        _cancellationTokenSource.Dispose();
        _keyQueue.Dispose();
        _isDisposed = true;
    }
}