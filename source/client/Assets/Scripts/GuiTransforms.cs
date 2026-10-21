using UnityEngine;
namespace Tank  {
    public static class GuiTransforms  {
        // Compose rotation in logical GUI space before the outer resolution scale.
        public static void Rotate(float degrees,Vector2 pivot) {
            Matrix4x4 outer=GUI.matrix;
            GUI.matrix=Matrix4x4.identity;
            // Unity unclips the local pivot through BeginGroup before constructing rotation.
            GUIUtility.RotateAroundPivot(degrees,pivot);
            GUI.matrix=outer*GUI.matrix;
        }
    }
}
