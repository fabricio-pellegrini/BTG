public class Downloader
{
    private static List<string> cache = new List<string>();
    public static async Task Main(string[] args)
    {
        for (int i = 0; i < 10; i++)
        {
            DownloadAsync("https://example.com/data/" + i);
        }
        Console.WriteLine("Downloads started");
        Console.WriteLine("Cache size: " + cache.Count);
    }
    private static async Task DownloadAsync(string url)
    {
        using (HttpClient client = new HttpClient())
        {
            var data = await client.GetStringAsync(url);
            cache.Add(data);
        }
    }
}
