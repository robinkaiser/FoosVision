// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Diagnostics;
using FoosVision.Ports.Media;

namespace FoosVision.Adapters.Recorder.UnitTests.Diagnostics;

public class VideoDumpFileWriterTests
{
    [Fact]
    public async Task WriteAsync_writes_tmp_file_then_moves_to_final_file()
    {
        FakeVideoDumpFileStore fileStore = new();
        FakeEncodedReplaySegmentFileWriter segmentWriter = new(fileStore);
        VideoDumpFileWriter testee = CreateWriter(fileStore, segmentWriter);
        VideoDumpRequest request = CreateRequest("recorder-game.mp4");

        await testee.WriteAsync(request, CancellationToken.None);

        Assert.Contains("videos", fileStore.CreatedDirectories);
        Assert.Equal("videos\\recorder-game.mp4.tmp", segmentWriter.FilePaths.Single());
        Assert.Equal(("videos\\recorder-game.mp4.tmp", "videos\\recorder-game.mp4", true), fileStore.Moves.Single());
        Assert.False(fileStore.Files.ContainsKey("videos\\recorder-game.mp4.tmp"));
        Assert.True(fileStore.Files.ContainsKey("videos\\recorder-game.mp4"));
    }

    [Fact]
    public async Task WriteAsync_removes_tmp_file_when_segment_writer_fails()
    {
        FakeVideoDumpFileStore fileStore = new();
        FakeEncodedReplaySegmentFileWriter segmentWriter = new(fileStore)
        {
            ThrowOnWrite = true,
        };
        VideoDumpFileWriter testee = CreateWriter(fileStore, segmentWriter);
        VideoDumpRequest request = CreateRequest("recorder-game.mp4");

        await Assert.ThrowsAsync<InvalidOperationException>(() => testee.WriteAsync(request, CancellationToken.None));

        Assert.Contains("videos\\recorder-game.mp4.tmp", fileStore.DeletedFiles);
        Assert.False(fileStore.Files.ContainsKey("videos\\recorder-game.mp4.tmp"));
        Assert.False(fileStore.Files.ContainsKey("videos\\recorder-game.mp4"));
    }

    [Fact]
    public async Task WriteAsync_does_nothing_when_disabled()
    {
        FakeVideoDumpFileStore fileStore = new();
        FakeEncodedReplaySegmentFileWriter segmentWriter = new(fileStore);
        VideoDumpFileWriter testee = CreateWriter(fileStore, segmentWriter, enabled: false);

        await testee.WriteAsync(CreateRequest("recorder-game.mp4"), CancellationToken.None);

        Assert.Empty(fileStore.CreatedDirectories);
        Assert.Empty(segmentWriter.FilePaths);
    }

