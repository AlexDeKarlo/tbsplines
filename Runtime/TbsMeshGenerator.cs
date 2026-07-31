using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TBSplineS
{
    /// <summary>
    /// How texture coordinates are laid out along the generated geometry.
    /// </summary>
    public enum TbsUvMode
    {
        /// <summary>V runs 0 to 1 over the visible span, spaced by knot parameter, so uneven knots stretch the texture.</summary>
        Clamp,

        /// <summary>V runs 0 to 1 over the visible span, spaced by real distance, so the texture keeps an even pace.</summary>
        UniformClamp,

        /// <summary>Like <see cref="Clamp"/>, but V keeps the coordinates of the full spline so clipping does not slide the texture.</summary>
        Clip,

        /// <summary>Like <see cref="UniformClamp"/>, but V keeps the coordinates of the full spline.</summary>
        UniformClip
    }

    /// <summary>
    /// Base class for components that build a mesh along a spline. Derive from it and implement
    /// <see cref="GenerateMesh"/> to add your own geometry; the shared width, color, UV, normal and collider
    /// handling comes for free.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public abstract class TbsMeshGenerator : TbsSplineUser
    {
        [SerializeField] protected float _size = 1f;
        [SerializeField] protected Color _color = Color.white;
        [SerializeField] protected Vector3 _offset;
        [SerializeField] protected float _rotation;
        [SerializeField] protected bool _useSplineSize = true;
        [SerializeField] protected bool _useSplineColor = true;
        [SerializeField] protected TbsUvMode _uvMode = TbsUvMode.Clamp;
        [SerializeField] protected Vector2 _uvScale = Vector2.one;
        [SerializeField] protected Vector2 _uvOffset;
        [SerializeField] protected float _uvRotation;
        [SerializeField] protected bool _doubleSided;
        [SerializeField] protected bool _flipFaces;
        [SerializeField] protected bool _recalculateNormals = true;
        [SerializeField] protected bool _calculateTangents = true;
        [SerializeField] protected bool _use32BitIndices;
        [SerializeField] protected bool _generateMeshCollider;

        /// <summary>Vertex positions being built, in world space. Cleared before each generation pass.</summary>
        protected readonly List<Vector3> _vertices = new List<Vector3>();

        /// <summary>Vertex normals being built, in world space.</summary>
        protected readonly List<Vector3> _normals = new List<Vector3>();

        /// <summary>Texture coordinates being built.</summary>
        protected readonly List<Vector2> _uv = new List<Vector2>();

        /// <summary>Vertex colors being built.</summary>
        protected readonly List<Color> _colors = new List<Color>();

        /// <summary>Triangle indices being built, three per triangle.</summary>
        protected readonly List<int> _triangles = new List<int>();

        Mesh _mesh;

        /// <summary>Base width of the geometry, multiplied by each sample's size when spline size is used.</summary>
        public float Size { get => _size; set { _size = value; SetDirty(); } }

        /// <summary>Base vertex color, multiplied by each sample's color when spline color is used.</summary>
        public Color Color { get => _color; set { _color = value; SetDirty(); } }

        /// <summary>How texture coordinates are laid out along the geometry.</summary>
        public TbsUvMode UvMode { get => _uvMode; set { _uvMode = value; SetDirty(); } }

        /// <summary>Duplicates the geometry with flipped winding so it is visible from both sides.</summary>
        public bool DoubleSided { get => _doubleSided; set { _doubleSided = value; SetDirty(); } }

        /// <summary>Keeps a <see cref="MeshCollider"/> on this object in sync with the generated mesh.</summary>
        public bool GenerateMeshCollider { get => _generateMeshCollider; set { _generateMeshCollider = value; SetDirty(); } }

        /// <summary>
        /// The generated mesh. It is owned by this component and destroyed with it, so do not store it in an
        /// asset; copy it with <see cref="Object.Instantiate(Object)"/> if you need to keep it.
        /// </summary>
        public Mesh Mesh => _mesh;

        /// <summary>
        /// Fills the vertex and triangle lists for one build. Implement this in your own generator: append to
        /// the protected lists in world space, and the base class handles the rest.
        /// </summary>
        /// <param name="sampleCount">Number of samples to walk along the spline. Always at least 2.</param>
        protected abstract void GenerateMesh(int sampleCount);

        protected override void Build()
        {
            _vertices.Clear();
            _normals.Clear();
            _uv.Clear();
            _colors.Clear();
            _triangles.Clear();
            if (ResolveCache() == null) return;
            int n = SampleCount;
            if (n < 2) return;
            GenerateMesh(n);
        }

        protected override void PostBuild()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter == null) return;
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "TBSplineS " + GetType().Name, hideFlags = HideFlags.DontSave };
            }

            Matrix4x4 w2l = transform.worldToLocalMatrix;
            for (int i = 0; i < _vertices.Count; i++) _vertices[i] = w2l.MultiplyPoint3x4(_vertices[i]);
            for (int i = 0; i < _normals.Count; i++) _normals[i] = w2l.MultiplyVector(_normals[i]).normalized;

            if (_doubleSided) MakeDoubleSided();
            else if (_flipFaces) FlipFaces();

            _mesh.Clear();
            _mesh.indexFormat = _use32BitIndices || _vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _mesh.SetVertices(_vertices);
            if (_colors.Count == _vertices.Count) _mesh.SetColors(_colors);
            if (_uv.Count == _vertices.Count) _mesh.SetUVs(0, _uv);
            _mesh.SetTriangles(_triangles, 0);
            if (_recalculateNormals || _normals.Count != _vertices.Count) _mesh.RecalculateNormals();
            else _mesh.SetNormals(_normals);
            if (_calculateTangents) _mesh.RecalculateTangents();
            _mesh.RecalculateBounds();
            filter.sharedMesh = _mesh;

            if (_generateMeshCollider)
            {
                var collider = GetComponent<MeshCollider>();
                if (collider == null) collider = gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = null;
                collider.sharedMesh = _mesh;
            }
        }

        /// <summary>
        /// Returns the cross-section axes at a sample, with the generator's roll applied.
        /// </summary>
        /// <param name="sample">Sample to take the frame from.</param>
        /// <param name="right">Receives the sideways axis.</param>
        /// <param name="up">Receives the up axis.</param>
        protected void OrientedFrame(in TbsSample sample, out Vector3 right, out Vector3 up)
        {
            Vector3 tangent = sample.Tangent.sqrMagnitude > TbsSplineMath.Epsilon ? sample.Tangent.normalized : Vector3.forward;
            right = sample.Right;
            up = sample.Up;
            if (Mathf.Abs(_rotation) > TbsSplineMath.Epsilon)
            {
                Quaternion roll = Quaternion.AngleAxis(_rotation, tangent);
                right = roll * right;
                up = roll * up;
            }
        }

        /// <summary>
        /// Appends one vertex to every channel at once.
        /// </summary>
        /// <param name="worldPos">Vertex position in world space.</param>
        /// <param name="worldNormal">Vertex normal in world space.</param>
        /// <param name="uv">Texture coordinate, normally passed through <see cref="ApplyUvTransform"/> first.</param>
        /// <param name="color">Vertex color.</param>
        /// <returns>Index of the new vertex, for use in the triangle list.</returns>
        protected int AddVertex(Vector3 worldPos, Vector3 worldNormal, Vector2 uv, Color color)
        {
            int index = _vertices.Count;
            _vertices.Add(worldPos);
            _normals.Add(worldNormal);
            _uv.Add(uv);
            _colors.Add(color);
            return index;
        }

        /// <summary>Width to use at a sample, combining <see cref="Size"/> with the per-point size if enabled.</summary>
        protected float SampleWidth(in TbsSample sample) => _size * (_useSplineSize ? sample.Size : 1f);

        /// <summary>Color to use at a sample, combining <see cref="Color"/> with the per-point color if enabled.</summary>
        protected Color SampleColor(in TbsSample sample) => _color * (_useSplineColor ? sample.Color : Color.white);

        /// <summary>
        /// Returns the V coordinate for a sample according to <see cref="UvMode"/>.
        /// </summary>
        /// <param name="localT">Position along the visible span, from 0 to 1.</param>
        /// <param name="distanceAlong">Distance walked so far, in world units.</param>
        /// <param name="totalLength">Total length of the visible span, in world units.</param>
        protected float MapV(float localT, float distanceAlong, float totalLength)
        {
            switch (_uvMode)
            {
                case TbsUvMode.Clamp:
                    return localT;
                case TbsUvMode.UniformClamp:
                    return totalLength > TbsSplineMath.Epsilon ? distanceAlong / totalLength : 0f;
                case TbsUvMode.Clip:
                    return UnclipPercent(localT);
                default:
                    return UnclipPercent(localT);
            }
        }

        /// <summary>
        /// Applies the generator's UV rotation, scale and offset. Pass every texture coordinate through this so
        /// your generator honours the shared UV settings.
        /// </summary>
        protected Vector2 ApplyUvTransform(Vector2 uv)
        {
            if (Mathf.Abs(_uvRotation) > TbsSplineMath.Epsilon)
            {
                float rad = _uvRotation * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);
                uv = new Vector2(uv.x * cos - uv.y * sin, uv.x * sin + uv.y * cos);
            }
            uv.x = uv.x * _uvScale.x + _uvOffset.x;
            uv.y = uv.y * _uvScale.y + _uvOffset.y;
            return uv;
        }

        void MakeDoubleSided()
        {
            int baseVerts = _vertices.Count;
            for (int i = 0; i < baseVerts; i++)
            {
                _vertices.Add(_vertices[i]);
                if (_normals.Count == baseVerts) _normals.Add(-_normals[i]);
                if (_uv.Count == baseVerts) _uv.Add(_uv[i]);
                if (_colors.Count == baseVerts) _colors.Add(_colors[i]);
            }
            int baseTris = _triangles.Count;
            for (int t = 0; t < baseTris; t += 3)
            {
                _triangles.Add(baseVerts + _triangles[t]);
                _triangles.Add(baseVerts + _triangles[t + 2]);
                _triangles.Add(baseVerts + _triangles[t + 1]);
            }
        }

        void FlipFaces()
        {
            for (int t = 0; t < _triangles.Count; t += 3)
            {
                int tmp = _triangles[t + 1];
                _triangles[t + 1] = _triangles[t + 2];
                _triangles[t + 2] = tmp;
            }
            for (int i = 0; i < _normals.Count; i++) _normals[i] = -_normals[i];
        }

        protected virtual void OnDestroy()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
            }
        }
    }
}
