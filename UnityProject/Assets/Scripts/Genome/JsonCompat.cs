using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WormWorld.Genome
{
    /// <summary>
    /// Compatibility helpers that provide deterministic JSON canonicalization using Newtonsoft.Json.
    /// </summary>
    public static class JsonCompat
    {
        /// <summary>
        /// Serializes an object to canonical JSON with lexicographically sorted object keys.
        /// </summary>
        /// <param name="obj">Object graph to serialize.</param>
        /// <returns>Canonical JSON string.</returns>
        public static string ToCanonicalJson(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            var serializer = JsonSerializer.CreateDefault();
            var token = JToken.FromObject(obj, serializer);
            var sorted = SortToken(token);
            return sorted.ToString(Formatting.None);
        }

        /// <summary>
        /// Normalizes arbitrary JSON text into canonical, minified form with sorted keys.
        /// </summary>
        /// <param name="json">Input JSON text.</param>
        /// <returns>Canonical JSON string.</returns>
        public static string Normalize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var token = JToken.Parse(json);
            var sorted = SortToken(token);
            return sorted.ToString(Formatting.None);
        }

        private static JToken SortToken(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var ordered = new JObject();
                    foreach (var property in token.Children<JProperty>().OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        ordered.Add(property.Name, SortToken(property.Value));
                    }

                    return ordered;
                case JTokenType.Array:
                    var array = new JArray();
                    foreach (var child in token.Children())
                    {
                        array.Add(SortToken(child));
                    }

                    return array;
                default:
                    return token.DeepClone();
            }
        }
    }
}
