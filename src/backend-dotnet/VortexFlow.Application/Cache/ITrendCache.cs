namespace VortexFlow.Application.Cache;

public interface ITrendCache
{
    Task SetTrendAsync(string platform, string hashtag, string data, TimeSpan? expiry = null);
    Task<string?> GetTrendAsync(string platform, string hashtag);
}
