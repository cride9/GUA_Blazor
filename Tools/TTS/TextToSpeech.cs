using GUA_Blazor.Service;
using LlmTornado.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GUA_Blazor.Tools.TTS;

public class TextToSpeech : AITool<GenerateScriptArguments>
{
    private static readonly KokoroService _kokoro = new();

    public TextToSpeech(string sessionId) : base(sessionId) { }

    protected override async Task<object?> ExecuteAsync(GenerateScriptArguments args)
    {
        if (args.Lines == null || !args.Lines.Any())
            throw new Exception("No script lines provided.");

        var outputFiles = new List<string>();

        for (int i = 0; i < args.Lines.Count; i++)
        {
            var line = args.Lines[i];
            var safeCharacter = string.Join("_", (line.Character ?? "Unknown").Split(Path.GetInvalidFileNameChars()));
            var filename = $"line_{i:D3}_{safeCharacter}.wav";

            var path = Sandbox.Resolve(filename, SessionId);
            await _kokoro.SynthesizeAsync(line.Text ?? "", line.Voice ?? "af_heart", path);
            outputFiles.Add(path);
        }

        var result = outputFiles.Select((p, i) =>
            $"{i}: [{args.Lines[i].Character} / {args.Lines[i].Voice}] → {p}");

        return $"Generated {outputFiles.Count} audio files:\n{string.Join("\n", result)}";
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "text_to_speech",
        "Converts a script with multiple characters into separate audio files using Kokoro TTS. Each line gets its own .wav file.",
        new
        {
            type = "object",
            properties = new
            {
                lines = new
                {
                    type = "array",
                    description = "Script lines with character, voice and text",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            character = new { type = "string", description = "Character name e.g. 'narrator', 'hero'" },
                            voice = new { type = "string", description = "Kokoro voice: af_heart, af_nova, am_fenrir, am_adam, bf_emma, bm_george" },
                            text = new { type = "string", description = "The text to speak" }
                        }
                    }
                }
            },
            required = new List<string> { "lines" }
        });
}

public class GenerateScriptArguments
{
    [JsonPropertyName("lines")]
    public List<ScriptLine>? Lines { get; set; }
}

public class ScriptLine
{
    [JsonPropertyName("character")]
    public string? Character { get; set; }

    [JsonPropertyName("voice")]
    public string? Voice { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}