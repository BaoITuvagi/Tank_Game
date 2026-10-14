package tank;
import java.nio.file.*;
import java.util.*;
import java.io.*;
public class Config  {
    public final Properties p=new Properties();
    public Config(Path path)throws IOException {
        try(var in=Files.newInputStream(path)) {
            p.load(in);
        }
        if(integer("tick.rate",30)!=30||integer("snapshot.rate",30)!=30)throw new IllegalArgumentException("v1 requires 30Hz");
        if(integer("max.players",8)<2||integer("max.players",8)>8||integer("max.rooms",40)<1||integer("max.rooms",40)>40||integer("max.sessions",320)<1||integer("max.sessions",320)>320)throw new IllegalArgumentException("capacity");
        int ticks=integer("match.ticks",5400);
        if(ticks<1||ticks>65535)throw new IllegalArgumentException("match ticks");
        if(!flag("benchmark")&&ticks!=5400)throw new IllegalArgumentException("short matches require benchmark=true");
        for(String key:new String[] {
            "impairment.loss","impairment.duplicate","impairment.reorder"
        }) {
            double n=number(key,0);
            if(n<0||n>1)throw new IllegalArgumentException(key);
        }
        for(String key:new String[] {
            "impairment.delay.ms","impairment.jitter.ms","impairment.burst.period.ms","impairment.burst.duration.ms"
        })if(number(key,0)<0)throw new IllegalArgumentException(key);
        mode();
    }
    public String value(String k,String d) {
        return p.getProperty(k,d);
    }
    public int integer(String k,int d) {
        return Integer.parseInt(value(k,""+d));
    }
    public double number(String k,double d) {
        double x=Double.parseDouble(value(k,""+d));
        if(!Double.isFinite(x))throw new IllegalArgumentException(k);
        return x;
    }
    public boolean flag(String k) {
        return Boolean.parseBoolean(value(k,"false"));
    }
    public Protocol.Mode mode() {
        return Protocol.Mode.valueOf(value("network.mode","COMBINED"));
    }
}
