namespace SuratGenerator.Core.Services;

public sealed class PreviewRequestTracker
{
    private long _latest;

    public long Next() => Interlocked.Increment(ref _latest);

    public bool IsCurrent(long requestId) => Volatile.Read(ref _latest) == requestId;
}
