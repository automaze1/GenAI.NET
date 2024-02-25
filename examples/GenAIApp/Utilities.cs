using Automation.GenerativeAI.Tools;
using Automation.GenerativeAI.UX.Services;
using Factobi.Data;
using System.IO;

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
        private static string GetContext(string portName, string query)
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

        /// <summary>
        /// Searches and provides relevant context for the given port name
        /// </summary>
        /// <param name="portName">Name of the port for which question is asked.</param>
        /// <returns>The context data for the given port.</returns>
        public static string GetPortPDAData(string portName)
        {
            var chatService = ServiceContainer.Resolve<ChatService>();
            var excelpath = Path.Combine(chatService.DocumentsPath, "Indian Ports PDA.xlsx");
            var db = Database.ConnectExcelDatabase(excelpath);

            var ports = db.GetTables();
            var port = ports.First(p => string.Equals(p, portName, StringComparison.OrdinalIgnoreCase));
            var table = db.GetData(port);
            return table.GetDataAsString(-1, false, true);
        }

        /// <summary>
        /// Returns list of ports
        /// </summary>
        /// <returns>A comma separated list of all ports</returns>
        public static string GetPortList()
        {
            var chatService = ServiceContainer.Resolve<ChatService>();
            var excelpath = Path.Combine(chatService.DocumentsPath, "Indian Ports PDA.xlsx");
            var db = Database.ConnectExcelDatabase(excelpath);

            var sheets = db.GetTables().Skip(2).ToArray();

            return string.Join(',', sheets);
        }
    }
}
