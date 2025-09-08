using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.SemanticKernel;
using System.Collections.Concurrent;

namespace CodeSharp.Tools;

public class BashTool : ITool
{
    public string Name => "Bash";

    private static readonly ConcurrentDictionary<string, BackgroundProcess> BackgroundProcesses = new();
    private static readonly object ProcessLock = new();
    private static Process? _persistentShell;
    private static readonly int MaxOutputLength = 30000;

    private class BackgroundProcess
    {
        public Process Process { get; set; }
        public StringBuilder Output { get; set; } = new();
        public StringBuilder Error { get; set; } = new();
        public DateTime StartTime { get; set; } = DateTime.Now;
        public string Description { get; set; } = "";
        public bool IsCompleted { get; set; } = false;
    }

    [KernelFunction("Bash"), Description(
         "Executes a given bash command in a persistent shell session with optional timeout, ensuring proper handling and security measures.\n\nBefore executing the command, please follow these steps:\n\n1. Directory Verification:\n   - If the command will create new directories or files, first use `ls` to verify the parent directory exists and is the correct location\n   - For example, before running \"mkdir foo/bar\", first use `ls foo` to check that \"foo\" exists and is the intended parent directory\n\n2. Command Execution:\n   - Always quote file paths that contain spaces with double quotes (e.g., cd \"path with spaces/file.txt\")\n   - Examples of proper quoting:\n     - cd \"/Users/name/My Documents\" (correct)\n     - cd /Users/name/My Documents (incorrect - will fail)\n     - python \"/path/with spaces/script.py\" (correct)\n     - python /path/with spaces/script.py (incorrect - will fail)\n   - After ensuring proper quoting, execute the command.\n   - Capture the output of the command.\n\nUsage notes:\n  - The command argument is required.\n  - You can specify an optional timeout in milliseconds (up to 600000ms / 10 minutes). If not specified, commands will timeout after 120000ms (2 minutes).\n  - It is very helpful if you write a clear, concise description of what this command does in 5-10 words.\n  - If the output exceeds 30000 characters, output will be truncated before being returned to you.\n  - You can use the `run_in_background` parameter to run the command in the background, which allows you to continue working while the command runs. You can monitor the output using the Bash tool as it becomes available. Never use `run_in_background` to run 'sleep' as it will return immediately. You do not need to use '&' at the end of the command when using this parameter.\n  - VERY IMPORTANT: You MUST avoid using search commands like `find` and `grep`. Instead use Grep, Glob, or Task to search. You MUST avoid read tools like `cat`, `head`, and `tail`, and use Read to read files.\n - If you _still_ need to run `grep`, STOP. ALWAYS USE ripgrep at `rg` first, which all Claude Code users have pre-installed.\n  - When issuing multiple commands, use the ';' or '&&' operator to separate them. DO NOT use newlines (newlines are ok in quoted strings).\n  - Try to maintain your current working directory throughout the session by using absolute paths and avoiding usage of `cd`. You may use `cd` if the User explicitly requests it.\n    <good-example>\n    pytest /foo/bar/tests\n    </good-example>\n    <bad-example>\n    cd /foo/bar && pytest tests\n    </bad-example>\n\n\n# Committing changes with git\n\nWhen the user asks you to create a new git commit, follow these steps carefully:\n\n1. You have the capability to call multiple tools in a single response. When multiple independent pieces of information are requested, batch your tool calls together for optimal performance. ALWAYS run the following bash commands in parallel, each using the Bash tool:\n  - Run a git status command to see all untracked files.\n  - Run a git diff command to see both staged and unstaged changes that will be committed.\n  - Run a git log command to see recent commit messages, so that you can follow this repository's commit message style.\n2. Analyze all staged changes (both previously staged and newly added) and draft a commit message:\n  - Summarize the nature of the changes (eg. new feature, enhancement to an existing feature, bug fix, refactoring, test, docs, etc.). Ensure the message accurately reflects the changes and their purpose (i.e. \"add\" means a wholly new feature, \"update\" means an enhancement to an existing feature, \"fix\" means a bug fix, etc.).\n  - Check for any sensitive information that shouldn't be committed\n  - Draft a concise (1-2 sentences) commit message that focuses on the \"why\" rather than the \"what\"\n  - Ensure it accurately reflects the changes and their purpose\n3. You have the capability to call multiple tools in a single response. When multiple independent pieces of information are requested, batch your tool calls together for optimal performance. ALWAYS run the following commands in parallel:\n   - Add relevant untracked files to the staging area.\n   - Create the commit with a message ending with:\n   🤖 Generated with [Claude Code](https://claude.ai/code)\n\n   Co-Authored-By: Claude <noreply@anthropic.com>\n   - Run git status to make sure the commit succeeded.\n4. If the commit fails due to pre-commit hook changes, retry the commit ONCE to include these automated changes. If it fails again, it usually means a pre-commit hook is preventing the commit. If the commit succeeds but you notice that files were modified by the pre-commit hook, you MUST amend your commit to include them.\n\nImportant notes:\n- NEVER update the git config\n- NEVER run additional commands to read or explore code, besides git bash commands\n- NEVER use the TodoWrite or Task tools\n- DO NOT push to the remote repository unless the user explicitly asks you to do so\n- IMPORTANT: Never use git commands with the -i flag (like git rebase -i or git add -i) since they require interactive input which is not supported.\n- If there are no changes to commit (i.e., no untracked files and no modifications), do not create an empty commit\n- In order to ensure good formatting, ALWAYS pass the commit message via a HEREDOC, a la this example:\n<example>\ngit commit -m \"$(cat <<'EOF'\n   Commit message here.\n\n   🤖 Generated with [Claude Code](https://claude.ai/code)\n\n   Co-Authored-By: Claude <noreply@anthropic.com>\n   EOF\n   )\"\n</example>\n\n# Creating pull requests\nUse the gh command via the Bash tool for ALL GitHub-related tasks including working with issues, pull requests, checks, and releases. If given a Github URL use the gh command to get the information needed.\n\nIMPORTANT: When the user asks you to create a pull request, follow these steps carefully:\n\n1. You have the capability to call multiple tools in a single response. When multiple independent pieces of information are requested, batch your tool calls together for optimal performance. ALWAYS run the following bash commands in parallel using the Bash tool, in order to understand the current state of the branch since it diverged from the main branch:\n   - Run a git status command to see all untracked files\n   - Run a git diff command to see both staged and unstaged changes that will be committed\n   - Check if the current branch tracks a remote branch and is up to date with the remote, so you know if you need to push to the remote\n   - Run a git log command and `git diff [base-branch]...HEAD` to understand the full commit history for the current branch (from the time it diverged from the base branch)\n2. Analyze all changes that will be included in the pull request, making sure to look at all relevant commits (NOT just the latest commit, but ALL commits that will be included in the pull request!!!), and draft a pull request summary\n3. You have the capability to call multiple tools in a single response. When multiple independent pieces of information are requested, batch your tool calls together for optimal performance. ALWAYS run the following commands in parallel:\n   - Create new branch if needed\n   - Push to remote with -u flag if needed\n   - Create PR using gh pr create with the format below. Use a HEREDOC to pass the body to ensure correct formatting.\n<example>\ngh pr create --title \"the pr title\" --body \"$(cat <<'EOF'\n## Summary\n<1-3 bullet points>\n\n## Test plan\n[Checklist of TODOs for testing the pull request...]\n\n🤖 Generated with [Claude Code](https://claude.ai/code)\nEOF\n)\"\n</example>\n\nImportant:\n- NEVER update the git config\n- DO NOT use the TodoWrite or Task tools\n- Return the PR URL when you're done, so the user can see it\n\n# Other common operations\n- View comments on a Github PR: gh api repos/foo/bar/pulls/123/comments")]
    public async Task<string> ExecuteAsync(
        [Description("The command to execute")]
        string command,
        [Description("Optional timeout in milliseconds (max 600000)")]
        int timeout = 120000,
        [Description(
            "Clear, concise description of what this command does in 5-10 words, in active voice. Examples:\nInput: ls\nOutput: List files in current directory\n\nInput: git status\nOutput: Show working tree status\n\nInput: npm install\nOutput: Install package dependencies\n\nInput: mkdir foo\nOutput: Create directory 'foo'")]
        string? description = null,
        [Description("Set to true to run this command in the background. Use BashOutput to read the output later.")]
        bool run_in_background = false
    )
    {
        try
        {
            // Validate timeout
            var actualTimeout = ValidateTimeout(timeout);

            // Preprocess command for security and path handling
            var processedCommand = PreprocessCommand(command);

            if (run_in_background)
            {
                return await RunCommandInBackground(processedCommand, actualTimeout, description);
            }
            else
            {
                return await RunCommandSynchronous(processedCommand, actualTimeout);
            }
        }
        catch (Exception ex)
        {
            return $"Error executing command: {ex.Message}";
        }
    }

