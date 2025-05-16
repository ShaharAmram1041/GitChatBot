using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;

public class CodeChunk
{
    [VectorStoreRecordKey]
    [TextSearchResultName]
    public required string Key { get; init; }

    [VectorStoreRecordData]
    [TextSearchResultValue]
    public required string Content { get; init; }

    [VectorStoreRecordData]
    public required string FilePath { get; init; }

    [VectorStoreRecordVector(3072)]
    public required ReadOnlyMemory<float> Embedding { get; set; }

}
