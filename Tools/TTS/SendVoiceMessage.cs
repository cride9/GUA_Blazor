using LlmTornado.Common;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.TTS;

public class SendVoiceMessage : AITool<SendVoiceMessageArguments>
{
    public SendVoiceMessage(string sessionId) : base(sessionId) { }

    protected override Task<object?> ExecuteAsync(SendVoiceMessageArguments args)
    {
        var path = Sandbox.Resolve(args.AudioPath!, SessionId);
        if (!File.Exists(path))
            throw new Exception($"Audio file not found: {path}");

        // Return a special marker that the UI can intercept
        return Task.FromResult<object?>($"[VOICE_MESSAGE_SENT] {path}");
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "send_voice_message",
        "Sends a generated .wav audio file to the user as a playable voice message in the chat.",
        new
        {
            type = "object",
            properties = new
            {
                audio_path = new { type = "string", description = "Absolute path to the .wav file to send." }
            },
            required = new List<string> { "audio_path" }
        });
}

public class SendVoiceMessageArguments
{
    [JsonPropertyName("audio_path")]
    public string? AudioPath { get; set; }
}
