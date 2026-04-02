using LlmTornado.Common;
using Microsoft.Playwright;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.Web;

public class BrowserUseTool : AITool<BrowserUseArguments>
{
    protected override async Task<object?> ExecuteAsync(BrowserUseArguments args)
    {
        if (string.IsNullOrWhiteSpace(args.Action))
            throw new Exception("Action is required.");

        string actionResult;
        var browser = BrowserSession.Instance;

        try
        {
            actionResult = args.Action.ToLower() switch
            {
                "go_to_url" => await browser.GoToUrlAsync(args.Url),
                "click_element" => await browser.ClickElementAsync(args.Index),
                "input_text" => await browser.InputTextAsync(args.Index, args.Text),
                "scroll_down" => await browser.ScrollAsync(1, args.ScrollAmount),
                "scroll_up" => await browser.ScrollAsync(-1, args.ScrollAmount),
                "send_keys" => await browser.SendKeysAsync(args.Keys),
                "go_back" => await browser.GoBackAsync(),
                "refresh" => await browser.RefreshAsync(),
                "wait" => await browser.WaitAsync(args.Seconds),
                "extract_content" => await browser.ExtractContentAsync(),
                _ => throw new Exception($"Unknown action: {args.Action}")
            };
        }
        catch (Exception ex)
        {
            actionResult = $"Action failed: {ex.Message}";
        }

        // After every action, return the result PLUS the new state of the browser
        bool includeScreenshot = args.Action.ToLower() is "go_to_url" or "go_back" or "refresh" or "extract_content";
        var currentState = await browser.GetCurrentStateAsync(includeScreenshot) as BrowserState;

        if (currentState is null)
            return "Error";

        var output = new BrowserUseOutput
        {
            ActionResult = actionResult,
            BrowserState = currentState
        };

        return output;
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "browser_use",
        "A powerful browser automation tool. Maintains state across calls. Returns the result of the action alongside the new page state (URL, interactive elements, and base64 screenshot).",
        new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "The browser action to perform.",
                    @enum = new[] { "go_to_url", "click_element", "input_text", "scroll_down", "scroll_up", "send_keys", "go_back", "refresh", "wait", "extract_content" }
                },
                url = new { type = "string", description = "URL for 'go_to_url' action." },
                index = new { type = "integer", description = "Element index for 'click_element' or 'input_text' actions." },
                text = new { type = "string", description = "Text for 'input_text' action." },
                scroll_amount = new { type = "integer", description = "Pixels to scroll for 'scroll_down' or 'scroll_up'." },
                keys = new { type = "string", description = "Keys to send for 'send_keys' action." },
                seconds = new { type = "integer", description = "Seconds to wait for 'wait' action." }
            },
            required = new List<string> { "action" }
        });
}

// 3. The Persistent Browser Session
// This ensures Playwright doesn't close between LLM tool calls.
public class BrowserSession
{
    public static readonly BrowserSession Instance = new();

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private const string BuildInteractiveElementsJs = @"
        () => {
            const elements = Array.from(document.querySelectorAll('button, a, input, select, textarea, [role=""button""], [tabindex]:not([tabindex=""-1""])'))
                .filter(el => {
                    const rect = el.getBoundingClientRect();
                    return rect.width > 0 && rect.height > 0 && window.getComputedStyle(el).visibility !== 'hidden';
                });
            
            elements.forEach((el, index) => el.setAttribute('data-browser-use-index', index));
            
            return elements.map((el, index) => {
                let text = el.innerText || el.value || el.getAttribute('aria-label') || el.placeholder || el.tagName.toLowerCase();
                text = text.replace(/\s+/g, ' ').trim().substring(0, 50);
                return `[${index}]<${el.tagName.toLowerCase()}>${text}</>`;
            }).join('\n');
        }
    ";

    private BrowserSession() { }

    public async Task EnsureInitializedAsync()
    {
        if (_page != null) return;

        await _initLock.WaitAsync();
        try
        {
            if (_page != null) return;

            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false, // Set to true if you don't want to see the browser UI
                Args = new[] { "--disable-web-security" }
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
            });

