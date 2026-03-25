namespace GUA_Blazor.Service;

public class WhisperService
{
    private readonly HttpClient _http = new();
    private readonly string _baseUrl;

    public WhisperService(string baseUrl = "http://localhost:8081")
    {
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// Returns the transcript as plain text.
    /// </summary>
    public async Task<string> TranscribeAsync(string audioPath, string language = "en")
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(await File.ReadAllBytesAsync(audioPath)),
            "file", Path.GetFileName(audioPath));
        form.Add(new StringContent("true"), "temperature_inc");
        form.Add(new StringContent(language), "language");
        form.Add(new StringContent("plain"), "response_format"); // plain / srt / vtt / json

        var resp = await _http.PostAsync($"{_baseUrl}/inference", form);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Returns an SRT file content string.
    /// </summary>
    public async Task<string> TranscribeToSrtAsync(string audioPath, string language = "en")
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(await File.ReadAllBytesAsync(audioPath)),
            "file", Path.GetFileName(audioPath));
        form.Add(new StringContent(language), "language");
        form.Add(new StringContent("srt"), "response_format");

        var resp = await _http.PostAsync($"{_baseUrl}/inference", form);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }
}
