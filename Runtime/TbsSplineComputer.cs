using System;
using System.Collections.Generic;
using UnityEngine;

namespace TBSplineS
{
    [AddComponentMenu("TBSplineS/Spline Computer")]
    [DisallowMultipleComponent]
    /// <summary>
    /// The scene component that owns splines. It holds one or more <see cref="TbsSpline"/> objects, keeps a
    /// <see cref="TbsSplineCache"/> for each, joins them at junctions, and converts between its own local space
    /// and world space. Everything that reads a spline goes through a computer, and its transform moves the
    /// splines with it.
    /// </summary>
    public sealed class TbsSplineComputer : MonoBehaviour
    {
        [SerializeField] List<TbsSpline> _splines = new List<TbsSpline> { CreateDefaultSpline() };
        [SerializeField] int _nextSplineId = 1;
        [SerializeField] List<TbsJunction> _junctions = new List<TbsJunction>();
        [SerializeField] int _nextJunctionId = 1;
        [SerializeField] float _editorGridHeight;
        [SerializeField] float _editorGridSize = 1f;
        [SerializeField] bool _editorShowHeightGuides = true;
        [SerializeField] int _editorDataVersion = 1;
        [SerializeField] bool _editorShowNumbers = true;
        [SerializeField] bool _editorRenderAll;

        /// <summary>Height of the scene-view editing grid. Editor state only; has no effect at runtime.</summary>
        public float EditorGridHeight
        {
            get => _editorGridHeight;
            set => _editorGridHeight = value;
        }

        /// <summary>Spacing of the scene-view editing grid. Editor state only; has no effect at runtime.</summary>
        public float EditorGridSize
        {
            get => _editorGridSize <= 0f ? 1f : _editorGridSize;
            set => _editorGridSize = value;
        }

        /// <summary>Draws vertical guides from knots down to the grid. Editor state only.</summary>
        public bool EditorShowHeightGuides
        {
            get => _editorShowHeightGuides;
            set => _editorShowHeightGuides = value;
        }

        /// <summary>Labels knots with their index in the scene view. Editor state only.</summary>
        public bool EditorShowNumbers
        {
            get => _editorShowNumbers;
            set => _editorShowNumbers = value;
        }

        /// <summary>Draws every spline rather than only the selected one. Editor state only.</summary>
        public bool EditorRenderAll
        {
            get => _editorRenderAll;
            set => _editorRenderAll = value;
        }

        [NonSerialized] readonly List<TbsSplineCache> _caches = new List<TbsSplineCache>();

        /// <summary>Number of splines held by this computer.</summary>
        public int SplineCount => _splines.Count;

        /// <summary>Returns the spline at the given slot. Slots shift as splines are added and removed, so
        /// prefer <see cref="GetSplineById"/> for anything stored across frames.</summary>
        public TbsSpline this[int index] => _splines[index];

        /// <summary>The first spline, or null when the computer is empty. A shortcut for single-spline setups.</summary>
        public TbsSpline Spline => _splines.Count > 0 ? _splines[0] : null;

        /// <summary>Every spline held by this computer, in slot order.</summary>
        public IReadOnlyList<TbsSpline> Splines => _splines;

        /// <summary>
        /// Appends a spline and gives it an identifier. Passing null adds an empty spline.
        /// </summary>
        public void AddSpline(TbsSpline spline)
        {
            _splines.Add(spline ?? new TbsSpline());
            EnsureIds();
        }