    [Fact]
    public void Retention_deletes_files_older_than_retention_days()
    {
        FakeVideoDumpFileStore fileStore = new();
        fileStore.AddFile("videos\\old.mp4", new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero), 100);
        fileStore.AddFile("videos\\new.mp4", new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero), 100);
        VideoDumpRetentionPolicy testee = new(fileStore);

        testee.Apply("videos", retentionDays: 1, maxTotalSizeBytes: 0, new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));

        Assert.Contains("videos\\old.mp4", fileStore.DeletedFiles);
        Assert.DoesNotContain("videos\\new.mp4", fileStore.DeletedFiles);
    }

    [Fact]
    public void Retention_deletes_oldest_files_until_size_limit_is_met()
    {
        FakeVideoDumpFileStore fileStore = new();
        fileStore.AddFile("videos\\oldest.mp4", new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero), 60);
        fileStore.AddFile("videos\\middle.mp4", new DateTimeOffset(2026, 6, 10, 1, 0, 0, TimeSpan.Zero), 60);
        fileStore.AddFile("videos\\newest.mp4", new DateTimeOffset(2026, 6, 10, 2, 0, 0, TimeSpan.Zero), 60);
        VideoDumpRetentionPolicy testee = new(fileStore);

        testee.Apply("videos", retentionDays: 0, maxTotalSizeBytes: 120, new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));

        Assert.Contains("videos\\oldest.mp4", fileStore.DeletedFiles);
        Assert.DoesNotContain("videos\\middle.mp4", fileStore.DeletedFiles);
        Assert.DoesNotContain("videos\\newest.mp4", fileStore.DeletedFiles);
    }

    [Fact]
    public void Retention_ignores_tmp_files_for_size_limit()
    {
        FakeVideoDumpFileStore fileStore = new();
        fileStore.AddFile("videos\\clip.mp4", new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero), 100);
        fileStore.AddFile("videos\\clip.mp4.tmp", new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero), 10_000);
        VideoDumpRetentionPolicy testee = new(fileStore);

        testee.Apply("videos", retentionDays: 0, maxTotalSizeBytes: 100, new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));

        Assert.Empty(fileStore.DeletedFiles);
    }

    private static VideoDumpFileWriter CreateWriter(
        FakeVideoDumpFileStore fileStore,
        FakeEncodedReplaySegmentFileWriter segmentWriter,
        bool enabled = true)
    {
        return new VideoDumpFileWriter(
            new VideoDumpFileWriterOptions("videos", enabled, RetentionDays: 7, MaxTotalSizeBytes: 1024),
            segmentWriter,
            fileStore,
            () => new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));
    }

    private static VideoDumpRequest CreateRequest(string fileName)
    {
        return new VideoDumpRequest(
            VideoDumpSessionKind.Game,
            new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero),
            fileName,
            CreateSegment());
    }

    private static EncodedReplaySegment CreateSegment()
    {
        return new EncodedReplaySegment(
            EncodedReplayCodec.H264,
            0,
            1,
            [
                new EncodedReplayParameterSet(EncodedReplayParameterSetType.SPS, [0, 0, 1, 0x67]),
                new EncodedReplayParameterSet(EncodedReplayParameterSetType.PPS, [0, 0, 1, 0x68]),
            ],
            [new EncodedReplayAccessUnit(0, true, true, [0, 0, 1, 0x65])]);
    }

    private class FakeEncodedReplaySegmentFileWriter(FakeVideoDumpFileStore fileStore) : IEncodedReplaySegmentFileWriter
    {
        public List<string> FilePaths { get; } = [];

        public bool ThrowOnWrite { get; set; }

        public Task WriteAsync(EncodedReplaySegment segment, string filePath, CancellationToken ct)
        {
            FilePaths.Add(filePath);
            fileStore.AddFile(filePath, new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero), 512);

            if (ThrowOnWrite)
            {
                throw new InvalidOperationException("Write failed.");
            }

            return Task.CompletedTask;
        }
    }

    private class FakeVideoDumpFileStore : IVideoDumpFileStore
    {
        public Dictionary<string, VideoDumpFileEntry> Files { get; } = [];

        public List<string> CreatedDirectories { get; } = [];

        public List<string> DeletedFiles { get; } = [];

        public List<(string SourcePath, string DestinationPath, bool Overwrite)> Moves { get; } = [];

        public void AddFile(string path, DateTimeOffset lastWriteTimeUtc, long sizeBytes)
        {
            Files[path] = new VideoDumpFileEntry(path, lastWriteTimeUtc, sizeBytes);
        }

        public void CreateDirectory(string path)
        {
            CreatedDirectories.Add(path);
        }

        public IReadOnlyList<VideoDumpFileEntry> EnumerateFiles(string directory, string searchPattern)
        {
            return Files
                .Values
                .Where(x => Path.GetDirectoryName(x.Path) == directory)
                .Where(x => searchPattern != "*.mp4" || x.Path.EndsWith(".mp4", StringComparison.Ordinal))
                .ToList();
        }

        public void DeleteFile(string path)
        {
            DeletedFiles.Add(path);
            Files.Remove(path);
        }

        public void MoveFile(string sourcePath, string destinationPath, bool overwrite)
        {
            Moves.Add((sourcePath, destinationPath, overwrite));
            VideoDumpFileEntry source = Files[sourcePath];
            Files.Remove(sourcePath);
            Files[destinationPath] = source with { Path = destinationPath };
        }
    }
}
