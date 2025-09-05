
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
