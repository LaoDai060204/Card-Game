using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace CardGame
{
    public static class Json
    {
        /// <summary>
        /// Load an object from a JSON file.
        /// Automatically inspect the serializable members of <typeparamref name="T"/>.
        /// Fill missing JSON properties with defaults and rewrite the file.
        /// If the file does not exist, create it with defaults and return the default instance.
        /// </summary>
        /// <typeparam name="T">Target type</typeparam>
        /// <param name="filePath">JSON file path</param>
        /// <returns>Deserialized object</returns>
        public static T LoadFromFile<T>(string filePath) where T : new()
        {
            if (!File.Exists(filePath))
            {
                T defaultInstance = new();
                SaveToFile(filePath, defaultInstance);
                return defaultInstance;
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                JObject jObject = JObject.Parse(jsonText);
                T defaultInstanceForMissing = new T();

                var resolver = new DefaultContractResolver();
                var contract = resolver.ResolveContract(typeof(T)) as JsonObjectContract;
                var properties = contract?.Properties;
                bool hasMissing = false;

                foreach (var prop in properties)
                {
                    if (!jObject.ContainsKey(prop.PropertyName))
                    {
                        object value = prop.ValueProvider.GetValue(defaultInstanceForMissing);
                        jObject.Add(new JProperty(prop.PropertyName, JToken.FromObject(value)));
                        hasMissing = true;
                    }
                }

                if (hasMissing)
                {
                    string updatedJson = jObject.ToString(Formatting.Indented);
                    File.WriteAllText(filePath, updatedJson);
                }

                return jObject.ToObject<T>();
            }
            catch
            {
                T defaultInstance = new();
                SaveToFile(filePath, defaultInstance);
                return defaultInstance;
            }
        }

        /// <summary>
        /// Serialize an object as JSON and save it to the specified path.
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <param name="filePath">Save path</param>
        /// <param name="obj">Object to save</param>
        public static void SaveToFile<T>(string filePath, T obj)
        {
            string json = JsonConvert.SerializeObject(obj, Formatting.Indented);
            string tmpPath = filePath + ".tmp";
            File.WriteAllText(tmpPath, json);

            if (File.Exists(filePath))
                File.Replace(tmpPath, filePath, null);
            else
                File.Move(tmpPath, filePath);
        }
    }
}