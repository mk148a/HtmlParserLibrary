using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HtmlParserLibrary
{
    public class HtmlParser
    {
        private int idCounter = 1; // Initialize a counter for generating unique IDs

        public string ConvertHtmlToJson(string htmlContent)
        {
            try
            {
                string preprocessedHtml = PreprocessHtmlForXml(htmlContent);
                string wrappedHtmlContent = $"<root>{preprocessedHtml}</root>";
                var document = XDocument.Parse(wrappedHtmlContent);
                var rootElement = ConvertNodeToJson(document.Root);
                return JsonSerializer.Serialize(rootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    type = "rawHtml",
                    isEditable = false,
                    content = htmlContent
                });
            }
        }

        private string PreprocessHtmlForXml(string htmlContent)
        {
            htmlContent = Regex.Replace(htmlContent, @"<(\w+)([^>]*)/>", "<$1$2></$1>");
            htmlContent = htmlContent.Replace("&", "&amp;");
            return htmlContent;
        }

        private dynamic ConvertNodeToJson(XElement element)
        {
            var jsonElement = new
            {
                id = (idCounter++).ToString("D5"),
                type = element.Name.LocalName,
                attributes = element.Attributes().ToDictionary(attr => attr.Name.LocalName, attr => attr.Value),
                isEditable = IsEditableElement(element),
                content = element.Nodes().Select(node =>
                    node is XElement
                        ? ConvertNodeToJson((XElement)node)
                        : (object)node.ToString()).ToList()
            };
            return jsonElement;
        }

        private bool IsEditableElement(XElement element)
        {
            string[] nonEditableTags = { "img", "video", "meta", "script", "style" };
            return !nonEditableTags.Contains(element.Name.LocalName.ToLower());
        }

        public string ConvertJsonToHtml(string jsonContent)
        {
            var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            var htmlBuilder = new StringBuilder();
            ConvertJsonToHtmlRecursive(jsonObject, htmlBuilder);
            return Regex.Unescape(htmlBuilder.ToString());
        }

        private void ConvertJsonToHtmlRecursive(JsonElement jsonObject, StringBuilder htmlBuilder)
        {
            if (jsonObject.ValueKind == JsonValueKind.Object)
            {
                if (jsonObject.TryGetProperty("type", out JsonElement typeElement))
                {
                    string tagName = typeElement.GetString();
                    htmlBuilder.Append($"<{tagName}");

                    if (jsonObject.TryGetProperty("attributes", out JsonElement attributesElement))
                    {
                        foreach (JsonProperty attribute in attributesElement.EnumerateObject())
                        {
                            htmlBuilder.Append($" {attribute.Name}=\"{attribute.Value.GetString()}\"");
                        }
                    }
                    htmlBuilder.Append(">");
                    if (jsonObject.TryGetProperty("content", out JsonElement contentElement))
                    {
                        if (contentElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement child in contentElement.EnumerateArray())
                            {
                                ConvertJsonToHtmlRecursive(child, htmlBuilder);
                            }
                        }
                        else if (contentElement.ValueKind == JsonValueKind.String)
                        {
                            htmlBuilder.Append(contentElement.GetString());
                        }
                    }
                    htmlBuilder.Append($"</{tagName}>");
                }
            }
            else if (jsonObject.ValueKind == JsonValueKind.String)
            {
                htmlBuilder.Append(jsonObject.GetString());
            }
        }

        public List<string> ProcessJsonData(string jsonContent)
        {
            var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            var processedList = new List<string>();
            var currentChunk = new StringBuilder();
            int chunkCounter = 1;
            int chunkIndex = 100;

            ProcessJsonRecursive(jsonObject, currentChunk, processedList, ref chunkCounter, ref chunkIndex);

            if (currentChunk.Length > 0)
            {
                processedList.Add($"##{chunkIndex:000}##{currentChunk}");
            }

            return processedList;
        }

        private void ProcessJsonRecursive(JsonElement jsonObject, StringBuilder currentChunk, List<string> processedList, ref int chunkCounter, ref int chunkIndex)
        {
            if (jsonObject.ValueKind == JsonValueKind.String || jsonObject.ValueKind == JsonValueKind.Number)
            {
                currentChunk.Append(jsonObject.ToString());

                if (currentChunk.Length >= 4000)
                {
                    processedList.Add($"##{chunkIndex:000}##{currentChunk.ToString()}");
                    currentChunk.Clear();
                    chunkIndex++;
                }
            }
            else if (jsonObject.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in jsonObject.EnumerateArray())
                {
                    ProcessJsonRecursive(item, currentChunk, processedList, ref chunkCounter, ref chunkIndex);
                }
            }
            else if (jsonObject.ValueKind == JsonValueKind.Object)
            {
                if (jsonObject.TryGetProperty("id", out JsonElement idElement))
                {
                    currentChunk.Append($"##{idElement.GetString()}##");
                }

                if (jsonObject.TryGetProperty("content", out JsonElement contentElement))
                {
                    if (contentElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var child in contentElement.EnumerateArray())
                        {
                            ProcessJsonRecursive(child, currentChunk, processedList, ref chunkCounter, ref chunkIndex);
                        }
                    }
                    else if (contentElement.ValueKind == JsonValueKind.String || contentElement.ValueKind == JsonValueKind.Number)
                    {
                        currentChunk.Append(contentElement.ToString());

                        if (currentChunk.Length >= 4000)
                        {
                            processedList.Add($"##{chunkIndex:000}##{currentChunk}");
                            currentChunk.Clear();
                            chunkIndex++;
                        }
                    }
                }
            }
        }

        public Dictionary<string, string> ParseEditedContent(string editedContent)
        {
            var chunks = new Dictionary<string, string>();
            string pattern = @"##\s*(?<id>\d{5})\s*##\s*(?<content>.*?)(?=(\s*##|\s*$))";

            var matches = Regex.Matches(editedContent, pattern);

            foreach (Match match in matches)
            {
                string id = match.Groups["id"].Value;
                string content = match.Groups["content"].Value;

                chunks[id] = content;
            }

            return chunks;
        }

        public JsonObject UpdateJsonRecursive(JsonObject jsonObject, Dictionary<string, string> editedChunks, bool isRoot = false)
        {
            if (jsonObject.TryGetPropertyValue("id", out JsonNode idNode))
            {
                string id = idNode.ToString();

                if (editedChunks.ContainsKey(id) && !isRoot)
                {
                    jsonObject["content"] = JsonValue.Create(editedChunks[id]);
                }
            }

            if (jsonObject.TryGetPropertyValue("content", out JsonNode contentNode) && contentNode is JsonArray contentArray)
            {
                for (int i = 0; i < contentArray.Count; i++)
                {
                    if (contentArray[i] is JsonObject nestedObject)
                    {
                        UpdateJsonRecursive(nestedObject, editedChunks);
                    }
                }
            }

            return jsonObject;
        }

        public string UpdateJsonWithEditedContent(string jsonContent, Dictionary<string, string> editedChunks)
        {
            var jsonObject = JsonSerializer.Deserialize<JsonObject>(jsonContent);
            var updatedJson = UpdateJsonRecursive(jsonObject, editedChunks, true);
            return JsonSerializer.Serialize(updatedJson, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
