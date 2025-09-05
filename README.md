# Senior Developer Technical Assessment – Debugging & Code Review

## Part 1 - Java Snippet

For readability, the following code snippet was used for the analysis:

```java
public class FileProcessor {

    private static List<String> lines = new ArrayList<>();

    public static void main(String[] args) throws Exception {
        ExecutorService executor = Executors.newFixedThreadPool(5);
        for (int i = 0; i < 10; i++) {
            executor.submit(() -> {
                try {
                    BufferedReader br = new BufferedReader(new FileReader("data.txt"));
                    String line;
                    while ((line = br.readLine()) != null) {
                        lines.add(line.toUpperCase());
                    }
                    br.close();
                } catch (Exception e) {
                    e.printStackTrace();
                }
            });
        }
        executor.shutdown();
        System.out.println("Lines processed: " + lines.size());
    }
}
```

### Assumptions

In addition to the provided code, the following assumptions were used to limit the scope of the analysis:

1. The program's goal is to read a text file and convert all lines to upper case.
2. The order of lines in the file is not relevant for the final result.
3. The text file may be large, so efficiency and scalability are important.

### Identified issues

#### Concurrency and scalability
1. Using a non-thread-safe data structure (`ArrayList`) to store processed lines while processing in parallel can lead to race conditions and inconsistent results. For example, if two threads try to add a line at the same time, data corruption may occur.

2. Because the file reading happens inside the thread creation loop, each thread opens and reads the entire file independently. As a result, every line will be processed multiple times (10 times in this example), producing incorrect results.

3. The number of tasks submitted (10) is larger than the thread pool size (5), which means some tasks will be queued while only 5 threads run concurrently.

4. `ExecutorService.shutdown()` is called immediately after submitting tasks, without waiting for their completion. This may cause the program to proceed before all lines are processed.

To solve these problems, several improvements can be applied to ensure safe and efficient parallel processing:

1. Read the file with a single producer thread and distribute lines to worker threads through a `BlockingQueue`. This guarantees each line is produced once and processed by multiple consumers in parallel.

2. Use a synchronized list or a thread-safe collection (e.g. `Collections.synchronizedList(new ArrayList<>())`) to store processed results so concurrent adds are safe.

3. Match the number of consumer threads with desired concurrency so resources are used efficiently.

4. After calling `shutdown()`, use `awaitTermination()` to wait for tasks to finish before proceeding.

5. Use `BlockingQueue.put()` on the producer side to apply backpressure when the queue is full. Combined with a bounded queue size this prevents unbounded memory growth.

6. On the consumer side use `BlockingQueue.poll(timeout)` to avoid consumers blocking indefinitely once the producer has finished.

A refactored example would look like this:

```java

import java.io.BufferedReader;
import java.io.FileReader;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.TimeUnit;

public class FileProcessor {

    private static final List<String> lines = Collections.synchronizedList(new ArrayList<>());

    private static final BlockingQueue<String> linesQueue = new LinkedBlockingQueue<>(500);

    public static void main(String[] args) throws Exception {

        ExecutorService readExecutor = Executors.newSingleThreadExecutor();
        readExecutor.submit(() -> {
            try {
                BufferedReader br = new BufferedReader(new FileReader("data.txt"));
                String line;
                while ((line = br.readLine()) != null) {
                    linesQueue.offer(line);
                }
                br.close();
            } catch (Exception e) {
                e.printStackTrace();
            }
        });

        ExecutorService executor = Executors.newFixedThreadPool(5);
        for (int i = 0; i < 5; i++) {
            executor.submit(() -> {
                try {
                    while (true) {
                        String line = linesQueue.poll(1000, TimeUnit.MILLISECONDS);
                        if (line == null) {
                            break;
                        } else {
                            lines.add(line.toUpperCase());
                        }
                    }
                } catch (Exception e) {
                    e.printStackTrace();
                }
            });
        }

        readExecutor.shutdown();
        readExecutor.awaitTermination(1, java.util.concurrent.TimeUnit.SECONDS);
        executor.shutdown();
        executor.awaitTermination(10, java.util.concurrent.TimeUnit.SECONDS);
        System.out.println("Lines processed: " + lines.size());
    }
}
```

