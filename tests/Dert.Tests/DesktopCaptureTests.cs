using System.Diagnostics;
using Dert.Cli.Authoring;
using Xunit;

namespace Dert.Tests;

public sealed class DesktopCaptureTests
{
    [Fact]
    public async Task Cli_exit_retains_both_utf8_streams_without_waiting_for_or_stopping_a_detached_window()
    {
        var root = Path.Combine(Path.GetTempPath(), "dert-capture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var eventName = "Local\\DertCapture" + Guid.NewGuid().ToString("N");
        using var releaseChild = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);
        Task<DesktopCommandResult>? run = null;
        Process? child = null;
        try
        {
            // This owned fixture has a CLI parent and a detached child holding inherited handles,
            // just as a GUI launched with UseShellExecute=false can outlive its launching CLI.
            File.WriteAllText(Path.Combine(root, "fixture.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
                  <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
                  <AssemblyName>DERT</AssemblyName>
                </PropertyGroup></Project>
                """);
            File.WriteAllText(Path.Combine(root, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
            File.WriteAllText(Path.Combine(root, "Program.cs"), """
                using System;
                using System.Diagnostics;
                using System.IO;
                using System.Text;
                using System.Threading;
                if (args[0] == "child")
                {
                    using var release = EventWaitHandle.OpenExisting(args[2]);
                    File.WriteAllText(args[1], Environment.ProcessId.ToString());
                    release.WaitOne(TimeSpan.FromSeconds(30));
                    return 0;
                }
                Console.OutputEncoding = new UTF8Encoding(false);
                Console.WriteLine("parent output café");
                Console.Error.WriteLine("parent error 日本語");
                var start = new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false, CreateNoWindow = true };
                start.ArgumentList.Add("child");
                start.ArgumentList.Add(args[1]);
                start.ArgumentList.Add(args[2]);
                using var child = Process.Start(start);
                return 7;
                """);
            var build = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var argument in new[] { "build", "fixture.csproj", "--nologo", "--verbosity", "quiet", "--output", "built" })
                build.ArgumentList.Add(argument);
            using (var compiler = Process.Start(build)!)
            {
                var stdout = compiler.StandardOutput.ReadToEndAsync();
                var stderr = compiler.StandardError.ReadToEndAsync();
                await compiler.WaitForExitAsync();
                Assert.True(compiler.ExitCode == 0, await stdout + await stderr);
            }
            var marker = Path.Combine(root, "child.pid");
            run = DesktopCommandRunner.RunAsync(Path.Combine(root, "built", "DERT.exe"), ["-help", marker, eventName]);
            var result = await run.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(7, result.ExitCode);
            Assert.False(result.Cancelled);
            Assert.Contains("parent output café", result.Output);
            Assert.Contains("parent error 日本語", result.Output);
            child = Process.GetProcessById(int.Parse(File.ReadAllText(marker)));
            Assert.False(child.HasExited);
        }
        finally
        {
            releaseChild.Set();
            if (run is not null) await run.WaitAsync(TimeSpan.FromSeconds(10));
            if (child is not null) { await child.WaitForExitAsync(); child.Dispose(); }
            // A failed assertion can precede reading the PID. Wait for that owned child as well.
            var marker = Path.Combine(root, "child.pid");
            if (File.Exists(marker))
                try
                {
                    using var remaining = Process.GetProcessById(int.Parse(File.ReadAllText(marker)));
                    await remaining.WaitForExitAsync();
                }
                catch (ArgumentException) { }
            Directory.Delete(root, true);
        }
    }
}
