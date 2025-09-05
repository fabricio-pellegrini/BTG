public class Downloader
{
    private const int HTTP_POOLED_CONNECTIONS_LIFETIME = 5;
    private const int HTTP_MAX_CONNECTIONS_PER_SERVER = 10;
    private const int HTTP_TIMEOUT = 10;

    public static async Task Main(string[] args)
    {
        //Default values in case no arguments are provided
        var baseUrl = "https://example.com/data";
        int amount = 10;

        if (args.Length == 2)
        {
            baseUrl = args[0];
            amount = int.Parse(args[1]);
        }

        HttpClient httpClient = InitHttpClient();

        var downloadedDataCache = new CacheManager();
        var urls = new URLBuilder(baseUrl, amount).BuildUrls();
        var downloaderManager = new DownloaderManager(httpClient, downloadedDataCache);
        await downloaderManager.DownloadMultipleAsync(urls, HTTP_MAX_CONNECTIONS_PER_SERVER);
        Console.WriteLine("Downloads completed");
        Console.WriteLine("Cache size: " + downloadedDataCache.GetCacheCount());
        httpClient.Dispose();
    }

    private static HttpClient InitHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(HTTP_POOLED_CONNECTIONS_LIFETIME),
            MaxConnectionsPerServer = HTTP_MAX_CONNECTIONS_PER_SERVER
        };
        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT)
        };
        return httpClient;
    }


}