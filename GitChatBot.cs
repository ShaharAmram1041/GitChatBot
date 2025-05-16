using LibGit2Sharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Memory;
using SemanticKernelPlayground.Infrastructure;
using SemanticKernelPlayground.Plugins.Documentation;
using System;


public class GitChatBot
{
    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;
    private readonly KernelFunction _getCommits;
    private readonly KernelFunction _generateReleaseNotes;
    private readonly VectorStoreIngestor _vectorStoreIngestor;
    private readonly CodeSearchPlugin _codebaseQnAService;
    private readonly string _githubUsername;
    private readonly string _githubToken;

    private string _currentRepoPath = string.Empty;


    public GitChatBot(
         Kernel kernel,
         IChatCompletionService chatService,
         KernelFunction getCommits,
         KernelFunction generateReleaseNotes,
         VectorStoreIngestor vectorStoreIngestor,
         CodeSearchPlugin codebaseQnAService,
         string githubUsername,
         string githubToken)
    {
        _kernel = kernel;
        _chatService = chatService;
        _getCommits = getCommits;
        _generateReleaseNotes = generateReleaseNotes;
        _vectorStoreIngestor = vectorStoreIngestor;
        _codebaseQnAService = codebaseQnAService;
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

                Console.Write("Do you want to see recent commits before committing? (yes/no): ");
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


            // Questions
            // Excercise 2
            if (string.Equals(userInput, "codebase", StringComparison.OrdinalIgnoreCase))
            {
                var repoPath = GetOrPromptRepoPath();
                if (string.IsNullOrEmpty(repoPath)) return;

                Console.WriteLine("Indexing codebase, please wait...");
                var files = CodeFileReader.ReadCodeFiles(repoPath);
                await _vectorStoreIngestor.IngestAsync(files);
                Console.WriteLine("Codebase is ready. You can now ask questions.");
                var flag = false;

                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("\n -> Ask a question about the codebase (or type 'change-repo' or 'exit'): ");
                    Console.ResetColor();

                    var query = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(query)) continue;

                    if (query.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Exiting code.");
                        flag = true;
                        break;
                    }

                    if (query.Equals("change-repo", StringComparison.OrdinalIgnoreCase))
                    {
                        repoPath = GetOrPromptRepoPath(forcePrompt: true);
                        if (string.IsNullOrEmpty(repoPath)) return;

                        Console.WriteLine("Re-indexing new codebase...");
                        files = CodeFileReader.ReadCodeFiles(repoPath);
                        await _vectorStoreIngestor.IngestAsync(files);
                        Console.WriteLine("New codebase is ready.");
                        continue;
                    }

                    var answer = await _kernel.InvokeAsync("CodeSearchPlugin", "AskQuestionAsync", new KernelArguments
                    {
                        { "query", query }
                    });


                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("Agent -> \n");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(answer.GetValue<string>());
                    Console.ResetColor();
                }
                if (flag)
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
