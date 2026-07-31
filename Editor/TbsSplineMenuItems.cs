using UnityEditor;
using UnityEngine;
using TBSplineS;

namespace TBSplineS.Editor
{
    static class TbsSplineMenuItems
    {
        [MenuItem("GameObject/TBSplineS/Spline Computer", false, 10)]
        static void CreateComputer(MenuCommand command)
        {
            var existing = Object.FindFirstObjectByType<TbsSplineComputer>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                return;
            }
            var go = new GameObject("Spline Computer", typeof(TbsSplineComputer));
            var parent = command.context as GameObject;
            if (parent != null)
            {
                GameObjectUtility.SetParentAndAlign(go, parent);
            }
            else if (SceneView.lastActiveSceneView != null)
            {
                go.transform.position = SceneView.lastActiveSceneView.pivot;
            }
            Undo.RegisterCreatedObjectUndo(go, "Create Spline Computer");
            Selection.activeGameObject = go;
        }
    }
}
