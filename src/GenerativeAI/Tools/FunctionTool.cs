using Automation.GenerativeAI.Interfaces;
using Automation.GenerativeAI.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using JsonSerializer = Automation.GenerativeAI.Utilities.JsonSerializer;

namespace Automation.GenerativeAI.Tools
{
    internal class ToolDefinition
    {
        public required string module { get; set; }
        public string classname { get; set; } = string.Empty;
        public string method { get; set; } = string.Empty;
        public Dictionary<string, object> parameters { get; set; } = [];
    }

    /// <summary>
    /// The base implementation of IFunctionTool interface as FunctionTool
    /// </summary>
    public abstract class FunctionTool : IFunctionTool
    {
        /// <summary>
        /// Represent the Result object that has success status and the output string.
        /// </summary>
        protected class Result
        {
            /// <summary>
            /// Flag to indicate if the execution result was successful.
            /// </summary>
            public bool success = false;

            /// <summary>
            /// The output object as a result of execution of this FunctionTool
            /// </summary>
            public object output = null;
        }

        /// <summary>
        /// Descriptor for this tool
        /// </summary>
        protected FunctionDescriptor descriptor;

        protected FunctionTool()
        {
            Name = this.GetType().Name;
            Description = $"Executes a tool: {Name}";
        }

        /// <summary>
        /// Updates the name of the tool
        /// </summary>
        /// <param name="name">Name of the tool to update</param>
        /// <returns>The updated FunctionTool object</returns>
        public FunctionTool WithName(string name)
        {
            Name = name;
            return this;
        }

        /// <summary>
        /// Updates description of the tool
        /// </summary>
        /// <param name="description">Description of the tool to be used for discovery</param>
        /// <returns>The updated FunctionTool object</returns>
        public FunctionTool WithDescription(string description)
        {
            if(!string.IsNullOrEmpty(description))
            {
                Description = description;
            }
            return this;
        }

        /// <summary>
        /// Name of the tool
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description of the tool
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets the function descriptor used for tool discovery by agents and LLM
        /// </summary>
        public FunctionDescriptor Descriptor
        {
            get 
            { 
                if(descriptor == null) { descriptor = GetDescriptor(); }
                return descriptor;
            }
        }

        /// <summary>
        /// Returns function descriptor for the tool to make it discoverable by Agent
        /// </summary>
        /// <returns>FunctionDescriptor</returns>
        protected abstract FunctionDescriptor GetDescriptor();

        /// <summary>
        /// Executes the tool with given context
        /// </summary>
        /// <param name="context">Execution context</param>
        /// <returns>Result</returns>
        protected abstract Task<Result> ExecuteCoreAsync(ExecutionContext context);

        /// <summary>
        /// Executes this tool asynchronously.
        /// </summary>
        /// <param name="context">ExecutionContext</param>
        /// <returns>Output string</returns>
        public async Task<string> ExecuteAsync(ExecutionContext context)
        {
            string output = $"ERROR: Failed to execute Tool: {this.Name}";
            object retval = null;
            bool success = false;
            try
            {
                if(ValidateParameterTypes(context, Descriptor.Parameters.Properties))
                {
                    Logger.WriteLog(LogLevel.Info, LogOps.Command, $"Started Executing tool: {Name}.");
                    var result = await ExecuteCoreAsync(context);
                    retval = result.output;
                    success = result.success;
                }
                else
                {
                    Logger.WriteLog(LogLevel.Error, LogOps.Result, $"Parameter validation for tool: {Name} has failed!");
                }
            }
            catch (System.Exception ex)
            {
                success = false;
                Logger.WriteLog(LogLevel.Error, LogOps.Exception, ex.Message);
                Logger.WriteLog(LogLevel.StackTrace, LogOps.Exception, ex.StackTrace);
            }
            finally
            {
                if (success)
                {
                    context.AddResult(Name, retval);
                    output = ToJsonString(retval);
                    Logger.WriteLog(LogLevel.Info, LogOps.Result, output.Substring(0, output.Length > 500 ? 500 : output.Length));
                }
                else
                {
                    output = $"ERROR: Failed to execute Tool: {this.Name}";
                }
            }
            return output;
        }

