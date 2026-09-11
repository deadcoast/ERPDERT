using System.Security.Cryptography;
using System.Text;
using Dert.Cli.Results;
using Dert.Cli.Repairs;
using Dert.Core;
using Xunit;

namespace Dert.Tests;

public class ObservationStoreTests
{
    [Fact]
    public void Record_round_trip_preserves_raw_punctuation_multiline_evidence_and_failed_outcome()
    {
        using var source = new ScratchDocument("STERT ; update ;; codex\r\nƒin.");
        var record = source.Path + ".json";
        try
        {
            var observation = Observation(source.Path, File.ReadAllText(source.Path));
            ObservationStore.WriteNew(observation, record);
            var read = ObservationStore.Read(record);
            Assert.Equal(observation.Evidence.ToArray(), read.Evidence.ToArray());
            Assert.Equal(observation.Origin, read.Origin); Assert.Equal(observation.ObservedAt, read.ObservedAt);
            Assert.Equal(ObservationOutcome.Failed, read.Outcome);
        }
        finally { if (File.Exists(record)) File.Delete(record); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_or_mismatched_writes_never_claim_success(bool partial)
    {
        using var source = new ScratchDocument("STERT ; update ;; codex\nƒin."); var record = source.Path + ".json";
        try
        {
            var error = Assert.Throws<IOException>(() => ObservationStore.WriteNew(Observation(source.Path, File.ReadAllText(source.Path)), record, new BrokenWriter(partial)));
            Assert.Contains("incomplete", error.Message); Assert.True(File.Exists(record));
            Assert.Throws<IOException>(() => ObservationStore.Read(record));
        }
        finally { if (File.Exists(record)) File.Delete(record); }
    }

    [Theory]
    [InlineData("{\"Format\":1,\"Format\":1,\"Observation\":null}")]
    [InlineData("{\"Format\":2,\"Observation\":null}")]
    [InlineData("{\"Format\":1,\"Observation\":{}}")]
    [InlineData("{\"Format\":1,\"Observation\":null,\"Other\":true}")]
    public void Incomplete_ambiguous_and_unrecognized_records_are_explained(string text)
    {
        using var source = new ScratchDocument(text);
        var error = Assert.Throws<IOException>(() => ObservationStore.Read(source.Path));
        Assert.Contains("complete supported local result", error.Message);
    }

    [Fact]
    public void Invalid_utf8_and_oversized_records_are_not_read_as_observations()
    {
        using var source = new ScratchDocument("text");
        File.WriteAllBytes(source.Path, [0xff, 0xff]);
        Assert.Throws<IOException>(() => ObservationStore.Read(source.Path));
        using (var stream = File.OpenWrite(source.Path)) stream.SetLength(8 * 1024 * 1024 + 1);
        Assert.Throws<IOException>(() => ObservationStore.Read(source.Path));
    }

    [Fact]
    public void Alternate_stream_and_source_destinations_are_refused_even_if_source_was_deleted()
    {
        using var source = new ScratchDocument("STERT ; update ;; codex\nƒin.");
        var observation = Observation(source.Path, File.ReadAllText(source.Path));
        File.Delete(source.Path);
        Assert.Throws<IOException>(() => ObservationStore.WriteNew(observation, source.Path));
        if (OperatingSystem.IsWindows()) Assert.Throws<IOException>(() => ObservationStore.WriteNew(observation, source.Path + ":record.json"));
    }

    internal static ResultObservation Observation(string path, string source) => new(Guid.NewGuid().ToString("N"),
        new DateTimeOffset(2026, 9, 9, 12, 30, 0, TimeSpan.FromHours(-7)), "test observation", "codex", ObservationOutcome.Failed,
        "Verification failed in the test fixture.", "Fixture only; no operation ran.",
        new(Path.GetFullPath(path), 1, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))), source),
        [new("evidence", "C:\\path\\\"quotes\" // ; ;; ,\r\nSTERT ; update ;; codex\nƒin.\0")]);

    private sealed class BrokenWriter(bool partial) : IRepairWriter
    {
        public void Rewrite(FileStream stream, byte[] bytes)
        { stream.WriteByte((byte)'{'); if (partial) throw new IOException("fixture interruption"); }
    }
}
