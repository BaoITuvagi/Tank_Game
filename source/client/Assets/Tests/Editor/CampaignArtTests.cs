using System.IO;
using NUnit.Framework;
using Tank;
using Tank.Campaign;
using UnityEngine;

public class CampaignArtTests {
    [Test] public void FacingLeftReversesForwardButKeepsTopAndBottomUpright(){
        Rect uv=CampaignArt.VehicleUV(3);
        Vector2 front=new Vector2(uv.x+uv.width,uv.y+uv.height*.5f),top=new Vector2(uv.x+uv.width*.5f,uv.y+uv.height);
        Assert.Less(front.x,.5f,"The forward gun must point to the left");Assert.Greater(top.y,.5f,"The top of the sprite must stay at the top");
        Assert.AreEqual(0,CampaignArt.VehicleRotation(3));Assert.AreEqual(0,CampaignArt.VehicleRotation(1));
        Assert.AreEqual(-90,CampaignArt.VehicleRotation(0));Assert.AreEqual(90,CampaignArt.VehicleRotation(2));
    }
    [Test] public void AuthoritativeMapContainsTreesRocksAndRoomsWithoutClientSideRandomWalls(){
        var kinds=new System.Collections.Generic.HashSet<byte>();
        for(int style=0;style<4;style++){
            var packet=Protocol.Decode(File.ReadAllBytes(Path.GetFullPath("../../source/server/target/interop/campaign-layout-"+style+".bin")),false);
            var client=new CampaignClient();client.Read(new WireReader(packet.Payload));var run=client.Run;
            foreach(byte cell in run.Terrain){Assert.LessOrEqual(cell,4);kinds.Add(cell);}
            Assert.AreEqual(0,run.Terrain[(run.Y-run.Bottom)*13+run.X],"Player must be on a walkable cell");
        }
        Assert.IsTrue(kinds.Contains(1));Assert.IsTrue(kinds.Contains(2));Assert.IsTrue(kinds.Contains(3));Assert.IsTrue(kinds.Contains(4));
    }
    [Test] public void EveryEnemyRoleAndBossUsesADifferentVehicleAsset(){
        var names=new System.Collections.Generic.HashSet<string>();var pixels=new System.Collections.Generic.HashSet<string>();
        foreach(EnemyRole role in System.Enum.GetValues(typeof(EnemyRole))){
            string name=CampaignArt.EnemyAsset(0,role);Assert.IsTrue(names.Add(name));Assert.IsNotNull(CampaignArt.Texture(name));
            Assert.IsTrue(pixels.Add(System.Convert.ToBase64String(File.ReadAllBytes("Assets/Resources/CampaignArt/"+name+".png"))));
        }
        for(int boss=1;boss<=4;boss++){
            string name=CampaignArt.EnemyAsset(boss,EnemyRole.Melee);Assert.IsTrue(names.Add(name));Assert.IsNotNull(CampaignArt.Texture(name));
            Assert.IsTrue(pixels.Add(System.Convert.ToBase64String(File.ReadAllBytes("Assets/Resources/CampaignArt/"+name+".png"))));
        }
    }

    [Test] public void FractionalGroundCellsOverlapInsteadOfExposingBlackGaps(){
        foreach(int width in new[]{800,1280,1917,1920}){
            float size=(width-32)/13f;
            for(int i=0;i<13;i++){
                Rect a=CampaignArt.GroundBounds(new Rect(i*size,123.37f,size,size));
                Rect b=CampaignArt.GroundBounds(new Rect((i+1)*size,123.37f+size,size,size));
                Assert.GreaterOrEqual(a.xMax,b.xMin);Assert.GreaterOrEqual(a.yMax,b.yMin);
                Assert.LessOrEqual(a.xMin,i*size);Assert.GreaterOrEqual(a.xMax,(i+1)*size);
            }
        }
    }

}
