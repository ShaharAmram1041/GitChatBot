using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using System.Text;
using LibGit2Sharp;
using SemanticKernelPlayground.Plugins.ReleaseNotes;
using SemanticKernelPlayground.Plugins.Commits;




// Configuration setup
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");


// Create the kernel and add the Azure OpenAI chat completion service
var builder = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(modelName, endpoint, apiKey);

// Register the kernel functions
builder.Plugins.AddFromType<CommitsPlugin>();
builder.Plugins.AddFromType<ReleaseNotesPlugin>();




// Build the kernel
var kernel = builder.Build();

// Create the chat completion service
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

AzureOpenAIPromptExecutionSettings openAiPromptExecutionSettings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

// Get the kernel functions
var generateReleaseNotesFn = kernel.Plugins.GetFunction("ReleaseNotesPlugin", "GenerateReleaseNotes");
var getCommits = kernel.Plugins.GetFunction("CommitsPlugin", "GetCommits");







var history = new ChatHistory();

do
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("Me -> ");
    Console.ResetColor();

    var userInput = Console.ReadLine();
    if (userInput == "exit")
    {
        break;
    }

    // === CUSTOM COMMAND TO GENERATE RELEASE NOTES ===
    if (userInput!.Trim().ToLower().Contains("release notes"))
    {
        Console.Write("Enter path to Git repo: ");
        var repoPath = Console.ReadLine();

        if (!Directory.Exists(Path.Combine(repoPath, ".git")))
        {
            Console.WriteLine("Not a Git repository.");
            continue;
        }

        // Call Semantic Kernel function
        var result = await kernel.InvokeAsync(generateReleaseNotesFn, new KernelArguments
        {
            ["repoPath"] = repoPath
        });

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Generated Release Notes:");
        Console.ResetColor();
        Console.WriteLine(result.GetValue<string>());
        continue; // Skip chat
    }

    // === CUSTOM COMMAND TO GENERATE RELEASE NOTES ===
    if (userInput!.Trim().ToLower().Contains("commit"))
    {
        Console.Write("Enter path to Git repo: ");
        var repoPath = Console.ReadLine();

        if (!Directory.Exists(Path.Combine(repoPath, ".git")))
        {
            Console.WriteLine("Not a Git repository.");
            continue;
        }

        // Call Semantic Kernel function
        var result = await kernel.InvokeAsync(getCommits, new KernelArguments
        {
            ["repoPath"] = repoPath
        });

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Commits:");
        Console.ResetColor();
        Console.WriteLine(result.GetValue<string>());
        continue; // Skip chat
    }






    // === DEFAULT CHAT FLOW ===
    history.AddUserMessage(userInput!);

    var streamingResponse =
        chatCompletionService.GetStreamingChatMessageContentsAsync(
            history,
            openAiPromptExecutionSettings,
            kernel);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("Agent -> ");
    Console.ResetColor();

    var fullResponse = "";
    await foreach (var chunk in streamingResponse)
    {
        Console.Write(chunk.Content);
        fullResponse += chunk.Content;
    }
    Console.WriteLine();

    history.AddMessage(AuthorRole.Assistant, fullResponse);

} while (true);
