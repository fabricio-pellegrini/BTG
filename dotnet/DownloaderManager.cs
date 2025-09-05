

public class DownloaderManager
{
    readonly HttpClient client;
    readonly CacheManager cache;

    public DownloaderManager(HttpClient client, CacheManager cache)
    {
        this.client = client;
        this.cache = cache;
    }

    public async Task DownloadMultipleAsync(List<string> urls, int maxConcurrency = 100)
    {
        var op = new ParallelOptions { MaxDegreeOfParallelism = maxConcurrency };
        await Parallel.ForEachAsync(urls, op, async (url, ct) =>
        {
            await DownloadAsync(url);
        });
    }

    private async Task DownloadAsync(string url)
    {
        try
        {
            using var responseMessage = await client.GetAsync(url);
            var data = await responseMessage.Content.ReadAsStringAsync();
            cache.AddToCache(url, data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to download {url}: {ex.Message}");
        }
    }
}