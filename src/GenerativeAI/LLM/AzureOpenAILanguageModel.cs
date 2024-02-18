using Automation.GenerativeAI.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Automation.GenerativeAI.LLM
{
    public class AzureOpenAILanguageModel : ILanguageModel
    {
        private readonly OpenAIClient openAIClient;

        /// <summary>
        /// Creates an instance of AzureOpenAILanguageModel.
        /// </summary>
        /// <param name="config">Config object that contains information required to 
        /// connect to AzureOpenAI API.</param>
        internal AzureOpenAILanguageModel(OpenAIConfig config)
        {
            openAIClient = new OpenAIClient(config);
        }

        /// <summary>
        /// Creates an instance of AzureOpenAILanguageModel.
        /// </summary>
        /// <param name="azureEndpoint">Endpoint URL for Azure OpenAI service</param>
        /// <param name="gptDeployment">Deployment Name for GPT model</param>
        /// <param name="embeddingDeployment">Deployment Name for text embedding model</param>
        /// <param name="apiversion">API version</param>
        /// <param name="apiKey">ApiKey for the language model</param>
        /// <param name="model">Model name to be used for chat completion</param>
        public AzureOpenAILanguageModel(string azureEndpoint, string gptDeployment, string embeddingDeployment, string apiversion, string apiKey, string model)
        {
            var config = new OpenAIConfig()
            {
                EndPointUrl = azureEndpoint,
                GPTDeployment = gptDeployment,
                EmbeddingDeployment = embeddingDeployment,
                ApiVersion = apiversion,
                ApiKey = apiKey,
                Model = model
            };

            openAIClient = new OpenAIClient(config);
        }

        /// <summary>
        /// Creates an instance of AzureOpenAILanguageModel with a config file.
        /// </summary>
        /// <param name="configFilePath">Full path of a json file that has values for the following
        /// keys: EndPointUrl, GPTDeployment, EmbeddingDeployment, ApiVersion, ApiKey, Model.</param>
        public AzureOpenAILanguageModel(string configFilePath)
        {
            var config = OpenAIConfig.Load(configFilePath);
            openAIClient = new OpenAIClient(config);
            Configuration.Instance.OpenAIConfig = config;
        }

        public string ModelName => openAIClient.ModelName;

        public IVectorTransformer VectorTransformer => openAIClient.VectorTransformer;

        public int PromptTokensUsed => openAIClient.PromptTokensUsed;

        public int CompletionTokensUsed => openAIClient.CompletionTokensUsed;

        public int MaxTokenLimit => openAIClient.MaxTokenLimit;

        public Task<LLMResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, double temperature)
        {
            return openAIClient.GetResponseAsync(messages, temperature);
        }

        public Task<LLMResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, IEnumerable<FunctionDescriptor> functions, double temperature)
        {
            return openAIClient.GetResponseAsync(messages, functions, temperature);
        }
    }
}
