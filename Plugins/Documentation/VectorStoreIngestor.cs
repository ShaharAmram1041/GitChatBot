#pragma warning disable SKEXP0001

using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.VectorData;
using SemanticKernelPlayground.Plugins.Documentation;

namespace SemanticKernelPlayground.Infrastructure;

public class VectorStoreIngestor
{
    private readonly IVectorStore _vectorStore;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private const int MaxAllowedChunkLength = 3000;

    public VectorStoreIngestor(IVectorStore vectorStore, ITextEmbeddingGenerationService embeddingService)
    {
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
    }

    public async Task IngestAsync(IEnumerable<CodeFile> files, string collectionName = "code_docs")
    {
        try
        {
            var collection = _vectorStore.GetCollection<string, CodeChunk>(collectionName);
            await collection.CreateCollectionIfNotExistsAsync();

            int chunkCounter = 0;
            foreach (var file in files)
            {
                try
                {
                    foreach (var chunk in Chunker.ChunkByMethod(file.Content))
                    {
                    
                        var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk);

                        var record = new CodeChunk
                        {
                            Key = $"chunk-{chunkCounter++}",
                            FilePath = file.FilePath,
                            Content = chunk,
                            Embedding = embedding
                        };

                        await collection.UpsertAsync(record);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file '{file.FilePath}': {ex.Message}");
                }
            }

            var results = collection.GetAsync(_ => true, int.MaxValue);
            Console.WriteLine("Codebase chunks saved into memory.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to ingest code files into vector store: {ex.Message}");
        }
    }
}
