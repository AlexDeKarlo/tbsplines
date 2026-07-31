using System;
using System.Collections.Generic;
using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// How a channel lays its source meshes along the spline.
    /// </summary>
    public enum TbsChannelType
    {
        /// <summary>Bends each mesh to follow the curve, so it stretches and deforms along the spline.</summary>
        Extrude,

        /// <summary>Drops each mesh at a point unchanged, keeping its original shape. Suits posts and props.</summary>
        Place
    }

    /// <summary>
    /// How a channel picks the next mesh when several are supplied.
    /// </summary>
    public enum TbsMeshIteration
    {
        /// <summary>Cycles through the meshes in order.</summary>
        Ordered,

        /// <summary>Picks at random, repeatably for a given seed.</summary>
        Random
    }

    /// <summary>
    /// One layer of repeated geometry along the spline. A spline mesh can hold several channels, so a fence's
    /// rails, posts and caps can be built by a single component.
    /// </summary>
    [Serializable]
    public sealed class TbsSplineMeshChannel
    {
        /// <summary>Label shown in the editor.</summary>
        public string Name = "Channel";

        /// <summary>Whether meshes are bent along the spline or dropped at points unchanged.</summary>
        public TbsChannelType Type = TbsChannelType.Extrude;

        /// <summary>Source meshes to repeat. They are consumed according to the iteration mode.</summary>
        public Mesh[] Meshes = Array.Empty<Mesh>();

        /// <summary>Number of repetitions. Ignored when <see cref="AutoCount"/> is on.</summary>
        [Min(1)] public int Count = 1;

        /// <summary>Derives the repetition count from the spline length and the source meshes' depth.</summary>
        public bool AutoCount;

        /// <summary>Where this channel starts along the spline, from 0 to 1.</summary>
        [Range(0f, 1f)] public float ClipFrom;

        /// <summary>Where this channel ends along the spline, from 0 to 1.</summary>
        [Range(0f, 1f)] public float ClipTo = 1f;

        /// <summary>Fraction of each repetition left empty, opening a gap between meshes.</summary>
        [Range(0f, 1f)] public float Spacing;

        /// <summary>How the next mesh is picked when several are supplied.</summary>
        public TbsMeshIteration Iteration = TbsMeshIteration.Ordered;

        /// <summary>Seed for all randomization in this channel. The same seed reproduces the same layout.</summary>
        public int Seed = 12345;

        /// <summary>Shift applied to the source mesh before it is laid along the spline.</summary>
        public Vector3 MeshOffset;

        /// <summary>Rotation applied to the source mesh before it is laid along the spline, in degrees.</summary>
        public Vector3 MeshRotation;

        /// <summary>Scale applied to the source mesh before it is laid along the spline.</summary>
        public Vector3 MeshScale = Vector3.one;

        /// <summary>Rotates each placed mesh randomly between the min and max rotation.</summary>
        public bool RandomRotation;

        /// <summary>Lower bound of the random rotation, in degrees.</summary>
        public Vector3 MinRotation;

        /// <summary>Upper bound of the random rotation, in degrees.</summary>
        public Vector3 MaxRotation;

        /// <summary>Scales each placed mesh randomly between the min and max scale.</summary>
        public bool RandomScale;

        /// <summary>Lower bound of the random scale.</summary>
        public float MinScale = 1f;

        /// <summary>Upper bound of the random scale.</summary>
        public float MaxScale = 1f;

        /// <summary>
        /// Returns the mesh to use for a given repetition, honouring <see cref="Iteration"/>.
        /// </summary>
        /// <param name="repeat">Index of the repetition.</param>
        /// <param name="random">Generator seeded from <see cref="Seed"/>.</param>
        /// <returns>The chosen mesh, or null when no meshes are assigned.</returns>
        public Mesh PickMesh(int repeat, System.Random random)
        {
            if (Meshes == null || Meshes.Length == 0) return null;
            int index = Iteration == TbsMeshIteration.Random ? random.Next(Meshes.Length) : repeat % Meshes.Length;
            return Meshes[index];
        }
    }

    /// <summary>
    /// Repeats your own meshes along the spline, either bent to follow the curve or dropped at points. Several
    /// channels can be layered in one component, which is how fences, guardrails, pipes with brackets and
    /// decorated roads are built.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("TBSplineS/Spline Mesh")]
    public sealed class TbsSplineMesh : TbsMeshGenerator
    {
        [SerializeField] List<TbsSplineMeshChannel> _channels = new List<TbsSplineMeshChannel> { new TbsSplineMeshChannel() };

        static readonly List<Vector3> _srcVerts = new List<Vector3>();
        static readonly List<Vector3> _srcNormals = new List<Vector3>();
        static readonly List<Vector2> _srcUv = new List<Vector2>();
        static readonly List<int> _srcTris = new List<int>();

        /// <summary>Channels layered by this component, in build order.</summary>
        public IReadOnlyList<TbsSplineMeshChannel> Channels => _channels;

        /// <summary>
        /// Appends a channel and rebuilds.
        /// </summary>
        /// <param name="channel">Channel to add. A default one is created when omitted.</param>
        /// <returns>The channel that was added, ready to be configured.</returns>
        public TbsSplineMeshChannel AddChannel(TbsSplineMeshChannel channel = null)
        {
            channel ??= new TbsSplineMeshChannel();
            _channels.Add(channel);
            SetDirty();
            return channel;
        }

        /// <summary>
        /// Removes the channel at the given slot and rebuilds. Out-of-range indices are ignored.
        /// </summary>
        public void RemoveChannel(int index)
        {
            if (index < 0 || index >= _channels.Count) return;
            _channels.RemoveAt(index);
            SetDirty();
        }

        protected override void GenerateMesh(int sampleCount)
        {
            for (int c = 0; c < _channels.Count; c++)
            {
                TbsSplineMeshChannel channel = _channels[c];
                if (channel == null || channel.Meshes == null || channel.Meshes.Length == 0) continue;
                var random = new System.Random(channel.Seed);
                float span = Mathf.Max(0f, channel.ClipTo - channel.ClipFrom);
                if (span <= TbsSplineMath.Epsilon) continue;

                int count = channel.AutoCount ? AutoCount(channel, span) : Mathf.Max(1, channel.Count);
                for (int r = 0; r < count; r++)
                {
                    Mesh mesh = channel.PickMesh(r, random);
                    if (mesh == null) continue;
                    if (channel.Type == TbsChannelType.Extrude)
                        ExtrudeRepeat(channel, mesh, r, count, span);
                    else
                        PlaceRepeat(channel, mesh, r, count, random);
                }
            }
        }

        int AutoCount(TbsSplineMeshChannel channel, float span)
        {
            float meshZ = 0f;
            int valid = 0;
            for (int i = 0; i < channel.Meshes.Length; i++)
            {
                if (channel.Meshes[i] == null) continue;
                meshZ += channel.Meshes[i].bounds.size.z * Mathf.Abs(channel.MeshScale.z);
                valid++;
            }
            if (valid == 0) return 1;
            meshZ /= valid;
            float rangeLength = Length * span;
            return meshZ > TbsSplineMath.Epsilon ? Mathf.Max(1, Mathf.RoundToInt(rangeLength / meshZ)) : 1;
        }

        void LoadSource(TbsSplineMeshChannel channel, Mesh mesh)
        {
            mesh.GetVertices(_srcVerts);
            mesh.GetNormals(_srcNormals);
            mesh.GetUVs(0, _srcUv);
            _srcTris.Clear();
            _srcTris.AddRange(mesh.triangles);
            Quaternion rot = Quaternion.Euler(channel.MeshRotation);
            Matrix4x4 pre = Matrix4x4.TRS(channel.MeshOffset, rot, channel.MeshScale);
            for (int i = 0; i < _srcVerts.Count; i++) _srcVerts[i] = pre.MultiplyPoint3x4(_srcVerts[i]);
            if (_srcNormals.Count == _srcVerts.Count)
                for (int i = 0; i < _srcNormals.Count; i++) _srcNormals[i] = rot * _srcNormals[i];
        }

        void ExtrudeRepeat(TbsSplineMeshChannel channel, Mesh mesh, int repeat, int count, float span)
        {
            LoadSource(channel, mesh);
            float segment = span / count;
            float from = channel.ClipFrom + repeat * segment;
            float to = from + segment * (1f - channel.Spacing);

            float zMin = float.MaxValue, zMax = float.MinValue;
            for (int i = 0; i < _srcVerts.Count; i++)
            {
                float z = _srcVerts[i].z;
                if (z < zMin) zMin = z;
                if (z > zMax) zMax = z;
            }
            float zRange = zMax - zMin;
            if (zRange <= TbsSplineMath.Epsilon) zRange = 1f;

            int baseVertex = _vertices.Count;
            TbsSample sample = default;
            bool hasNormals = _srcNormals.Count == _srcVerts.Count;
            bool hasUv = _srcUv.Count == _srcVerts.Count;
            for (int i = 0; i < _srcVerts.Count; i++)
            {
                Vector3 v = _srcVerts[i];
                float zn = (v.z - zMin) / zRange;
                float localT = Mathf.Lerp(from, to, zn);
                Evaluate(localT, ref sample);
                OrientedFrame(sample, out Vector3 right, out Vector3 up);
                float scale = _size * (_useSplineSize ? sample.Size : 1f);
                Vector3 pos = sample.Position + (right * v.x + up * v.y) * scale;
                _vertices.Add(pos);
                if (hasNormals)
                {
                    Vector3 nrm = _srcNormals[i];
                    Vector3 tangent = sample.Tangent.sqrMagnitude > TbsSplineMath.Epsilon ? sample.Tangent.normalized : Vector3.forward;
                    _normals.Add((right * nrm.x + up * nrm.y + tangent * nrm.z).normalized);
                }
                else _normals.Add(up);
                _uv.Add(hasUv ? ApplyUvTransform(_srcUv[i]) : Vector2.zero);
                _colors.Add(SampleColor(sample));
            }
            AppendTriangles(baseVertex);
        }

        void PlaceRepeat(TbsSplineMeshChannel channel, Mesh mesh, int repeat, int count, System.Random random)
        {
            LoadSource(channel, mesh);
            float localT = count > 1
                ? Mathf.Lerp(channel.ClipFrom, channel.ClipTo, (float)repeat / (count - 1))
                : (channel.ClipFrom + channel.ClipTo) * 0.5f;
            TbsSample sample = default;
            Evaluate(localT, ref sample);

            Quaternion baseRot = sample.Rotation;
            if (channel.RandomRotation)
            {
                Vector3 e = new Vector3(
                    Mathf.Lerp(channel.MinRotation.x, channel.MaxRotation.x, (float)random.NextDouble()),
                    Mathf.Lerp(channel.MinRotation.y, channel.MaxRotation.y, (float)random.NextDouble()),
                    Mathf.Lerp(channel.MinRotation.z, channel.MaxRotation.z, (float)random.NextDouble()));
                baseRot *= Quaternion.Euler(e);
            }
            float sizeScale = _size * (_useSplineSize ? sample.Size : 1f);
            if (channel.RandomScale) sizeScale *= Mathf.Lerp(channel.MinScale, channel.MaxScale, (float)random.NextDouble());
            Matrix4x4 trs = Matrix4x4.TRS(sample.Position, baseRot, Vector3.one * sizeScale);

            int baseVertex = _vertices.Count;
            bool hasNormals = _srcNormals.Count == _srcVerts.Count;
            bool hasUv = _srcUv.Count == _srcVerts.Count;
            Color color = SampleColor(sample);
            for (int i = 0; i < _srcVerts.Count; i++)
            {
                _vertices.Add(trs.MultiplyPoint3x4(_srcVerts[i]));
                _normals.Add(hasNormals ? (baseRot * _srcNormals[i]).normalized : Vector3.up);
                _uv.Add(hasUv ? ApplyUvTransform(_srcUv[i]) : Vector2.zero);
                _colors.Add(color);
            }
            AppendTriangles(baseVertex);
        }

        void AppendTriangles(int baseVertex)
        {
            for (int i = 0; i < _srcTris.Count; i++) _triangles.Add(baseVertex + _srcTris[i]);
        }
    }
}
