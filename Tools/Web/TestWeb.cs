using LlmTornado.Common;
using Microsoft.Playwright;
using System.Text;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.Web;

public class TestWeb : AITool<TestWebArguments>
{
    private readonly IPlaywright? _playwrightService;
    private IBrowser? _browser;
    private IPage? _page;

    public TestWeb()
    {
        _playwrightService = Playwright.CreateAsync().Result;
        _browser = _playwrightService.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true, // Set to true if you don't want to see the browser UI
            Args = new[] { "--disable-web-security" }
        }).Result;

        var context = _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },

        }).Result;

        _page = context.NewPageAsync().Result;
    }

    protected override async Task<object?> ExecuteAsync(TestWebArguments args)
    {
        if (string.IsNullOrWhiteSpace(args.HtmlPath))
            throw new Exception("html_path is required.");

        var page = _page;

        StringBuilder logs = new();

        void OnConsole(object? sender, IConsoleMessage msg)
        {
            logs.AppendLine($"{msg.Type}: {msg.Text}");
        }

        void OnPageError(object? sender, string error)
        {
            logs.AppendLine($"pageerror: {error}");
        }

        // Attach listeners
        page.Console += OnConsole;
        page.PageError += OnPageError;

        try
        {
            var path = args.HtmlPath.Replace("\\", "/");

            if (!path.StartsWith("file:///"))
                path = $"file:///{path}";

            await page.GotoAsync(path);

            await page.WaitForTimeoutAsync(args.WaitMs ?? 2000);
        }
        finally
        {
            // Important: detach to avoid duplicate logs on next run
            page.Console -= OnConsole;
            page.PageError -= OnPageError;
        }

        return logs.ToString();
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "test_web",
        "Loads a local HTML file and captures console output (log, error, warning).",
        new
        {
            type = "object",
            properties = new
            {
                html_path = new { type = "string", description = "Full path to the local HTML file." },
                wait_ms = new { type = "integer", description = "Time to wait for scripts to execute (ms)." }
            },
            required = new List<string> { "html_path" }
        });
}

public class TestWebArguments
{
    [JsonPropertyName("html_path")]
    public string? HtmlPath { get; set; }

    [JsonPropertyName("wait_ms")]
    public int? WaitMs { get; set; } = 2000;
}