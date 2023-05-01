using System.Collections.Generic;
using UnityEditor;

namespace OccaSoftware.Buto.Editor
{
    public class FogDensityMaskMenuItem : EditorWindow
    {
        private static string objectName = "Buto Fog Density Mask";
        private static List<System.Type> types = new List<System.Type>()
        {
            typeof(Buto.FogDensityMask)
        };

        [MenuItem("GameObject/Effects/Buto Fog Density Mask")]
        public static void CreateFogDensityMask()
        {
            ButoEditorCommon.CreateChildAndSelect(objectName, types);
        }
    }
}
