using UnityEditor;
using UnityEngine;
using TBSplineS;

namespace TBSplineS.Editor
{
    [CustomEditor(typeof(TbsSplineUser), true)]
    [CanEditMultipleObjects]
    public class TbsSplineUserEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var user = target as TbsSplineUser;
            TbsInspectorGUI.Header(FriendlyName(target.GetType().Name));

            serializedObject.Update();

            SerializedProperty computerProp = serializedObject.FindProperty("_computer");
            SerializedProperty splineIdProp = serializedObject.FindProperty("_splineId");
            TbsInspectorGUI.Section("Spline");
            if (computerProp != null) EditorGUILayout.PropertyField(computerProp, new GUIContent("Spline Computer"));
            if (splineIdProp != null)
            {
                if (user != null && user.Computer != null && computerProp != null && !computerProp.hasMultipleDifferentValues)
                    TbsSplineIdDropdown.Draw(splineIdProp, user.Computer);
                else
                    EditorGUILayout.PropertyField(splineIdProp, new GUIContent("Spline Id"));
            }

            TbsInspectorGUI.Section("Settings");
            DrawPropertiesExcluding(serializedObject, "m_Script", "_computer", "_splineId");

            serializedObject.ApplyModifiedProperties();

            GUILayout.Space(6f);
            if (TbsInspectorGUI.SecondaryButton("Rebuild"))
            {
                foreach (Object t in targets)
                    if (t is TbsSplineUser u) u.RebuildImmediate();
            }
        }

        static string FriendlyName(string typeName)
        {
            if (typeName.StartsWith("Tbs")) typeName = typeName.Substring(3);
            return ObjectNames.NicifyVariableName(typeName);
        }
    }
}
