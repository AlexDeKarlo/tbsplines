using System.IO;
using UnityEditor;
using UnityEngine;

namespace TBSplineS.Editor
{
    public static class TbsComponentIcons
    {
        static readonly (string type, string icon)[] Map =
        {
            ("TbsSplineComputer", "comp-spline-computer"),
            ("TbsSplineFollower", "comp-follower"),
            ("TbsSplineProjector", "comp-projector"),
            ("TbsSplinePositioner", "comp-positioner"),
            ("TbsPathGenerator", "comp-path-gen"),
            ("TbsTubeGenerator", "comp-tube-gen"),
            ("TbsSplineMesh", "comp-spline-mesh"),
            ("TbsSurfaceGenerator", "comp-surface-gen"),
            ("TbsObjectController", "comp-object-controller"),
            ("TbsBoxColliderGenerator", "comp-box-collider-gen"),
            ("TbsEdgeColliderGenerator", "comp-edge-collider-gen"),
            ("TbsLengthCalculator", "comp-length-calculator"),
            ("TbsSplineTriggerZone", "comp-trigger")
        };

        [MenuItem("TBSplineS/Dev/Assign Component Icons")]
        public static void AssignAll()
        {
            int assigned = 0;
            foreach ((string type, string icon) in Map)
            {
                string scriptPath = FindAssetPath(type, "t:MonoScript", ".cs");
                string iconPath = FindAssetPath(icon, "t:Texture2D", ".png");
                if (scriptPath == null || iconPath == null)
                {
                    Debug.LogWarning($"TBSplineS: cannot assign icon for {type} (script: {scriptPath ?? "missing"}, icon: {iconPath ?? "missing"})");
                    continue;
                }
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                var importer = AssetImporter.GetAtPath(scriptPath) as MonoImporter;
                if (texture == null || importer == null) continue;
                importer.SetIcon(texture);
                importer.SaveAndReimport();
                assigned++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"TBSplineS: assigned {assigned}/{Map.Length} component icons");
        }

        static string FindAssetPath(string name, string filter, string extension)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " " + filter))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name && path.EndsWith(extension)) return path;
            }
            return null;
        }
    }
}
