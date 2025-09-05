# Senior Developer Technical Assessment – Debugging & Code Review

## Part 1 - Java Snippet

Apenas para facilitar a leitura, foi utilizado o seguinte código para a análise:

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

### Premissas 

Além do código fornecido, foram consideradas as seguintes premissas para limitar o escopo da análise:

1. O objetivo do programa é ler um arquivo de texto e converter todas as linhas para letras maiúsculas.
2. A ordem das linhas no arquivo não é relevante para o resultado final.
3. O arquivo de texto pode ser grande, então a eficiência e a escalabilidade são importantes.

### Problemas identificados

#### Concorrência e Escalabilidade
1. Utilizar uma estrutura de dados não thread-safe ('ArrayList') para armazenar as linhas processadas em paralelo, pode levar a condições de corrida e resultados inconsistentes. Por exemplo, se duas threads tentarem adicionar uma linha ao mesmo tempo, isso pode resultar na sobrescrita de dados.

2. Como o processo de leitura do arquivo é feito dentro do loop que cria as threads, cada thread está lendo o arquivo inteiro de forma independente. Consequentemente, cada linha do arquivo será processada múltiplas vezes (10 vezes, no caso), levando a aplicação gerar um resultado incorreto.

3. A quantidade de threads (10) é maior que o tamanho do pool de threads (5), dessa forma, algumas threads podem ficar ociosas enquanto outras estão processando.

4. O método `shutdown()` do `ExecutorService` é chamado imediatamente após o envio das tarefas, sem aguardar a conclusão de todas as threads. Isso pode levar a um encerramento prematuro do executor antes que todas as linhas sejam processadas.

Para resolver esses problemas, podemos fazer algumas melhorias no código para garantir que o processamento paralelo seja mais eficiente e seguro:

1. Ler o arquivo em uma única thread e distribuir as linhas para serem processadas através de uma fila bloqueante (BlockingQueue) para que as threads responsáveis pelo processamento dos dados possam consumi-las de forma segura. Dessa forma, garantimos que cada linha seja processada uma única vez (único producer) e o processamento de converter para maiúsculas seja feito em paralelo (múltiplos consumers).

2. Para armazenar o resultado das linhas processadas, podemos utilizar uma lista sincronizada ou uma coleção thread-safe, como `Collections.synchronizedList(new ArrayList<>())`. Dessa forma, caso duas threads tentem adicionar o resultado do processamento ao mesmo tempo, a estrutura de dados garantirá a integridade dos dados.

3. Ajustar o número de threads no pool para ser igual ao número de tarefas, garantindo que todas as threads possam ser utilizadas eficientemente.

4. Após a invocação de `shutdown()`, utilizar `awaitTermination()` para garantir que o programa aguarde a conclusão de todas as tarefas antes de prosseguir.

5. Utilizar a interface `put` do `BlockingQueue` para adicionar linhas à fila, garantindo que o produtor espere se a fila estiver cheia, evitando perda de dados. Isso associado a um tamanho máximo para a fila, permite controlar o uso de memória e evitar que essa fila cresça indefinidamente para arquivos muito grandes.

6. Utilizar a interface `poll` do `BlockingQueue`, informando um tempo máximo de espera, caso a lista esteja vazia, para evitar que as threads fiquem bloqueadas indefinidamente caso a fila esteja vazia e o produtor já tenha terminado de adicionar linhas.

Ao fim dessa refatoração teríamos o seguinte código:

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

#### Má Práticas de Codificação

1. Violação do princípio de única responsabilidade: Tanto a leitura do arquivo quanto o processamento das linhas estão sendo feitas na mesma classe. Podemos separar a lógica de leitura do arquivo e processamento das linhas em classes distintas, como `LineProducer` e `LineConsumer`. Isso melhora a legibilidade e a manutenção do código.

2. Uso de valores mágicos: O número 5 (número de threads) é usados diretamente no código, o que dificulta a compreensão do seu significado. Podemos definir uma constante para esse valores, melhorando a legibilidade do código. 

3. O nome do arquivo está hardcoded, o que dificulta a reutilização do código. Podemos passar o nome do arquivo como um argumento para o programa e definir um valor padrão caso nenhum argumento seja fornecido.

4. Utilização do `System.out.println` para exibir o número de linhas processadas. Em um ambiente de produção, é mais apropriado utilizar uma biblioteca de logging para registrar informações, erros e depurações. Para esse exercicio, mantivemos o `System.out.println`, mas em um cenário real, consideraríamos o uso de uma biblioteca de logging como SLF4J que permite desacoplar a lógica de logging do código de negócios.

5. Nome de variáveis pouco significativas: Nomes como `lines` e `executor` são genéricos e não transmitem claramente seu propósito. Utilizar nomes mais descritivos, como `processedLines` e `consumerExecutor`, pode melhorar a clareza do código.

6. Falta de tratamento adequado de exceções: Atualmente, as exceções são capturadas e impressas no console, mas não há um tratamento adequado para lidar com erros. Podemos melhorar o tratamento de exceções, registrando os erros em um log e implementando uma estratégia de recuperação ou notificação adequada.

7. Utilização de `try-with-resources` para garantir o fechamento adequado do `BufferedReader`, evitando possíveis vazamentos de recursos.

### Código Refatorado
Ao fim teríamos o seguinte código:

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

### Executando o código

Para executar o código refatorado, utilize o script [runJava.bat](runJava.bat).

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

> Caso ocorra algum erro, verifique se o Java está corretamente instalado e configurado no seu sistema.

### Sugestões de Melhoria Adicionais

