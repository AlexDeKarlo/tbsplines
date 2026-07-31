using UnityEditor;
using UnityEngine;
using TBSplineS;

namespace TBSplineS.Editor
{
    [CustomEditor(typeof(TbsSplineComputer))]
    public sealed class TbsSplineComputerEditor : UnityEditor.Editor
    {
        int _computersInScene;
        bool _advanced;

        void OnEnable()
        {
            _computersInScene = Object.FindObjectsByType<TbsSplineComputer>(FindObjectsSortMode.None).Length;
            _advanced = EditorPrefs.GetBool("TBSplineS.AdvancedFoldout", false);
        }

        public override void OnInspectorGUI()
        {
            var computer = (TbsSplineComputer)target;
            TbsInspectorGUI.Header($"{computer.SplineCount} splines · {computer.GetTotalLength():F1} m");
            if (TbsInspectorGUI.PrimaryButton("Edit in Scene   (Alt+E)", 34f))
                TbsSplineEditorActions.ActivateEditTool();

            bool adv = EditorGUILayout.Foldout(_advanced, "Settings", true);
            if (adv != _advanced) { _advanced = adv; EditorPrefs.SetBool("TBSplineS.AdvancedFoldout", adv); }
            if (_advanced)
            {
                EditorGUI.indentLevel++;
                TbsInspectorGUI.Section("Editor Grid");
                EditorGUI.BeginChangeCheck();
                float height = EditorGUILayout.FloatField("Grid Height (Y)", computer.EditorGridHeight);
                float cell = EditorGUILayout.FloatField("Grid Cell Size", computer.EditorGridSize);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(computer, "Edit Grid");
                    computer.EditorGridHeight = height;
                    computer.EditorGridSize = Mathf.Max(0.05f, cell);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(computer);
                    SceneView.RepaintAll();
                }
                TbsInspectorGUI.Section("Display");
                EditorGUI.BeginChangeCheck();
                bool renderAll = EditorGUILayout.Toggle(new GUIContent("Render All Handles", "Draw points/numbers/guides for EVERY spline, not just the selected one"), computer.EditorRenderAll);
                bool guides = EditorGUILayout.Toggle(new GUIContent("Always Show Height Guides", "Vertical dashed line from every knot down to the grid plane"), computer.EditorShowHeightGuides);
                bool numbers = EditorGUILayout.Toggle(new GUIContent("Show Knot Numbers", "Numbered badges on the knots of the selected spline"), computer.EditorShowNumbers);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(computer, "Edit Display Settings");
                    computer.EditorRenderAll = renderAll;
                    computer.EditorShowHeightGuides = guides;
                    computer.EditorShowNumbers = numbers;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(computer);
                    SceneView.RepaintAll();
                }
                EditorGUI.indentLevel--;
            }
            if (_computersInScene > 1)
                EditorGUILayout.HelpBox("Keep a single Spline Computer per scene.", MessageType.Warning);
        }
    }
}
