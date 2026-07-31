using System.Collections.Generic;
using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// Fills the area enclosed by the spline with a flat surface, optionally extruded into a solid. Best suited
    /// to closed splines: lakes, plazas, platforms and building footprints. The outline is triangulated as a fan
    /// from its centroid, so strongly concave shapes may need to be split into several splines.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("TBSplineS/Surface Generator")]
    public sealed class TbsSurfaceGenerator : TbsMeshGenerator
    {
        [SerializeField] float _expand;
        [SerializeField] float _extrude;

        /// <summary>Pushes the outline outwards from the spline, in world units. Negative values shrink it.</summary>
        public float Expand { get => _expand; set { _expand = value; SetDirty(); } }

        /// <summary>Depth pulled down from the surface to make it solid, in world units. Zero leaves it flat.</summary>
        public float Extrude { get => _extrude; set { _extrude = value; SetDirty(); } }

        static readonly List<Vector3> _outline = new List<Vector3>();

        protected override void GenerateMesh(int sampleCount)
        {
            _outline.Clear();
            TbsSample sample = default;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = sampleCount > 1 ? (float)i / (sampleCount - 1) : 0f;
                Evaluate(t, ref sample);
                _outline.Add(sample.Position + sample.Right * _expand);
            }
            if (IsClosed && _outline.Count > 1 && Vector3.Distance(_outline[0], _outline[_outline.Count - 1]) < 1e-4f)
                _outline.RemoveAt(_outline.Count - 1);

            int m = _outline.Count;
            if (m < 3) return;

            Vector3 normal = NewellNormal(_outline);
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < m; i++) centroid += _outline[i];
            centroid /= m;
            PlanarBasis(normal, out Vector3 uAxis, out Vector3 vAxis);
            Color color = _color;

            AddCap(centroid, normal, uAxis, vAxis, color, Vector3.zero, false);

            if (Mathf.Abs(_extrude) > TbsSplineMath.Epsilon)
            {
                Vector3 down = -normal * _extrude;
                AddCap(centroid, -normal, uAxis, vAxis, color, down, true);
                AddWalls(down, color);
            }
        }

        void AddCap(Vector3 centroid, Vector3 normal, Vector3 uAxis, Vector3 vAxis, Color color, Vector3 offset, bool reverse)
        {
            int m = _outline.Count;
            int centerIndex = AddVertex(centroid + offset, normal, new Vector2(0.5f, 0.5f), color);
            int ringStart = _vertices.Count;
            for (int i = 0; i < m; i++)
            {
                Vector3 p = _outline[i] + offset;
                Vector2 uv = ApplyUvTransform(new Vector2(Vector3.Dot(p - centroid, uAxis), Vector3.Dot(p - centroid, vAxis)));
                AddVertex(p, normal, uv, color);
            }
            for (int i = 0; i < m; i++)
            {
                int a = ringStart + i;
                int b = ringStart + (i + 1) % m;
                _triangles.Add(centerIndex);
                if (reverse) { _triangles.Add(b); _triangles.Add(a); }
                else { _triangles.Add(a); _triangles.Add(b); }
            }
        }

        void AddWalls(Vector3 down, Color color)
        {
            int m = _outline.Count;
            for (int i = 0; i < m; i++)
            {
                Vector3 a = _outline[i];
                Vector3 b = _outline[(i + 1) % m];
                Vector3 edge = b - a;
                Vector3 wallNormal = Vector3.Cross(edge, down);
                wallNormal = wallNormal.sqrMagnitude > TbsSplineMath.Epsilon ? wallNormal.normalized : Vector3.up;
                int t0 = AddVertex(a, wallNormal, ApplyUvTransform(new Vector2(0f, 0f)), color);
                int t1 = AddVertex(b, wallNormal, ApplyUvTransform(new Vector2(1f, 0f)), color);
                int b0 = AddVertex(a + down, wallNormal, ApplyUvTransform(new Vector2(0f, 1f)), color);
                int b1 = AddVertex(b + down, wallNormal, ApplyUvTransform(new Vector2(1f, 1f)), color);
                _triangles.Add(t0); _triangles.Add(b0); _triangles.Add(t1);
                _triangles.Add(t1); _triangles.Add(b0); _triangles.Add(b1);
            }
        }

        static Vector3 NewellNormal(List<Vector3> polygon)
        {
            Vector3 n = Vector3.zero;
            int m = polygon.Count;
            for (int i = 0; i < m; i++)
            {
                Vector3 a = polygon[i];
                Vector3 b = polygon[(i + 1) % m];
                n.x += (a.y - b.y) * (a.z + b.z);
                n.y += (a.z - b.z) * (a.x + b.x);
                n.z += (a.x - b.x) * (a.y + b.y);
            }
            return n.sqrMagnitude > TbsSplineMath.Epsilon ? n.normalized : Vector3.up;
        }

        static void PlanarBasis(Vector3 normal, out Vector3 uAxis, out Vector3 vAxis)
        {
            Vector3 reference = Mathf.Abs(normal.y) < 0.99f ? Vector3.up : Vector3.right;
            uAxis = Vector3.Cross(reference, normal).normalized;
            vAxis = Vector3.Cross(normal, uAxis).normalized;
        }
    }
}
