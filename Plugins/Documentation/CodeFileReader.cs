using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticKernelPlayground.Plugins.Documentation
{
    public class CodeFile
    {
        public string FilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
    
    public static class CodeFileReader
    {
        public static List<CodeFile> ReadCodeFiles(string rootDirectory)
        {
            var codeFiles = new List<CodeFile>();
            var excludedFolders = new[] { ".git", ".vs", "bin", "obj", "node_modules" };

            foreach (var file in Directory.EnumerateFiles(rootDirectory, "*.*", SearchOption.AllDirectories))
            {
                try
                {
                    // Skip files from excluded folders
                    if (excludedFolders.Any(f => file.Contains(Path.DirectorySeparatorChar + f + Path.DirectorySeparatorChar)))
                        continue;

                    var content = File.ReadAllText(file);
                    codeFiles.Add(new CodeFile
                    {
                        FilePath = file,
                        Content = content
                    });
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
            }

            return codeFiles;
        }
}
}

