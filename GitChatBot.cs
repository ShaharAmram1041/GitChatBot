using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel;
using System;
using System.Threading.Tasks;
using System.IO;

public class GitChatBot
{
    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;
    private readonly KernelFunction _getCommits;
    private readonly KernelFunction _generateReleaseNotes;

    public GitChatBot(
         Kernel kernel,
         IChatCompletionService chatService,
         KernelFunction getCommits,
         KernelFunction generateReleaseNotes)
        {
            _kernel = kernel;
            _chatService = chatService;
            _getCommits = getCommits;
            _generateReleaseNotes = generateReleaseNotes;
        }


    public async Task RunAsync()
    {
       
        var history = new ChatHistory();
        var userInput = string.Empty;

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Me -> ");
            Console.ResetColor();

            userInput = Console.ReadLine()?.Trim() ?? string.Empty;

            if (string.Equals(userInput, "exit", StringComparison.OrdinalIgnoreCase))
                break;

            if (userInput.Contains("release notes", StringComparison.OrdinalIgnoreCase))
            {
                Console.Write("Enter Git repo path: ");
                var repoPath = Console.ReadLine();

                if (!Directory.Exists(Path.Combine(repoPath, ".git")))
                {
                    Console.WriteLine("Not a Git repository.");
                    continue;
                }

                var commitResult = await _kernel.InvokeAsync(_getCommits, new KernelArguments
                {
                    ["repoPath"] = repoPath
                });

                string commits = commitResult.GetValue<string>() ?? "";

                if (string.IsNullOrWhiteSpace(commits))
                {
                    Console.WriteLine("⚠️ No commits found.");
                    continue;
                }

                var notesResult = await _kernel.InvokeAsync(_generateReleaseNotes, new KernelArguments
                {
                    ["input"] = commits
                });

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Release Notes:\n" + notesResult.GetValue<string>());
                Console.ResetColor();
                continue;
            }

            if (userInput.Contains("commit", StringComparison.OrdinalIgnoreCase))
            {
                Console.Write("Enter path to Git repo: ");
                var repoPath = Console.ReadLine();

                if (!Directory.Exists(Path.Combine(repoPath, ".git")))
                {
                    Console.WriteLine("Not a Git repository.");
                    continue;
                }

                var commitResult = await _kernel.InvokeAsync(_getCommits, new KernelArguments
                {
                    ["repoPath"] = repoPath
                });

                var commits = commitResult.GetValue<string>() ?? "";

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Commits:\n" + commits);
                Console.ResetColor();
                continue; // ✅ CRITICAL: prevents fall-through to GPT
            }

            // Default chat flow (ONLY runs when no command matched)
            history.AddUserMessage(userInput);

            var response = _chatService.GetStreamingChatMessageContentsAsync(
                history,
                new AzureOpenAIPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                },
                _kernel
            );

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Agent -> ");
            Console.ResetColor();

            string fullResponse = "";
            await foreach (var chunk in response)
            {
                Console.Write(chunk.Content);
                fullResponse += chunk.Content;
            }

            Console.WriteLine();
            history.AddMessage(AuthorRole.Assistant, fullResponse);
        }
    }
}
