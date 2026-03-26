using LlmTornado.Common;
using System.IO.Compression;
using System.Security;
using System.Text.Json.Serialization;

namespace GUA_Blazor.Tools.Filesystem;

public class ZipDirectory : AITool<ZipDirectoryArguments>
{
    private static readonly string SandboxPath =
        Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "ai_files_temp"));

    protected override Task<string> ExecuteAsync(ZipDirectoryArguments args)
        => Task.Run(() => Execute(args));

    protected override string Execute(ZipDirectoryArguments args)
    {
        if (string.IsNullOrEmpty(args.SourcePath) || string.IsNullOrEmpty(args.DestinationZipName))
            throw new Exception("sourcePath and destinationZipName are required.");

        var destName = args.DestinationZipName.EndsWith(".zip")
            ? args.DestinationZipName
            : args.DestinationZipName + ".zip";

        var fullSourcePath = Path.GetFullPath(Path.Combine(SandboxPath, args.SourcePath.TrimStart('/', '\\')));
        var fullDestPath = Path.GetFullPath(Path.Combine(SandboxPath, destName));

        if (!fullSourcePath.StartsWith(SandboxPath) || !fullDestPath.StartsWith(SandboxPath))
            throw new SecurityException("Access denied: Path outside sandbox!");

        if (File.Exists(fullDestPath))
            File.Delete(fullDestPath);

        if (Directory.Exists(fullSourcePath))
        {
            ZipFile.CreateFromDirectory(fullSourcePath, fullDestPath);
        }
        else if (File.Exists(fullSourcePath))
        {
            using var zip = ZipFile.Open(fullDestPath, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(fullSourcePath, Path.GetFileName(fullSourcePath));
        }
        else
        {
            throw new Exception($"Source not found: {args.SourcePath}");
        }

        return $"Archive created: {fullDestPath}";
    }

    public override ToolFunction GetToolFunction() => new ToolFunction(
        "zip_directory",
        "Zips a directory or file inside ai_files_temp into a .zip archive.",
        new
        {
            type = "object",
            properties = new
            {
                sourcePath = new { type = "string", description = "Relative path inside ai_files_temp to zip, e.g. 'my_folder'" },
                destinationZipName = new { type = "string", description = "Output zip filename, e.g. 'result.zip'" }
            },
            required = new List<string> { "sourcePath", "destinationZipName" }
        });
}

public class ZipDirectoryArguments
{
    [JsonPropertyName("sourcePath")]
    public string? SourcePath { get; set; }

    [JsonPropertyName("destinationZipName")]
    public string? DestinationZipName { get; set; }
}