using GUA_Blazor.Tools;
using LlmTornado.ChatFunctions;
using LlmTornado.Common;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools;

public class AskUserArgs
{
    [JsonPropertyName("question")]
    [Description("The question to ask the user.")]
    public string Question { get; set; } = string.Empty;
}

public class AskUser : AITool<AskUserArgs>, IAITool
{
    public AskUser() : base("") { }

    protected override async Task<object?> ExecuteAsync(AskUserArgs args)
    {
        return await Task.FromResult($"[USER_PROMPT_REQUIRED]: {args.Question}");
    }

    public override ToolFunction GetToolFunction()
    {
        return new ToolFunction("ask_user", "Temporarily stops the AI loop and waits for a user message by asking a question.", new
        {
            type = "object",
            properties = new
            {
                question = new
                {
                    type = "string",
                    description = "The question or clarification needed from the user."
                }
            },
            required = new[] { "question" }
        });
    }
}
