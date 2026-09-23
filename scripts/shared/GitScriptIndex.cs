using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace Template.Scripting;

/// <summary>
/// Provides staged-file and index operations used by repository automation scripts.
/// </summary>
/// <remarks>
/// This helper reads repository state from the index and staged object database so scripts can inspect and update tracked shell-script entries without invoking an external command-line client.
/// Returned script paths use repository-relative forward slashes.
/// </remarks>
public sealed class GitScriptIndex
{
    private readonly GitRepositoryContext _repositoryContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitScriptIndex"/> class.
    /// </summary>
    /// <param name="repositoryContext">Opened repository context used for index and staged-content access.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="repositoryContext"/> is <see langword="null"/>.</exception>
    public GitScriptIndex(GitRepositoryContext repositoryContext)
    {
        _repositoryContext = repositoryContext ?? throw new ArgumentNullException(nameof(repositoryContext));
    }

    /// <summary>
    /// Lists staged shell scripts whose index state represents added, modified, or renamed content.
    /// </summary>
    /// <returns>
    /// Repository-relative forward-slash paths for staged <c>*.sh</c> files whose staged state should be prepared before commit.
    /// </returns>
    /// <remarks>
    /// The result is derived from staged status flags rather than working-tree content.
    /// Copy detection is not exposed as a separate status flag by <c>LibGit2Sharp</c>, so copied entries appear through the staged path that exists in the index.
    /// </remarks>
    /// <exception cref="GitScriptException">Thrown when repository status cannot be read.</exception>
    public IReadOnlyList<string> ListStagedShellScripts()
    {
        try
        {
            return _repositoryContext.Repository
                .RetrieveStatus(new StatusOptions())
                .Where(entry => IsShellScriptPath(entry.FilePath) && HasRelevantStagedState(entry.State))
                .Select(entry => NormalizeRepositoryRelativePath(entry.FilePath))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }
        catch (LibGit2SharpException exception)
        {
            throw new GitScriptException($"Failed to read staged shell scripts from repository '{_repositoryContext.RepositoryRootPath}'.", exception);
        }
    }

