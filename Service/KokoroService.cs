using System.Text;
using System.Text.Json;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace GUA_Blazor.Service;

public class KokoroService
{
    private readonly string _outputDir;
    private static readonly HttpClient _http = new();
    private readonly string _baseUrl;

    public KokoroService(string baseUrl = "http://localhost:8082")
    {
        _baseUrl = baseUrl;
        _outputDir = Path.Combine(Environment.CurrentDirectory, "ai_files_temp");
        Directory.CreateDirectory(_outputDir);
    }

    public async Task<string> SynthesizeAsync(string text, string voice, string filename)
    {
        var outPath = Path.Combine(_outputDir, filename);

        var payload = JsonSerializer.Serialize(new { text, voice, output = outPath });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var resp = await _http.PostAsync($"{_baseUrl}/synthesize", content);

        if (!resp.IsSuccessStatusCode)
        {
            var error = await resp.Content.ReadAsStringAsync();
            throw new Exception($"Kokoro TTS failed: {resp.StatusCode} - {error}");
        }

        return outPath;
    }
}