Além das melhorias já implementadas, podemos considerar as seguintes sugestões para aprimorar ainda mais o código:

1. Caso seja necessário ter mais de uma estrategia de de produção ou consumo, podemos definir interfaces para `LineProducer` e `LineConsumer`, permitindo a implementação de diferentes estratégias de leitura e processamento de linhas.
2. Podemos encapsular a lógica de criação e gerenciamento dos executores em uma classe dedicada, como `ExecutorManager`, para melhorar a organização do código.
3. Como já mencionado, podemos incluir uma biblioteca de logging para melhorar o registro de informações e erros.
4. Para melhorar a observabilidade do sistema, podemos adicionar métricas para monitorar o desempenho do processamento de linhas, como o tempo médio de processamento por linha e o número de linhas processadas por segundo.
5. Podemos adicionar o suporte a ferramentas de gestão de dependências e build, como Maven ou Gradle, para facilitar a construção, teste e empacotamento do projeto.
6. Utilizar variáveis de ambiente ou arquivos de configuração para definir parâmetros como o número de threads, o tamanho da fila e o caminho do arquivo, permitindo maior flexibilidade na configuração do sistema sem a necessidade de alterar o código-fonte.



## Part 2 - C# Snippet

Apenas para facilitar a leitura, foi utilizado o seguinte código para a análise:
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

### Premissas 

Além do código fornecido, foram consideradas as seguintes premissas para limitar o escopo da análise:

1. O objetivo do programa é carregar páginas web em um cache em memória.
2. Mesmo que uma página retorne um erro (por exmplo, 404 Not Found), a aplicação irá guardar o erro no cache. 
3. Ao final da execução, deve ser exibido o número de páginas carregadas no cache.

### Problemas identificados

#### Concorrência e Escalabilidade

1. A estrutura de dados utilizada para armazenar o cache (`List<String>`) não é adequada por ser uma coleção não thread-safe. Isso pode levar a condições de corrida e resultados inconsistentes, como perda de dados ou exceções em tempo de execução, quando múltiplas threads tentam adicionar dados ao cache simultaneamente. Além disso, o uso de uma lista simples pode não ser eficiente para operações de busca ou remoção, especialmente se o cache crescer significativamente, impactando a escalabilidade do sistema. Uma opção melhor seria utilizar um `ConcurrentDictionary`, que é thread-safe e oferece melhor desempenho para operações de leitura e escrita concorrentes.
2. As chamadas para `DownloadAsync` são iniciadas, mas não aguardadas. Isso significa que o método `Main` pode terminar sua execução antes que todas as tarefas de download sejam concluídas, resultando em um cache incompleto e um tamanho de cache incorreto sendo exibido.
3. Não há nenhum limite de paralelismo para execução do `DownloadAsync`, o que pode sobrecarregar o servidor ou a rede. Para contornar esse problema, podemos definir um limite máximo de paralelismo, utilizando `Parallel.ForEachAsync` com a opção `MaxDegreeOfParallelism`.
4. Não é uma boa prática criar uma nova instancia do `HttpClient` para cada requisição HTTP. Uma vez que tal pratica pode ocasionar o esgotamento de sockets e cenários de alta carga.


#### Má Práticas de Codificação
1. Violação do princípio de única responsabilidade: Tanto o gerenciamento do cache quanto o download dos dados estão sendo feitos na mesma classe. Podemos separar a lógica de download e gerenciamento do cache em classes distintas, como `DownloaderManager` e `CacheManager`. Isso melhora a legibilidade e a manutenção do código.
2. Uso de valores mágicos: O número 10 (número de downloads) é usado diretamente no código, o que dificulta a compreensão do seu significado. Podemos definir uma variável para esse valor, melhorando a legibilidade do código.
3. O URL base está hardcoded, o que dificulta a reutilização do código. Podemos passar o URL base como um argumento para o programa e definir um valor padrão caso nenhum argumento seja fornecido. Além disso, podemos criar uma classe para encapsular a lógica de construção das URLs, permitindo incluir validações nesse processo.
4. Utilização do `Console.WriteLine` para exibir o número de páginas carregadas no cache. Em um ambiente de produção, é mais apropriado utilizar uma biblioteca de logging para registrar informações, erros e depurações. Para esse exercício, mantivemos o `Console.WriteLine`, mas em um cenário real, consideraríamos o uso de uma biblioteca de logging como Serilog ou NLog que oferece mais funcionalidades e flexibilidade.
5. Nome de variáveis pouco significativas: Nomes como `cache` e `client` são genéricos e não transmitem claramente seu propósito. Utilizar nomes mais descritivos, como `downloadedDataCache` e `httpClient`, pode melhorar a clareza do código.
6. Falta de tratamento adequado de exceções: Atualmente, as exceções não são capturadas, o que pode levar a falhas silenciosas ou crashes inesperados. Podemos melhorar o tratamento de exceções, registrando os erros em um log e implementando uma estratégia de recuperação ou notificação adequada.
7. Utilizar o `HTTPClient` sem definir um tempo limite (timeout) pode causar a aplicação ficar esperando mais do que deveria, caso o servidor demore a responder ou não responda.

### Código Refatorado
Ao fim teríamos o seguinte código:
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
````

### Executando o código

Para executar o código refatorado, utilize o script [runDotNet.bat](runDotNet.bat).

```bash
.\runDotNet.bat
Checking DotNet installation...
9.0.304
Running .NET application...
Downloads completed
Cache size: 10
```

> Caso ocorra algum erro, verifique se o .NET SDK está corretamente instalado e configurado no seu sistema.