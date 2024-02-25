using Automation.GenerativeAI;
using Automation.GenerativeAI.Interfaces;
using Automation.GenerativeAI.Tools;
using Automation.GenerativeAI.UX.Services;
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
provides the final answer based on the context received. All the charges mentioned in the context are in USD.
While making the function call to get relevant context you must provide the complete query for better context. 
Always answer based on the context, if answer is not present in the context, respond that you don't have the 
knowledge about the given query.";

        public ChatService()
        {
            var exepath = Assembly.GetExecutingAssembly().Location;
            DocumentsPath = Path.Combine(Path.GetDirectoryName(exepath), "Documents");
            var logFilePath = Path.Combine(DocumentsPath, "GenAIChatApp.log");

            var responses = new Dictionary<string, string>()
            {
                { "Hi, there!", "Hi There! I am your Automation Agent, How may I assist you?"},
                { "What can you do for me?", "I can do plenty of things ranging from generating content, understanding large documents to running some tools."},
                { "Great, tell me a funny joke","Why do software engineers prefer dark mode?\r\n\r\nBecause light attracts bugs, and they want to avoid unnecessary debugging!" },
            };
            //languageModel = new MockLanguageModel("Test", responses);
            generativeAIService = Application.GetAIService();
            Application.SetLogFilePath(logFilePath);

            languageModel = CreateLanguageModel();

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

        static string GetFullPath(string filename)
        {
            var asm = Assembly.GetExecutingAssembly();
            var location = asm.Location;
            UriBuilder uri = new UriBuilder(location);
            string path = Uri.UnescapeDataString(uri.Path);
            var exedir = Path.GetDirectoryName(path);
            return Path.Combine(exedir, filename);
        }

        private static ILanguageModel CreateLanguageModel()
        {
            //Create GenAI service
            var svc = Application.GetAIService();

            //Create OpenAI Language model
            var configFile = GetFullPath("OpenAIConfig.json");

            if (!File.Exists(configFile)) throw new FileNotFoundException($"OpenAI conig json file is not available at {configFile}");

            var json = File.ReadAllText(configFile);
            var config = FunctionTool.Deserialize<Dictionary<string, string>>(json);
            return svc.CreateAzureOpenAIModel(config["Model"], config["EndPointUrl"], config["GPTDeployment"], config["EmbeddingDeployment"], config["ApiVersion"], config["ApiKey"]);
        }
    }
}
