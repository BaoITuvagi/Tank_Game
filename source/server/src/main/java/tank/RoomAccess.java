package tank;
import java.nio.charset.StandardCharsets;
import java.security.*;
final class RoomAccess  {
    private byte[] salt,hash;
    volatile boolean locked;
    static void validate(String value) {
        if(value==null||value.length()>32||value.getBytes(StandardCharsets.UTF_8).length>128)throw new Database.Failure(Protocol.Error.INVALID_REQUEST);
    }
    void set(String value) {
        validate(value);
        if(value.isEmpty()) {
            salt=hash=null;
            locked=false;
            return;
        }
        salt=new byte[16];
        new SecureRandom().nextBytes(salt);
        hash=digest(value);
        locked=true;
    }
    private byte[] digest(String value) {
        try {
            var d=MessageDigest.getInstance("SHA-256");
            d.update(salt);
            return d.digest(value.getBytes(StandardCharsets.UTF_8));
        }
        catch(NoSuchAlgorithmException e) {
            throw new IllegalStateException(e);
        }
    }
    boolean matches(String value) {
        validate(value);
        return !locked||MessageDigest.isEqual(hash,digest(value));
    }
}
