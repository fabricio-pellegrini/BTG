

public class URLBuilder
{
    private string baseUrl;
    private int amount;

    public URLBuilder(string baseUrl, int amount)
    {
        this.baseUrl = baseUrl;
        this.amount = amount;
    }

    private bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    public List<string> BuildUrls()
    {
        if (!IsValidUrl(baseUrl))
        {
            throw new ArgumentException("Invalid base URL");
        }
        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero");
        }

        var urls = new List<string>();
        for (int i = 0; i < amount; i++)
        {
            string url = $"{baseUrl}/{i}";
            urls.Add(url);
        }
        return urls;
    }
}