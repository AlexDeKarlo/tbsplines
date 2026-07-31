using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// Drives an <see cref="EdgeCollider2D"/> so its outline follows the spline, for 2D ground and rails.
    /// Only the X and Y axes of the spline are used.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(EdgeCollider2D))]
    [AddComponentMenu("TBSplineS/Edge Collider 2D Generator")]
    public sealed class TbsEdgeColliderGenerator : TbsSplineUser
    {
        [SerializeField] float _offset;

        /// <summary>Sideways shift of the outline from the spline, in world units.</summary>
        public float Offset
        {
            get => _offset;
            set { _offset = value; SetDirty(); }
        }

        protected override void PostBuild()
        {
            var edge = GetComponent<EdgeCollider2D>();
            if (edge == null) return;
            int n = SampleCount;
            if (n < 2)
            {
                edge.points = new[] { Vector2.zero, Vector2.right };
                return;
            }

            var points = new Vector2[n];
            TbsSample sample = default;
            Matrix4x4 w2l = transform.worldToLocalMatrix;
            for (int i = 0; i < n; i++)
            {
                float localT = n > 1 ? (float)i / (n - 1) : 0f;
                Evaluate(localT, ref sample);
                Vector3 world = sample.Position + sample.Right * _offset;
                Vector3 local = w2l.MultiplyPoint3x4(world);
                points[i] = new Vector2(local.x, local.y);
            }
            edge.points = points;
        }
    }
}
