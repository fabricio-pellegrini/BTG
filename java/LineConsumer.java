
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
