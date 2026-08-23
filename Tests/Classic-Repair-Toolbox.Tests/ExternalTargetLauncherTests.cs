using System.Reflection;
using CRT;

namespace ClassicRepairToolbox.Tests;

// Characterisation tests for ExternalTargetLauncher - the only sanctioned way the UI opens
// an external link or a local file. Board data is community-contributed, so this is a real
// trust boundary: it must accept http/https/mailto and files inside the data root, and
// reject everything else.
//
// IMPORTANT - why this file uses reflection:
// TryOpen's SUCCESS path calls Process.Start(UseShellExecute = true), which would really
// launch a browser or open a file on whatever machine runs these tests. So the accept cases
// are exercised against the two private predicates that decide the outcome, and only the
// REJECT cases go through the public TryOpen (they return false without starting anything).
//
// If these reflection lookups ever fail, the fix is to make the predicates `internal` and
// add [InternalsVisibleTo] to the app project - not to call TryOpen with a valid target.
public sealed class ExternalTargetLauncherTests : IDisposable
{
    private readonly string thisDataRoot;
    private readonly string thisOutsideRoot;

    public ExternalTargetLauncherTests()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "crt-tests-" + Guid.NewGuid().ToString("N"));
        this.thisDataRoot = Path.GetFullPath(Path.Combine(baseDir, "Data"));
        this.thisOutsideRoot = Path.GetFullPath(Path.Combine(baseDir, "Outside"));

        Directory.CreateDirectory(Path.Combine(this.thisDataRoot, "Commodore", "C64"));
        Directory.CreateDirectory(this.thisOutsideRoot);

        File.WriteAllText(Path.Combine(this.thisDataRoot, "Commodore", "C64", "notes.txt"), "inside");
        File.WriteAllText(Path.Combine(this.thisOutsideRoot, "secrets.txt"), "outside");
    }

    public void Dispose()
    {
        try
        {
            string baseDir = Path.GetDirectoryName(this.thisDataRoot)!;
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, recursive: true);
            }
        }
        catch
        {
            // A leftover temp folder is not worth failing a test run over.
        }
    }

    // ------------------------------------------------------------- private predicates

    private static MethodInfo GetPrivateMethod(string name)
    {
        MethodInfo? method = typeof(ExternalTargetLauncher)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

        Assert.True(method is not null,
            $"ExternalTargetLauncher.{name} not found - if it was renamed, update these tests.");

        return method!;
    }

    private static bool TryResolveDataRootScopedFilePath(string target, string dataRoot, out string localPath)
    {
        object?[] args = { target, dataRoot, null };
        bool result = (bool)GetPrivateMethod("TryResolveDataRootScopedFilePath").Invoke(null, args)!;
        localPath = (string)(args[2] ?? string.Empty);
        return result;
    }

    private static bool TryCreateAllowedUri(string target, out Uri? uri)
    {
        object?[] args = { target, null };
        bool result = (bool)GetPrivateMethod("TryCreateAllowedUri").Invoke(null, args)!;
        uri = args[1] as Uri;
        return result;
    }

    // ----------------------------------------------------------------- allowed schemes

    [Theory]
    [InlineData("http://classic-repair-toolbox.dk")]
    [InlineData("https://classic-repair-toolbox.dk/data")]
    [InlineData("HTTPS://UPPER.CASE.SCHEME")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("  https://leading-and-trailing-space.example  ")]
    public void Allowed_uri_schemes_are_accepted(string target)
    {
        Assert.True(TryCreateAllowedUri(target, out Uri? uri));
        Assert.NotNull(uri);
    }

    [Theory]
    [InlineData("ftp://example.com/file.zip")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("ms-msdt:/id")]
    [InlineData("not a uri at all")]
    [InlineData("/relative/path.txt")]
    public void Other_uri_schemes_are_not_accepted_as_uris(string target)
    {
        Assert.False(TryCreateAllowedUri(target, out _));
    }

    // ------------------------------------------------------------- data-root scoping

    [Fact]
    public void A_relative_path_resolving_inside_the_data_root_is_accepted()
    {
        Assert.True(TryResolveDataRootScopedFilePath(
            "Commodore/C64/notes.txt", this.thisDataRoot, out string localPath));

        Assert.Equal(
            Path.Combine(this.thisDataRoot, "Commodore", "C64", "notes.txt"),
            localPath);
    }

    [Fact]
    public void Forward_slashes_are_accepted_on_every_platform()
    {
        // Board data is authored on all three OSes, so separators are normalised.
        Assert.True(TryResolveDataRootScopedFilePath(
            "Commodore/C64/notes.txt", this.thisDataRoot, out _));
    }

    [Fact]
    public void An_absolute_path_inside_the_data_root_is_accepted()
    {
        string absolute = Path.Combine(this.thisDataRoot, "Commodore", "C64", "notes.txt");

        Assert.True(TryResolveDataRootScopedFilePath(absolute, this.thisDataRoot, out string localPath));
        Assert.Equal(absolute, localPath);
    }

    [Fact]
    public void A_directory_traversal_escaping_the_data_root_is_rejected()
    {
        // The attack this boundary exists to stop.
        Assert.False(TryResolveDataRootScopedFilePath(
            "../Outside/secrets.txt", this.thisDataRoot, out _));
    }

    [Fact]
    public void A_deep_directory_traversal_escaping_the_data_root_is_rejected()
    {
        Assert.False(TryResolveDataRootScopedFilePath(
            "Commodore/C64/../../../Outside/secrets.txt", this.thisDataRoot, out _));
    }

    [Fact]
    public void An_absolute_path_outside_the_data_root_is_rejected()
    {
        string absolute = Path.Combine(this.thisOutsideRoot, "secrets.txt");

        Assert.False(TryResolveDataRootScopedFilePath(absolute, this.thisDataRoot, out _));
    }

    [Fact]
    public void The_data_root_itself_is_rejected()
    {
        Assert.False(TryResolveDataRootScopedFilePath(this.thisDataRoot, this.thisDataRoot, out _));
    }

    [Fact]
    public void A_sibling_directory_with_the_data_root_as_a_name_prefix_is_rejected()
    {
        // Guards the trailing-separator check: "<root>Evil" must not pass a StartsWith test
        // against "<root>". This is what AppendDirectorySeparator is for.
        string siblingWithPrefix = this.thisDataRoot + "Evil";
        Directory.CreateDirectory(siblingWithPrefix);
        string planted = Path.Combine(siblingWithPrefix, "secrets.txt");
        File.WriteAllText(planted, "outside");

        Assert.False(TryResolveDataRootScopedFilePath(planted, this.thisDataRoot, out _));
    }

    [Fact]
    public void A_path_inside_the_data_root_that_does_not_exist_is_rejected()
    {
        Assert.False(TryResolveDataRootScopedFilePath(
            "Commodore/C64/missing.txt", this.thisDataRoot, out _));
    }

    [Fact]
    public void A_directory_inside_the_data_root_is_rejected_because_it_is_not_a_file()
    {
        Assert.False(TryResolveDataRootScopedFilePath(
            "Commodore/C64", this.thisDataRoot, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_target_is_rejected(string target)
    {
        Assert.False(TryResolveDataRootScopedFilePath(target, this.thisDataRoot, out _));
    }

    [Fact]
    public void An_empty_data_root_rejects_everything()
    {
        Assert.False(TryResolveDataRootScopedFilePath("notes.txt", "", out _));
    }

    // ------------------------------------------------------ end-to-end (reject only)

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryOpen_rejects_an_empty_target(string target)
    {
        Assert.False(ExternalTargetLauncher.TryOpen(target, this.thisDataRoot));
    }

    [Theory]
    [InlineData("../Outside/secrets.txt")]
    [InlineData("Commodore/C64/../../../Outside/secrets.txt")]
    [InlineData("Commodore/C64/missing.txt")]
    public void TryOpen_rejects_a_target_outside_or_absent_from_the_data_root(string target)
    {
        // Safe to call end-to-end: every one of these returns false before Process.Start.
        Assert.False(ExternalTargetLauncher.TryOpen(target, this.thisDataRoot));
    }

    [Fact]
    public void TryOpen_rejects_an_absolute_path_outside_the_data_root()
    {
        string absolute = Path.Combine(this.thisOutsideRoot, "secrets.txt");
        Assert.False(ExternalTargetLauncher.TryOpen(absolute, this.thisDataRoot));
    }
}
