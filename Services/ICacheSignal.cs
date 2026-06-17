using Microsoft.Extensions.Primitives;

namespace CarCacheApi.Services;

public interface ICacheSignal
{
    IChangeToken GetToken();
    void Invalidate();
}
