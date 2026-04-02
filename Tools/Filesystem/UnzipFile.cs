using LlmTornado.Common;
using System.IO.Compression;
using System.Security;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.Filesystem;

public class UnzipFile : AITool<UnzipFileArguments>
{
    public UnzipFile(string sessionId) : base(sessionId) { }

    protected override Task<object?> ExecuteAsync(UnzipFileArguments args)
        => Task.Run(() => Execute(args));

    protected override object? Execute(UnzipFileArguments args)
    {
        if (string.IsNullOrEmpty(args.ZipPath))
            throw new Exception("zipPath is required.");

        var fullZipPath = Sandbox.Resolve(args.ZipPath, SessionId);
        var destinationDir = string.IsNullOrEmpty(args.DestinationDirectory) 
            ? Path.GetDirectoryName(fullZipPath)! 
            : Sandbox.Resolve(args.DestinationDirectory, SessionId);

        if (!File.Exists(fullZipPath))
            throw new Exception($"Zip file not found: {args.ZipPath}");

        if (!Directory.Exists(destinationDir))
            Directory.CreateDirectory(destinationDir);

        ZipFile.ExtractToDirectory(fullZipPath, destinationDir, overwriteFiles: true);

        return $"Successfully unzipped {args.ZipPath} to {destinationDir}.";
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "unzip_file",
        "Unzips a .zip archive inside the sandbox.",
        new
        {
            type = "object",
            properties = new
            {
                zipPath = new { type = "string", description = "Relative path to the .zip file inside the sandbox." },
                destinationDirectory = new { type = "string", description = "Optional relative path to the destination directory. Defaults to the same directory as the zip file." }
            },
            required = new List<string> { "zipPath" }
        });
}

public class UnzipFileArguments
{
    [JsonPropertyName("zipPath")]
    public string? ZipPath { get; set; }

    [JsonPropertyName("destinationDirectory")]
    public string? DestinationDirectory { get; set; }
}
