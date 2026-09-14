using FluentAssertions;
using Xunit;

namespace MyToDo.Tests.Packaging;

public sealed class PackagingContractTests
{
    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void Build_script_runs_restore_full_test_and_single_file_publish_pipeline()
    {
        var buildScript = Path.Combine(RepositoryRoot, "build.ps1");
        File.Exists(buildScript).Should().BeTrue($"build script should exist at {buildScript}");

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"MyToDo-Packaging-{Guid.NewGuid():N}");
        var publishDirectory = Path.Combine(tempDirectory, "publish");
        var logPath = Path.Combine(tempDirectory, "dotnet.log");
        var shimPath = Path.Combine(tempDirectory, "dotnet.cmd");
        Directory.CreateDirectory(tempDirectory);
        Directory.CreateDirectory(publishDirectory);
        File.WriteAllText(Path.Combine(publishDirectory, "stale.txt"), "stale");
        File.WriteAllText(shimPath, "@echo off\r\necho %*>>\"%MYTODO_DOTNET_LOG%\"\r\necho %* | findstr /C:\"publish\" >nul\r\nif not errorlevel 1 (\r\n  if not exist \"%MYTODO_DOTNET_PUBLISH_DIR%\" mkdir \"%MYTODO_DOTNET_PUBLISH_DIR%\"\r\n  echo packaged>\"%MYTODO_DOTNET_PUBLISH_DIR%\\MyToDo.exe\"\r\n)\r\nexit /b 0\r\n");

        var originalDotnet = Environment.GetEnvironmentVariable("MYTODO_DOTNET");
        var originalLog = Environment.GetEnvironmentVariable("MYTODO_DOTNET_LOG");
        var originalPublish = Environment.GetEnvironmentVariable("MYTODO_DOTNET_PUBLISH_DIR");
        try
        {
            Environment.SetEnvironmentVariable("MYTODO_DOTNET", shimPath);
            Environment.SetEnvironmentVariable("MYTODO_DOTNET_LOG", logPath);
            Environment.SetEnvironmentVariable("MYTODO_DOTNET_PUBLISH_DIR", publishDirectory);

            var result = RunPowerShell("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", buildScript, "-OutputPath", publishDirectory);

            result.ExitCode.Should().Be(0, result.Output);
            File.Exists(Path.Combine(publishDirectory, "stale.txt")).Should().BeFalse();
            var executable = new FileInfo(Path.Combine(publishDirectory, "MyToDo.exe"));
            executable.Exists.Should().BeTrue();
            executable.Length.Should().BeGreaterThan(0);

            var invocations = File.ReadAllLines(logPath);
            invocations.Should().ContainSingle(x => x.Contains("restore", StringComparison.Ordinal));
            invocations.Should().ContainSingle(x => x.Contains("test", StringComparison.Ordinal) && !x.Contains("--filter", StringComparison.Ordinal));
            invocations.Should().ContainSingle(x => x.Contains("publish", StringComparison.Ordinal)
                                                   && x.Contains("-r win-x64", StringComparison.Ordinal)
                                                   && x.Contains("-p:RuntimeIdentifier=win-x64", StringComparison.Ordinal)
                                                   && x.Contains("-p:SelfContained=true", StringComparison.Ordinal)
                                                   && x.Contains("-p:PublishSingleFile=true", StringComparison.Ordinal)
                                                   && x.Contains("-p:IncludeNativeLibrariesForSelfExtract=true", StringComparison.Ordinal)
                                                   && x.Contains("-p:DebugSymbols=false", StringComparison.Ordinal)
                                                   && x.Contains("-p:DebugType=None", StringComparison.Ordinal)
                                                   && x.Contains(publishDirectory, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MYTODO_DOTNET", originalDotnet);
            Environment.SetEnvironmentVariable("MYTODO_DOTNET_LOG", originalLog);
            Environment.SetEnvironmentVariable("MYTODO_DOTNET_PUBLISH_DIR", originalPublish);
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void Readme_documents_runtime_controls_storage_soft_delete_and_build_launch()
    {
        var path = Path.Combine(RepositoryRoot, "README.md");
        File.Exists(path).Should().BeTrue($"README should exist at {path}");
        var readme = File.ReadAllText(path);

        readme.Should().Contain("Windows 10").And.Contain("Windows 11");
        readme.Should().Contain("Enter").And.Contain("Escape").And.Contain("History");
        readme.Should().Contain("%LOCALAPPDATA%\\MyToDo\\mytodo.db").And.Contain("%LOCALAPPDATA%\\MyToDo\\settings.json");
        readme.Should().Contain("soft-delete").And.Contain("deleted");
        readme.Should().Contain(".\\build.ps1").And.Contain("artifacts/publish/MyToDo.exe");
        readme.Should().Contain("self-contained").And.Contain(".NET");
    }

    private static (int ExitCode, string Output) RunPowerShell(params string[] arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}