            _page = await context.NewPageAsync();
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<string> GoToUrlAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) throw new Exception("URL is required.");
        if (!url.StartsWith("http")) url = "https://" + url;

        await _page!.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.Load });
        return $"Navigated to {url}";
    }

    public async Task<string> ClickElementAsync(int? index)
    {
        if (index == null) throw new Exception("Index is required.");
        var success = await _page!.EvaluateAsync<bool>($@"() => {{
            const el = document.querySelector(`[data-browser-use-index=""{index}""]`);
            if(el) {{ el.click(); return true; }} return false;
        }}");

        if (!success) throw new Exception($"Element [{index}] not found.");
        await WaitForNetworkIdle();
        return $"Clicked element [{index}]";
    }

    public async Task<string> InputTextAsync(int? index, string? text)
    {
        if (index == null || string.IsNullOrEmpty(text)) throw new Exception("Index and text are required.");
        var success = await _page!.EvaluateAsync<bool>($@"() => {{
            const el = document.querySelector(`[data-browser-use-index=""{index}""]`);
            if(el) {{ el.value = '{text}'; el.dispatchEvent(new Event('input', {{ bubbles: true }})); return true; }} return false;
        }}");

        if (!success) throw new Exception($"Element [{index}] not found.");
        return $"Input '{text}' into element [{index}]";
    }

    public async Task<string> ScrollAsync(int direction, int? scrollAmount)
    {
        int amount = scrollAmount ?? 800;
        await _page!.EvaluateAsync($"window.scrollBy(0, {direction * amount});");
        await Task.Delay(500); // Give UI time to settle
        return $"Scrolled {(direction > 0 ? "down" : "up")} by {amount} pixels.";
    }

    public async Task<string> SendKeysAsync(string? keys)
    {
        if (string.IsNullOrEmpty(keys)) throw new Exception("Keys are required.");
        await _page!.Keyboard.PressAsync(keys);
        await WaitForNetworkIdle();
        return $"Sent keys: {keys}";
    }

    public async Task<string> GoBackAsync()
    {
        await _page!.GoBackAsync();
        return "Navigated back.";
    }

    public async Task<string> RefreshAsync()
    {
        await _page!.ReloadAsync();
        return "Refreshed page.";
    }

    public async Task<string> WaitAsync(int? seconds)
    {
        int waitMs = (seconds ?? 3) * 1000;
        await Task.Delay(waitMs);
        return $"Waited {seconds ?? 3} seconds.";
    }

    public async Task<string> ExtractContentAsync(int maxChars = 3000)
    {
        var content = await _page!.EvaluateAsync<string>("() => document.body.innerText");

        if (string.IsNullOrWhiteSpace(content)) return "Page has no readable text content.";

        // Collapse excessive whitespace/newlines
        content = System.Text.RegularExpressions.Regex.Replace(content, @"\n{3,}", "\n\n");
        content = System.Text.RegularExpressions.Regex.Replace(content, @" {2,}", " ");
        content = content.Trim();

        if (content.Length <= maxChars)
            return content;

        return content[..maxChars] + $"\n\n[TRUNCATED — {content.Length - maxChars} more characters not shown. Scroll down and call extract_content again to read further.]";
    }

    public async Task<object> GetCurrentStateAsync(bool includeScreenshot = false)
    {
        if (_page == null) return new { error = "Browser not initialized." };

        await WaitForNetworkIdle();

        string interactiveElements = await _page.EvaluateAsync<string>(BuildInteractiveElementsJs);

        // Strip elements that have no useful label (just tag names like "div", "span", "button")
        var usefulLines = (interactiveElements?.Split('\n') ?? [])
            .Where(line => {
                // Keep lines where the text inside <tag>...</> is more than just a tag name or empty
                var match = System.Text.RegularExpressions.Regex.Match(line, @"<\w+>(.+)<\/>");
                return match.Success && match.Groups[1].Value.Length > 2;
            })
            .ToArray();

        interactiveElements = string.Join('\n', usefulLines.Length > 50 ? usefulLines[..50] : usefulLines)
            + (usefulLines.Length > 50 ? $"\n... ({usefulLines.Length - 50} more elements not shown)" : "");

        // Truncate to first 50 elements to avoid flooding context
        var lines = interactiveElements?.Split('\n') ?? [];
        if (lines.Length > 50)
            interactiveElements = string.Join('\n', lines[..50]) + $"\n... ({lines.Length - 50} more elements not shown, scroll to reveal)";

        var state = new BrowserState()
        {
            Url = _page.Url,
            Title = await _page.TitleAsync(),
            InteractiveElements = interactiveElements!,
            Instructions = "Use the index in the brackets [index] for your next 'click_element' or 'input_text' action.",
            ScreenshotBase64 = null,
            ScreenshotMime = null
        };

        if (includeScreenshot)
        {
            var screenshotBytes = await _page.ScreenshotAsync(new PageScreenshotOptions
            {
                Type = ScreenshotType.Jpeg,
                Quality = 40
            });
            state.ScreenshotMime = "image/jpeg";
            state.ScreenshotBase64 = $"data:{state.ScreenshotMime};base64,{Convert.ToBase64String(screenshotBytes)}";
        }

        return state;
    }

    private async Task WaitForNetworkIdle()
    {
        try
        {
            await _page!.WaitForLoadStateAsync((LoadState?)WaitUntilState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 3000 });
        }
        catch { /* Ignore timeout if network isn't completely idle */ }
    }
}

public class BrowserUseArguments
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("index")]
    public int? Index { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("scroll_amount")]
    public int? ScrollAmount { get; set; }

    [JsonPropertyName("keys")]
    public string? Keys { get; set; }

    [JsonPropertyName("seconds")]
    public int? Seconds { get; set; }
}

public sealed class BrowserUseOutput
{
    [JsonPropertyName("action_result")]
    public string? ActionResult { get; set; }

    [JsonPropertyName("browser_state")]
    public BrowserState? BrowserState { get; set; }
}

public sealed class BrowserState
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("instructions")]
    public string? Instructions { get; set; }

    [JsonPropertyName("interactive_elements_map")]
    public string InteractiveElements { get; set; }

    // ha base64-ben jön
    [JsonPropertyName("screenshot_base64")]
    public string? ScreenshotBase64 { get; set; }

    [JsonPropertyName("screenshot_mime")]
    public string? ScreenshotMime { get; set; } = "image/jpeg";
}