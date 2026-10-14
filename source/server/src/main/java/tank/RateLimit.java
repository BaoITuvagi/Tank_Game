package tank;
public final class RateLimit  {
    final double rate,burst;
    double tokens;
    long time=System.nanoTime();
    public RateLimit(double perSecond,double burst) {
        rate=perSecond;
        this.burst=burst;
        tokens=burst;
    }
    public synchronized boolean allow() {
        long now=System.nanoTime();
        tokens=Math.min(burst,tokens+(now-time)/1e9*rate);
        time=now;
        if(tokens<1)return false;
        tokens--;
        return true;
    }
    public synchronized boolean expired() {
        return System.nanoTime()-time>60_000_000_000L;
    }
}
