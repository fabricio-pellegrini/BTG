using System.Collections.Concurrent;
public class CacheManager
{
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public void AddToCache(string key, string value)
    {
        _cache.AddOrUpdate(key, value, (k, v) => value);
    }

    public int GetCacheCount()
    {
        return _cache.Count;
    }
}
