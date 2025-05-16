#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates.
#pragma warning disable SKEXP0020 // Type is for evaluation purposes only and is subject to change or removal in future updates.
#pragma warning disable SKEXP0001

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.VectorData;
using SemanticKernelPlayground.Infrastructure;
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
var embeddingModel = configuration["EmbeddingModel"] ?? throw new ApplicationException("EmbeddingModel not found");
var githubUsername = configuration["GitHub:Username"] ?? throw new ApplicationException("GitHub:Username not found");
var githubToken = configuration["GitHub:Token"] ?? throw new ApplicationException("GitHub:Token not found");

// Configure kernel and services
var builder = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(modelName, endpoint, apiKey) // Chat
    .AddAzureOpenAITextEmbeddingGeneration(embeddingModel, endpoint, apiKey) // Embeddings
    .AddInMemoryVectorStore(); // Vector memory

// Register custom services
builder.Services.AddSingleton<VectorStoreIngestor>();
builder.Services.AddSingleton<CodeSearchPlugin>();

// Load built-in plugins
builder.Plugins.AddFromType<CommitsPlugin>();
builder.Plugins.AddFromType<CodeSearchPlugin>();

// Build the kernel
var kernel = builder.Build();

// Resolve services from kernel DI
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
var vectorStore = kernel.GetRequiredService<IVectorStore>();
var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
var ingestor = kernel.GetRequiredService<VectorStoreIngestor>();
var codeQnAService = kernel.GetRequiredService<CodeSearchPlugin>();

// Load prompt-based plugin
string releaseNotesPluginDir = Path.Combine(AppContext.BaseDirectory, "../../../../SemanticKernelPlayground/Plugins/ReleaseNotes");
var releaseNotesPlugin = kernel.CreatePluginFromPromptDirectory(releaseNotesPluginDir, "ReleaseNotes");
kernel.Plugins.Add(releaseNotesPlugin);
var generateReleaseNotes = releaseNotesPlugin["GenerateReleaseNotes"];

// Retrieve kernel functions
var getCommits = kernel.Plugins.GetFunction("CommitsPlugin", "GetCommits");

// Run the Git chatbot
var gitChatBot = new GitChatBot(
    kernel,
    chatCompletionService,
    getCommits,
    generateReleaseNotes,
    ingestor,
    codeQnAService,
    githubUsername,
    githubToken);

await gitChatBot.RunAsync();
