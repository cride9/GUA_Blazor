using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System.Text;
using System.Text.Json;
using System.Web;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace GUA_Blazor.Tools.Web;

public record SearchResult(string Title, string Link, string Snippet);

public static class WebScraper
{
    private static string apiKey = Environment.GetEnvironmentVariable("google_api")!;
    private static string searchEngineId = Environment.GetEnvironmentVariable("google_engine")!;
    static WebScraper()
    {
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
    }
    private static readonly HttpClient client = new HttpClient(new HttpClientHandler() { AutomaticDecompression = System.Net.DecompressionMethods.All });
    public static async Task<string> Search(string query)
    {
        try
        {
            var results = await PerformSearch(query);
            if (results == null || results.Count == 0)
                return "No search results found.";

            StringBuilder sb = new();
            int count = 1;

            foreach (var result in results)
            {
                sb.AppendLine($"{count++}. {result.Title}");
                sb.AppendLine($"\tLink: {result.Link}");
                sb.AppendLine($"\tSnippet: {result.Snippet}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public static async Task<List<SearchResult>> PerformSearch(string query)
    {
        string encodedQuery = HttpUtility.UrlEncode(query);

        string searchUrl = $"https://www.googleapis.com/customsearch/v1?key={apiKey}&cx={searchEngineId}&q={encodedQuery}";

        string jsonResponse = await client.GetStringAsync(searchUrl);

        using JsonDocument doc = JsonDocument.Parse(jsonResponse);
        var results = new List<SearchResult>();

        if (doc.RootElement.TryGetProperty("items", out JsonElement items))
        {
            foreach (JsonElement item in items.EnumerateArray())
            {
                string title = item.GetProperty("title").GetString() ?? "";
                string link = item.GetProperty("link").GetString() ?? "";
                string snippet = item.GetProperty("snippet").GetString() ?? "";

                results.Add(new SearchResult(title, link, snippet));
            }
        }

        return results;
    }

    public static async Task<string> ScrapeTextFromUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return "Invalid URL provided.";

        int? targetPage = null;
        if (!string.IsNullOrEmpty(uri.Fragment) && uri.Fragment.StartsWith("#page="))
        {
            string pageNumberStr = uri.Fragment.Substring("#page=".Length);
            if (int.TryParse(pageNumberStr, out int pageNumber))
            {
                targetPage = pageNumber;
            }
        }

        try
        {
            // A kliensnek érdemes beállítani egy User-Agentet, hogy ne tiltsanak ki minket könnyen
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36");
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9,hu;q=0.8");
            request.Headers.AcceptEncoding.ParseAdd("gzip");
            request.Headers.AcceptEncoding.ParseAdd("deflate");
            request.Headers.AcceptEncoding.ParseAdd("br");
            request.Headers.Add("DNT", "1"); // Do Not Track
            request.Headers.Add("Upgrade-Insecure-Requests", "1");

            using HttpResponseMessage response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

            if (contentType.Contains("application/pdf"))
            {
                using Stream pdfStream = await response.Content.ReadAsStreamAsync();
                // PDF-ből jelenleg nem szedünk ki linkeket, de a jövőben bővíthető lenne.
                return ExtractTextFromPdf(pdfStream, targetPage);
            }
            else if (contentType.Contains("text/html"))
            {
                if (targetPage.HasValue)
                {
                    return "Specifying a page number with '#page=N' is only supported for PDF files.";
                }
                string htmlContent = await response.Content.ReadAsStringAsync();

                // Itt hívjuk meg a továbbfejlesztett HTML feldolgozót
                var (extractedText, links) = ExtractTextAndLinksFromHtml(htmlContent, uri);

                // Összeállítjuk a végső kimenetet
                var resultBuilder = new StringBuilder();
                resultBuilder.Append(extractedText);

                if (links.Any())
                {
                    resultBuilder.AppendLine();
                    resultBuilder.AppendLine();
                    resultBuilder.AppendLine("Links: ");
                    foreach (var link in links)
                    {
                        resultBuilder.AppendLine(link);
                    }
                }

                return resultBuilder.ToString();
            }
            else
            {
                return await response.Content.ReadAsStringAsync();
            }
        }
        catch (HttpRequestException e)
        {
            return $"Failed to download content from {url}. Status: {e.StatusCode} ({e.Message})";
        }
        catch (Exception ex)
        {
            return $"An unexpected error occurred while scraping {url}. Details: {ex.Message}";
        }
    }

    /// <summary>
    /// Uses AngleSharp to parse HTML and extract the main text content.
    /// </summary>
    private static (string Text, List<string> Links) ExtractTextAndLinksFromHtml(string htmlContent, Uri baseUri)
    {
        var context = BrowsingContext.New(Configuration.Default);
        IDocument document = context.OpenAsync(req => req.Content(htmlContent)).Result;

        // --- 1. Linkek kigyűjtése ---
        var uniqueLinks = new HashSet<string>();
        var linkElements = document.QuerySelectorAll("a[href]");

        foreach (var linkElement in linkElements.OfType<IHtmlAnchorElement>())
        {
            string href = linkElement.Href; // Az .Href már abszolút URL-t ad vissza!
            if (!string.IsNullOrWhiteSpace(href) &&
                !href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) &&
                !href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) &&
                !href.Contains("#")) // Egyszerűsített szűrés az oldalon belüli linkekre
            {
                uniqueLinks.Add(href);
            }
        }

        // --- 2. Zajszűrés és szöveg kinyerése ---
        IElement contentElement = null;

        // Stratégia: Próbáljuk megtalálni a fő tartalmi részt
        string[] contentSelectors = { "main", "article", "[role='main']", "#content", "#main-content", ".post-content", ".article-body" };
        foreach (var selector in contentSelectors)
        {
            contentElement = document.QuerySelector(selector);
            if (contentElement != null)
            {
                break; // Megvan, ezt használjuk!
            }
        }

        // Ha nem találtunk specifikus tartalmi részt, használjuk a body-t fallbackként
        if (contentElement == null)
        {
            contentElement = document.Body;
        }

        // Távolítsuk el a felesleges elemeket a kiválasztott (vagy a body) részből
        var elementsToRemove = contentElement.QuerySelectorAll("script, style, nav, header, footer, aside, form, noscript, svg, button, input, [role='navigation'], [role='banner'], [role='contentinfo'], [class*='cookie'], [id*='cookie']");
        foreach (var element in elementsToRemove)
        {
            element.Remove();
        }

        string rawText = contentElement.TextContent;

        // --- 3. Szöveg tisztítása ---
        var lines = rawText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            string trimmedLine = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedLine))
            {
                // Opcionális: Szűrhetjük a túl rövid, valószínűleg csak menüelemeket tartalmazó sorokat
                if (trimmedLine.Length > 2)
                {
                    builder.AppendLine(trimmedLine);
                }
            }
        }

        return (builder.ToString(), uniqueLinks.ToList());
    }

    /// <summary>
    /// Uses PdfPig to extract text from a PDF stream.
    /// If a targetPage is specified, it extracts text from that page only.
    /// Otherwise, it extracts text from the entire document.
    /// </summary>
    /// <param name="pdfStream">The stream containing the PDF data.</param>
    /// <param name="targetPage">An optional 1-based page number to extract text from.</param>
    private static string ExtractTextFromPdf(Stream pdfStream, int? targetPage = null)
    {
        using (PdfDocument document = PdfDocument.Open(pdfStream))
        {
            // If a specific page is requested
            if (targetPage.HasValue)
            {
                int pageNum = targetPage.Value;
                // Validate that the requested page number exists in the document
                if (pageNum > 0 && pageNum <= document.NumberOfPages)
                {
                    Page page = document.GetPage(pageNum);
                    return page.Text;
                }
                else
                {
                    return $"Error: Page {pageNum} not found. The PDF only has {document.NumberOfPages} pages.";
                }
            }
            else // If no specific page is requested, get text from all pages
            {
                var builder = new StringBuilder();
                foreach (Page page in document.GetPages())
                {
                    builder.AppendLine(page.Text);
                }
                return builder.ToString();
            }
        }
    }
}
