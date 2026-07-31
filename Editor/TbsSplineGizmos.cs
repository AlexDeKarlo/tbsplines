using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using TBSplineS;

namespace TBSplineS.Editor
{
    static class TbsSplineGizmos
    {
        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected, typeof(TbsSplineComputer))]
        static void DrawSplineGizmo(TbsSplineComputer computer, GizmoType gizmoType)
        {
            if ((gizmoType & GizmoType.Selected) != 0 && ToolManager.activeToolType == typeof(TbsSplineComputerTool)) return;
            TbsSplineSceneRenderer.Get(computer).DrawIdle();
        }

        [DrawGizmo(GizmoType.Selected, typeof(TbsSplineFollower))]
        static void DrawFollowerGizmo(TbsSplineFollower follower, GizmoType gizmoType)
        {
            if (follower.Computer == null) return;
            int index = follower.Computer.IndexOfSplineId(follower.SplineId);
            if (index < 0) return;
            TbsSample sample = default;
            follower.Computer.EvaluateAtDistance(index, follower.Distance, ref sample);
            float size = HandleUtility.GetHandleSize(sample.Position);
            Handles.color = new Color(0.35f, 1f, 0.45f, 0.9f);
            Handles.SphereHandleCap(0, sample.Position, Quaternion.identity, size * 0.15f, EventType.Repaint);
            Handles.DrawLine(sample.Position, sample.Position + sample.Tangent * size);
        }
    }
}
