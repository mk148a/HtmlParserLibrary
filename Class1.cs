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
                JsonSerializerOptions jso = new JsonSerializerOptions();
                jso.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                jso.WriteIndented = true;
                return JsonSerializer.Serialize(rootElement, jso);
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

            var isEditable = IsEditableElement(element);

            var jsonElement = new
            {
                id = (idCounter++).ToString("D5"),
                type = element.Name.LocalName,
                attributes = element.Attributes().ToDictionary(attr => attr.Name.LocalName, attr => attr.Value),
                isEditable = isEditable,
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

            // If the element is in the non-editable list, return false
            if (nonEditableTags.Contains(element.Name.LocalName.ToLower()))
            {
                return false;
            }

            // Check if the element has any child elements
            bool hasChildElements = element.Elements().Any();

            // If it has child elements, it's not directly editable, unless it directly contains text.
            if (hasChildElements)
            {
                return false;
            }

            // If the element has no children and contains text, it is editable
            return !string.IsNullOrWhiteSpace(element.Value);
        }

        public string ConvertJsonToHtml(string jsonContent)
        {
            // Deserialize the JSON content into a JsonElement
            jsonContent = jsonContent.Replace("&amp;", "&");

            var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonContent);

            // Initialize a StringBuilder to construct the HTML output
            var htmlBuilder = new StringBuilder();

            // Recursively convert JSON back to HTML
            ConvertJsonToHtmlRecursive(jsonObject, htmlBuilder);

            // Unescape any JSON escape sequences in the final HTML string
            string finalHtml = Regex.Unescape(htmlBuilder.ToString());

            return finalHtml;
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
                bool hasEditableContent = false;

                if (jsonObject.TryGetProperty("isEditable", out JsonElement isEditableElement) && isEditableElement.GetBoolean())
                {
                    hasEditableContent = true;

                    if (jsonObject.TryGetProperty("id", out JsonElement idElement))
                    {
                        currentChunk.Append($"##{idElement.GetString()}##");
                    }
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
                        if (hasEditableContent)
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
        }

        public Dictionary<string, string> ParseEditedContent(string editedContent)
        {
            var finalEditedContent = editedContent.Replace("# #", "##");
            var chunks = new Dictionary<string, string>();
            string pattern = @"##\s*(?<id>\d{5})\s*##\s*(?<content>.*?)(?=(\s*##|\s*$))"; // ID'leri ve içerikleri yakalamak için güncellenmiş regex

            // Tüm ID'leri ve içerikleri yakala
            var matches = Regex.Matches(finalEditedContent, pattern);

            foreach (Match match in matches)
            {
                string id = match.Groups["id"].Value;
                string content = match.Groups["content"].Value;

                chunks[id] = content;
            }

            return chunks;
        }
        private JsonObject CloneJsonObject(JsonObject original)
        {
            // JsonObject'i JSON stringine serialize et, sonra deserialize ederek kopyala
            var jsonString = original.ToJsonString();
            return JsonNode.Parse(jsonString).AsObject();
        }
        public JsonObject UpdateJsonRecursive(JsonObject jsonObject, Dictionary<string, string> editedChunks, bool isRoot = false)
        {
            if (jsonObject.TryGetPropertyValue("id", out JsonNode idNode))
            {
                string id = idNode.ToString();

                // Eğer ID editedChunks içinde varsa, content'i güncelle
                if (editedChunks.ContainsKey(id) && !isRoot)
                {
                    jsonObject["content"] = JsonValue.Create(editedChunks[id]);
                }
            }

            // İçerik bir array ise, recursive olarak alt öğelere in
            if (jsonObject.TryGetPropertyValue("content", out JsonNode contentNode))
            {
                if (contentNode is JsonArray contentArray)
                {
                    for (int i = 0; i < contentArray.Count; i++)
                    {
                        if (contentArray[i] is JsonObject nestedObject)
                        {
                            // Burada nesne manuel olarak kopyalanıyor
                            var clonedNestedObject = CloneJsonObject(nestedObject);
                            contentArray[i] = UpdateJsonRecursive(clonedNestedObject, editedChunks);
                        }
                    }
                }
                else if (contentNode is JsonObject nestedObject)
                {
                    // Eğer content bir object ise, recursive olarak güncelle
                    jsonObject["content"] = UpdateJsonRecursive(CloneJsonObject(nestedObject), editedChunks);
                }
            }

            return jsonObject;
        }

        public string UpdateJsonWithEditedContent(string jsonContent, Dictionary<string, string> editedChunks)
        {
            var jsonObject = JsonSerializer.Deserialize<JsonObject>(jsonContent);
            var updatedJson = UpdateJsonRecursive(jsonObject, editedChunks, true);
            JsonSerializerOptions jso = new JsonSerializerOptions();
            jso.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            jso.WriteIndented = true;
            return JsonSerializer.Serialize(updatedJson, jso);
        }
    }
}
