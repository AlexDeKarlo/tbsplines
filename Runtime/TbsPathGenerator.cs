using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// Builds a flat ribbon of geometry along the spline: roads, paths and racing tracks. An optional shape
    /// curve lifts the cross-section, which is how gutters and cambered roads are made.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("TBSplineS/Path Generator")]
    public sealed class TbsPathGenerator : TbsMeshGenerator
    {
        [SerializeField, Min(1)] int _slices = 1;
        [SerializeField] bool _useShapeCurve;
        [SerializeField] AnimationCurve _shape = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        [SerializeField] float _shapeExposure = 1f;

        /// <summary>
        /// Number of quads across the ribbon. Raise it when the shape curve needs more resolution or the
        /// surface is lit per-vertex.
        /// </summary>
        public int Slices
        {
            get => _slices;
            set { _slices = Mathf.Max(1, value); SetDirty(); }
        }

        protected override void GenerateMesh(int sampleCount)
        {
            int slices = Mathf.Max(1, _slices);
            int cols = slices + 1;
            float totalLength = Length;
            TbsSample sample = default;
            float distance = 0f;
            Vector3 prevPos = Vector3.zero;

            for (int i = 0; i < sampleCount; i++)
            {
                float localT = sampleCount > 1 ? (float)i / (sampleCount - 1) : 0f;
                Evaluate(localT, ref sample);
                if (i > 0) distance += Vector3.Distance(prevPos, sample.Position);
                prevPos = sample.Position;

                OrientedFrame(sample, out Vector3 right, out Vector3 up);
                Vector3 tangent = sample.Tangent.sqrMagnitude > TbsSplineMath.Epsilon ? sample.Tangent.normalized : Vector3.forward;
                float width = SampleWidth(sample);
                Color color = SampleColor(sample);
                float v = MapV(localT, distance, totalLength);
                Vector3 center = sample.Position + right * _offset.x + up * _offset.y + tangent * _offset.z;

                for (int j = 0; j < cols; j++)
                {
                    float u = (float)j / slices;
                    float across = (u - 0.5f) * width;
                    float lift = _useShapeCurve ? _shape.Evaluate(u) * _shapeExposure : 0f;
                    Vector3 pos = center + right * across + up * lift;
                    _vertices.Add(pos);
                    _normals.Add(up);
                    _uv.Add(ApplyUvTransform(new Vector2(u, v)));
                    _colors.Add(color);
                }
            }

            for (int i = 0; i < sampleCount - 1; i++)
            {
                int row0 = i * cols;
                int row1 = (i + 1) * cols;
                for (int j = 0; j < slices; j++)
                {
                    _triangles.Add(row0 + j);
                    _triangles.Add(row1 + j);
                    _triangles.Add(row0 + j + 1);
                    _triangles.Add(row0 + j + 1);
                    _triangles.Add(row1 + j);
                    _triangles.Add(row1 + j + 1);
                }
            }
        }
    }
}
