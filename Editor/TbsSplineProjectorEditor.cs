using UnityEditor;
using UnityEngine;
using TBSplineS;

namespace TBSplineS.Editor
{
    [CustomEditor(typeof(TbsSplineProjector))]
    [CanEditMultipleObjects]
    public sealed class TbsSplineProjectorEditor : UnityEditor.Editor
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
            TbsInspectorGUI.Header("Spline Projector");
            EditorGUILayout.PropertyField(_computer);
            TbsSplineIdDropdown.Draw(_splineId, _computer.objectReferenceValue as TbsSplineComputer);
            DrawPropertiesExcluding(serializedObject, "m_Script", "_computer", "_splineId");
            serializedObject.ApplyModifiedProperties();

            GUILayout.Space(4f);
            if (TbsInspectorGUI.PrimaryButton("Project Now", 30f))
            {
                foreach (Object item in targets)
                {
                    var projector = (TbsSplineProjector)item;
                    Undo.RecordObject(projector.transform, "Project On Spline");
                    projector.Snap();
                }
            }
            if (TbsInspectorGUI.SecondaryButton("Edit in Scene")) TbsSplineEditorActions.ActivateEditTool();
        }
    }
}
