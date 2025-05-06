using Microsoft.SemanticKernel;
using LibGit2Sharp;
using System.Text;
using System.ComponentModel;

namespace SemanticKernelPlayground.Plugins.ReleaseNotes;

public class ReleaseNotesPlugin
{
    [KernelFunction, 
    Description("Generates basic release notes by reading the last " +
    "20 Git commits from the specified local repository path. Each commit is formatted with author and timestamp.")]
    public string GenerateReleaseNotes(string repoPath)
    {
        if (!Directory.Exists(Path.Combine(repoPath, ".git")))
        {
            return "Not a valid Git repository.";
        }

        var sb = new StringBuilder();

        using var repo = new Repository(repoPath);
        foreach (var commit in repo.Commits.Take(20))
        {
            sb.AppendLine($"- {commit.MessageShort} ({commit.Author.Name} on {commit.Author.When.LocalDateTime})");
        }

        // Very basic release notes logic
        return $"Release Notes:\n{sb}";
    }
}
