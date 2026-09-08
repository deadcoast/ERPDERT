namespace Dert.Tests;

internal static class SyntaxFixtures
{
    // These are test-owned inputs, not live extracts from project documentation.
    internal static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}
