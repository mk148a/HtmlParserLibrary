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
                // HTML içeriğini önceden işliyoruz
                string preprocessedHtml = PreprocessHtmlForXml(htmlContent);

                // HTML içeriğini tek bir kök element içine sarıyoruz
                string wrappedHtmlContent = $"<root>{preprocessedHtml}</root>";

                // XDocument ile parse ediyoruz
                var document = XDocument.Parse(wrappedHtmlContent);
                var rootElement = ConvertNodeToJson(document.Root);
                JsonSerializerOptions jso = new JsonSerializerOptions();
                jso.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                jso.WriteIndented = true;
                return JsonSerializer.Serialize(rootElement, jso);
            }
            catch (Exception ex)
            {
                // XML parse hatası durumunda hatalı içeriği ham haliyle geri döndür
                return JsonSerializer.Serialize(new
                {
                    type = "rawHtml",
                    isEditable = false,
                    content = htmlContent
                });
            }
        }

        private string PreprocessHtmlForXml(string htmlContent)
        { // Self-closing tagleri uygun formatta değiştir
            htmlContent = Regex.Replace(htmlContent, @"<(\w+)([^>]*)/>", "<$1$2></$1>");

            // '&' karakterini XML uyumlu hale getiriyoruz
            htmlContent = htmlContent.Replace("&", "&amp;");

            return htmlContent;
        }

        private dynamic ConvertNodeToJson(XElement element)
        {
            // Check if the element is editable
            var isEditable = IsEditableElement(element);

            // Create a list to store content
            var contentList = new List<dynamic>();

            // Iterate through all child nodes
            foreach (var node in element.Nodes())
            {
                if (node is XElement childElement)
                {
                    // If it's an element, recursively process it
                    contentList.Add(ConvertNodeToJson(childElement));
                }
                else if (node is XText textNode)
                {
                    // If it's text, create a new JSON element for the text with an ID
                    contentList.Add(new
                    {
                        id = (idCounter++).ToString("D5"),
                        type = "text",
                        isEditable = true, // Text nodes are editable
                        content = textNode.Value
                    });
                }
            }

            // Create the JSON structure
            var jsonElement = new
            {
                id = (idCounter++).ToString("D5"),
                type = element.Name.LocalName,
                attributes = element.Attributes().ToDictionary(attr => attr.Name.LocalName, attr => attr.Value),
                isEditable = isEditable,
                content = contentList
            };

            return jsonElement;
        }

        private bool IsEditableElement(XElement element)
        {
            string[] nonEditableTags = { "img", "video", "meta", "script", "style", "br", "hr" };

            if (nonEditableTags.Contains(element.Name.LocalName.ToLower()))
            {
                return false;
            }

            bool hasChildElements = element.Elements().Any();
            if (hasChildElements)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(element.Value);
        }

        public string ConvertJsonToHtml(string jsonContent)
        {
            var jsonObject = JsonSerializer.Deserialize<JsonElement>(jsonContent);

            // Eğer JSON içeriği bir "root" elemanı içeriyorsa, sadece içeriğini işleyelim
            if (jsonObject.ValueKind == JsonValueKind.Object &&
                jsonObject.TryGetProperty("type", out JsonElement typeElement) &&
                typeElement.GetString() == "root")
            {
                if (jsonObject.TryGetProperty("content", out JsonElement contentElement))
                {
                    var htmlBuilder = new StringBuilder();
                    // Sadece root'un içeriğini HTML'ye dönüştürüyoruz
                    foreach (JsonElement child in contentElement.EnumerateArray())
                    {
                        ConvertJsonToHtmlRecursive(child, htmlBuilder);
                    }
                    return Regex.Unescape(htmlBuilder.ToString());
                }
            }

            // Eğer root elemanı yoksa normal şekilde işleme devam
            return ConvertJsonToHtmlRecursive(jsonObject, new StringBuilder());
        }

        public string ConvertJsonToHtmlRecursive(JsonElement jsonObject, StringBuilder htmlBuilder)
        {
            if (jsonObject.ValueKind == JsonValueKind.Object)
            {
                if (jsonObject.TryGetProperty("type", out JsonElement typeElement))
                {
                    string tagName = typeElement.GetString();

                    // Eğer etiket tipi "text" ise, sadece içeriği yazdır, etiketin kendisini değil.
                    if (tagName == "text")
                    {
                        if (jsonObject.TryGetProperty("content", out JsonElement textContentElement))
                        {
                            htmlBuilder.Append(textContentElement.GetString());
                        }
                    }
                    else
                    {
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
            }
            else if (jsonObject.ValueKind == JsonValueKind.String)
            {
                htmlBuilder.Append(jsonObject.GetString());
            }

            return htmlBuilder.ToString();
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
            var jsonString = original.ToJsonString();
            return JsonNode.Parse(jsonString).AsObject();
        }
        public JsonObject UpdateJsonRecursive(JsonObject jsonObject, Dictionary<string, string> editedChunks, bool isRoot = false)
        {
            if (jsonObject.TryGetPropertyValue("id", out JsonNode idNode))
            {
                string id = idNode.ToString();

                // Eğer ID editedChunks içinde varsa ve içerik boş değilse, content'i güncelle
                if (editedChunks.ContainsKey(id) && !isRoot)
                {
                    var newContent = editedChunks[id];
                    if (!string.IsNullOrEmpty(newContent))
                    {
                        jsonObject["content"] = JsonValue.Create(newContent);
                    }
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
