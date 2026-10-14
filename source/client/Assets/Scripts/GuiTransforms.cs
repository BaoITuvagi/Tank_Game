using UnityEngine;
namespace Tank  {
    public static class GuiTransforms  {
        // Compose rotation in logical GUI space before the outer resolution scale.
        public static void Rotate(float degrees,Vector2 pivot) {
            GUI.matrix=GUI.matrix*Matrix4x4.TRS(new Vector3(pivot.x,pivot.y,0),Quaternion.AngleAxis(degrees,Vector3.forward),Vector3.one)*Matrix4x4.Translate(new Vector3(-pivot.x,-pivot.y,0));
        }
    }
}