#### Coding anti-patterns

1. Single Responsibility Principle violation: file reading and line processing are mixed in the same class. Splitting responsibilities into `LineProducer` and `LineConsumer` improves readability and maintainability.

2. Magic numbers: the literal `5` used for thread count should be a named constant for clarity.

3. Hard-coded filename reduces reusability. Accept the filename as a program argument with a sensible default.

4. Using `System.out.println` for reporting is acceptable in a small exercise, but in production use a logging library (SLF4J/Logback) to decouple logging from business code.

5. Poorly named variables reduce readability; prefer descriptive names like `processedLines` and `consumerExecutor`.

6. Exception handling is minimal: exceptions are printed but not handled. Improve by logging and defining recovery or notification strategies.

7. Use try-with-resources for `BufferedReader` to ensure resources are always released.

### Refactored code
An improved structure could look like this:

```java
// FileProcessor.java
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.LinkedBlockingQueue;

public class FileProcessor {

    private static final int CONSUMER_POOL_SIZE = 5;

    private static final int AWAIT_TERMINATION_TIMEOUT = 10;
    private static final String DEFAULT_FILENAME = "data.txt";
    private static final int DEFAULT_QUEUE_CAPACITY = 500;

    public static void main(String[] args) throws Exception {

        String filePath = DEFAULT_FILENAME;
        if (args.length > 0) {
            filePath = args[0];
        }

        List<String> processedLines = Collections.synchronizedList(new ArrayList<>());
        BlockingQueue<String> lineQueue = new LinkedBlockingQueue<>(DEFAULT_QUEUE_CAPACITY);

        LineProducer lineReader = new LineProducer(filePath, lineQueue);
        LineConsumer lineConsumer = new LineConsumer(lineQueue, processedLines);

        ExecutorService producerExecutor = Executors.newSingleThreadExecutor();
        producerExecutor.submit(() -> lineReader.produce());

        ExecutorService consumerExecutor = Executors.newFixedThreadPool(CONSUMER_POOL_SIZE);
        for (int i = 0; i < CONSUMER_POOL_SIZE; i++) {
            consumerExecutor.submit(() -> lineConsumer.consume());
        }

        producerExecutor.shutdown();
        producerExecutor.awaitTermination(AWAIT_TERMINATION_TIMEOUT, java.util.concurrent.TimeUnit.SECONDS);
        consumerExecutor.shutdown();
        consumerExecutor.awaitTermination(AWAIT_TERMINATION_TIMEOUT, java.util.concurrent.TimeUnit.SECONDS);
        System.out.println("Lines processed: " + processedLines.size());
    }

}   

// LineProducer.java
import java.io.BufferedReader;
import java.io.FileReader;
import java.util.concurrent.BlockingQueue;

public class LineProducer {

    private final String filePath;
    private final BlockingQueue<String> linesQueue;

    public LineProducer(String filePath, BlockingQueue<String> linesQueue) {
        this.filePath = filePath;
        this.linesQueue = linesQueue;
    }

    public void produce() {
        try (BufferedReader br = new BufferedReader(new FileReader(filePath))) {
            String line;
            while ((line = br.readLine()) != null) {
                linesQueue.put(line);
            }
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            System.err.println("Line production interrupted");
        } catch (Exception e) {
            System.err.println("Error reading file: " + e.getMessage());
        }
    }
}

// LineConsumer.java
import java.util.List;
import java.util.concurrent.BlockingQueue;
import java.util.concurrent.TimeUnit;

public class LineConsumer {

    private static final int TIME_TO_WAIT = 1000;
    private final List<String> lines;
    private final BlockingQueue<String> queue;

    public LineConsumer(BlockingQueue<String> queue, List<String> lines) {
        this.lines = lines;
        this.queue = queue;
    }

    public void consume() {
        try {
            while (true) {
                String line = queue.poll(TIME_TO_WAIT, TimeUnit.MILLISECONDS);
                if (line == null) {
                    break;
                } else {
                    lines.add(line.toUpperCase());
                }
            }
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            System.err.println("Line consumption interrupted unexpectedly");
        }
    }
}
```

