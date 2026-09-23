using System.IO;
using LibGit2Sharp;

namespace Template.Scripting;

/// <summary>
/// Discovers and opens the repository used by file-based automation scripts.
/// </summary>
/// <remarks>
/// The constructor requires a real directory path inside a non-bare checkout.
/// It uses <see cref="Repository.Discover(string)"/> to locate the repository metadata from the supplied starting directory and then opens a <see cref="Repository"/> instance that must be disposed by the caller.
/// Opening the repository may read repository metadata and object data from disk, but it does not modify repository state.
/// </remarks>
public sealed class GitRepositoryContext : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitRepositoryContext"/> class.
    /// </summary>
    /// <param name="startingDirectory">Directory used as the discovery root for locating the repository.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="startingDirectory"/> is empty or cannot be normalized to a valid path.</exception>
    /// <exception cref="ScriptConfigurationException">Thrown when <paramref name="startingDirectory"/> does not exist or resolves to a bare repository checkout.</exception>
    /// <exception cref="GitScriptException">Thrown when repository discovery or repository opening fails.</exception>
    public GitRepositoryContext(string startingDirectory)
    {
        if (string.IsNullOrWhiteSpace(startingDirectory))
        {
            throw new ArgumentException("Starting directory cannot be empty.", nameof(startingDirectory));
        }

        string absoluteStartingDirectory;

        try
        {
            absoluteStartingDirectory = Path.GetFullPath(startingDirectory);
        }
        catch (Exception exception) when (exception is NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException($"Starting directory '{startingDirectory}' is not a valid path.", nameof(startingDirectory), exception);
        }

        if (!Directory.Exists(absoluteStartingDirectory))
        {
            throw new ScriptConfigurationException($"Starting directory '{absoluteStartingDirectory}' does not exist.");
        }

        string? discoveredRepositoryPath;

        try
        {
            discoveredRepositoryPath = Repository.Discover(absoluteStartingDirectory);
        }
        catch (LibGit2SharpException exception)
        {
            throw new GitScriptException($"Failed to discover a repository from '{absoluteStartingDirectory}'.", exception);
        }

        if (string.IsNullOrWhiteSpace(discoveredRepositoryPath))
        {
            throw new ScriptConfigurationException($"No repository was found from starting directory '{absoluteStartingDirectory}'. Run this script from inside the repository checkout.");
        }

        try
        {
            Repository = new Repository(discoveredRepositoryPath);
        }
        catch (LibGit2SharpException exception)
        {
            throw new GitScriptException($"Failed to open the repository discovered at '{discoveredRepositoryPath}' from '{absoluteStartingDirectory}'.", exception);
        }

        RepositoryPath = NormalizeAbsolutePath(Repository.Info.Path);

        if (Repository.Info.IsBare || string.IsNullOrWhiteSpace(Repository.Info.WorkingDirectory))
        {
            Repository.Dispose();
            throw new ScriptConfigurationException($"Repository '{RepositoryPath}' does not have a working directory. Repository automation scripts require a non-bare checkout.");
        }

        RepositoryRootPath = NormalizeAbsolutePath(Repository.Info.WorkingDirectory);
    }

    /// <summary>
    /// Gets the opened repository instance.
    /// </summary>
    /// <value>
    /// The <see cref="LibGit2Sharp.Repository"/> discovered from the starting directory.
    /// </value>
    /// <remarks>
    /// The returned repository remains valid until <see cref="Dispose"/> is called on this context.
    /// </remarks>
    public Repository Repository { get; }

    /// <summary>
    /// Gets the normalized repository metadata path.
    /// </summary>
    /// <value>
    /// The absolute path to the repository metadata directory for a non-bare checkout.
    /// </value>
    public string RepositoryPath { get; }

    /// <summary>
    /// Gets the normalized repository root working-directory path.
    /// </summary>
    /// <value>
    /// The absolute path to the repository checkout root without a trailing directory separator.
    /// </value>
    public string RepositoryRootPath { get; }

    /// <summary>
    /// Releases the opened repository handle.
    /// </summary>
    /// <remarks>
    /// Disposing this context closes the underlying <see cref="Repository"/> instance and releases any unmanaged resources held by <c>LibGit2Sharp</c>.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Repository.Dispose();
        _disposed = true;
    }

    private static string NormalizeAbsolutePath(string path)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
