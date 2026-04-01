using LlmTornado.Common;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.WhisperTools;

public class ExtractAudio : AITool<ExtractAudioArguments>
{
    public ExtractAudio(string sessionId) : base(sessionId) { }

    protected override string Execute(ExtractAudioArguments args)
    {
        var videoPath = Sandbox.Resolve(args.VideoPath!, SessionId);
        var audioPath = Path.Combine(WorkPath, Path.GetFileNameWithoutExtension(videoPath) + ".mp3");

        using var proc = new Process();
        proc.StartInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -i \"{videoPath}\" -vn -acodec libmp3lame -ab 128k \"{audioPath}\"",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        proc.Start();

        if (!proc.WaitForExit(120_000))
        {
            try
            {
                proc.Kill();
            }
            catch
            {
            }

            proc.WaitForExit();

            if (File.Exists(audioPath))
                File.Delete(audioPath);

            throw new Exception("FFmpeg timed out: Process exceeded 1 minute limit and was force closed.");
        }

        var stderr = proc.StandardError.ReadToEnd();

        if (proc.ExitCode != 0 || !File.Exists(audioPath))
            throw new Exception($"FFmpeg failed (ExitCode {proc.ExitCode}): {stderr}");

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