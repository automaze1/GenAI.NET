using Automation.GenerativeAI;
using Automation.GenerativeAI.Interfaces;
using Automation.GenerativeAI.LLM;
using Automation.GenerativeAI.UX.Services;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System.IO;
using System.Reflection;
using ChatMessage = Automation.GenerativeAI.UX.Models.ChatMessage;
using Role = Automation.GenerativeAI.Interfaces.Role;

namespace GenAIApp
{
    class ChatService : IChatService
    {
        ILanguageModel languageModel { get; set; }
        IGenerativeAIService generativeAIService { get; set; }
        public string DocumentsPath { get; set; }
        Dictionary<string, IVectorStore> stores = new Dictionary<string, IVectorStore>();

        string systemMessage = @"You are an intelligent assistant that analyzes the given query carefully and 
if the given query is related to a port then it makes a function call to get relevant context on the query and
provides the final answer based on the context received. While making the function call to get relevant context
you must provide the complete query for better context. Always answer based on the context, if answer is not 
present in the context, respond that you don't have the knowledge about the given query.";

        public ChatService()
        {
            var exepath = Assembly.GetExecutingAssembly().Location;
            DocumentsPath = Path.Combine(Path.GetDirectoryName(exepath), "Documents");
            var responses = new Dictionary<string, string>()
            {
                { "Hi, there!", "Hi There! I am your Automation Agent, How may I assist you?"},
                { "What can you do for me?", "I can do plenty of things ranging from generating content, understanding large documents to running some tools."},
                { "Great, tell me a funny joke","Why do software engineers prefer dark mode?\r\n\r\nBecause light attracts bugs, and they want to avoid unnecessary debugging!" },
            };
            //languageModel = new MockLanguageModel("Test", responses);
            generativeAIService = Application.GetAIService();

            //Create OpenAI Language model
            var azureEndpoint = "https://aai-ps-openai-use2.openai.azure.com/";
            var deployment = "aai_ps_gpt35_turbo";
            var embedding = "aai_ps_text_embedding_ada";
            var apiversion = "2023-08-01-preview";
            var apikey = "8413622cec58474fbec6d9c0f6c08ef0"; 
            languageModel = generativeAIService.CreateAzureOpenAIModel("gpt-35-turbo", azureEndpoint, deployment, embedding, apiversion, apikey);

            var logFilePath = Path.Combine(DocumentsPath, "GenAIChatApp.log");
            Application.InitializeAzureOpenAI(azureEndpoint, deployment, embedding, apiversion, apikey, "gpt-35-turbo", logFilePath);
            CreateVectorDatabases();
        }

        public IVectorStore GetVectorStore(string portName)
        {
            portName = portName.ToLower().Replace("port", "").Trim();
            foreach (var item in stores)
            {
                if (item.Key.Contains(portName)) return item.Value;
            }

            return null;
        }

        private void CreateVectorDatabases()
        {
            var files = Directory.GetFiles(DocumentsPath);
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);
                if (!extension.Equals(".txt", StringComparison.CurrentCultureIgnoreCase)) continue;
            
                var vdbpath = Path.ChangeExtension(file, "vdb");
                IVectorStore? store;
                if (File.Exists(vdbpath)) 
                {
                    store = generativeAIService.DeserializeVectorStore(vdbpath);
                }
                else
                {
                    store = Application.CreateVectorDatabaseForSemanticSearch(file, vdbpath);
                }

                stores.Add(Path.GetFileNameWithoutExtension(file).ToLower(), store);
            }
        }

        public async Task CreateChatSessionAsync(string id)
        {
            await Task.Run(() =>
            {
                CreateConversation(id);
            });
        }

        private IConversation CreateConversation(string id)
        {
            var conversation = generativeAIService.CreateConversation(id, languageModel);
            conversation.AppendMessage(systemMessage, Role.system);
            var tool = new DLLFunctionTools(Assembly.GetExecutingAssembly().Location, "GenAIApp.Utilities");
            conversation.AddToolSet(tool);
            return conversation;
        }

        public async Task<IEnumerable<ChatMessage>> GetChatHistoryAsync(string id)
        {
            return await Task.FromResult(Enumerable.Empty<ChatMessage>());
        }

        public async Task<string> SendMessageAsync(string id, string message)
        {
            var conversation = generativeAIService.GetConversation(id);
            if (null == conversation)
            {
                conversation = CreateConversation(id);
            }
            conversation.AppendMessage(message, Role.user);
            var msg = await conversation.GetResponseAsync(0.8);
            return msg.content;
        }

        public Task UpadateChatHistoryAsync(string id, IEnumerable<ChatMessage> messages)
        {
            throw new NotImplementedException();
        }
    }
}
