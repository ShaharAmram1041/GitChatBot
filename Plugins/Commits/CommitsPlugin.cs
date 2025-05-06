using Microsoft.SemanticKernel;
using LibGit2Sharp;
using System.Text;
using System.ComponentModel;

namespace SemanticKernelPlayground.Plugins.Commits;

public class CommitsPlugin
{
    [KernelFunction, Description("Retrieves the last 10 commits from a local Git repository at the specified path.")]
    public string GetCommits(string repoPath)
    {
        if (string.IsNullOrWhiteSpace(repoPath) || !Directory.Exists(repoPath))
        {
            return "The specified path is invalid or does not exist.";
        }

        var gitFolder = Path.Combine(repoPath, ".git");
        if (!Directory.Exists(gitFolder))
        {
            return "The specified path is not a Git repository.";
        }

        try
        {
            var sb = new StringBuilder();
            using var repo = new Repository(repoPath);

            var commits = repo.Commits.Take(10).ToList();

            if (!commits.Any())
            {
                return "No commits found in this repository.";
            }

            for (int i = 0; i < commits.Count; i++)
            {
                var commit = commits[i];
                sb.AppendLine($"{i + 1}. {commit.MessageShort}");
                sb.AppendLine($"   - Author: {commit.Author.Name}");
                sb.AppendLine($"   - Date: {commit.Author.When.LocalDateTime}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Failed to read Git commits: {ex.Message}";
        }
    }
}