        /// <summary>
        /// Executes this tool asynchronously.
        /// </summary>
        /// <param name="context">ExecutionContext</param>
        /// <returns>Output string</returns>
        public string Execute(ExecutionContext context)
        {
            return ExecuteAsync(context).GetAwaiter().GetResult();
        }

        private static bool IsEnumerable(Type type)
        {
            return type.GetInterface(typeof(IEnumerable).FullName) != null;
        }

        private static bool ValidateParameterTypes(ExecutionContext context, IEnumerable<ParameterDescriptor> parameters)
        {
            foreach(ParameterDescriptor parameter in parameters)
            {
                object data = null;
                bool found = context.TryGetValue(parameter.Name, out data);
                if (!parameter.Required && !found)
                {
                    context[parameter.Name] = null;
                    continue;
                }

                if(found && parameter.Type.Type == TypeDescriptor.StringType.Type)
                {
                    context[parameter.Name] = ToJsonString(data);
                }
                else if (data != null && parameter.Type is ArrayTypeDescriptor && IsEnumerable(data.GetType()))
                {
                    IEnumerable collection = (IEnumerable)data;
                    var elementType = ((ArrayTypeDescriptor)parameter.Type).ItemType.Type;
                    switch (elementType)
                    {
                        case "string":
                            context[parameter.Name] = collection.Cast<string>().ToList();
                            break;
                        case "number":
                            context[parameter.Name] = collection.Cast<double>().ToList();
                            break;
                        case "boolean":
                            context[parameter.Name] = collection.Cast<bool>().ToList();
                            break;
                        case "integer":
                            context[parameter.Name] = collection.Cast<int>().ToList();
                            break;
                        default:
                            break;
                    }
                    
                }
                else if(data != null && data is string)
                {
                    var value = Convert((string)data, parameter.Type);
                    if(value == null) return false;

                    context[parameter.Name] = value;
                }
            }

            return true;
        }

        /// <summary>
        /// Convert the given string data to a give type, either by parsing or deserializing from JSON.
        /// </summary>
        /// <param name="data">Input data as string</param>
        /// <param name="type">Type description for data conversion.</param>
        /// <returns>Object of the desired type.</returns>
        public static object Convert(string data, TypeDescriptor type)
        {
            switch (type.Type)
            {
                case "number":
                    double number = 0.0;
                    if (!double.TryParse(data, out number)) return null;
                    return number;
                case "boolean":
                    bool val = false;
                    if (!bool.TryParse(data, out val)) return null;
                    return val;
                case "integer":
                    int integer = 0;
                    if (!int.TryParse(data, out integer)) return null;
                    return integer;
                case "array":
                case "object":
                    object obj = null;
                    var serializer = new JsonSerializer();
                    obj = serializer.Deserialize<object>(data);
                    return obj;
                case "string":
                    return data;
                default:
                    break;
            }

            return null;
        }

        /// <summary>
        /// Utility method to serialize a given object to JSON string
        /// </summary>
        /// <param name="obj">Object to serialize</param>
        /// <returns>JSON string</returns>
        public static string ToJsonString(object obj)
        {
            if(obj == null) return string.Empty;
            if (obj is string || obj.GetType().IsValueType) return obj.ToString();

            var serializer = new JsonSerializer();
            var json = serializer.Serialize(obj);
            return json;
        }

        /// <summary>
        /// Utility method to Deserialize the given json string to specific object type.
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <param name="json">Input JSON string</param>
        /// <returns>Deserialized object</returns>
        public static T Deserialize<T>(string json)
        {
            var serializer = new JsonSerializer();
            return serializer.Deserialize<T>(json);
        }

        /// <summary>
        /// Utility method to check if the input string is a JSON string.
        /// </summary>
        /// <param name="str">input string</param>
        /// <returns>True if JSON</returns>
        public static bool IsJsonString(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return false;

            str = str.Trim();
            if ((str.StartsWith("{") && str.EndsWith("}")) ||
                (str.StartsWith("[") && str.EndsWith("]"))) return true;

            return false;
        }

