using System.IO;
using NUnit.Framework;
using Tank;
using UnityEditor;
using UnityEngine;

public class MenuUiTests {
    [Test] public void NarrowHostRoomRetainsSpaceForSummaryAndFourActions() {
        var layout=MenuLayout.Fit(640,400);
        Assert.GreaterOrEqual(layout.Content.height,80+60+256+48,"Summary, host actions and padding must fit in the scrollable viewport.");
    }
    [Test] public void ImportedCartoonFontsContainVietnameseUiTextAndKeepTheirLicenses() {
        foreach(string name in new[]{"Bungee-Regular","Nunito-Bold"}) {
            var font=Resources.Load<Font>("MenuArt/Fonts/"+name);
            Assert.IsNotNull(font,name);
            foreach(char letter in "Đăng nhập Đăng ký Tạo phòng Sẵn sàng Nhập vai Máu Thời gian ăâđêôơưĂÂĐÊÔƠƯẠẶẬẤẦẨẪộỵ")
                Assert.IsTrue(font.HasCharacter(letter),name+" missing U+"+((int)letter).ToString("X4"));
        }
        foreach(string license in new[]{"Bungee-OFL.txt","Nunito-OFL.txt","NOTICE.txt"})
            Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath,"Resources/MenuArt/Fonts",license)),license);
    }
    [TestCase(960,600)] [TestCase(1280,800)] [TestCase(1366,768)] [TestCase(1600,900)] [TestCase(1920,1080)] [TestCase(640,400)]
    public void AuthenticationFitsScrollableViewportWithoutCoveringLogo(int width,int height) {
        var layout=MenuLayout.Fit(width,height);
        Assert.GreaterOrEqual(layout.Width,width);Assert.GreaterOrEqual(layout.Height,height);
        Assert.GreaterOrEqual(layout.AuthCard.x,0);Assert.LessOrEqual(layout.AuthCard.xMax,layout.Width);
        Assert.GreaterOrEqual(layout.AuthCard.y,76);Assert.LessOrEqual(layout.AuthCard.yMax,layout.Height);
        Assert.GreaterOrEqual(layout.AuthCard.width,460);Assert.GreaterOrEqual(layout.AuthCard.height,350);
        Assert.IsFalse(layout.AuthCard.Overlaps(layout.AuthLogo));
        if(width>=1100)Assert.AreEqual(layout.AuthLogo.width,layout.AuthLogo.height);
    }
    [Test] public void MenuArtworkKeepsOriginalDimensionsAndIndependentImportSettings() {
        var artRoot=Path.Combine(Application.dataPath,"Resources/MenuArt");
        var repoRoot=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        foreach(var name in new[]{"logo","anh_nen"}) {
            string path="Assets/Resources/MenuArt/"+name+".png";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(texture,path);
            Assert.AreEqual(name=="logo"?1254:1672,texture.width);Assert.AreEqual(name=="logo"?1254:941,texture.height);
            CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(repoRoot,name+".png")),File.ReadAllBytes(Path.Combine(artRoot,name+".png")));
        }
        foreach(var path in Directory.GetFiles(artRoot,"*.png",SearchOption.AllDirectories)) {
            string assetPath="Assets"+path.Substring(Application.dataPath.Length).Replace('\\','/');
            var importer=(TextureImporter)AssetImporter.GetAtPath(assetPath);
            Assert.IsFalse(importer.mipmapEnabled,assetPath);Assert.AreEqual(TextureImporterNPOTScale.None,importer.npotScale,assetPath);
            Assert.AreEqual(assetPath=="Assets/Resources/MenuArt/logo.png"||assetPath=="Assets/Resources/MenuArt/anh_nen.png"?2048:assetPath.Contains("pop up window")?256:assetPath.Contains("icon panel")?64:128,importer.maxTextureSize,assetPath);
            Assert.AreEqual(FilterMode.Bilinear,importer.filterMode,assetPath);
            Assert.AreEqual(TextureWrapMode.Clamp,importer.wrapMode,assetPath);Assert.IsTrue(importer.alphaIsTransparency,assetPath);
        }
        foreach(var tone in new[]{"green","yellow","red"})foreach(var state in new[]{"idle","hover","clicked"})
            Assert.IsNotNull(Resources.Load<Texture2D>("MenuArt/"+tone+"/button wider "+state),tone+" "+state);
        Assert.IsTrue(File.Exists(Path.Combine(artRoot,"ChequeredInk-LICENSE.txt")));
    }
}
