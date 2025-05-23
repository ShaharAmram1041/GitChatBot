# 🤖 GitChatBot

**AI-Powered Git Assistant & Codebase Companion — built with .NET 9, Semantic Kernel, and Azure OpenAI**

GitChatBot is a powerful and extensible **.NET 9 console application** that allows developers to interact naturally with Git repositories, commit history, and codebases using **natural language**. It leverages **Microsoft's Semantic Kernel** to orchestrate plugin functionality and **Azure OpenAI** for intelligent response generation.
<br/><br/>


## ✨ Features

- 🔍 View recent Git commits and compare changes
- 📝 Automatically generate professional release notes from commit history
- 💬 Ask questions about the indexed codebase in natural language
- 💡 Let the AI choose and invoke the correct tools automatically
- 🔁 Perform common Git operations: `commit`, `push`, `pull`
- 🧠 Embed and search code using vector memory (Semantic Search)
- 🛠️ Plugin-based architecture with function auto-selection (via `FunctionChoiceBehavior.Auto()`)
<br/><br/>


## 📦 Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/)
- Azure OpenAI access with deployment credentials
- Git repository **must be available locally** on your machine (provide local path)
- Azure Cognitive Search or other vector memory provider (if semantic memory is used)
- API keys/tokens:
  - Azure OpenAI API key and endpoint
  - GitHub token (for authenticated push/pull)


<br/><br/>


## 🚀 Getting Started

Follow these steps to get **GitChatBot** up and running locally:

### 1. Clone the Repository

```bash
git clone https://github.com/ShaharAmram1041/GitChatBot.git
cd GitChatBot
```

### 2. Restore Dependencies

```bash
git restore
```

### 3. Configure Environment Variables

Create a file named appsettings.json in the root directory and add the following structure:
```bash
{
  "ModelName": "<your-model-name>",
  "EmbeddingModel": "<your-embedding-model-name>",
  "Endpoint": "https://<your-endpoint>.openai.azure.com/",
  "ApiKey": "<your-azure-openai-key>",
  "DeploymentName": "<your-deployment-name>"
  "GitHub": {
    "Username": "<your-github-username>",
    "Token": "<your-github-token>"
  }
}
```
### 4. Build the Application
```bash
dotnet build
```
### 5. Run the Application
```bash
dotnet run
```



