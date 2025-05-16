using System.Text.RegularExpressions;
using SharpToken;

namespace SemanticKernelPlayground.Plugins.Documentation
{
    public static class Chunker
    {
        private const int MaxTokensPerChunk = 2048;
        private const int TokenStride = 1024; // for overlapping context



        // Option 1: chunck by method, regular expression
        public static IEnumerable<string> ChunkByMethod(string fileContent)
        {
            var chunks = new List<string>();

            try
            {
                var methodRegex = new Regex(@"(public|private|protected|internal)\s+[\w<>\[\]]+\s+\w+\s*\(.*?\)\s*\{", RegexOptions.Singleline);
                var matches = methodRegex.Matches(fileContent);

                if (matches.Count == 0)
                {
                    // Fallback: treat the whole file as one chunk
                    chunks.Add(fileContent);
                    return chunks;
                }

                for (int i = 0; i < matches.Count; i++)
                {
                    var currentStart = matches[i].Index;
                    var currentEnd = (i < matches.Count - 1) ? matches[i + 1].Index : fileContent.Length;

                    var chunkContent = fileContent.Substring(currentStart, currentEnd - currentStart);
                    var methodSignature = matches[i].Value.Trim();
                    var methodNameMatch = Regex.Match(methodSignature, @"\s(\w+)\s*\(");

                    string methodName = methodNameMatch.Success ? methodNameMatch.Groups[1].Value : $"method_{i}";

                    chunks.Add(chunkContent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while chunking file: {ex.Message}");
                chunks.Add("Chunking failed due to an error. Original content is returned as a fallback.");
                chunks.Add(fileContent);
            }

            return chunks;
        }




        // Option 2: chunck by number of tokens
        public static IEnumerable<string> ChunkByTokens(string fileContent, string model = "text-embedding-3-large")
        {
            var encoding = GptEncoding.GetEncodingForModel(model);
            var tokens = encoding.Encode(fileContent);

            for (int i = 0; i < tokens.Count; i += TokenStride)
            {
                var chunkTokens = tokens.Skip(i).Take(MaxTokensPerChunk).ToList();
                if (chunkTokens.Count == 0) break;

                string chunk = encoding.Decode(chunkTokens);
                yield return chunk;
            }
        }
    }


}
