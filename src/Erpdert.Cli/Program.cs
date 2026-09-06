using System.Text;
using Erpdert.Cli;
using Spectre.Console;

Console.OutputEncoding = new UTF8Encoding(false);
var executable = Environment.ProcessPath;
string[]? invocation = executable is null ? null :
    Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
        ? [executable, typeof(CliApplication).Assembly.Location] : [executable];
return CliApplication.Run(args, AnsiConsole.Console, invocation: invocation);
