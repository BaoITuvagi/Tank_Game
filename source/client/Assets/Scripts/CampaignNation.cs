namespace Tank.Campaign {
    // Selection card defaults; active runs receive authoritative stats from the server.
    public sealed class CampaignNation {
        public readonly int Id,MaxHp,ShotDamage,MeleeDamage,Armor,SkillCooldown,SkillRange;
        public readonly string Name,Role,SkillName,SkillDescription;
        CampaignNation(int id,string name,string role,int hp,int shot,int melee,int armor,int cooldown,int range,string skill,string description){Id=id;Name=name;Role=role;MaxHp=hp;ShotDamage=shot;MeleeDamage=melee;Armor=armor;SkillCooldown=cooldown;SkillRange=range;SkillName=skill;SkillDescription=description;}
        static readonly CampaignNation Usa=new CampaignNation(0,"Mỹ","Cân bằng · Diện rộng",100,40,20,0,24,12,"Không kích","70 sát thương trong bán kính 2 ô.\nTự chọn địch gần nhất trong 12 ô.");
        static readonly CampaignNation Ussr=new CampaignNation(1,"Liên Xô","Chống chịu · Phòng thủ",140,32,28,3,20,1,"Pháo đài thép","Nhận 3 khiên; gây 45 sát thương quanh xe trong 1 ô.\nCó thể dùng khi chưa có địch.");
        static readonly CampaignNation Italy=new CampaignNation(5,"Ý","Hỏa lực · Xuyên giáp",85,48,16,0,16,8,"Pháo xuyên giáp","120 sát thương xuyên đội hình và khiên hỗ trợ.\nNgắm theo hướng xe, tầm 8 ô; vật cản chặn đạn.");
        public static CampaignNation For(int nation){return nation==1?Ussr:nation==5?Italy:Usa;}
    }
}
