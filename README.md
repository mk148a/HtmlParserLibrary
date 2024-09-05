
# HtmlParserLibrary

HtmlParserLibrary is a versatile .NET library designed to seamlessly convert HTML content to JSON and vice versa. This library is built with robustness and scalability in mind, making it ideal for both small-scale and large-scale data processing tasks. It comes equipped with advanced error handling, preprocessing features, and flexible usage scenarios to ensure that your HTML and JSON data conversions are reliable and efficient.

## Features

- **HTML to JSON Conversion**: Converts HTML structures into a well-formed JSON format. It handles complex nested elements, attributes, and text content with ease, ensuring that the output JSON is both accurate and human-readable.
- **JSON to HTML Conversion**: Reconstructs HTML documents from JSON structures, preserving the original document's structure, elements, attributes, and even formatting. This feature is especially useful for dynamically generating HTML content from JSON data sources.
- **Error Handling**: Integrates sophisticated error handling mechanisms to process XML parsing errors, enabling continued processing even with malformed or incomplete HTML. Errors are logged and the system gracefully degrades to prevent application crashes.
- **Preprocessing**: Includes a suite of preprocessing tools to clean and prepare HTML content for conversion. This involves fixing common HTML issues such as self-closing tags, unescaped characters, and other non-XML-compliant constructs.
- **Large-Scale Processing**: Optimized for handling large HTML or JSON datasets. The library can process data in chunks, making it suitable for high-performance environments where memory management is crucial.
- **Customizable Output**: Provides options to customize the JSON output, allowing you to include or exclude specific HTML elements, attributes, or content based on your application's requirements.

## Installation

HtmlParserLibrary can be easily integrated into your .NET project via NuGet or by cloning the repository and building it manually.

### NuGet Installation (Coming Soon)

```bash
dotnet add package HtmlParserLibrary
```

### Manual Installation

1. Clone the repository:
    ```bash
    git clone https://github.com/yourusername/HtmlParserLibrary.git
    ```
2. Build the project using Visual Studio or the .NET CLI:
    ```bash
    dotnet build
    ```

## Usage

### Converting HTML to JSON

The following example demonstrates how to convert a simple HTML document into a JSON object:

```csharp
using HtmlParserLibrary;

string htmlContent = "<html><head><title>Example</title></head><body><p>Hello, world!</p></body></html>";
HtmlParser parser = new HtmlParser();
string jsonOutput = parser.ConvertHtmlToJson(htmlContent);

Console.WriteLine(jsonOutput);
```

**Output Example:**
```json
{
  "html": {
    "head": {
      "title": "Example"
    },
    "body": {
      "p": "Hello, world!"
    }
  }
}
```

### Converting JSON to HTML

This example shows how to convert a JSON structure back into an HTML document:

```csharp
using HtmlParserLibrary;

string jsonContent = "{ "html": { "head": { "title": "Example" }, "body": { "p": "Hello, world!" } } }";
HtmlParser parser = new HtmlParser();
string htmlOutput = parser.ConvertJsonToHtml(jsonContent);

Console.WriteLine(htmlOutput);
```

**Output Example:**
```html
<html>
  <head>
    <title>Example</title>
  </head>
  <body>
    <p>Hello, world!</p>
  </body>
</html>
```

### Advanced Usage: Processing Large JSON Data

For scenarios involving large datasets, the library provides mechanisms to process data in chunks:

```csharp
using HtmlParserLibrary;

string largeJsonContent = "Your large JSON content here...";
HtmlParser parser = new HtmlParser();
List<string> processedChunks = parser.ProcessJsonData(largeJsonContent);

foreach (var chunk in processedChunks)
{
    Console.WriteLine(chunk);
}
```

### Error Handling

The library is equipped to handle and log errors during the conversion process. If an XML parsing error occurs, the library will attempt to recover and include the raw HTML content within the JSON output for manual inspection:

```csharp
using HtmlParserLibrary;

try
{
    string htmlContent = "<html><head><title>Example</title><body><p>Hello, world!</p></body></html>";
    HtmlParser parser = new HtmlParser();
    string jsonOutput = parser.ConvertHtmlToJson(htmlContent);
    Console.WriteLine(jsonOutput);
}
catch (HtmlParsingException ex)
{
    Console.WriteLine("An error occurred while parsing HTML: " + ex.Message);
}
```

## Use Cases

- **Web Scraping**: Easily convert scraped HTML content into structured JSON for further data processing or analysis.
- **Content Management Systems (CMS)**: Convert user-generated HTML content into JSON for storage in a database, enabling easy retrieval and modification.
- **Email Processing**: Parse and convert HTML email templates into JSON, allowing for dynamic content generation or template manipulation.
- **API Development**: Use JSON as a standard format for handling HTML content in API responses or requests, ensuring consistent and easy-to-parse data exchange.

## Contributing

We welcome contributions! If you have ideas for new features, find bugs, or want to improve the documentation, please feel free to open an issue or submit a pull request.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for more details.

## Acknowledgments

- Special thanks to the open-source community for providing valuable feedback and contributions that have helped shape this library.
- Inspiration for this project came from the need to manage large HTML datasets in a more structured and error-tolerant manner.