        /// <summary>
        /// Creates tool with the help of given json string.
        /// </summary>
        /// <param name="json">JSON string defining the Function Tool.</param>
        /// <returns>IFunctionTool or null</returns>
        public static IFunctionTool CreateTool(string json)
        {
            var serializer = new JsonSerializer();
            var data = serializer.Deserialize(json);

            var def = TryGetToolDefinition(data);

            return CreateTool(def);
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

        private static IFunctionTool CreateTool(ToolDefinition toolDefinition)
        {
            var fullPath = Path.GetFullPath(toolDefinition.module);
            if(!File.Exists(fullPath)) 
            {
                fullPath = GetFullPath(toolDefinition.module);
            }
            Assembly assembly = Assembly.LoadFrom(fullPath);
            Type type = null;
            if (assembly == null) return null;
            
            type = assembly.GetType(toolDefinition.classname, false, true);
            if(type == null) return null;

            if(type.IsAssignableTo(typeof(IFunctionTool))) //We have a type of IFuctionTool, so just find the constructor
            {
                var constructors = type.GetConstructors();
                var values = new List<object>();
                foreach (var constructor in constructors)
                {
                    var parameters = constructor.GetParameters();
                    foreach (var parameter in parameters)
                    {
                        if (toolDefinition.parameters.ContainsKey(parameter.Name))
                        {
                            var pValue = toolDefinition.parameters[parameter.Name];
                            if (pValue is string str) { pValue = str; }
                            else if (pValue is IDictionary dictionary)
                            {
                                pValue = InstanciateToolsInDictionary(dictionary);
                            }
                            else if(pValue is IEnumerable enumerable)
                            {
                                pValue = InstanciateToolsInList(enumerable);
                            }
                            values.Add(pValue);
                        }
                        else if (parameter.HasDefaultValue)
                        {
                            values.Add(parameter.DefaultValue);
                        }
                        else
                        {
                            values.Clear();
                            break; //we don't have all parameters required for this construtor.
                        }
                    }

                    if (values.Count == parameters.Length)
                    {
                        return constructor.Invoke(values.ToArray()) as IFunctionTool;
                    }
                }
            }
            else
            {
                var toolset = new DLLFunctionTools(fullPath, toolDefinition.classname);
                return toolset.GetTool(toolDefinition.method);
            }

            return null;
        }

        private static object InstanciateToolsInDictionary(IDictionary dictionary)
        {
            foreach(DictionaryEntry entry in dictionary)
            {
                var value = entry.Value;
                if(value is ToolDefinition toolDefinition)
                {
                    var tool = CreateTool(toolDefinition);
                    if(tool != null)
                    {
                        dictionary[entry.Key] = tool;
                    }
                }
            }

            return dictionary;
        }

        private static object InstanciateToolsInList(IEnumerable list)
        {
            List<object> results = new List<object>();

            bool allTools = true;
            foreach (object entry in list)
            {
                if (entry is ToolDefinition toolDefinition)
                {
                    var tool = CreateTool(toolDefinition);
                    if (tool != null)
                    {
                        results.Add(tool);
                    }
                }
                else
                {
                    results.Add(entry);
                    allTools = false;
                }
            }
            if(allTools) return results.Cast<IFunctionTool>();
            return results;
        }

        private static ToolDefinition TryGetToolDefinition(Dictionary<string, object> data)
        {
            try
            {
                var def = new ToolDefinition() { module = (string)data["module"] };
                def.method = (string)data["method"];
                def.classname = (string)data["classname"];
                def.parameters = data["parameters"] as Dictionary<string, object>;
                foreach (var item in def.parameters)
                {
                    if (item.Value is Dictionary<string, object> obj)
                    {
                        var newdef = TryGetToolDefinition(obj);
                        if(newdef != null) { def.parameters[item.Key] = newdef; }
                    }
                    else if(item.Value is object[] array)
                    {
                        if(array.All(x => x is Dictionary<string, object>))
                        {
                            var tools = array.Cast<Dictionary<string, object>>().Select(TryGetToolDefinition).ToArray();
                            def.parameters[item.Key] = tools;
                        }
                    }
                }

                return def;
            }
            catch (Exception)
            {
                return null;
            }            
        }
    }
}