### Running the code

Use the [runJava.bat](runJava.bat) script to run the refactored Java code:

```bash
.\runJava.bat
Checking Java installation...
java version "18.0.2.1" 2022-08-18
Java(TM) SE Runtime Environment (build 18.0.2.1+1-1)
Java HotSpot(TM) 64-Bit Server VM (build 18.0.2.1+1-1, mixed mode, sharing)
Compiling Java files...
Running FileProcessor...
Lines processed: 100
Cleaning up...
Done.
```

> If an error occurs, verify that Java is installed and configured correctly on your system.

### Additional improvement suggestions

In addition to the implemented changes, consider the following improvements:

1. If multiple production or consumption strategies are required, define interfaces for `LineProducer` and `LineConsumer` to allow alternative implementations.
2. Encapsulate executor creation and management in an `ExecutorManager` class for better organization.
3. Add a logging library to improve observability and error reporting.
4. Add metrics to monitor processing performance, e.g. average processing time per line and throughput.
5. Add support for build tools like Maven or Gradle to simplify building, testing and packaging the project.
6. Use environment variables or configuration files to set parameters such as thread counts, queue size and file path instead of hard-coding values.


## Part 2 - C# Snippet

For readability, the following C# code was used for the analysis:
```csharp
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
```

### Assumptions

The following assumptions were used to limit the scope of the analysis:

1. The program's goal is to load a number of resources from a base URL and store them in an in-memory cache.
2. The URL format is `{url_base}/{i}`, where `url_base` is a valid URL and `i` is an integer from 0 to N-1, where N is the number of resources to fetch.
3. Even if a request returns an error (e.g., 404 Not Found), the application will record the outcome in the cache.
4. At the end of execution, the program must display the number of resources stored in the cache.

### Identified issues

#### Concurrency and scalability

1. The cache is implemented as a non-thread-safe `List<string>`. This can cause race conditions and inconsistent results when multiple threads add data concurrently. A `ConcurrentDictionary` or other concurrent collection is preferable for concurrent reading and writing.
2. `DownloadAsync` calls are started but not awaited. `Main` can exit before downloads complete, producing an incomplete cache and an incorrect cache size.
3. There's no parallelism limit; too many concurrent requests can overload the network or the remote server. Use `Parallel.ForEachAsync` with `MaxDegreeOfParallelism` option to limit concurrency.
4. Creating a new `HttpClient` per request is bad practice and can exhaust sockets under load. Reuse a single `HttpClient` or use `IHttpClientFactory`.


#### Coding anti-patterns
1. Single Responsibility Principle violation: cache management and downloading are mixed in one class. Separate responsibilities into `DownloaderManager` and `CacheManager`.
2. Magic numbers: the literal `10` should be named for clarity.
3. Hard-coded base URL reduces flexibility. Accept the base URL as an argument and validate it. A `URLBuilder` class can encapsulate this logic.
4. Use a logging library instead of `Console.WriteLine` for production systems. For this exercise, we will keep it simple.
5. Use descriptive variable names instead of `cache` and `client`.
6. Improve exception handling and include timeouts for HTTP requests to avoid indefinite waits.
7. Use `HttpClient` with a `SocketsHttpHandler` to configure connection pooling and timeouts.

### Refactored code
An improved implementation could look like this:
```csharp
// CacheManager.cs
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

//CacheManager.cs
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



// DownloaderManager.cs
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

// URLBuilder.cs


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
```

### Running the code

Use the [runDotNet.bat](runDotNet.bat) script to run the refactored .NET application:

```bash
.\runDotNet.bat
Checking DotNet installation...
9.0.304
Running .NET application...
Downloads completed
Cache size: 10
```

> If an error happens, verify the .NET SDK is installed and configured correctly on your system.

### Additional improvement suggestions

1. Add a logging library to improve observability and error reporting.
2. Add metrics to monitor download performance such as average download time and throughput.
3. Add NuGet packaging and dependency management to facilitate building, testing and packaging.
4. Use environment variables or configuration files for parameters like thread counts, timeouts and base URL to avoid hardcoding values in source.
