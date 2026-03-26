using LlmTornado.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GUA_Blazor.Tools.TTS;

public class MergeAudio : AITool<MergeAudioArguments>
{
    public MergeAudio(string sessionId) : base(sessionId) { }

    protected override async Task<string> ExecuteAsync(MergeAudioArguments args)
    {
        var outputPath = Sandbox.Resolve(args.OutputFilename ?? "merged_audio.wav", SessionId);
        var listPath = Sandbox.Resolve("concat_list.txt", SessionId);

        var lines = args.AudioFiles!.Select(f => $"file '{Sandbox.Resolve(f, SessionId).Replace("\\", "/").Replace("'", "\\'")}'");
        await File.WriteAllTextAsync(listPath, string.Join("\n", lines));

        var proc = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-f concat -safe 0 -i \"{listPath}\" -c copy \"{outputPath}\" -y",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false
        })!;

        proc.StandardInput.Close();
        var stderrTask = proc.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            proc.Kill();
            throw new Exception("FFmpeg merge timed out.");
        }

        var stderr = await stderrTask;

        if (!File.Exists(outputPath))
            throw new Exception($"FFmpeg failed. Error log: {stderr}");

        return $"Merged audio saved: {outputPath}";
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "merge_audio",
        "Merges multiple .wav audio files sequentially into one using FFmpeg.",
        new
        {
            type = "object",
            properties = new
            {
                audio_files = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "Ordered list of absolute paths to .wav files to merge"
                },
                output_filename = new { type = "string", description = "Output filename e.g. 'story.wav'" }
            },
            required = new List<string> { "audio_files" }
        });
}

public class MergeAudioArguments
{
    [JsonPropertyName("audio_files")]
    public List<string>? AudioFiles { get; set; }

    [JsonPropertyName("output_filename")]
    public string? OutputFilename { get; set; }
}