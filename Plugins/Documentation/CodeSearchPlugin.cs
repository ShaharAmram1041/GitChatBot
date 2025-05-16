#pragma warning disable CS0618 // Obsolete warning
#pragma warning disable SKEXP0001

using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using System.Text;
using System.ComponentModel;

public class CodeSearchPlugin
{
    private readonly ITextEmbeddingGenerationService _embedding;
    private readonly IChatCompletionService _chat;
    private readonly IVectorStore _vectorStore;

    public CodeSearchPlugin(
        ITextEmbeddingGenerationService embedding,
        IChatCompletionService chat,
        IVectorStore vectorStore)
    {
        _embedding = embedding;
        _chat = chat;
        _vectorStore = vectorStore;
    }

    [KernelFunction("AskQuestionAsync"), Description("Ask a natural language question about the codebase.")]
    public async Task<string> AskQuestionAsync(string query)
    {
        try
        {
            var collection = _vectorStore.GetCollection<string, CodeChunk>("code_docs");

            var textSearch = new VectorStoreTextSearch<CodeChunk>(
                collection,
                _embedding);

            var results = await textSearch.GetTextSearchResultsAsync(query, new() { Top = 5 });

            var contextBuilder = new StringBuilder();
            await foreach (var result in results.Results)
            {
                contextBuilder.AppendLine($"// From: {result.Name}");
                contextBuilder.AppendLine(result.Value);
                contextBuilder.AppendLine();
            }

            var prompt = $"""
            You are a code assistant. Use the context below to answer the question.

            Context:
            {contextBuilder}

            Question: {query}
            """;

            var response = await _chat.GetChatMessageContentAsync(new ChatHistory(prompt));
            return response.Content ?? "No response content returned.";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during question answering: {ex.Message}");
            return "Sorry, something went wrong while answering your question.";
        }
    }
}