        /// <summary>
        /// Assigns fresh identifiers to any splines missing one or sharing one, and does the same for their
        /// knots.
        /// </summary>
        public void EnsureIds()
        {
            for (int i = 0; i < _splines.Count; i++)
            {
                TbsSpline spline = _splines[i];
                if (spline == null) continue;
                bool duplicate = false;
                for (int j = 0; j < i; j++)
                {
                    if (_splines[j] != null && _splines[j].Id == spline.Id)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (spline.Id <= 0 || duplicate) spline.Id = _nextSplineId++;
                if (spline.Id >= _nextSplineId) _nextSplineId = spline.Id + 1;
            }
        }

        /// <summary>
        /// Finds a spline's slot by its identifier.
        /// </summary>
        /// <returns>The slot, or -1 when no spline carries that identifier.</returns>
        public int IndexOfSplineId(int id)
        {
            for (int i = 0; i < _splines.Count; i++)
            {
                if (_splines[i] != null && _splines[i].Id == id) return i;
            }
            return -1;
        }

        /// <summary>
        /// Returns the spline with the given identifier, or null when there is none.
        /// </summary>
        public TbsSpline GetSplineById(int id)
        {
            int index = IndexOfSplineId(id);
            return index >= 0 ? _splines[index] : null;
        }

        /// <summary>
        /// Returns the cache of the spline with the given identifier, or null when there is none.
        /// </summary>
        public TbsSplineCache GetCacheById(int id)
        {
            int index = IndexOfSplineId(id);
            return index >= 0 ? GetCache(index) : null;
        }

        /// <summary>
        /// Removes the spline at the given slot and drops every cache, so later slots shift down.
        /// </summary>
        public void RemoveSplineAt(int index)
        {
            _splines.RemoveAt(index);
            for (int i = 0; i < _caches.Count; i++) _caches[i]?.Dispose();
            _caches.Clear();
            ValidateJunctions();
        }

        /// <summary>
        /// Removes a knot and detaches it from any junction it took part in.
        /// </summary>
        /// <returns>False when either index is out of range.</returns>
        public bool RemoveKnot(int splineIndex, int knotIndex)
        {
            if (splineIndex < 0 || splineIndex >= _splines.Count) return false;
            TbsSpline spline = _splines[splineIndex];
            if (spline == null || knotIndex < 0 || knotIndex >= spline.Count) return false;
            var reference = new TbsKnotRef(spline.Id, spline[knotIndex].Id);
            spline.RemoveKnotAt(knotIndex);
            RemoveKnotFromJunctions(reference);
            ValidateJunctions();
            return true;
        }

        /// <summary>
        /// Returns the cache for a spline, creating it on first use. This is how you get at sampling, length
        /// and nearest-point queries.
        /// </summary>
        /// <param name="index">Slot of the spline.</param>
        /// <exception cref="ArgumentOutOfRangeException">The slot is outside the computer.</exception>
        public TbsSplineCache GetCache(int index)
        {
            if (index < 0 || index >= _splines.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Spline index {index} is out of range on '{name}' with {_splines.Count} splines.");
            while (_caches.Count < _splines.Count) _caches.Add(null);
            if (_caches.Count > _splines.Count)
            {
                for (int i = _splines.Count; i < _caches.Count; i++) _caches[i]?.Dispose();
                _caches.RemoveRange(_splines.Count, _caches.Count - _splines.Count);
            }
            TbsSplineCache cache = _caches[index];
            if (cache == null || cache.Spline != _splines[index])
            {
                cache?.Dispose();
                cache = new TbsSplineCache(_splines[index]);
                _caches[index] = cache;
            }
            return cache;
        }

        /// <summary>
        /// Builds every cache up front, so the first sample of a heavy spline does not land on a gameplay
        /// frame. Worth calling during loading.
        /// </summary>
        public void Warmup()
        {
            for (int i = 0; i < _splines.Count; i++) GetCache(i).Warmup();
        }

        /// <summary>
        /// Returns the length of one spline in world units, or 0 when the slot is out of range.
        /// </summary>
        public float GetLength(int splineIndex = 0) =>
            splineIndex >= 0 && splineIndex < _splines.Count ? GetCache(splineIndex).TotalLength : 0f;

        /// <summary>
        /// Returns the combined length of every spline on this computer, in world units.
        /// </summary>
        public float GetTotalLength()
        {
            float total = 0f;
            for (int i = 0; i < _splines.Count; i++) total += GetCache(i).TotalLength;
            return total;
        }

        /// <summary>
        /// Samples a spline at a normalized position and returns the result in world space.
        /// </summary>
        /// <param name="splineIndex">Slot of the spline.</param>
        /// <param name="t">Position from 0 to 1, spaced by real distance.</param>
        /// <param name="sample">Receives the result.</param>
        public void Evaluate(int splineIndex, float t, ref TbsSample sample)
        {
            GetCache(splineIndex).EvaluateAtT(t, ref sample);
            TransformSample(ref sample);
        }

        /// <summary>
        /// Samples a spline at an arc length from its start and returns the result in world space.
        /// </summary>
        /// <param name="splineIndex">Slot of the spline.</param>
        /// <param name="distance">Distance in world units.</param>
        /// <param name="sample">Receives the result.</param>
        public void EvaluateAtDistance(int splineIndex, float distance, ref TbsSample sample)
        {
            GetCache(splineIndex).EvaluateAtDistance(distance, ref sample);
            TransformSample(ref sample);
        }

        /// <summary>
        /// Returns just the world position at a normalized point on a spline.
        /// </summary>
        public Vector3 EvaluatePosition(int splineIndex, float t)
        {
            TbsSample sample = default;
            Evaluate(splineIndex, t, ref sample);
            return sample.Position;
        }

        /// <summary>
        /// Finds the point on a spline closest to a world position.
        /// </summary>
        /// <param name="splineIndex">Slot of the spline.</param>
        /// <param name="worldPoint">World position to search from.</param>
        /// <param name="sample">Receives the nearest point, in world space.</param>
        /// <returns>Distance along the spline to that point, in world units.</returns>
        public float GetNearestPoint(int splineIndex, Vector3 worldPoint, ref TbsSample sample)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            float t = GetCache(splineIndex).GetNearestPoint(local, ref sample);
            TransformSample(ref sample);
            return t;
        }

        /// <summary>
        /// Returns where a knot sits along its spline, from 0 at the start to 1 at the end.
        /// </summary>
        public float GetKnotPercent(int splineIndex, int knotIndex)
        {
            if (splineIndex < 0 || splineIndex >= _splines.Count) return 0f;
            TbsSplineCache cache = GetCache(splineIndex);
            float total = cache.TotalLength;
            return total > TbsSplineMath.Epsilon ? Mathf.Clamp01(cache.KnotToDistance(knotIndex) / total) : 0f;
        }

        /// <summary>
        /// Advances a normalized position by a real-world distance, which is what keeps movement at a constant
        /// speed regardless of how the knots are spaced.
        /// </summary>
        /// <param name="splineIndex">Slot of the spline.</param>
        /// <param name="startT">Starting position, from 0 to 1.</param>
        /// <param name="distance">Distance to travel in world units. Negative values move backwards.</param>
        /// <returns>The resulting normalized position.</returns>
        public float Travel(int splineIndex, float startT, float distance)
        {
            if (splineIndex < 0 || splineIndex >= _splines.Count) return 0f;
            TbsSplineCache cache = GetCache(splineIndex);
            float total = cache.TotalLength;
            if (total <= TbsSplineMath.Epsilon) return 0f;
            float d = Mathf.Clamp01(startT) * total + distance;
            d = cache.Spline.Closed ? Mathf.Repeat(d, total) : Mathf.Clamp(d, 0f, total);
            return d / total;
        }

        /// <summary>
        /// Walks the spline and reports the first place where it hits scene geometry, for dropping paths onto
        /// terrain or detecting obstructions.
        /// </summary>
        /// <param name="splineIndex">Slot of the spline.</param>
        /// <param name="hit">Receives the hit details.</param>
        /// <param name="t">Receives the normalized position along the spline where the hit occurred.</param>
        /// <param name="layerMask">Layers to test against.</param>
        /// <param name="query">Whether trigger colliders count as hits.</param>
        /// <returns>True when something was hit.</returns>
        public bool Raycast(int splineIndex, out RaycastHit hit, out float t, int layerMask = ~0, QueryTriggerInteraction query = QueryTriggerInteraction.UseGlobal)
        {
            hit = default;
            t = 0f;
            if (splineIndex < 0 || splineIndex >= _splines.Count) return false;
            TbsSplineCache cache = GetCache(splineIndex);
            cache.Warmup();
            int count = cache.SampleCount;
            if (count < 2) return false;
            Transform trs = transform;
            Vector3 prev = trs.TransformPoint(cache.GetSamplePosition(0));
            for (int i = 1; i < count; i++)
            {
                Vector3 current = trs.TransformPoint(cache.GetSamplePosition(i));
                if (Physics.Linecast(prev, current, out hit, layerMask, query))
                {
                    TbsSample sample = default;
                    t = cache.GetNearestPoint(trs.InverseTransformPoint(hit.point), ref sample);
                    return true;
                }
                prev = current;
            }
            return false;
        }

        void TransformSample(ref TbsSample sample)
        {
            Transform trs = transform;
            sample.Position = trs.TransformPoint(sample.Position);
            sample.Tangent = trs.TransformDirection(sample.Tangent);
            sample.Up = trs.TransformDirection(sample.Up);
        }

        /// <summary>Junctions tying knots of these splines together.</summary>
        public IReadOnlyList<TbsJunction> Junctions => _junctions;

        /// <summary>
        /// Turns a knot reference into current slot indices, which is needed because identifiers stay put while
        /// slots shift.
        /// </summary>
        /// <param name="reference">Reference to resolve.</param>
        /// <param name="splineIndex">Receives the spline slot, or -1.</param>
        /// <param name="knotIndex">Receives the knot slot, or -1.</param>
        /// <returns>True when both were found.</returns>
        public bool ResolveRef(TbsKnotRef reference, out int splineIndex, out int knotIndex)
        {
            splineIndex = IndexOfSplineId(reference.SplineId);
            knotIndex = splineIndex >= 0 ? _splines[splineIndex].IndexOfKnotId(reference.KnotId) : -1;
            return splineIndex >= 0 && knotIndex >= 0;
        }

        /// <summary>
        /// Returns the world position of a referenced knot, or <see cref="Vector3.zero"/> when it no longer
        /// exists.
        /// </summary>
        public Vector3 GetKnotWorld(TbsKnotRef reference)
        {
            return ResolveRef(reference, out int s, out int k)
                ? transform.TransformPoint(_splines[s][k].Position)
                : Vector3.zero;
        }

        /// <summary>
        /// Returns whether a referenced knot sits at either end of its spline. Only endpoints can start a
        /// connection to another spline.
        /// </summary>
        public bool IsEndpoint(TbsKnotRef reference)
        {
            return ResolveRef(reference, out int s, out int k) && _splines[s].IsEndpointIndex(k);
        }

        /// <summary>
        /// Builds a reference to a knot from its current slots. Store the reference, not the slots.
        /// </summary>
        public TbsKnotRef MakeRef(int splineIndex, int knotIndex)
        {
            return new TbsKnotRef(_splines[splineIndex].Id, _splines[splineIndex][knotIndex].Id);
        }

        /// <summary>
        /// Returns the junction a knot takes part in, or null when it is unconnected.
        /// </summary>
        public TbsJunction GetJunctionOfKnot(TbsKnotRef reference)
        {
            for (int i = 0; i < _junctions.Count; i++)
            {
                if (_junctions[i].Contains(reference)) return _junctions[i];
            }
            return null;
        }

        /// <summary>
        /// Returns the junction with the given identifier, or null when there is none.
        /// </summary>
        public TbsJunction GetJunctionById(int junctionId)
        {
            for (int i = 0; i < _junctions.Count; i++)
            {
                if (_junctions[i].Id == junctionId) return _junctions[i];
            }
            return null;
        }

        /// <summary>
        /// Ties two knots together into a junction so followers can pass between their splines. Joining a knot
        /// that already belongs to a junction merges the two.
        /// </summary>
        /// <returns>The junction holding both knots, or null when the references are equal or unresolvable.</returns>
        public TbsJunction ConnectKnots(TbsKnotRef a, TbsKnotRef b)
        {
            if (a.Equals(b)) return null;
            if (!ResolveRef(a, out _, out _) || !ResolveRef(b, out _, out _)) return null;
            TbsJunction ja = GetJunctionOfKnot(a);
            TbsJunction jb = GetJunctionOfKnot(b);
            TbsJunction target;
            if (ja != null && jb != null)
            {
                if (ja == jb) return ja;
                for (int i = 0; i < jb.Members.Count; i++)
                {
                    if (!ja.Contains(jb.Members[i])) ja.Members.Add(jb.Members[i]);
                }
                _junctions.Remove(jb);
                target = ja;
            }
            else if (ja != null)
            {
                if (!ja.Contains(b)) ja.Members.Add(b);
                target = ja;
            }
            else if (jb != null)
            {
                if (!jb.Contains(a)) jb.Members.Add(a);
                target = jb;
            }
            else
            {
                target = new TbsJunction { Id = _nextJunctionId++ };
                target.Members.Add(a);
                target.Members.Add(b);
                _junctions.Add(target);
            }
            PropagateFromKnot(a);
            return target;
        }

        /// <summary>
        /// Splits a segment at the given parameter and inserts a knot there, leaving the curve's shape
        /// unchanged.
        /// </summary>
        /// <param name="splineIndex">Slot of the spline.</param>
        /// <param name="segment">Segment to split.</param>
        /// <param name="t">Parameter within the segment, from 0 to 1.</param>
        /// <returns>Index of the new knot, or -1 when the arguments are out of range.</returns>
        public int InsertKnotOnSegment(int splineIndex, int segment, float t)
        {
            if (splineIndex < 0 || splineIndex >= _splines.Count) return -1;
            TbsSpline spline = _splines[splineIndex];
            if (segment < 0 || segment >= spline.SegmentCount) return -1;
            t = Mathf.Clamp01(t);
            TbsSample sample = default;
            GetCache(splineIndex).EvaluateSegment(segment, t, ref sample);
            TbsCurve curve = spline.GetCurve(segment);
            curve.Split(t, out TbsCurve left, out TbsCurve right);
            int startIndex = segment;
            int endIndex = (segment + 1) % spline.Count;
            spline.BeginChange();
            TbsKnot start = spline[startIndex];
            if (start.Mode != TbsTangentMode.Linear && start.Mode != TbsTangentMode.Broken) start.Mode = TbsTangentMode.Broken;
            start.TangentOut = Quaternion.Inverse(start.Rotation) * (left.P1 - left.P0);
            spline.SetKnot(startIndex, start);
            TbsKnot end = spline[endIndex];
            if (end.Mode != TbsTangentMode.Linear && end.Mode != TbsTangentMode.Broken) end.Mode = TbsTangentMode.Broken;
            end.TangentIn = Quaternion.Inverse(end.Rotation) * (right.P2 - right.P3);
            spline.SetKnot(endIndex, end);
            Vector3 tangentDir = sample.Tangent.sqrMagnitude > TbsSplineMath.Epsilon ? sample.Tangent : right.P1 - left.P2;
            Quaternion rotation = tangentDir.sqrMagnitude > TbsSplineMath.Epsilon
                ? Quaternion.LookRotation(tangentDir, sample.Up)
                : Quaternion.identity;
            Quaternion inverse = Quaternion.Inverse(rotation);
            var middle = new TbsKnot(left.P3, inverse * (left.P2 - left.P3), inverse * (right.P1 - right.P0), rotation, TbsTangentMode.Broken);
            spline.InsertKnot(segment + 1, middle);
            spline.EndChange();
            return segment + 1;
        }

        /// <summary>
        /// Joins the end of one spline into the middle of another, splitting the target segment to make a knot
        /// to attach to. This is how a side road is branched off a main one.
        /// </summary>
        /// <param name="incoming">Endpoint knot that joins in.</param>
        /// <param name="targetSplineIndex">Slot of the spline being joined.</param>
        /// <param name="segment">Segment of the target to split.</param>
        /// <param name="t">Parameter within that segment, from 0 to 1.</param>
        /// <returns>The junction that was created, or null when the arguments are out of range.</returns>
        public TbsJunction ConnectEndpointToCurve(TbsKnotRef incoming, int targetSplineIndex, int segment, float t)
        {
            int newIndex = InsertKnotOnSegment(targetSplineIndex, segment, t);
            if (newIndex < 0) return null;
            TbsKnotRef target = MakeRef(targetSplineIndex, newIndex);
            return ConnectKnots(target, incoming);
        }

        /// <summary>
        /// Removes a junction, leaving its knots in place but no longer connected.
        /// </summary>
        public void Disconnect(int junctionId)
        {
            TbsJunction junction = GetJunctionById(junctionId);
            if (junction != null) _junctions.Remove(junction);
        }

        /// <summary>
        /// Detaches a knot from every junction, dropping any junction left with fewer than two members.
        /// </summary>
        public void RemoveKnotFromJunctions(TbsKnotRef reference)
        {
            for (int i = _junctions.Count - 1; i >= 0; i--)
            {
                _junctions[i].Members.RemoveAll(m => m.Equals(reference));
                if (_junctions[i].Count < 2) _junctions.RemoveAt(i);
            }
        }

        /// <summary>
        /// Drops junction members whose knots no longer exist, and any junction left with fewer than two.
        /// Worth calling after editing splines through your own code.
        /// </summary>
        public void ValidateJunctions()
        {
            for (int i = _junctions.Count - 1; i >= 0; i--)
            {
                _junctions[i].Members.RemoveAll(m => !ResolveRef(m, out _, out _));
                if (_junctions[i].Count < 2) _junctions.RemoveAt(i);
            }
        }

        /// <summary>
        /// Moves the other knots of a junction to follow one that was just moved, so a branch point stays
        /// welded together while it is dragged.
        /// </summary>
        public void PropagateFromKnot(TbsKnotRef moved)
        {
            TbsJunction junction = GetJunctionOfKnot(moved);
            if (junction == null) return;
            if (!ResolveRef(moved, out int ms, out int mk)) return;
            TbsKnot anchor = _splines[ms][mk];
            Vector3 world = transform.TransformPoint(anchor.Position);
            SnapJunctionExcept(junction, world, anchor.Rotation, moved);
        }

        /// <summary>
        /// Re-welds every junction, pulling each one's members back onto a common position.
        /// </summary>
        public void PropagateAllJunctions()
        {
            for (int i = 0; i < _junctions.Count; i++)
            {
                TbsJunction junction = _junctions[i];
                if (junction.Count == 0) continue;
                TbsKnotRef anchor = junction.Members[0];
                if (!ResolveRef(anchor, out int s, out int k)) continue;
                TbsKnot anchorKnot = _splines[s][k];
                Vector3 world = transform.TransformPoint(anchorKnot.Position);
                SnapJunctionExcept(junction, world, anchorKnot.Rotation, anchor);
            }
        }

        /// <summary>
        /// Switches a junction between a free corner and a smoothed pass-through, re-aligning its tangents.
        /// </summary>
        /// <returns>False when no junction carries that identifier.</returns>
        public bool SetJunctionMode(int junctionId, TbsJunctionMode mode)
        {
            TbsJunction junction = GetJunctionById(junctionId);
            if (junction == null) return false;
            junction.Mode = mode;
            PropagateAllJunctions();
            return true;
        }

        void SnapJunctionExcept(TbsJunction junction, Vector3 world, Quaternion anchorRotation, TbsKnotRef except)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            bool smooth = junction.Mode == TbsJunctionMode.Smooth;
            for (int i = 0; i < junction.Members.Count; i++)
            {
                TbsKnotRef member = junction.Members[i];
                if (member.Equals(except)) continue;
                if (!ResolveRef(member, out int s, out int k)) continue;
                TbsSpline spline = _splines[s];
                TbsKnot knot = spline[k];
                bool changed = false;
                if ((knot.Position - local).sqrMagnitude >= 1e-10f)
                {
                    knot.Position = local;
                    changed = true;
                }
                if (smooth && knot.Rotation != anchorRotation)
                {
                    knot.Rotation = anchorRotation;
                    changed = true;
                }
                if (changed) spline.SetKnot(k, knot);
            }
        }

