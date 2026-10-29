package tank;
import java.nio.file.Path;
import java.util.Arrays;
/** Run while server is stopped; password is read from console, never command arguments. */
public final class AdminSetup {
    public static void main(String[] args)throws Exception {
        Config cfg=new Config(Path.of(args.length>0?args[0]:"config/server.properties"));
        String name=args.length>1?args[1]:"admin";
        var console=System.console();
        char[] password;
        if(console!=null) {
            password=console.readPassword("Mat khau admin (it nhat 12 ky tu): ");
            char[] confirm=console.readPassword("Nhap lai: ");
            boolean equal=Arrays.equals(password,confirm);Arrays.fill(confirm,'\0');
            if(!equal)throw new IllegalArgumentException("Mat khau khong khop");
        } else {
            String secret=System.getenv("TANK_ADMIN_PASSWORD");
            if(secret==null)throw new IllegalStateException("Can terminal tuong tac hoac bien moi truong TANK_ADMIN_PASSWORD");
            password=secret.toCharArray();
        }
        try(var store=new AdminStore(cfg.value("admin.database.path","runtime/admin.db"))){store.init(name,password);}
        System.out.println("Da khoi tao admin: "+name);
    }
}
