namespace DacpacEntityGenerator.Services;

/// <summary>
/// Resolves the workspace-rooted input and output directories, handling both
/// development (project directory) and published/bin execution contexts.
/// </summary>
public class PathResolverService
{
    private const string ProjectFolderName = "_DacpacEntityGenerator";

    /// <summary>
    /// Returns the workspace root directory.  When the process is running from
    /// inside a <c>bin</c> folder the method walks up to the solution root.
    /// </summary>
    public string ResolveWorkspaceRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();

        if (currentDir.Contains("bin"))
            return Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", ".."));

        return currentDir;
    }

    /// <summary>
    /// Returns the absolute paths for the generator's input and output directories.
    /// </summary>
    public (string InputDirectory, string OutputDirectory) ResolveDirectories()
    {
        var workspaceRoot  = ResolveWorkspaceRoot();
        var projectDir     = Path.Combine(workspaceRoot, "src", ProjectFolderName);
        var inputDirectory = Path.Combine(projectDir, "_input");
        var outputDirectory = Path.Combine(projectDir, "_output");
        return (inputDirectory, outputDirectory);
    }
}