    /// <summary>
    /// Lists every tracked shell script present in the index.
    /// </summary>
    /// <returns>
    /// Repository-relative forward-slash paths for all tracked <c>*.sh</c> files in the repository index.
    /// </returns>
    /// <remarks>
    /// The result comes from the current index entries and excludes conflict stages other than the staged entry.
    /// </remarks>
    /// <exception cref="GitScriptException">Thrown when the repository index cannot be read.</exception>
    public IReadOnlyList<string> ListTrackedShellScripts()
    {
        try
        {
            return _repositoryContext.Repository.Index
                .Where(entry => entry.StageLevel == StageLevel.Staged && IsShellScriptPath(entry.Path))
                .Select(entry => NormalizeRepositoryRelativePath(entry.Path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }
        catch (LibGit2SharpException exception)
        {
            throw new GitScriptException($"Failed to enumerate tracked shell scripts from repository '{_repositoryContext.RepositoryRootPath}'.", exception);
        }
    }

    /// <summary>
    /// Lists staged shell scripts whose staged blob content contains CRLF or mixed line endings.
    /// </summary>
    /// <returns>
    /// Repository-relative forward-slash paths for staged <c>*.sh</c> files whose staged content is not LF-only.
    /// </returns>
    /// <remarks>
    /// This method reads staged blob content from the index instead of reading files from the working directory.
    /// Files without newline characters are treated as LF-compatible and are not returned.
    /// </remarks>
    /// <exception cref="GitScriptException">Thrown when staged content cannot be read from the repository object database.</exception>
    public IReadOnlyList<string> ListStagedShellScriptsWithNonLfLineEndings()
    {
        try
        {
            var index = _repositoryContext.Repository.Index;
            var nonLfPaths = new List<string>();

            foreach (var path in ListStagedShellScripts())
            {
                var entry = GetRequiredIndexEntry(index, path);
                var blob = GetRequiredBlob(entry, path);
                var lineEndingKind = DetectLineEndingKind(blob, path);

                if (lineEndingKind is LineEndingKind.Crlf or LineEndingKind.Mixed)
                {
                    nonLfPaths.Add(path);
                }
            }

            return nonLfPaths
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }
        catch (LibGit2SharpException exception)
        {
            throw new GitScriptException($"Failed to inspect staged shell-script content in repository '{_repositoryContext.RepositoryRootPath}'.", exception);
        }
        catch (IOException exception)
        {
            throw new GitScriptException($"Failed to read staged shell-script content in repository '{_repositoryContext.RepositoryRootPath}'.", exception);
        }
    }

    /// <summary>
    /// Sets the executable mode bit in the index for the supplied repository-relative paths.
    /// </summary>
    /// <param name="paths">Repository-relative file paths to update.</param>
    /// <remarks>
    /// Paths are validated, normalized to forward slashes, resolved against the staged index entry, and written back with <see cref="Mode.ExecutableFile"/>.
    /// The method updates only the index entry and does not change the working-directory file mode directly.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="paths"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when any supplied path is empty, absolute, or not normalized as a repository-relative path.</exception>
    /// <exception cref="GitScriptException">Thrown when an index entry cannot be read, cannot be converted to a blob, or cannot be written back to the repository index.</exception>
    public void SetExecutableBit(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var normalizedPaths = paths
            .Select(NormalizeAndValidateInputPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (normalizedPaths.Length == 0)
        {
            return;
        }

        try
        {
            var index = _repositoryContext.Repository.Index;
            var updatedAnyEntry = false;

            foreach (var path in normalizedPaths)
            {
                var entry = GetRequiredIndexEntry(index, path);

                if (entry.Mode == Mode.ExecutableFile)
                {
                    continue;
                }

                if (!IsFileMode(entry.Mode))
                {
                    throw new GitScriptException($"Path '{path}' in repository '{_repositoryContext.RepositoryRootPath}' has unsupported index mode '{entry.Mode}'.");
                }

                var blob = GetRequiredBlob(entry, path);
                index.Add(blob, path, Mode.ExecutableFile);
                updatedAnyEntry = true;
            }

            if (updatedAnyEntry)
            {
                index.Write();
            }
        }
        catch (LibGit2SharpException exception)
        {
            throw new GitScriptException($"Failed to update executable index entries in repository '{_repositoryContext.RepositoryRootPath}'.", exception);
        }
    }

    private Blob GetRequiredBlob(IndexEntry entry, string path)
    {
        var blob = _repositoryContext.Repository.Lookup(entry.Id, ObjectType.Blob) as Blob;

        return blob
            ?? throw new GitScriptException($"Path '{path}' in repository '{_repositoryContext.RepositoryRootPath}' does not resolve to a staged blob.");
    }

    private static LineEndingKind DetectLineEndingKind(Blob blob, string path)
    {
        using var contentStream = blob.GetContentStream();

        var previousWasCarriageReturn = false;
        var sawLineFeed = false;
        var sawCarriageReturnLineFeed = false;
        var sawStandaloneCarriageReturn = false;

        while (true)
        {
            var nextByte = contentStream.ReadByte();
            if (nextByte < 0)
            {
                break;
            }

            if (previousWasCarriageReturn)
            {
                if (nextByte == '\n')
                {
                    sawCarriageReturnLineFeed = true;
                    previousWasCarriageReturn = false;
                    continue;
                }

                sawStandaloneCarriageReturn = true;
                previousWasCarriageReturn = false;
            }

            if (nextByte == '\r')
            {
                previousWasCarriageReturn = true;
                continue;
            }

            if (nextByte == '\n')
            {
                sawLineFeed = true;
            }
        }

        if (previousWasCarriageReturn)
        {
            sawStandaloneCarriageReturn = true;
        }

        if (sawStandaloneCarriageReturn || (sawCarriageReturnLineFeed && sawLineFeed))
        {
            return LineEndingKind.Mixed;
        }

        if (sawCarriageReturnLineFeed)
        {
            return LineEndingKind.Crlf;
        }

        return LineEndingKind.Lf;
    }

    private static IndexEntry GetRequiredIndexEntry(LibGit2Sharp.Index index, string path)
    {
        var entry = index
            .FirstOrDefault(candidate => candidate.StageLevel == StageLevel.Staged && string.Equals(candidate.Path, path, StringComparison.Ordinal));

        return entry
            ?? throw new GitScriptException($"Path '{path}' is not present as a staged index entry.");
    }

    private static bool HasRelevantStagedState(FileStatus state)
        => state.HasFlag(FileStatus.NewInIndex)
           || state.HasFlag(FileStatus.ModifiedInIndex)
           || state.HasFlag(FileStatus.RenamedInIndex);

    private static bool IsFileMode(Mode mode)
        => mode is Mode.NonExecutableFile or Mode.NonExecutableGroupWritableFile or Mode.ExecutableFile;

    private static bool IsShellScriptPath(string path)
        => path.EndsWith(".sh", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeAndValidateInputPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Input path cannot be null, empty, or whitespace.", nameof(path));
        }

        if (Path.IsPathRooted(path))
        {
            throw new ArgumentException($"Input path '{path}' must be repository-relative.", nameof(path));
        }

        var normalizedPath = NormalizeRepositoryRelativePath(path);
        var segments = normalizedPath.Split('/', StringSplitOptions.None);

        if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            throw new ArgumentException($"Input path '{path}' must be a normalized repository-relative file path.", nameof(path));
        }

        return normalizedPath;
    }

    private static string NormalizeRepositoryRelativePath(string path)
        => path.Replace('\\', '/');

    private enum LineEndingKind
    {
        Lf,
        Crlf,
        Mixed
    }
}