        /// <summary>
        /// Fuses two splines joined end to end at a junction into a single continuous spline, removing the
        /// junction and the duplicated knot.
        /// </summary>
        /// <returns>False when the junction does not join exactly two endpoints.</returns>
        public bool MergeEndpointJunction(int junctionId)
        {
            TbsJunction junction = GetJunctionById(junctionId);
            if (junction == null || junction.Count != 2) return false;
            if (!ResolveRef(junction.Members[0], out int sa, out int ka)) return false;
            if (!ResolveRef(junction.Members[1], out int sb, out int kb)) return false;
            if (sa == sb) return false;
            TbsSpline a = _splines[sa];
            TbsSpline b = _splines[sb];
            if (a.Closed || b.Closed) return false;
            if (!a.IsEndpointIndex(ka) || !b.IsEndpointIndex(kb)) return false;
            if (ka == 0) a.Reverse();
            if (kb != 0) b.Reverse();
            int aId = a.Id;
            int bId = b.Id;
            var remap = new Dictionary<int, int> { { b[0].Id, a[a.Count - 1].Id } };
            for (int i = 1; i < b.Count; i++)
            {
                TbsKnot knot = b[i];
                int oldId = knot.Id;
                knot.Id = 0;
                a.AddKnot(knot);
                remap[oldId] = a[a.Count - 1].Id;
            }
            _junctions.Remove(junction);
            RemapJunctionRefs(bId, aId, remap);
            int removeIndex = IndexOfSplineId(bId);
            if (removeIndex >= 0) RemoveSplineAt(removeIndex);
            ValidateJunctions();
            return true;
        }

