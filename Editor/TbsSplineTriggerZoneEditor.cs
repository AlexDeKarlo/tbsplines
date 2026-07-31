using UnityEditor;
using UnityEngine;
using TBSplineS;

namespace TBSplineS.Editor
{
    [CustomEditor(typeof(TbsSplineTriggerZone))]
    [CanEditMultipleObjects]
    public sealed class TbsSplineTriggerZoneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var zone = target as TbsSplineTriggerZone;
            TbsInspectorGUI.Header("Spline Trigger");

            serializedObject.Update();
            TbsInspectorGUI.Section("Spline");
            SerializedProperty computerProp = serializedObject.FindProperty("_computer");
            SerializedProperty idProp = serializedObject.FindProperty("_splineId");
            EditorGUILayout.PropertyField(computerProp, new GUIContent("Spline Computer"));
            if (zone != null && zone.Computer != null && !computerProp.hasMultipleDifferentValues)
                TbsSplineIdDropdown.Draw(idProp, zone.Computer);
            else
                EditorGUILayout.PropertyField(idProp, new GUIContent("Spline Id"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_position"), new GUIContent("Position", "Drag the marker in the Scene view or slide here"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_direction"), new GUIContent("Direction"));

            TbsInspectorGUI.Section("Firing");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_fireOnce"), new GUIContent("Fire Once"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_cooldown"), new GUIContent("Cooldown (s)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_onlyFollower"), new GUIContent("Only Follower"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_markerColor"), new GUIContent("Marker Color"));

            TbsInspectorGUI.Section("Events");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_onFirstCross"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_onRepeatCross"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_onCrossed"));
            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying && zone != null)
                EditorGUILayout.LabelField("Crossings", zone.CrossCount.ToString());
        }

        void OnSceneGUI()
        {
            var zone = target as TbsSplineTriggerZone;
            if (zone == null || zone.Computer == null) return;
            if (!zone.TryGetWorldPosition(out Vector3 world, out Vector3 tangent)) return;

            float size = HandleUtility.GetHandleSize(world) * 0.16f;
            Handles.color = zone.MarkerColor;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(world, size * 1.4f, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                int index = zone.Computer.IndexOfSplineId(zone.SplineId);
                if (index < 0 && zone.Computer.SplineCount > 0) index = 0;
                if (index >= 0)
                {
                    TbsSample near = default;
                    float t = zone.Computer.GetNearestPoint(index, moved, ref near);
                    Undo.RecordObject(zone, "Move Trigger");
                    zone.Position = t;
                    EditorUtility.SetDirty(zone);
                }
            }
            Handles.color = Color.white;
            Vector3 dir = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : Vector3.forward;
            if (zone.Direction != TbsTriggerType.Backward)
                Handles.ArrowHandleCap(0, world + dir * size * 2f, Quaternion.LookRotation(dir), size * 4f, EventType.Repaint);
            if (zone.Direction != TbsTriggerType.Forward)
                Handles.ArrowHandleCap(0, world - dir * size * 2f, Quaternion.LookRotation(-dir), size * 4f, EventType.Repaint);
        }
    }
}
