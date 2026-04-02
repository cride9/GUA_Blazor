using GUA_Blazor.Components;
using GUA_Blazor.Models;
using GUA_Blazor.Tools.Web;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<SessionFactory>();

var app = builder.Build();

var browser = BrowserSession.Instance;
await browser.EnsureInitializedAsync();

var sessionsPath = Path.Combine(builder.Environment.ContentRootPath, "sessions");
Directory.CreateDirectory(sessionsPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(sessionsPath),
    RequestPath = "/sessions"
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

app.Run();
