using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace HtmlParserLibrary
{
    public class HtmlParser
    {
        private string PreprocessHtmlForXml(string htmlContent)
        {
            // Self-closing tagleri uygun formatta değiştir
            htmlContent = Regex.Replace(htmlContent, @"<(\w+)([^>]*)/>", "<$1$2></$1>");

            // '&' karakterini XML uyumlu hale getiriyoruz
            htmlContent = htmlContent.Replace("&", "&amp;");

            return htmlContent;
        }

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
                return JsonSerializer.Serialize(rootElement, new JsonSerializerOptions { WriteIndented = true });
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

        private dynamic ConvertNodeToJson(XElement element)
        {
            var jsonElement = new
            {
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

            // Root elementin içindeki içerikleri direkt olarak işliyoruz
            if (jsonObject.TryGetProperty("content", out JsonElement contentElement))
            {
                foreach (JsonElement child in contentElement.EnumerateArray())
                {
                    ConvertJsonToHtmlRecursive(child, htmlBuilder);
                }
            }

            return htmlBuilder.ToString();
        }

        private void ConvertJsonToHtmlRecursive(JsonElement jsonObject, StringBuilder htmlBuilder)
        {
            if (jsonObject.ValueKind == JsonValueKind.Object)
            {
                if (jsonObject.TryGetProperty("type", out JsonElement typeElement))
                {
                    string tagName = typeElement.GetString();
                    var localHtml = new StringBuilder($"<{tagName}");

                    if (jsonObject.TryGetProperty("attributes", out JsonElement attributesElement))
                    {
                        foreach (JsonProperty attribute in attributesElement.EnumerateObject())
                        {
                            localHtml.Append($" {attribute.Name}=\"{attribute.Value.GetString()}\"");
                        }
                    }

                    localHtml.Append(">");

                    if (jsonObject.TryGetProperty("content", out JsonElement contentElement))
                    {
                        foreach (JsonElement child in contentElement.EnumerateArray())
                        {
                            ConvertJsonToHtmlRecursive(child, localHtml);
                        }
                    }

                    localHtml.Append($"</{tagName}>");
                    htmlBuilder.Append(localHtml.ToString());
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

            ProcessJsonRecursive(jsonObject, currentChunk, processedList, chunkCounter);

            if (currentChunk.Length > 0)
            {
                processedList.Add($"##{chunkCounter:0000}## {currentChunk}");
            }

            return processedList;
        }

        private void ProcessJsonRecursive(JsonElement jsonObject, StringBuilder currentChunk, List<string> processedList, int chunkCounter)
        {
            if (jsonObject.ValueKind == JsonValueKind.String || jsonObject.ValueKind == JsonValueKind.Number)
            {
                currentChunk.Append(jsonObject.ToString());

                if (currentChunk.Length >= 4000)
                {
                    lock (processedList)
                    {
                        processedList.Add($"##{chunkCounter:0000}## {currentChunk.ToString()}");
                    }
                    currentChunk.Clear();
                    chunkCounter++;
                }
            }
            else if (jsonObject.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in jsonObject.EnumerateArray())
                {
                    var localChunk = new StringBuilder();
                    int localCounter = Interlocked.Increment(ref chunkCounter);

                    ProcessJsonRecursive(item, localChunk, processedList, localCounter);

                    lock (processedList)
                    {
                        if (localChunk.Length > 0)
                        {
                            processedList.Add($"##{localCounter:0000}## {localChunk.ToString()}");
                        }
                    }
                }
            }
            else if (jsonObject.ValueKind == JsonValueKind.Object)
            {
                if (jsonObject.TryGetProperty("content", out JsonElement contentElement))
                {
                    if (contentElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var child in contentElement.EnumerateArray())
                        {
                            ProcessJsonRecursive(child, currentChunk, processedList, chunkCounter);
                        }
                    }
                    else if (contentElement.ValueKind == JsonValueKind.String || contentElement.ValueKind == JsonValueKind.Number)
                    {
                        currentChunk.Append(contentElement.ToString());

                        if (currentChunk.Length >= 4000)
                        {
                            processedList.Add($"##{chunkCounter:0000}## {currentChunk}");
                            currentChunk.Clear();
                            chunkCounter++;
                        }
                    }
                }
            }
        }
    }
}
