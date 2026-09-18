namespace MisWidgets.Feed.Core;

/// <summary>
/// Coordinates the process lifetime without retaining WinRT callback objects.
/// </summary>
public sealed class ProviderLifetime : IDisposable
{
    private readonly ManualResetEventSlim _stopEvent = new(false);
    private int _stopRequested;

    public bool IsStopRequested => Volatile.Read(ref _stopRequested) != 0;

    public void RequestStop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) == 0)
        {
            _stopEvent.Set();
        }
    }

    public void Wait() => _stopEvent.Wait();

    public void Dispose() => _stopEvent.Dispose();
}
