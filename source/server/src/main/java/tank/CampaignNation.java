package tank;

/** Campaign-only starting stats. No changes to shared nations or multiplayer rules. */
public record CampaignNation(int maxHp,int shotDamage,int meleeDamage,int armor,int skillCooldown,int skillRange) {
    public static final int SHOT_COOLDOWN=5;
    private static final CampaignNation USA=new CampaignNation(100,40,20,0,24,12);
    private static final CampaignNation USSR=new CampaignNation(140,32,28,3,20,1);
    private static final CampaignNation ITALY=new CampaignNation(85,48,16,0,16,8);
    public static CampaignNation of(int nation){return switch(nation){case 0->USA;case 1->USSR;case 5->ITALY;default->throw new IllegalArgumentException("campaign nation");};}
}
