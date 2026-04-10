using LlmTornado.ChatFunctions;
using LlmTornado.Common;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GUA_Blazor.Tools.Web;

namespace GUA_Blazor.Tools;

/// <summary>
/// Adaptive vision tool using Falcon Perception (0.6B).
/// 3-layer pipeline: detection mode routing → perception → presentation routing.
/// Works for any visual task: finding UI elements, objects, spatial reasoning, etc.
/// </summary>
public class VisionDetectTool : AITool<VisionDetectArgs>
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private const string VISION_URL = "http://localhost:8090";

    protected override async Task<object?> ExecuteAsync(VisionDetectArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Query))
            throw new Exception("Query is required. Describe what you want to find.");

        // Get current screenshot from the browser
        var browser = BrowserSession.Instance;
        var screenshotBytes = await browser.GetScreenshotBytesAsync();
        if (screenshotBytes == null || screenshotBytes.Length == 0)
            return "No browser page open. Navigate to a page first using browser_use.";

        var imageB64 = Convert.ToBase64String(screenshotBytes);

        // Call adaptive vision pipeline
        var requestBody = JsonSerializer.Serialize(new
        {
            image_b64 = imageB64,
            query = args.Query,
            task_context = args.Context ?? "",
            presentation = args.Presentation ?? "auto",
        });

        try
        {
            var response = await _http.PostAsync(
                $"{VISION_URL}/vision",
                new StringContent(requestBody, Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return $"Vision server error ({response.StatusCode}): {err}";
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(json);
            var root = result.RootElement;

            var count = root.GetProperty("count").GetInt32();
            var detMode = root.GetProperty("detection_mode").GetString();
            var presMode = root.GetProperty("presentation_mode").GetString();
            var totalMs = root.TryGetProperty("total_ms", out var ms) ? ms.GetInt32() : 0;

            if (count == 0)
                return $"No '{args.Query}' found. (mode={detMode}, {totalMs}ms)";

            var sb = new StringBuilder();
            sb.AppendLine($"Found {count} '{args.Query}' (mode={detMode}, presentation={presMode}, {totalMs}ms):");
            sb.AppendLine();

            // Always include coordinates
            var coordsText = root.GetProperty("coords_text").GetString() ?? "";
            sb.AppendLine(coordsText);

            // Include summary
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() : "";
            if (!string.IsNullOrEmpty(summary))
            {
                sb.AppendLine();
                sb.AppendLine(summary);
            }

            sb.AppendLine();
            sb.AppendLine("Use click_coordinates or click_coordinates_batch with the center/centroid coordinates above.");

            // If overlay image is included, add it as a user message with image
            if (root.TryGetProperty("overlay_image_b64", out var overlayEl))
            {
                var overlayB64 = overlayEl.GetString();
                if (!string.IsNullOrEmpty(overlayB64))
                {
                    sb.AppendLine("[Set-of-Marks overlay image attached — numbered colored regions mark each detection]");
                }
            }

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Cannot reach vision server at {VISION_URL}. Error: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            return "Vision detection timed out (60s).";
        }
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "vision_detect",
        "Find objects, UI elements, or any visual element on the current browser page using AI vision (Falcon Perception). " +
        "Returns pixel coordinates (center, centroid, bounding box) for clicking. " +
        "Automatically chooses fast detection or precise segmentation based on the task. " +
        "Use for: captcha grids, canvas elements, iframes, buttons, spatial reasoning, measuring, comparing objects.",
        new
        {
            type = "object",
            properties = new
            {
                query = new
                {
                    type = "string",
                    description = "What to find. Natural language: 'bus', 'submit button', 'red checkbox', 'person on the left'."
                },
                context = new
                {
                    type = "string",
                    description = "Optional task context to help routing. E.g. 'captcha solving' (fast) or 'measure distance between players' (precise segmentation)."
                },
                presentation = new
                {
                    type = "string",
                    description = "How to present results. 'auto' (default, system decides), 'coords' (text only, fastest), 'crops' (cropped images of each detection), 'overlay' (Set-of-Marks annotated image).",
                    @enum = new[] { "auto", "coords", "crops", "overlay" }
                }
            },
            required = new List<string> { "query" }
        });
}

public class VisionDetectArgs
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("context")]
    public string? Context { get; set; }

    [JsonPropertyName("presentation")]
    public string? Presentation { get; set; }
}
