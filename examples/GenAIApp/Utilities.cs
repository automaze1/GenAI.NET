using Automation.GenerativeAI.Tools;
using Automation.GenerativeAI.UX.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenAIApp
{
    public class Utilities
    {
        /// <summary>
        /// Searches and provides relevant context for the given query
        /// </summary>
        /// <param name="portName">Name of the port for which question is asked.</param>
        /// <param name="query">User's query for which context is needed.</param>
        /// <returns></returns>
        public static string GetContext(string portName, string query)
        {
            var chatService = ServiceContainer.Resolve<ChatService>();
            portName = portName.ToLower().Replace("port", "").Trim();

            var txtfilepath = Path.Combine(chatService.DocumentsPath, $"{portName}.txt");
            if(File.Exists(txtfilepath))
            {
                return File.ReadAllText(txtfilepath);
            }

            var store = chatService.GetVectorStore(portName);
            if (store != null)
            {
                var searchTool = SearchTool.ForSemanticeSearch(store).WithMaxResultCount(5);
                var results = searchTool.SearchAsync(query, string.Empty).GetAwaiter().GetResult();
                return results.Select(x => x.content).Aggregate((x, y) => $"{x}\n{y}");
            }

            return $"No knowledge available for port: {portName}";
        }
    }
}
