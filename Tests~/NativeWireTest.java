import cn.zero3d.buglywebrequest.WireBridge;
import com.sun.net.httpserver.HttpServer;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.*;

public final class NativeWireTest {
    private static void check(boolean value,String message) { if (!value) throw new AssertionError(message); }
    private static final byte[] payload="function=111&name=%E6%B5%8B%E8%AF%95".getBytes(StandardCharsets.UTF_8);
    private static final byte[] binary={0,1,(byte)255,0,13,10};
    private static final String[] names={"Content-Type"}, values={"application/x-www-form-urlencoded"};
    private static final class Collector implements WireBridge.Callback {
        final CountDownLatch done;
        final Map<Integer,WireBridge.Result> results=new ConcurrentHashMap<>();
        final AtomicInteger callbacks=new AtomicInteger();
        Collector(int expected) { done=new CountDownLatch(expected); }
        public void onComplete(int index,WireBridge.Result result) { callbacks.incrementAndGet(); results.put(index,result); done.countDown(); }
        void await() throws Exception { check(done.await(12,TimeUnit.SECONDS),"Completion timed out"); }
    }
    public static void main(String[] args) throws Exception {
        HttpServer server=HttpServer.create(new InetSocketAddress("127.0.0.1",0),0);
        ExecutorService handlers=Executors.newCachedThreadPool(); server.setExecutor(handlers);
        AtomicInteger flaky=new AtomicInteger(), notFound=new AtomicInteger(), bodyErrors=new AtomicInteger();
        CountDownLatch cancelStarted=new CountDownLatch(2);
        server.createContext("/ok", exchange -> {
            if (!"POST".equals(exchange.getRequestMethod()) || !Arrays.equals(payload,exchange.getRequestBody().readAllBytes()) ||
                !values[0].equals(exchange.getRequestHeaders().getFirst(names[0]))) bodyErrors.incrementAndGet();
            exchange.sendResponseHeaders(200,binary.length); exchange.getResponseBody().write(binary); exchange.close();
        });
        server.createContext("/flaky", exchange -> {
            exchange.getRequestBody().readAllBytes(); int status=flaky.incrementAndGet()%2==1?503:200;
            exchange.sendResponseHeaders(status,binary.length); exchange.getResponseBody().write(binary); exchange.close();
        });
        server.createContext("/missing", exchange -> {
            notFound.incrementAndGet(); exchange.sendResponseHeaders(404,binary.length); exchange.getResponseBody().write(binary); exchange.close();
        });
        server.createContext("/slow", exchange -> {
            try { Thread.sleep(2500); } catch (InterruptedException ignored) { } exchange.close();
        });
        server.createContext("/cancel", exchange -> {
            cancelStarted.countDown(); try { Thread.sleep(2500); } catch (InterruptedException ignored) { } exchange.close();
        });
        server.start(); String root="http://127.0.0.1:"+server.getAddress().getPort();
        try {
            for (int transport=0;transport<2;transport++) {
                Collector collector=new Collector(4);
                WireBridge.submit(0,root+"/ok",payload,names,values,transport,3,3,10,1000,false,collector);
                WireBridge.submit(1,root+"/flaky",payload,names,values,transport,3,3,10,1000,false,collector);
                WireBridge.submit(2,root+"/missing",payload,names,values,transport,3,3,10,1000,false,collector);
                WireBridge.submit(3,root+"/slow",payload,names,values,transport,1,2,10,1000,false,collector);
                collector.await();
                check(collector.callbacks.get()==4,"Retry generated duplicate callbacks");
                check(Arrays.equals(binary,collector.results.get(0).getData()),"Binary changed");
                check(collector.results.get(1).isSuccess() && collector.results.get(1).getAttempts()==2,"Retry did not recover");
                check(!collector.results.get(2).isSuccess() && collector.results.get(2).getAttempts()==1,"404 retry policy wrong");
                check("timeout".equals(collector.results.get(3).getFailure()) && collector.results.get(3).getAttempts()==2,"Timeout retry policy wrong");
                check(collector.results.get(3).getDurationMs()>=collector.results.get(3).getLastAttemptMs(),"Timing aggregation wrong");
                Collector limited=new Collector(1);
                WireBridge.submit(0,root+"/ok",payload,names,values,transport,3,3,0,2,false,limited);
                limited.await(); check("response_too_large".equals(limited.results.get(0).getFailure()) && limited.results.get(0).getAttempts()==1,"Response limit failed");
            }
            Collector canceled=new Collector(2);
            WireBridge.Job a=WireBridge.submit(0,root+"/cancel",payload,names,values,0,5,3,0,1000,false,canceled);
            WireBridge.Job b=WireBridge.submit(1,root+"/cancel",payload,names,values,1,5,3,0,1000,false,canceled);
            check(cancelStarted.await(4,TimeUnit.SECONDS),"Cancel requests did not start");
            a.cancel(); b.cancel(); a.cancel(); b.cancel(); canceled.await(); Thread.sleep(100);
            check(canceled.callbacks.get()==2 && "canceled".equals(canceled.results.get(0).getFailure()) && "canceled".equals(canceled.results.get(1).getFailure()),"Cancellation callback not deduplicated");
            check(bodyErrors.get()==0 && notFound.get()==2,"Body or HTTP policy failed");
            System.out.println("PASS both transports: independent URLs, POST/body/header/binary, retry/final-only callback, HTTP failure, timeout, limit, cancellation, timing");
        } finally { server.stop(0); handlers.shutdownNow(); }
    }
}
