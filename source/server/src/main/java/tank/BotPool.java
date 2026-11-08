package tank;

import java.security.SecureRandom;
import java.util.*;
import java.util.concurrent.*;

/** Synthetic identities have durable result/report IDs but no usable login credentials. */
final class BotPool {
    private static final String[] NAMES={"MinhQuan","HuyPhong","LinhNek","NamBao","DucAnh","KhanhSky","BinTran","TuanKiet","ThanhLong","AnNhien","QuangHuy","HaiDang","BaoNgoc","PhongVu","HoangNam","NhatMinh","VietAnh","GiaBao","TieuLam","DuyKhanh","NhatHa","KimAnh","DatNguyen","MeoMun"};
    private static final String[] FAMILY={"Nguyen","Tran","Le","Pham","Hoang","Huynh","Phan","Vu","Vo","Dang","Bui","Do","Ngo","Duong","Ly","Dinh","Truong","Mai","Cao","Lam"};
    private static final String[] MIDDLE={"Minh","Quang","Hoang","Thanh","Duc","Ngoc","Gia","Tuan","Bao","Nhat","Hong","Thuy","Hai","Xuan","Kim","Anh"};
    private static final String[] GIVEN={"Quan","Phong","Linh","Bao","Anh","Khanh","Kiet","Long","Huy","Dang","Ngoc","Vu","Nam","Minh","Ha","Lam","Duy","Dat","Son","Tung","Phuc","Thao","Nhi","Yen"};
    final Database db;
    final Queue<Database.Account> available=new ConcurrentLinkedQueue<>();
    final Set<Long> leased=ConcurrentHashMap.newKeySet();
    final SecureRandom random=new SecureRandom();
    boolean loaded;
    int count;
    ArrayDeque<String> names;
    record Identity(long id,String username,String name){}
    BotPool(Database db){this.db=db;}
    boolean nameUsed(String name,long except)throws Exception {
        return !db.reports.rows("SELECT a.id FROM accounts a LEFT JOIN player_profiles p ON p.account_id=a.id WHERE a.id<>? AND (a.username=? COLLATE NOCASE OR p.display_name=? COLLATE NOCASE) LIMIT 1",except,name,name).isEmpty();
    }
    String nextName()throws Exception {
        if(names==null) {
            var candidates=new LinkedHashSet<>(List.of(NAMES));
            for(String middle:MIDDLE)for(String given:GIVEN)if(!middle.equals(given)) {
                candidates.add(middle+given);
                for(String family:FAMILY)candidates.add(family+middle+given);
            }
            var shuffled=new ArrayList<>(candidates);Collections.shuffle(shuffled,random);names=new ArrayDeque<>(shuffled);
        }
        while(!names.isEmpty()){String name=names.removeFirst();if(!nameUsed(name,0))return name;}
        return null;
    }
    Database.Account normalize(Identity old)throws Exception {
        String name=old.name;
        if(name.matches("[A-Za-z]{3,24}")&&old.username.equals(name.toLowerCase(Locale.ROOT))&&!nameUsed(name,old.id))return new Database.Account(old.id,name);
        name=name.replaceAll("[_0-9]+$","");
        if(!name.matches("[A-Za-z]{3,24}")||nameUsed(name,old.id))name=nextName();
        if(name==null)return null;
        db.db.setAutoCommit(false);
        try {
            db.reports.execute("UPDATE accounts SET username=? WHERE id=? AND is_bot=1",name.toLowerCase(Locale.ROOT),old.id);
            db.setDisplayName(old.id,name);db.db.commit();
        }catch(Exception e){db.db.rollback();throw e;}finally{db.db.setAutoCommit(true);}
        return new Database.Account(old.id,name);
    }
    // All acquisition and SQL run on the database worker. Release never waits for SQL.
    Database.Account acquire(Set<Long> excluded)throws Exception {
        if(!loaded) {
            var identities=new ArrayList<Identity>();
            try(var s=db.db.prepareStatement("SELECT a.id,a.username,coalesce(p.display_name,a.username) FROM accounts a LEFT JOIN player_profiles p ON p.account_id=a.id WHERE a.is_bot=1 ORDER BY a.id LIMIT 512");var rows=s.executeQuery()) {
                while(rows.next())identities.add(new Identity(rows.getLong(1),rows.getString(2),rows.getString(3)));
            }
            var ready=new ArrayList<Database.Account>();for(var old:identities){var account=normalize(old);if(account!=null)ready.add(account);}
            available.addAll(ready);count=identities.size();loaded=true;
        }
        for(int n=available.size();n>0;n--) {
            var account=available.poll();if(account==null)break;
            if(excluded.contains(account.id())){available.offer(account);continue;}
            if(nameUsed(account.name(),account.id())) {
                account=normalize(new Identity(account.id(),account.name().toLowerCase(Locale.ROOT),account.name()));
                if(account==null)continue;
            }
            if(leased.add(account.id()))return account;
        }
        if(count>=512)return null;
        String name=nextName();if(name==null)return null;
        byte[] salt=new byte[16],hash=new byte[32];random.nextBytes(salt);random.nextBytes(hash);
        long id;
        try(var s=db.db.prepareStatement("INSERT INTO accounts(username,salt,hash,iterations,created,is_bot) VALUES(?,?,?,?,?,1) RETURNING id")) {
            s.setString(1,name.toLowerCase(Locale.ROOT));s.setBytes(2,salt);s.setBytes(3,hash);s.setInt(4,600000);s.setLong(5,System.currentTimeMillis());
            try(var result=s.executeQuery()){result.next();id=result.getLong(1);}
        }
        db.reports.execute("INSERT INTO player_profiles(account_id,display_name) VALUES(?,?)",id,name);
        count++;leased.add(id);return new Database.Account(id,name);
    }
    void release(Database.Account account){if(account!=null&&leased.remove(account.id()))available.offer(account);}
}
