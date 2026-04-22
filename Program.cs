using GUA_Blazor.Components;
using GUA_Blazor.Service;
using GUA_Blazor.Models;
using GUA_Blazor.Tools.Web;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<SessionFactory>();

var app = builder.Build();

try {
    var browser = BrowserSession.Instance;
    await browser.EnsureInitializedAsync();
} catch (Exception ex) {
    Console.WriteLine($"Warning: Playwright init failed: {ex.Message}. Browser tools will not work.");
}

var sessionsPath = Path.Combine(builder.Environment.ContentRootPath, "sessions");
Directory.CreateDirectory(sessionsPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(sessionsPath),
    RequestPath = "/sessions"
});

var screenshotsPath = "/tmp/gua_screenshots";
Directory.CreateDirectory(screenshotsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(screenshotsPath),
    RequestPath = "/screenshots"
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


// Minimal REST API for programmatic agent invocation
app.MapPost("/api/agent", async (HttpContext ctx) =>
{
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    var json = System.Text.Json.JsonDocument.Parse(body);
    var message = json.RootElement.GetProperty("message").GetString() ?? "";
    var sessionId = Guid.NewGuid().ToString("N");

    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers.Append("Cache-Control", "no-cache");
    ctx.Response.Headers.Append("Connection", "keep-alive");

    var ai = new AIService(sessionId);
    var writer = ctx.Response.BodyWriter;

    await ai.SendMessageAgent(message, new List<string>(), async (chunk) =>
    {
        var data = $"data: {System.Text.Json.JsonSerializer.Serialize(chunk)}\n\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(data);
        await ctx.Response.Body.WriteAsync(bytes);
        await ctx.Response.Body.FlushAsync();
    }, ctx.RequestAborted);

    // Signal end
    var endBytes = System.Text.Encoding.UTF8.GetBytes("data: [DONE]\n\n");
    await ctx.Response.Body.WriteAsync(endBytes);
    await ctx.Response.Body.FlushAsync();
});

app.Run();
