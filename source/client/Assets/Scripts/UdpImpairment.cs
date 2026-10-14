using System;
using System.Collections.Generic;
namespace Tank  {
    [Serializable] public class ImpairmentSettings  {
        public bool Enabled;
        public int Seed=20261007,DelayMs,JitterMs,BurstPeriodMs,BurstDurationMs,Capacity=8192;
        public double Loss,Duplicate,Reorder;
    }
    /** Explicit test-only wrapper. Poll drives delivery; both directions have independent seeded streams. */
    public class UdpImpairment  {
        readonly ImpairmentSettings settings;
        readonly Random tx,rx;
        readonly SortedDictionary<double,Action> delayed=new SortedDictionary<double,Action>();
        readonly double start=ClientCore.NowUs;
        long serial;
        public long Dropped;
        public UdpImpairment(ImpairmentSettings s) {
            settings=s;
            tx=new Random(s.Seed);
            rx=new Random(s.Seed^0x6571);
            if(s.Capacity<1||s.Loss<0||s.Loss>1||s.Duplicate<0||s.Duplicate>1||s.Reorder<0||s.Reorder>1)throw new ArgumentException("impairment settings");
        }
        public void Dispatch(Action action,bool outbound) {
            lock(delayed) {
                if(!settings.Enabled) {
                    action();
                    return;
                }
                Random rng=outbound?tx:rx;
                double now=ClientCore.NowUs;
                if((settings.BurstPeriodMs>0&&(now-start)/1000%settings.BurstPeriodMs<settings.BurstDurationMs)||rng.NextDouble()<settings.Loss) {
                    Dropped++;
                    return;
                }
                int copies=rng.NextDouble()<settings.Duplicate?2:1;
                for(int i=0;i<copies;i++) {
                    if(delayed.Count>=settings.Capacity) {
                        Dropped++;
                        continue;
                    }
                    double delay=Math.Max(0,settings.DelayMs+(rng.NextDouble()*2-1)*settings.JitterMs)+(rng.NextDouble()<settings.Reorder?40:0);
                    double at=now+delay*1000+i+ ++serial*.0001;
                    while(delayed.ContainsKey(at))at+=.01;
                    delayed.Add(at,action);
                }
            }
        }
        public void Poll() {
            var ready=new List<Action>();
            lock(delayed) {
                double now=ClientCore.NowUs;
                foreach(var entry in delayed) {
                    if(entry.Key>now)break;
                    ready.Add(entry.Value);
                }
                foreach(var a in ready) {
                    var first=First();
                    delayed.Remove(first);
                }
            }
            foreach(var action in ready)action();
        }
        double First() {
            foreach(var key in delayed.Keys)return key;
            throw new InvalidOperationException();
        }
    }
}
