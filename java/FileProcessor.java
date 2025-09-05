
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
