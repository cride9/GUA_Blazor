using LlmTornado.Common;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.WhisperTools;

public class BurnSubtitles : AITool<BurnSubtitlesArguments>
{
    public BurnSubtitles(string sessionId) : base(sessionId) { }

    protected override async Task<object?> ExecuteAsync(BurnSubtitlesArguments args)
    {
        var color = (args.Color ?? "yellow").ToLowerInvariant() switch
        {
            "yellow" => "&H0000FFFF",
            "white" => "&H00FFFFFF",
            "red" => "&H000000FF",
            "green" => "&H0000FF00",
            "blue" => "&H00FF0000",
            _ => "&H0000FFFF"
        };

        var alignment = (args.Position ?? "bottom").ToLowerInvariant() switch
        {
            "top" => 8,
            "bottom" => 2,
            _ => 2
        };

        var videoPath = Sandbox.Resolve(args.VideoPath!, SessionId);
        var srtPath = Sandbox.Resolve(args.SrtPath!, SessionId);

        var outputPath = Path.Combine(
            Path.GetDirectoryName(videoPath)!,
            Path.GetFileNameWithoutExtension(videoPath) + "_captioned.mp4");

        var escapedSrt = srtPath.Replace("\\", "/").Replace(":", "\\:");

        var filter = $"subtitles='{escapedSrt}':force_style='" +
                     $"FontName={args.Font ?? "Arial"}," +
                     $"FontSize={args.FontSize ?? 24}," +
                     $"PrimaryColour={color}," +
                     $"Bold={(args.Bold == true ? 1 : 0)}," +
                     $"Alignment={alignment}'";

        var proc = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{videoPath}\" -vf \"{filter}\" -c:v libx264 -preset ultrafast -c:a copy \"{outputPath}\" -y",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        })!;

        proc.StandardInput.Close();

        var stderrTask = proc.StandardError.ReadToEndAsync();
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        try
        {
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            proc.Kill();
            throw new Exception("FFmpeg timed out after 10 minutes.");
        }

        var stderr = await stderrTask;
        await stdoutTask;

        if (!File.Exists(outputPath))
            throw new Exception($"FFmpeg failed. stderr: {stderr}");

        return $"Video with subtitles saved: {outputPath}";
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "burn_subtitles",
        "Burns an SRT subtitle file into a video using FFmpeg with custom styling.",
        new
        {
            type = "object",
            properties = new
            {
                video_path = new { type = "string", description = "Absolute path to the source video." },
                srt_path = new { type = "string", description = "Absolute path to the .srt file." },
                font = new { type = "string", description = "Font name e.g. 'Arial', 'Impact'." },
                font_size = new { type = "integer", description = "Font size. Default: 24." },
                color = new { type = "string", description = "Subtitle color: yellow, white, red, green, blue." },
                bold = new { type = "boolean", description = "Whether to bold the text." },
                position = new { type = "string", @enum = new[] { "bottom", "top" }, description = "Where to place subtitles." }
            },
            required = new List<string> { "video_path", "srt_path" }
        });
}

public class BurnSubtitlesArguments
{
    [JsonPropertyName("video_path")]
    public string? VideoPath { get; set; }

    [JsonPropertyName("srt_path")]
    public string? SrtPath { get; set; }

    [JsonPropertyName("font")]
    public string? Font { get; set; }

    [JsonPropertyName("font_size")]
    public int? FontSize { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("bold")]
    public bool? Bold { get; set; }

    [JsonPropertyName("position")]
    public string? Position { get; set; }
}