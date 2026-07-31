using UnityEditor;
using UnityEngine;
using TBSplineS;

namespace TBSplineS.Editor
{
    [CustomEditor(typeof(TbsSplineFollower))]
    [CanEditMultipleObjects]
    public sealed class TbsSplineFollowerEditor : UnityEditor.Editor
    {
        SerializedProperty _computer;
        SerializedProperty _splineId;

        void OnEnable()
        {
            _computer = serializedObject.FindProperty("_computer");
            _splineId = serializedObject.FindProperty("_splineId");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            TbsInspectorGUI.Header("Spline Follower");
            EditorGUILayout.PropertyField(_computer);
            TbsSplineIdDropdown.Draw(_splineId, _computer.objectReferenceValue as TbsSplineComputer);
            DrawPropertiesExcluding(serializedObject, "m_Script", "_computer", "_splineId");
            serializedObject.ApplyModifiedProperties();

            if (targets.Length == 1)
            {
                var follower = (TbsSplineFollower)target;
                if (follower.Computer != null)
                {
                    float length = follower.Length;
                    if (length > 0f)
                    {
                        EditorGUI.BeginChangeCheck();
                        float distance = EditorGUILayout.Slider("Preview Distance", follower.Distance, 0f, length);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(follower.transform, "Scrub Follower");
                            follower.Distance = distance;
                        }
                    }
                }
            }
            GUILayout.Space(4f);
            if (TbsInspectorGUI.SecondaryButton("Edit in Scene")) TbsSplineEditorActions.ActivateEditTool();
        }
    }
}
