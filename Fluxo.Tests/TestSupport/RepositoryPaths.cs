namespace Fluxo.Tests.TestSupport;

internal static class RepositoryPaths
{
    public static string Root
    {
        get
        {
            var directory = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(directory)
                   && !System.IO.File.Exists(Path.Combine(directory, "Fluxo.slnx")))
            {
                directory = System.IO.Directory.GetParent(directory)?.FullName;
            }

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new DirectoryNotFoundException("Could not find repository root.");
            }

            return directory;
        }
    }

    public static string File(params string[] relativeSegments) =>
        Path.Combine(Root, Path.Combine(relativeSegments));
}
