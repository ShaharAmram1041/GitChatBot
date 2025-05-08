using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using SemanticKernelPlayground.Plugins.Commits;
using System;
using System.IO;

// Configuration setup
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");
var githubUsername = configuration["GitHub:Username"] ?? throw new ApplicationException("GitHub:Username not found");
var githubToken = configuration["GitHub:Token"] ?? throw new ApplicationException("GitHub:Token not found");

// Create the kernel and add the Azure OpenAI chat completion service
var builder = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(modelName, endpoint, apiKey);

// Register the kernel functions
builder.Plugins.AddFromType<CommitsPlugin>();




// Build the kernel
var kernel = builder.Build();


// Create the chat completion service
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

AzureOpenAIPromptExecutionSettings openAiPromptExecutionSettings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

// Get the kernel function
var getCommits = kernel.Plugins.GetFunction("CommitsPlugin", "GetCommits");


// Get the skprompt
string releaseNotesPluginDir = Path.Combine(AppContext.BaseDirectory, "../../../../SemanticKernelPlayground/Plugins/ReleaseNotes");
var releaseNotesPlugin = kernel.CreatePluginFromPromptDirectory(releaseNotesPluginDir, "ReleaseNotes");
var generateReleaseNotes = releaseNotesPlugin["GenerateReleaseNotes"];

// Run the chatbot
var gitChatBot = new GitChatBot(kernel, chatCompletionService, getCommits, generateReleaseNotes, githubUsername, githubToken);
await gitChatBot.RunAsync();

