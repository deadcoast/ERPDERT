namespace Dert.Cli.Discovery;

internal static class NpmPowerShellShim
{
    // Complete cmd-shim Node template, with no shebang arguments. Custom bodies
    // and unfamiliar generator versions remain unknown rather than guessed.
    internal static bool IsSupported(string text)
    {
        if (text.StartsWith('\uFEFF')) text = text[1..];
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') == Template;
    }
    internal static string Read(string path)
    {
        const int limit = 256 * 1024;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bytes = new byte[limit + 1];
        var count = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
        if (count > limit) throw new IOException("The PowerShell launcher exceeds the 256 KiB inspection limit.");
        return new System.Text.UTF8Encoding(false, true).GetString(bytes, 0, count);
    }
    private const string Template = """
#!/usr/bin/env pwsh
$basedir=Split-Path $MyInvocation.MyCommand.Definition -Parent

$exe=""
if ($PSVersionTable.PSVersion -lt "6.0" -or $IsWindows) {
  # Fix case when both the Windows and Linux builds of Node
  # are installed in the same directory
  $exe=".exe"
}
$ret=0
if (Test-Path "$basedir/node$exe") {
  # Support pipeline input
  if ($MyInvocation.ExpectingInput) {
    $input | & "$basedir/node$exe"  "$basedir/node_modules/@openai/codex/bin/codex.js" $args
  } else {
    & "$basedir/node$exe"  "$basedir/node_modules/@openai/codex/bin/codex.js" $args
  }
  $ret=$LASTEXITCODE
} else {
  # Support pipeline input
  if ($MyInvocation.ExpectingInput) {
    $input | & "node$exe"  "$basedir/node_modules/@openai/codex/bin/codex.js" $args
  } else {
    & "node$exe"  "$basedir/node_modules/@openai/codex/bin/codex.js" $args
  }
  $ret=$LASTEXITCODE
}
exit $ret
""";
}
