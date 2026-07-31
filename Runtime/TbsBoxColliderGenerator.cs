using System.Collections.Generic;
using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// Lines the spline with a chain of child <see cref="BoxCollider"/> objects, one per sampled segment. Cheaper
    /// at runtime than a mesh collider and works with non-convex paths, at the cost of a coarser surface.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("TBSplineS/Box Collider Generator")]
    public sealed class TbsBoxColliderGenerator : TbsSplineUser
    {
        [SerializeField] Vector2 _boxSize = Vector2.one;
        [SerializeField] bool _useSplineSize = true;

        readonly List<BoxCollider> _boxes = new List<BoxCollider>();

        /// <summary>Cross-section of each box: X is width across the spline, Y is height.</summary>
        public Vector2 BoxSize
        {
            get => _boxSize;
            set { _boxSize = value; SetDirty(); }
        }

        /// <summary>Number of boxes currently spawned.</summary>
        public int BoxCount => _boxes.Count;

        protected override void PostBuild()
        {
            GatherExisting();
            TbsSplineCache cache = ResolveCache();
            int n = cache != null ? SampleCount : 0;
            int segments = n >= 2 ? n - 1 : 0;

            while (_boxes.Count > segments)
            {
                BoxCollider extra = _boxes[_boxes.Count - 1];
                _boxes.RemoveAt(_boxes.Count - 1);
                DestroyObject(extra != null ? extra.gameObject : null);
            }
            while (_boxes.Count < segments)
            {
                var go = new GameObject("TBS BoxCollider");
                go.transform.SetParent(transform, false);
                go.AddComponent<TbsSpawnedMarker>();
                _boxes.Add(go.AddComponent<BoxCollider>());
            }
            if (segments == 0) return;

            TbsSample a = default;
            TbsSample b = default;
            for (int i = 0; i < segments; i++)
            {
                Evaluate((float)i / (n - 1), ref a);
                Evaluate((float)(i + 1) / (n - 1), ref b);
                BoxCollider box = _boxes[i];
                Transform t = box.transform;
                Vector3 dir = b.Position - a.Position;
                float length = dir.magnitude;
                t.position = (a.Position + b.Position) * 0.5f;
                t.localScale = Vector3.one;
                if (length > TbsSplineMath.Epsilon)
                    t.rotation = Quaternion.LookRotation(dir / length, Vector3.Slerp(a.Up, b.Up, 0.5f));
                float width = _boxSize.x * (_useSplineSize ? (a.Size + b.Size) * 0.5f : 1f);
                box.center = Vector3.zero;
                box.size = new Vector3(width, _boxSize.y, Mathf.Max(length, 1e-4f));
            }
        }

        void GatherExisting()
        {
            _boxes.Clear();
            var markers = GetComponentsInChildren<TbsSpawnedMarker>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] == null) continue;
                var box = markers[i].GetComponent<BoxCollider>();
                if (box != null) _boxes.Add(box);
            }
        }

        static void DestroyObject(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
