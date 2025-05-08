using LibGit2Sharp;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using System;
using System.IO;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;


public class GitChatBot
{
    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;
    private readonly KernelFunction _getCommits;
    private readonly KernelFunction _generateReleaseNotes;
    private readonly string _githubUsername;
    private readonly string _githubToken;

    private string _currentRepoPath = string.Empty;


    public GitChatBot(
         Kernel kernel,
         IChatCompletionService chatService,
         KernelFunction getCommits,
         KernelFunction generateReleaseNotes,
         string githubUsername,
         string githubToken)
    {
        _kernel = kernel;
        _chatService = chatService;
        _getCommits = getCommits;
        _generateReleaseNotes = generateReleaseNotes;
        _githubUsername = githubUsername;
        _githubToken = githubToken;
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

            // Handle change repo
            if (userInput.Contains("change repo", StringComparison.OrdinalIgnoreCase))
            {
                _currentRepoPath = string.Empty;
                Console.WriteLine("Repository path cleared. You will be prompted again.");
                continue;
            }

            // Release Notes command
            if (userInput.Contains("release notes", StringComparison.OrdinalIgnoreCase))
            {
                var repoPath = GetOrPromptRepoPath();
                if (string.IsNullOrEmpty(repoPath)) return;

                var commitResult = await _kernel.InvokeAsync(_getCommits, new KernelArguments
                {
                    ["repoPath"] = repoPath
                });

                string commits = commitResult.GetValue<string>() ?? "";

                if (string.IsNullOrWhiteSpace(commits))
                {
                    Console.WriteLine("No commits found.");
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

            // Commit command
            if (userInput.Contains("commit", StringComparison.OrdinalIgnoreCase))
            {
                var repoPath = GetOrPromptRepoPath();
                if (string.IsNullOrEmpty(repoPath)) return;

                Console.Write("Want to get the commits from the branch? (yes/no): ");
                var yesNo = Console.ReadLine()?.Trim().ToLower();

                if (string.IsNullOrWhiteSpace(yesNo) || (yesNo != "yes" && yesNo != "no"))
                {
                    Console.WriteLine("Invalid input. Please enter 'yes' or 'no'.");
                    continue;
                }

                if (yesNo == "yes")
                {
                    var commitResult = await _kernel.InvokeAsync(_getCommits, new KernelArguments
                    {
                        ["repoPath"] = repoPath
                    });

                    string commits = commitResult.GetValue<string>() ?? "";

                    if (string.IsNullOrWhiteSpace(commits))
                    {
                        Console.WriteLine("No commits found.");
                        continue;
                    }
                    Console.WriteLine("\n\n" + commits);
                    continue;

                }
                else
                {
                    Console.Write("Enter commit message: ");
                    var message = Console.ReadLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        Console.WriteLine("Commit message cannot be empty.");
                        continue;
                    }

                    gitActions("commit", message);                
                    continue;
                }
            }

            // Push command
            if (userInput.Contains("push", StringComparison.OrdinalIgnoreCase))
            {
                var repoPath = GetOrPromptRepoPath();
                if (string.IsNullOrEmpty(repoPath)) return;
                gitActions("push");
                continue;
            }


            // Pull command
            if (userInput.Contains("pull", StringComparison.OrdinalIgnoreCase))
            {
                var repoPath = GetOrPromptRepoPath();
                if (string.IsNullOrEmpty(repoPath)) return;
                gitActions("pull");
                continue;
            }


            // Default chat flow
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

    private string GetOrPromptRepoPath(bool forcePrompt = false)
    {
        if (!forcePrompt && Directory.Exists(Path.Combine(_currentRepoPath ?? "", ".git")))
        {
            return _currentRepoPath!;
        }

        Console.Write("Enter Git repo path: ");
        var inputPath = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(inputPath) || !Directory.Exists(Path.Combine(inputPath, ".git")))
        {
            Console.WriteLine("Invalid Git repository path.");
            return string.Empty;
        }

        _currentRepoPath = inputPath;
        return _currentRepoPath;
    }


    private void gitActions(string action, string commitMessage="")
    {                
        using var repo = new Repository(_currentRepoPath);

        switch (action.ToLower())
        {
            case "commit":
                Commands.Stage(repo, "*");

                var author = new Signature("ChatBot", "chatbot@example.com", DateTimeOffset.Now);
                var commit = repo.Commit(commitMessage, author, author);

                Console.WriteLine($"Commit successful: {commit.Sha}"); 
                break;

            case "push":
                var remote = repo.Network.Remotes["origin"];
                var options = new PushOptions
                {
                    CredentialsProvider = (_url, _user, _cred) =>
                        new UsernamePasswordCredentials
                        {
                            Username = _githubUsername,
                            Password = _githubToken
                        }
                };

                Console.WriteLine($"Pushing to github...");
                repo.Network.Push(remote, @"refs/heads/master", options);
                Console.WriteLine("Push successful.");
                break;

            case "pull":
                var signature = new Signature("ChatBot", "chatbot@example.com", DateTimeOffset.Now);

                var pullOptions = new PullOptions
                {
                    FetchOptions = new FetchOptions
                    {
                        CredentialsProvider = (_url, _user, _cred) =>
                            new UsernamePasswordCredentials
                            {
                                Username = _githubUsername,
                                Password = _githubToken
                            }
                    }
                };

                var result = Commands.Pull(repo, signature, pullOptions);
                Console.WriteLine($"Pull result: {result.Status}"); 
                break;

            default:
                Console.WriteLine("Invalid action.");
                break;
        }
    }
}