    private static int ValidateTimeout(int? timeout)
    {
        const int defaultTimeout = 120000; // 2 minutes
        const int maxTimeout = 600000; // 10 minutes

        if (!timeout.HasValue)
            return defaultTimeout;

        if (timeout.Value > maxTimeout)
            return maxTimeout;

        if (timeout.Value < 1000) // Minimum 1 second
            return 1000;

        return timeout.Value;
    }

    private static string PreprocessCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command cannot be empty");

        // Basic security validation - reject potentially dangerous commands
        var lowerCommand = command.ToLower().Trim();

        // Check for discouraged commands
        if (IsDiscouragedCommand(lowerCommand))
        {
            throw new ArgumentException($"Command '{command}' is discouraged. Use appropriate tools instead.");
        }

        // Auto-quote paths with spaces (basic implementation)
        return QuotePathsWithSpaces(command);
    }

    private static bool IsDiscouragedCommand(string lowerCommand)
    {
        var discouragedCommands = new[]
        {
            "find ", "grep ", "cat ", "head ", "tail "
        };

        return discouragedCommands.Any(cmd => lowerCommand.StartsWith(cmd));
    }

    private static string QuotePathsWithSpaces(string command)
    {
        // Enhanced implementation to auto-quote paths with spaces
        if (string.IsNullOrWhiteSpace(command))
            return command;

        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var processedParts = new List<string>();

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            // Check if this looks like a path and contains spaces that aren't already quoted
            if (IsLikelyPath(part) && part.Contains(' ') && !IsAlreadyQuoted(part))
            {
                processedParts.Add($"\"{part}\"");
            }
            else
            {
                processedParts.Add(part);
            }
        }

        return string.Join(" ", processedParts);
    }

    private static bool IsLikelyPath(string text)
    {
        // Simple heuristics to identify potential paths
        return text.Contains('/') || text.Contains('\\') ||
               text.StartsWith('.') || text.StartsWith('~') ||
               (text.Length > 2 && text[1] == ':'); // Windows drive letters
    }

    private static bool IsAlreadyQuoted(string text)
    {
        return (text.StartsWith('"') && text.EndsWith('"')) ||
               (text.StartsWith('\'') && text.EndsWith('\''));
    }

    private static async Task<string> RunCommandSynchronous(string command, int timeout)
    {
        var process = CreateShellProcess();
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                output.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                error.AppendLine(e.Data);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Send command to shell
            await process.StandardInput.WriteLineAsync(command);
            await process.StandardInput.FlushAsync();

            // Wait for completion or timeout
            var completed = process.WaitForExit(timeout);

            if (!completed)
            {
                process.Kill();
                return "Command timed out and was terminated.";
            }

            var result = new StringBuilder();

            if (output.Length > 0)
                result.AppendLine(output.ToString());

            if (error.Length > 0)
                result.AppendLine($"Error: {error}");

            var resultString = result.ToString();
            return TruncateOutput(resultString);
        }
        finally
        {
            process.Dispose();
        }
    }

    private static async Task<string> RunCommandInBackground(string command, int timeout, string? description)
    {
        var process = CreateShellProcess();
        var processId = Guid.NewGuid().ToString("N")[..8];

        var backgroundProcess = new BackgroundProcess
        {
            Process = process,
            Description = string.IsNullOrWhiteSpace(description) ? "Background command" : description
        };

        BackgroundProcesses[processId] = backgroundProcess;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                backgroundProcess.Output.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                backgroundProcess.Error.AppendLine(e.Data);
            }
        };

        process.Exited += (_, _) => { backgroundProcess.IsCompleted = true; };

        try
        {
            process.EnableRaisingEvents = true;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.StandardInput.WriteLineAsync(command);
            await process.StandardInput.FlushAsync();

            return $"Command started in background (ID: {processId}). Description: {backgroundProcess.Description}\n" +
                   "Use BashOutput tool to monitor progress.";
        }
        catch
        {
            BackgroundProcesses.TryRemove(processId, out _);
            process.Dispose();
            throw;
        }
    }

    private static Process CreateShellProcess()
    {
        var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = isWindows ? "powershell.exe" : "/bin/bash",
                Arguments = isWindows ? "-Command -" : "",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Environment.CurrentDirectory
            }
        };

        return process;
    }

    // Additional helper methods for managing background processes
    public static string[] GetBackgroundProcessIds()
    {
        return BackgroundProcesses.Keys.ToArray();
    }

    public static string? GetBackgroundProcessOutput(string processId)
    {
        if (!BackgroundProcesses.TryGetValue(processId, out var bgProcess))
            return null;

        var result = new StringBuilder();

        if (bgProcess.Output.Length > 0)
            result.AppendLine(bgProcess.Output.ToString());

        if (bgProcess.Error.Length > 0)
            result.AppendLine($"Error: {bgProcess.Error}");

        if (bgProcess.IsCompleted)
            result.AppendLine("[Process completed]");

        return TruncateOutput(result.ToString());
    }

    public static bool KillBackgroundProcess(string processId)
    {
        if (!BackgroundProcesses.TryRemove(processId, out var bgProcess))
            return false;

        try
        {
            if (!bgProcess.Process.HasExited)
                bgProcess.Process.Kill();
            bgProcess.Process.Dispose();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string TruncateOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
            return string.Empty;

        if (output.Length <= MaxOutputLength)
            return output;

        return output[..MaxOutputLength] + "\n\n[Output truncated - exceeded 30000 characters]";
    }
}