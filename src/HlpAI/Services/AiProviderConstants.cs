namespace HlpAI.Services;

/// <summary>
/// Constants for AI provider default URLs and configurations
/// </summary>
public static class AiProviderConstants
{
    /// <summary>
    /// Default URLs for local AI providers
    /// </summary>
    public static class DefaultUrls
    {
        public const string Ollama = "http://localhost:11434";
        public const string LmStudio = "http://localhost:1234";
        public const string OpenWebUi = "http://localhost:3000";
        public const string OpenAi = "https://api.openai.com";
        public const string OpenAiV1 = "https://api.openai.com/v1";
        public const string Anthropic = "https://api.anthropic.com";
        public const string AnthropicV1 = "https://api.anthropic.com/v1";
        public const string DeepSeek = "https://api.deepseek.com/v1";
    }

    /// <summary>
    /// Default models for AI providers
    /// </summary>
    public static class DefaultModels
    {
        public const string Ollama = "llama3.2";
        public const string LmStudio = "default";
        public const string OpenWebUi = "default";
        public const string OpenAi = "gpt-4o-mini";
        public const string Anthropic = "claude-3-5-haiku-20241022";
        public const string DeepSeek = "deepseek-chat";
    }
}