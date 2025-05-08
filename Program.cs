using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SemanticKernelPlayground.Plugins.Commits;

// Configuration setup
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .Build();


// Read required settings
var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");
var githubUsername = configuration["GitHub:Username"] ?? throw new ApplicationException("GitHub:Username not found");
var githubToken = configuration["GitHub:Token"] ?? throw new ApplicationException("GitHub:Token not found");


// Configure Semantic Kernel and OpenAI chat model
var builder = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(modelName, endpoint, apiKey);


// Load plugins (functions)
builder.Plugins.AddFromType<CommitsPlugin>();


// Build the kernel
var kernel = builder.Build();


// Get chat completion service from the kernel
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();


// Get kernel function for commits
var getCommits = kernel.Plugins.GetFunction("CommitsPlugin", "GetCommits");


// Load prompt-based plugin for release notes generation
string releaseNotesPluginDir = Path.Combine(AppContext.BaseDirectory, "../../../../SemanticKernelPlayground/Plugins/ReleaseNotes");
var releaseNotesPlugin = kernel.CreatePluginFromPromptDirectory(releaseNotesPluginDir, "ReleaseNotes");
var generateReleaseNotes = releaseNotesPlugin["GenerateReleaseNotes"];


// Run the Git chatbot
var gitChatBot = new GitChatBot(kernel, chatCompletionService, getCommits, generateReleaseNotes, githubUsername, githubToken);
await gitChatBot.RunAsync();

