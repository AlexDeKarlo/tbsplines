using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// How the open ends of a tube are closed off.
    /// </summary>
    public enum TbsTubeCap
    {
        /// <summary>Ends are left open.</summary>
        None,

        /// <summary>Ends are closed with a flat disc. Skipped on closed splines and on partial revolutions.</summary>
        Flat
    }

    /// <summary>
    /// Builds a tube of geometry along the spline: pipes, cables, tunnels and wires. Set the revolve angle
    /// below a full turn to produce an open trough or half-pipe instead.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("TBSplineS/Tube Generator")]
    public sealed class TbsTubeGenerator : TbsMeshGenerator
    {
        [SerializeField, Min(3)] int _sides = 12;
        [SerializeField, Range(1f, 360f)] float _revolve = 360f;
        [SerializeField] TbsTubeCap _capMode = TbsTubeCap.Flat;
        [SerializeField] float _uvTwist;

        /// <summary>Number of segments around the tube. Higher values give a rounder profile at more cost.</summary>
        public int Sides
        {
            get => _sides;
            set { _sides = Mathf.Max(3, value); SetDirty(); }
        }

        protected override void GenerateMesh(int sampleCount)
        {
            int sides = Mathf.Max(3, _sides);
            int ringVerts = sides + 1;
            float revolve = Mathf.Clamp(_revolve, 1f, 360f);
            bool fullTube = revolve >= 359.999f;
            float totalLength = Length;
            TbsSample sample = default;
            float distance = 0f;
            Vector3 prevPos = Vector3.zero;

            Vector3 startCenter = Vector3.zero;
            Vector3 startTangent = Vector3.forward;
            Vector3 endCenter = Vector3.zero;
            Vector3 endTangent = Vector3.forward;
            Color startColor = Color.white;
            Color endColor = Color.white;

            for (int i = 0; i < sampleCount; i++)
            {
                float localT = sampleCount > 1 ? (float)i / (sampleCount - 1) : 0f;
                Evaluate(localT, ref sample);
                if (i > 0) distance += Vector3.Distance(prevPos, sample.Position);
                prevPos = sample.Position;

                OrientedFrame(sample, out Vector3 right, out Vector3 up);
                Vector3 tangent = sample.Tangent.sqrMagnitude > TbsSplineMath.Epsilon ? sample.Tangent.normalized : Vector3.forward;
                float radius = SampleWidth(sample) * 0.5f;
                Color color = SampleColor(sample);
                Vector3 center = sample.Position + right * _offset.x + up * _offset.y + tangent * _offset.z;
                float v = MapV(localT, distance, totalLength);

                for (int k = 0; k < ringVerts; k++)
                {
                    float f = (float)k / sides;
                    float ang = f * revolve * Mathf.Deg2Rad;
                    Vector3 radial = right * Mathf.Cos(ang) + up * Mathf.Sin(ang);
                    _vertices.Add(center + radial * radius);
                    _normals.Add(radial);
                    _uv.Add(ApplyUvTransform(new Vector2(f + _uvTwist * localT, v)));
                    _colors.Add(color);
                }

                if (i == 0) { startCenter = center; startTangent = tangent; startColor = color; }
                if (i == sampleCount - 1) { endCenter = center; endTangent = tangent; endColor = color; }
            }

            for (int i = 0; i < sampleCount - 1; i++)
            {
                int r0 = i * ringVerts;
                int r1 = (i + 1) * ringVerts;
                for (int k = 0; k < sides; k++)
                {
                    _triangles.Add(r0 + k);
                    _triangles.Add(r0 + k + 1);
                    _triangles.Add(r1 + k);
                    _triangles.Add(r0 + k + 1);
                    _triangles.Add(r1 + k + 1);
                    _triangles.Add(r1 + k);
                }
            }

            if (_capMode == TbsTubeCap.Flat && fullTube && !IsClosed)
            {
                AddCap(0, sides, startCenter, -startTangent, startColor, true);
                AddCap((sampleCount - 1) * ringVerts, sides, endCenter, endTangent, endColor, false);
            }
        }

        void AddCap(int ringStart, int sides, Vector3 center, Vector3 normal, Color color, bool reverse)
        {
            int centerIndex = _vertices.Count;
            _vertices.Add(center);
            _normals.Add(normal);
            _uv.Add(ApplyUvTransform(new Vector2(0.5f, 0.5f)));
            _colors.Add(color);
            for (int k = 0; k < sides; k++)
            {
                int a = ringStart + k;
                int b = ringStart + k + 1;
                if (reverse)
                {
                    _triangles.Add(centerIndex);
                    _triangles.Add(b);
                    _triangles.Add(a);
                }
                else
                {
                    _triangles.Add(centerIndex);
                    _triangles.Add(a);
                    _triangles.Add(b);
                }
            }
        }
    }
}
