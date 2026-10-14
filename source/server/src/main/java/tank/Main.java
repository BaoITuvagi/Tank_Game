package tank;
import java.nio.file.*;
import java.io.*;
public final class Main  {
    public static void main(String[] args)throws Exception {
        Config cfg=new Config(Path.of(args.length==0?"config/server.properties":args[0]));
        Path log=Path.of(cfg.value("log.path","runtime/server.log")).toAbsolutePath();
        Files.createDirectories(log.getParent());
        OutputStream file=Files.newOutputStream(log,StandardOpenOption.CREATE,StandardOpenOption.APPEND);
        System.setOut(new PrintStream(new Tee(System.out,file),true,java.nio.charset.StandardCharsets.UTF_8));
        System.setErr(new PrintStream(new Tee(System.err,file),true,java.nio.charset.StandardCharsets.UTF_8));
        Server server=new Server(cfg);
        Runtime.getRuntime().addShutdownHook(new Thread(server::close));
        server.start();
        String stop=cfg.value("control.stop.path","");
        if(cfg.flag("benchmark")&&!stop.isBlank()) {
            while(server.running&&!Files.exists(Path.of(stop)))Thread.sleep(250);
            server.close();
        }
    }
    static class Tee extends OutputStream  {
        final OutputStream console,file;
        Tee(OutputStream c,OutputStream f) {
            console=c;
            file=f;
        }
        public void write(int x)throws IOException {
            console.write(x);
            synchronized(file) {
                file.write(x);
            }
        }
        public void write(byte[] b,int off,int len)throws IOException {
            console.write(b,off,len);
            synchronized(file) {
                file.write(b,off,len);
            }
        }
        public void flush()throws IOException {
            console.flush();
            file.flush();
        }
    }
}
