namespace InTheLoop.Core.Storage;

public sealed class StoreOptions
{
    public const string DataDirectoryVariable = "INTHELOOP_DATA_DIR";

    public string DataDirectory { get; set; } = DefaultDataDirectory();

    public string ReviewsDirectory => Path.Combine(DataDirectory, "reviews");

    public string ProjectsDirectory => Path.Combine(DataDirectory, "projects");

    public static string DefaultDataDirectory()
    {
        var overridden = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        return !string.IsNullOrWhiteSpace(overridden)
            ? Path.GetFullPath(overridden)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InTheLoop");
    }
}
