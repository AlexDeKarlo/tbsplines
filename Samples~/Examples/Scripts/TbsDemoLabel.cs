using UnityEngine;

[ExecuteAlways]
public class TbsDemoLabel : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
#if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
            cam = UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
        if (cam == null) return;
        Vector3 dir = transform.position - cam.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.LookRotation(dir);
    }
}