        void RemapJunctionRefs(int fromSplineId, int toSplineId, Dictionary<int, int> knotIdMap)
        {
            for (int j = 0; j < _junctions.Count; j++)
            {
                List<TbsKnotRef> members = _junctions[j].Members;
                for (int m = 0; m < members.Count; m++)
                {
                    TbsKnotRef member = members[m];
                    if (member.SplineId != fromSplineId) continue;
                    if (knotIdMap.TryGetValue(member.KnotId, out int newKnotId))
                        members[m] = new TbsKnotRef(toSplineId, newKnotId);
                }
            }
        }

        /// <summary>
        /// Copies a run of knots into a brand new spline on this computer, optionally shifted.
        /// </summary>
        /// <param name="splineIndex">Slot of the spline to copy from.</param>
        /// <param name="knotIds">Identifiers of the knots to copy.</param>
        /// <param name="offset">Shift applied to the copies, in local space.</param>
        /// <returns>Slot of the new spline, or -1 when the arguments are empty or out of range.</returns>
        public int DuplicateKnotsToNewSpline(int splineIndex, ICollection<int> knotIds, Vector3 offset)
        {
            if (splineIndex < 0 || splineIndex >= _splines.Count || knotIds == null || knotIds.Count == 0) return -1;
            TbsSpline source = _splines[splineIndex];
            var copy = new TbsSpline();
            for (int i = 0; i < source.Count; i++)
            {
                if (!knotIds.Contains(source[i].Id)) continue;
                TbsKnot knot = source[i];
                knot.Id = 0;
                knot.Position += offset;
                copy.AddKnot(knot);
            }
            if (copy.Count == 0) return -1;
            AddSpline(copy);
            return _splines.Count - 1;
        }

