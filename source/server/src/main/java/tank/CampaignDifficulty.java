package tank;

import static tank.CampaignRun.*;

/** Every 500m introduces a distinct squad, without changing other game modes. */
final class CampaignDifficulty {
    record Stage(int index,String name,Role[] roles,int spacing){}
    static final String[] NAMES={"Trinh sát","Tuyến khiên","Hỏa lực kép","Mũi đột kích","Hai tuyến hỏa lực","Hậu cần kép","Gọng kìm","Hỏa lực ba hướng","Tiên phong đông quân","Tuyến phòng thủ kép","Đội hình chiều sâu","Bốn khẩu đội","Mũi nhọn bọc sườn","Ba pháo hai hậu cần","Đột kích hỏa lực","Hai cánh tám xe","Hậu cần chiều sâu","Tiên phong và ba pháo","Ba tuyến tiếp viện","Vòng vây Vu-a-lo"};
    // C = close assault, X = ranged, H = support. Squad sizes never decrease.
    static final String[] SQUADS={"CCX","CCXH","CXXH","CCCXH","CCXXH","CCXHH","CCCXXH","CCXXXH","CCCCXH","CCXXHH","CCCXXHH","CCXXXXH","CCCCXXH","CCXXXHH","CCCXXXH","CCCCXXHH","CCCXXHHH","CCCCXXXH","CCXXXHHH","CCCXXXHH"};
    static Stage at(int distance){
        int index=Math.clamp(distance/500,0,19);String pattern=SQUADS[index];Role[] roles=new Role[pattern.length()];
        for(int i=0;i<roles.length;i++)roles[i]=pattern.charAt(i)=='C'?Role.MELEE:pattern.charAt(i)=='X'?Role.RANGED:Role.SUPPORT;
        return new Stage(index,NAMES[index],roles,Math.max(8,12-index/4));
    }
    static int hp(int row,Role role){return Math.max(20,30+at(row).index()*4-(role==Role.SUPPORT?10:0));}
    static int damage(int row,Role role){int stage=at(row).index();return role==Role.MELEE?6+stage:role==Role.RANGED?8+stage:3+stage/3;}
}
