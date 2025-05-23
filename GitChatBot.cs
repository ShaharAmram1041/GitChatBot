using LibGit2Sharp;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using SemanticKernelPlayground.Infrastructure;
using SemanticKernelPlayground.Plugins.Documentation;

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
    private readonly AzureOpenAIPromptExecutionSettings _promptSettings;

    private string _currentRepoPath = string.Empty;


    public GitChatBot(
         Kernel kernel,
         IChatCompletionService chatService,
         KernelFunction getCommits,
         KernelFunction generateReleaseNotes,
         VectorStoreIngestor vectorStoreIngestor,
         CodeSearchPlugin codebaseQnAService,
         string githubUsername,
         string githubToken,
         AzureOpenAIPromptExecutionSettings promptSettings)
    {
        _kernel = kernel;
        _chatService = chatService;
        _getCommits = getCommits;
        _generateReleaseNotes = generateReleaseNotes;
        _vectorStoreIngestor = vectorStoreIngestor;
        _codebaseQnAService = codebaseQnAService;
        _githubUsername = githubUsername;
        _githubToken = githubToken;
        _promptSettings = promptSettings;
    }

    public async Task RunAsync()
    {
        DisplayAvailableCommands();
        var history = new ChatHistory();
        string userInput;

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Me -> ");
            Console.ResetColor();

            userInput = Console.ReadLine()?.Trim() ?? string.Empty;
            string normalizedInput = userInput.ToLowerInvariant();

            if (normalizedInput == "exit")
                break;

            if (normalizedInput == "help" || normalizedInput == "?")
            {
                DisplayAvailableCommands();
                continue;
            }

            if (normalizedInput.Contains("change repo"))
            {
                _currentRepoPath = string.Empty;
                Console.WriteLine("Repository path cleared. You will be prompted again.");
                continue;
            }

            if (normalizedInput.Contains("release notes"))
            {
                var repoPath = GetOrPromptRepoPath();
                if (string.IsNullOrEmpty(repoPath)) continue;

                var commitResult = await _kernel.InvokeAsync(_getCommits, new KernelArguments
                {
                    ["repoPath"] = repoPath
                });

                var commits = commitResult.GetValue<string>() ?? "";

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
                Console.WriteLine("\nRelease Notes:\n" + notesResult.GetValue<string>());
                Console.ResetColor();
                continue;
            }

            if (normalizedInput.Contains("commit"))
            {
                var repoPath = GetOrPromptRepoPath();
                if (string.IsNullOrEmpty(repoPath)) continue;

                Console.WriteLine("What would you like to do?");
                Console.WriteLine(" ├─ Type `view` to see recent commits.");
                Console.WriteLine(" ├─ Type `commit` to make a new commit.");
                Console.Write("Your choice: ");

                var commitAction = Console.ReadLine()?.Trim().ToLowerInvariant();

                switch (commitAction)
                {
                    case "view":
                        var commitResult = await _kernel.InvokeAsync(_getCommits, new KernelArguments
                        {
                            ["repoPath"] = repoPath
                        });

                        var commitLog = commitResult.GetValue<string>() ?? "";
                        if (string.IsNullOrWhiteSpace(commitLog))
                        {
                            Console.WriteLine("No commits found.");
                        }
                        else
                        {
                            Console.WriteLine("Recent Commits:\n" + commitLog);
                        }
                        break;

                    case "commit":
                        Console.Write("Enter commit message: ");
                        var message = Console.ReadLine()?.Trim();

                        if (string.IsNullOrWhiteSpace(message))
                        {
                            Console.WriteLine("Commit message cannot be empty.");
                        }
                        else
                        {
                            gitActions("commit", message);
                        }
                        break;

                    default:
                        Console.WriteLine("Invalid option. Please enter `view` or `commit`.");
                        break;
                }
                continue;
            }

            if (normalizedInput.Contains("push"))
            {
                var repoPath = GetOrPromptRepoPath();
                if (string.IsNullOrEmpty(repoPath)) continue;

                gitActions("push");
                continue;
            }

            if (normalizedInput.Contains("pull"))
            {
                var repoPath = GetOrPromptRepoPath();
                if (string.IsNullOrEmpty(repoPath)) continue;

                gitActions("pull");
                continue;
            }

            if (normalizedInput == "codebase")
            {
                var repoPath = GetOrPromptRepoPath();
                if (string.IsNullOrEmpty(repoPath)) continue;

                Console.WriteLine("Indexing codebase, please wait...");
                var files = CodeFileReader.ReadCodeFiles(repoPath);
                await _vectorStoreIngestor.IngestAsync(files);
                Console.WriteLine("Codebase indexed. You can now ask questions.");

                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Ask a question about the codebase.");
                    Console.WriteLine("(Type 'change-repo' to switch repositories or 'exit' to quit).");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("Me -> ");
                    Console.ResetColor();


                    var query = Console.ReadLine()?.Trim();
                    if (string.IsNullOrEmpty(query)) continue;

                    if (query.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;
                    if (query.Equals("change-repo", StringComparison.OrdinalIgnoreCase))
                    {
                        repoPath = GetOrPromptRepoPath(forcePrompt: true);
                        if (string.IsNullOrEmpty(repoPath)) break;

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
                    Console.Write("Agent -> ");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(answer.GetValue<string>());
                    Console.ResetColor();
                }

                continue;
            }

            // Fallback: General chat assistant
            history.AddUserMessage(userInput);
            
            var response = await _chatService.GetChatMessageContentAsync(history, _promptSettings, _kernel);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Agent -> ");
            Console.ResetColor();

            Console.WriteLine(response.Content);
            history.AddAssistantMessage(response.Content!);

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

    private void DisplayAvailableCommands()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Welcome to GitChatBot :)");
        Console.WriteLine("You can interact with your Git repository and codebase. Here are some commands you can use:");
        Console.WriteLine(" ├─ 'release notes`      - Generate release notes from recent commits");
        Console.WriteLine(" ├─ `commit`             - View or make a new commit");
        Console.WriteLine(" ├─ 'push`               - Push your local changes to GitHub");
        Console.WriteLine(" ├─ 'pull`               - Pull latest changes from GitHub");
        Console.WriteLine(" ├─ `codebase`           - Load and ask questions about the codebase");
        Console.WriteLine(" ├─ `change repo`        - Switch to a different Git repository");
        Console.WriteLine(" └─ `exit`               - Quit the application");
        Console.WriteLine("\nYou can also ask free-form natural language questions at any time.\n");
        Console.ResetColor();
    }

}