        /// <summary>
        /// Copies a whole spline into a new one on this computer.
        /// </summary>
        /// <returns>Slot of the new spline, or -1 when the source slot is out of range.</returns>
        public int DuplicateSpline(int index)
        {
            if (index < 0 || index >= _splines.Count) return -1;
            TbsSpline source = _splines[index];
            var copy = new TbsSpline();
            for (int i = 0; i < source.Count; i++)
            {
                TbsKnot knot = source[i];
                knot.Id = 0;
                knot.Position += new Vector3(0f, 0f, 1f);
                copy.AddKnot(knot);
            }
            copy.Closed = source.Closed;
            AddSpline(copy);
            return _splines.Count - 1;
        }

        /// <summary>
        /// Cuts a spline in two at a knot, leaving the original with everything up to the cut and putting the
        /// rest into a new spline. The knot itself is duplicated so both halves keep an endpoint there.
        /// </summary>
        /// <param name="splineIndex">Slot of the spline to cut.</param>
        /// <param name="knotId">Identifier of the knot to cut at.</param>
        /// <returns>Slot of the new spline, or -1 when the cut is not possible.</returns>
        public int SplitSplineAtKnot(int splineIndex, int knotId)
        {
            if (splineIndex < 0 || splineIndex >= _splines.Count) return -1;
            TbsSpline spline = _splines[splineIndex];
            if (spline.Closed) return -1;
            int k = spline.IndexOfKnotId(knotId);
            if (k <= 0 || k >= spline.Count - 1) return -1;
            int sourceId = spline.Id;
            var movedIds = new List<int>();
            var tail = new TbsSpline();
            for (int i = k + 1; i < spline.Count; i++)
            {
                TbsKnot knot = spline[i];
                movedIds.Add(knot.Id);
                tail.AddKnot(knot);
            }
            TbsKnot seam = spline[k];
            seam.Id = 0;
            tail.InsertKnot(0, seam);
            for (int i = spline.Count - 1; i > k; i--) spline.RemoveKnotAt(i);
            AddSpline(tail);
            var map = new Dictionary<int, int>();
            for (int i = 0; i < movedIds.Count; i++) map[movedIds[i]] = movedIds[i];
            RemapJunctionRefs(sourceId, tail.Id, map);
            ValidateJunctions();
            return _splines.Count - 1;
        }

        void Awake()
        {
            EnsureIds();
        }

        void OnValidate()
        {
            if (_editorDataVersion < 1)
            {
                _editorDataVersion = 1;
                _editorShowHeightGuides = true;
            }
            EnsureIds();
            for (int i = 0; i < _splines.Count; i++) _splines[i]?.OnExternalMutation();
            ValidateJunctions();
            PropagateAllJunctions();
        }

        static TbsSpline CreateDefaultSpline()
        {
            var spline = new TbsSpline();
            spline.AddKnot(new TbsKnot(new Vector3(-2f, 0f, 0f)));
            spline.AddKnot(new TbsKnot(new Vector3(2f, 0f, 0f)));
            return spline;
        }
    }
}
