using Microsoft.Extensions.Primitives;

namespace CarCacheApi.Services;

public class CacheSignal : ICacheSignal
{
    private CancellationTokenSource _cts = new();

    public IChangeToken GetToken()
    {
        // Return a change token linked to the current token source
        return new CancellationChangeToken(_cts.Token);
    }

    public void Invalidate()
    {
        // Thread-safe replacement of the CancellationTokenSource
        var oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        
        // Cancel the old CTS, which triggers eviction of all cache entries monitoring its token
        oldCts.Cancel();
        oldCts.Dispose();
    }
}
