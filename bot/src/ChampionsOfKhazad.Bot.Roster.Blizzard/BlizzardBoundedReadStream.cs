namespace ChampionsOfKhazad.Bot.Roster.Blizzard;

// Does not own the response stream. Count decompressed bytes, not just Content-Length.
internal sealed class BlizzardBoundedReadStream(Stream source, int limit) : Stream
{
    private int _remaining = limit;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer) => CountRead(source.Read(buffer[..Math.Min(buffer.Length, _remaining + 1)]));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        CountRead(await source.ReadAsync(buffer[..Math.Min(buffer.Length, _remaining + 1)], cancellationToken));

    private int CountRead(int count)
    {
        if (count > _remaining)
            throw new InvalidDataException("Blizzard response exceeded the auction lookup body limit.");
        _remaining -= count;
        return count;
    }

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
