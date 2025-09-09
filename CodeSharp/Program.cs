using System.Runtime.InteropServices;
using System.Text;
using CodeSharp.Infrastructure;
using CodeSharp.Tools;
using CodeSharp.UI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Spectre.Console;

namespace CodeSharp;

public class Program
{
    public static async Task Main(string[] args)
    {
        SetupConsoleForEmojis();
        
        using var uiManager = new UIManager();
        using var cancellationTokenSource = new CancellationTokenSource();

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        var chatHistory = new ChatHistory();
        var kernel = KernelFactory.CreateKernel(ConfigService.GetConfig().Model,
            builder => builder.AddToolContext(), 
            kernel1 => { });

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        var settings = new OpenAIPromptExecutionSettings()
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 32000
        };

        InitializeSystemPrompt(chatHistory);
        
        uiManager.InputReceived += async input => 
        {
            await HandleUserInput(chatHistory, chatCompletionService, settings, kernel, uiManager, input);
        };

        try
        {
            await uiManager.StartAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Application error: {ex.Message}");
        }
    }

    private static void InitializeSystemPrompt(ChatHistory chatHistory)
    {
        chatHistory.AddSystemMessage("You are Code Sharp, Anthropic's official CLI for TokenAI.");
        chatHistory.AddSystemMessage(
            $"""
             You are an interactive CLI tool that helps users with software engineering tasks. Use the instructions below and the tools available to you to assist the user.

             IMPORTANT: Assist with defensive security tasks only. Refuse to create, modify, or improve code that may be used maliciously. Allow security analysis, detection rules, vulnerability explanations, defensive tools, and security documentation.
             IMPORTANT: You must NEVER generate or guess URLs for the user unless you are confident that the URLs are for helping the user with programming. You may use URLs provided by the user in their messages or local files.

             Working directory: {Environment.CurrentDirectory}
             Platform: {RuntimeInformation.OSDescription}
             Today's date: {DateTime.Now:yyyy-MM-dd}
             Model: {ConfigService.GetConfig().Model}
             API Base: {ConfigService.GetConfig().ApiBase}
             """);
    }

    private static async Task HandleUserInput(ChatHistory chatHistory, IChatCompletionService chatCompletionService, 
        OpenAIPromptExecutionSettings settings, Kernel kernel, UIManager uiManager, string input)
    {
        var contents = new ChatMessageContentItemCollection();
        
        if (chatHistory.Count == 2) 
        {
            contents.Add(new TextContent(
                """
                <system-reminder>
                # important-instruction-reminders
                Do what has been asked; nothing more, nothing less.
                NEVER create files unless they're absolutely necessary for achieving your goal.
                ALWAYS prefer editing an existing file to creating a new one.
                NEVER proactively create documentation files (*.md) or README files. Only create documentation files if explicitly requested by the User.

                IMPORTANT: this context may or may not be relevant to your tasks. You should not respond to this context or otherwise consider it in your response unless it is highly relevant to your task. Most of the time, it is not relevant.
                </system-reminder>
                """));
        }

        contents.Add(new TextContent(input));
        chatHistory.AddUserMessage(contents);
        
        uiManager.AddAssistantMessage();

        try
        {
            await foreach (var item in chatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, settings, kernel))
            {
                if (item is OpenAIStreamingChatMessageContent content)
                {
                    if (content.ToolCallUpdates?.Count > 0)
                    {
                        foreach (var update in content.ToolCallUpdates)
                        {
                            if (!string.IsNullOrEmpty(update.FunctionName))
                            {
                                uiManager.AppendToCurrentMessage($"[grey][[{Markup.Escape(update.FunctionName)}]][/grey] ");
                            }
                            else
                            {
                                using var readArgs = new StreamReader(update.FunctionArgumentsUpdate.ToStream());
                                var argsText = await readArgs.ReadToEndAsync();
                                uiManager.AppendToCurrentMessage($"[green]{Markup.Escape(argsText)}[/]");
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(content.Content))
                    {
                        uiManager.AppendToCurrentMessage(Markup.Escape(content.Content));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            uiManager.AddErrorMessage($"Error processing response: {ex.Message}");
        }
    }

    private static void SetupConsoleForEmojis()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            AnsiConsole.MarkupLine("[red]Warning: Failed to set UTF8 encoding. Text display may be limited.[/]");
        }
    }
}