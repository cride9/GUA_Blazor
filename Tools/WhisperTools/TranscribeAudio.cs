using LlmTornado.Common;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.WhisperTools;

public class TranscribeAudio : AITool<TranscribeArguments>
{
    private static readonly HttpClient _http = new();

    protected override async Task<string> ExecuteAsync(TranscribeArguments args)
    {
        var format = args.Format ?? "srt";
        var language = args.Language ?? "en";

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(await File.ReadAllBytesAsync(args.AudioPath!)),
            "file", Path.GetFileName(args.AudioPath!));
        form.Add(new StringContent(language), "language");
        form.Add(new StringContent(format), "response_format");

        var resp = await _http.PostAsync("http://localhost:8081/inference", form);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadAsStringAsync();

        var outPath = Path.ChangeExtension(args.AudioPath!, format == "plain" ? ".txt" : $".{format}");
        await File.WriteAllTextAsync(outPath, result);

        return $"Transcription saved: {outPath}\n\n{result}";
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "transcribe_audio",
        "Transcribes an audio file using Whisper. Returns transcript and saves it next to the audio file.",
        new
        {
            type = "object",
            properties = new
            {
                audio_path = new { type = "string", description = "Absolute path to the mp3/wav file." },
                language = new { type = "string", description = "Language code e.g. 'en', 'hu'. Default: en." },
                format = new { type = "string", @enum = new[] { "plain", "srt", "vtt" }, description = "Output format. Use srt for subtitle burning." }
            },
            required = new List<string> { "audio_path" }
        });
}

public class TranscribeArguments
{
    [JsonPropertyName("audio_path")]
    public string? AudioPath { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }
}