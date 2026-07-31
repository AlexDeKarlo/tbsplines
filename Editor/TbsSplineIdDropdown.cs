using UnityEditor;
using UnityEngine;
using TBSplineS;

namespace TBSplineS.Editor
{
    public static class TbsSplineIdDropdown
    {
        public static void Draw(SerializedProperty idProperty, TbsSplineComputer computer)
        {
            if (computer == null)
            {
                EditorGUILayout.PropertyField(idProperty, new GUIContent("Spline Id"));
                EditorGUILayout.HelpBox("Assign a Spline Computer to pick a spline.", MessageType.None);
                return;
            }
            if (NeedsIdRepair(computer))
            {
                Undo.RecordObject(computer, "Repair Spline Ids");
                computer.EnsureIds();
                EditorUtility.SetDirty(computer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(computer);
            }
            if (!idProperty.hasMultipleDifferentValues && computer.SplineCount > 0 &&
                computer.IndexOfSplineId(idProperty.intValue) < 0)
            {
                idProperty.serializedObject.Update();
                idProperty.intValue = computer[0].Id;
                idProperty.serializedObject.ApplyModifiedProperties();
            }
            int current = idProperty.intValue;
            Rect rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, new GUIContent("Spline"));
            if (EditorGUI.DropdownButton(rect, new GUIContent(Describe(computer, current)), FocusType.Keyboard))
            {
                Object[] targets = idProperty.serializedObject.targetObjects;
                string path = idProperty.propertyPath;
                var menu = new GenericMenu();
                for (int i = 0; i < computer.SplineCount; i++)
                {
                    int id = computer[i].Id;
                    menu.AddItem(new GUIContent(Describe(computer, id)), id == current, () =>
                    {
                        var fresh = new SerializedObject(targets);
                        SerializedProperty prop = fresh.FindProperty(path);
                        if (prop != null)
                        {
                            prop.intValue = id;
                            fresh.ApplyModifiedProperties();
                        }
                        fresh.Dispose();
                    });
                }
                menu.ShowAsContext();
            }
        }

        static bool NeedsIdRepair(TbsSplineComputer computer)
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < computer.SplineCount; i++)
            {
                TbsSpline spline = computer[i];
                if (spline == null) continue;
                if (spline.Id <= 0 || !seen.Add(spline.Id)) return true;
            }
            return false;
        }

        static string Describe(TbsSplineComputer computer, int id)
        {
            int index = computer.IndexOfSplineId(id);
            if (index < 0) return $"#{id} (missing)";
            TbsSpline spline = computer[index];
            string closed = spline.Closed ? " · closed" : "";
            return $"#{id} · {spline.Count} knots · {computer.GetLength(index):F1} m{closed}";
        }
    }
}
