#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0020 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0001

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SemanticKernelPlayground.Infrastructure;
using SemanticKernelPlayground.Plugins.Commits;
//using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.VectorData;
using OpenAI.VectorStores;
//using SemanticKernelPlayground.Infrastructure;

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


var builder = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(modelName, endpoint, apiKey) // for chat
    .AddAzureOpenAITextEmbeddingGeneration(embeddingModel, endpoint, apiKey) // for embeddings
    .AddInMemoryVectorStore(); // for memory



builder.Services.AddSingleton<VectorStoreIngestor>();
builder.Services.AddSingleton<CodeSearchPlugin>();




// Load plugins (functions)
builder.Plugins.AddFromType<CommitsPlugin>();
builder.Plugins.AddFromType<CodeSearchPlugin>();



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

// Manually resolve the services
var vectorStore = kernel.GetRequiredService<IVectorStore>();
var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

// Manually create the ingestor
var ingestor = new VectorStoreIngestor(vectorStore, embeddingService);

var codeQnAService = new CodeSearchPlugin(
    embeddingService,
    chatCompletionService,
    vectorStore
);


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

