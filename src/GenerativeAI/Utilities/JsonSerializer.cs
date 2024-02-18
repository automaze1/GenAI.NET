using System.Collections.Generic;
using System.Text.Json;
using JSerializer = System.Text.Json.JsonSerializer;

namespace Automation.GenerativeAI.Utilities
{
    internal class JsonSerializer
    {
        public T Deserialize<T>(string json)
        {
            return JSerializer.Deserialize<T>(json);
        }

        public Dictionary<string, object> Deserialize(string json)
        {
            // Parse the json text to a JsonElement
            JsonElement jsonElement = JSerializer.Deserialize<JsonElement>(json);

            // Create an empty dictionary to store the key-value pairs
            Dictionary<string, object> dictionary = new Dictionary<string, object>();

            // Loop through the properties of the JsonElement
            foreach (JsonProperty property in jsonElement.EnumerateObject())
            {
                // Get the property name and value
                string key = property.Name;
                object value = GetObject(property.Value);

                // Add the key-value pair to the dictionary
                dictionary.Add(key, value);
            }

            return dictionary;
        }

        public string Serialize<T>(T value)
        {
            return JSerializer.Serialize(value);
        }

        public static object GetObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Undefined:
                    break;
                case JsonValueKind.Object:
                    break;
                case JsonValueKind.Array:
                    return GetArray(element);
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                default:
                    break;
            }
            return element;
        }

        private static object[] GetArray(JsonElement element)
        {
            var result = new object[element.GetArrayLength()];
            int i = 0;
            foreach (var item in element.EnumerateArray())
            {
                result[i++] = GetObject(item);
            }

            return result;
        }
    }
}
