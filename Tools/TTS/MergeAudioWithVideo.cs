using LlmTornado.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GUA_Blazor.Tools.TTS;

public class MergeAudioWithVideo : AITool<MergeAudioWithVideoArguments>
{
    public MergeAudioWithVideo(string sessionId) : base(sessionId) { }

    protected override async Task<object?> ExecuteAsync(MergeAudioWithVideoArguments args)
    {
        var outputPath = Sandbox.Resolve(args.OutputFilename ?? "final_video.mp4", SessionId);
        var videoPath = Sandbox.Resolve(args.VideoPath!, SessionId);
        var audioPath = Sandbox.Resolve(args.AudioPath!, SessionId);

        var proc = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-stream_loop -1 -i \"{videoPath}\" -i \"{audioPath}\" " +
                        $"-map 0:v -map 1:a -c:v copy -shortest \"{outputPath}\" -y",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        })!;

        proc.StandardInput.Close();
        var stderrTask = proc.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            proc.Kill();
            throw new Exception("FFmpeg timed out.");
        }

        var stderr = await stderrTask;

        if (!File.Exists(outputPath))
            throw new Exception($"FFmpeg failed. Error log: {stderr}");

        return $"Final video saved: {outputPath}";
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "merge_audio_with_video",
        "Merges a TTS audio file with a video. Loops the video if it is shorter than the audio. Replaces original video audio.",
        new
        {
            type = "object",
            properties = new
            {
                video_path = new { type = "string", description = "Absolute path to the source video (e.g. minecraft parkour)." },
                audio_path = new { type = "string", description = "Absolute path to the merged TTS audio .wav file." },
                output_filename = new { type = "string", description = "Output filename e.g. 'final_video.mp4'" }
            },
            required = new List<string> { "video_path", "audio_path" }
        });
}

public class MergeAudioWithVideoArguments
{
    [JsonPropertyName("video_path")]
    public string? VideoPath { get; set; }

    [JsonPropertyName("audio_path")]
    public string? AudioPath { get; set; }

    [JsonPropertyName("output_filename")]
    public string? OutputFilename { get; set; }
}