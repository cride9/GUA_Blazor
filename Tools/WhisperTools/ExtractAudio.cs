using LlmTornado.Common;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.WhisperTools;

public class ExtractAudio : AITool<ExtractAudioArguments>
{
    protected override string Execute(ExtractAudioArguments args)
    {
        var outputDir = Path.Combine(Environment.CurrentDirectory, "ai_files_temp");
        Directory.CreateDirectory(outputDir);

        var audioPath = Path.Combine(outputDir,
            Path.GetFileNameWithoutExtension(args.VideoPath!) + ".mp3");

        var proc = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{args.VideoPath}\" -vn -ar 16000 -ac 1 -ab 64k \"{audioPath}\" -y",
            RedirectStandardError = true,
            UseShellExecute = false
        })!;
        proc.WaitForExit();

        if (!File.Exists(audioPath))
            throw new Exception($"FFmpeg failed: {proc.StandardError.ReadToEnd()}");

        return $"Audio extracted: {audioPath}";
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "extract_audio",
        "Extracts audio from a video file (mp4, mkv, avi, mov) and saves it as mp3.",
        new
        {
            type = "object",
            properties = new
            {
                video_path = new { type = "string", description = "Absolute path to the video file." }
            },
            required = new List<string> { "video_path" }
        });
}

public class ExtractAudioArguments
{
    [JsonPropertyName("video_path")]
    public string? VideoPath { get; set; }
}