using System;
using System.Collections.Generic;
using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// The curve itself: an ordered list of knots plus the rules for interpolating between them. This is pure
    /// data with no scene presence, held by a <see cref="TbsSplineComputer"/>. Every change bumps
    /// <see cref="Version"/> and raises <see cref="Changed"/>, which is how spline users know to rebuild.
    /// </summary>
    [Serializable]
    public class TbsSpline : ISerializationCallbackReceiver
    {
        static int _globalVersion;

        const int CurrentDataVersion = 1;

        [SerializeField] List<TbsKnot> _knots = new List<TbsKnot>();
        [SerializeField] bool _closed;
        [SerializeField] int _id;
        [SerializeField] int _nextKnotId = 1;
        [SerializeField] TbsSplineType _type = TbsSplineType.Bezier;
        [SerializeField, Range(0f, 1f)] float _knotParametrization;
        [SerializeField] bool _linearAverageDirection = true;
        [SerializeField] int _dataVersion;
        [SerializeField] List<TbsTriggerGroup> _triggerGroups = new List<TbsTriggerGroup>();

        [NonSerialized] int _version;
        [NonSerialized] int _batchDepth;
        [NonSerialized] bool _batchPending;

        /// <summary>
        /// Raised after the spline changes, carrying the spline, the index of the knot involved (-1 when the
        /// change is global) and what kind of change it was.
        /// </summary>
        public event Action<TbsSpline, int, TbsSplineModification> Changed;

        /// <summary>Number of knots.</summary>
        public int Count => _knots.Count;

        /// <summary>
        /// Counter bumped on every change. Compare it against a stored value to detect that the spline moved
        /// without subscribing to <see cref="Changed"/>.
        /// </summary>
        public int Version => _version;

        /// <summary>Identifier of this spline inside its computer, stable as other splines come and go.</summary>
        public int Id
        {
            get => _id;
            set => _id = value;
        }

        /// <summary>
        /// Number of curve segments. An open spline has one fewer than its knots; a closed one has as many as
        /// its knots, and none at all below three.
        /// </summary>
        public int SegmentCount => _closed ? _knots.Count >= 3 ? _knots.Count : 0 : Mathf.Max(0, _knots.Count - 1);

        /// <summary>Returns the knot at the given index. Write knots back with <see cref="SetKnot"/>.</summary>
        public TbsKnot this[int index] => _knots[index];

        /// <summary>Interpolation used between knots. Changing it rebuilds the whole curve.</summary>
        public TbsSplineType Type
        {
            get => _type;
            set
            {
                if (_type == value) return;
                _type = value;
                OnExternalMutation();
            }
        }

        /// <summary>
        /// Spacing exponent for Catmull-Rom splines: 0 is uniform, 0.5 centripetal, 1 chordal. Centripetal
        /// avoids the loops and cusps uniform spacing produces around tight, unevenly spaced knots. Ignored by
        /// every other spline type.
        /// </summary>
        public float KnotParametrization
        {
            get => _knotParametrization;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(_knotParametrization, clamped)) return;
                _knotParametrization = clamped;
                if (_type == TbsSplineType.CatmullRom) Notify(-1, TbsSplineModification.Full);
            }
        }

        /// <summary>
        /// On linear splines, averages the direction at each knot so objects turn smoothly through a corner
        /// instead of snapping. Ignored by every other spline type.
        /// </summary>
        public bool LinearAverageDirection
        {
            get => _linearAverageDirection;
            set
            {
                if (_linearAverageDirection == value) return;
                _linearAverageDirection = value;
                if (_type == TbsSplineType.Linear) Notify(-1, TbsSplineModification.Full);
            }
        }

        /// <summary>
        /// Whether the last knot joins back to the first, forming a loop.
        /// </summary>
        public bool Closed
        {
            get => _closed;
            set
            {
                if (_closed == value) return;
                _closed = value;
                if (_knots.Count > 0)
                {
                    ApplyModesAround(0);
                    ApplyModesAround(_knots.Count - 1);
                }
                Notify(-1, TbsSplineModification.ClosedChanged);
            }
        }

        /// <summary>
        /// Suspends change notifications so a run of edits raises a single event instead of one per knot. Pair
        /// every call with <see cref="EndChange"/>; the calls nest.
        /// </summary>
        public void BeginChange()
        {
            _batchDepth++;
        }

        /// <summary>
        /// Resumes change notifications and, once the outermost batch closes, raises a single
        /// <see cref="TbsSplineModification.Full"/> event if anything changed.
        /// </summary>
        public void EndChange()
        {
            if (_batchDepth <= 0) return;
            _batchDepth--;
            if (_batchDepth == 0 && _batchPending)
            {
                _batchPending = false;
                _version = ++_globalVersion;
                Changed?.Invoke(this, -1, TbsSplineModification.Full);
            }
        }

        /// <summary>
        /// Appends a knot to the end of the spline.
        /// </summary>
        public void AddKnot(TbsKnot knot)
        {
            InsertKnot(_knots.Count, knot);
        }

        /// <summary>
        /// Inserts a knot at the given index and re-smooths its neighbours. An unset or already-taken
        /// identifier is replaced with a fresh one.
        /// </summary>
        /// <param name="index">Position to insert at, from 0 to <see cref="Count"/>.</param>
        /// <param name="knot">Knot to insert.</param>
        public void InsertKnot(int index, TbsKnot knot)
        {
            knot.NormalizeRotation();
            if (knot.Id <= 0 || IndexOfKnotId(knot.Id) >= 0) knot.Id = _nextKnotId++;
            if (knot.Id >= _nextKnotId) _nextKnotId = knot.Id + 1;
            _knots.Insert(index, knot);
            ApplyModesAround(index);
            Notify(index, TbsSplineModification.KnotAdded);
        }

        /// <summary>
        /// Finds a knot by its identifier.
        /// </summary>
        /// <returns>Index of the knot, or -1 when no knot carries that identifier.</returns>
        public int IndexOfKnotId(int knotId)
        {
            for (int i = 0; i < _knots.Count; i++)
            {
                if (_knots[i].Id == knotId) return i;
            }
            return -1;
        }

        /// <summary>
        /// Returns whether the knot sits at either end of the spline. Always false on a closed spline, which
        /// has no ends.
        /// </summary>
        public bool IsEndpointIndex(int index)
        {
            if (_closed) return false;
            return index == 0 || index == _knots.Count - 1;
        }

        /// <summary>
        /// Assigns fresh identifiers to any knots missing one or sharing one. Runs on deserialization; call it
        /// after building a knot list by hand.
        /// </summary>
        public void EnsureKnotIds()
        {
            for (int i = 0; i < _knots.Count; i++)
            {
                TbsKnot knot = _knots[i];
                bool duplicate = false;
                for (int j = 0; j < i; j++)
                {
                    if (_knots[j].Id == knot.Id)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (knot.Id <= 0 || duplicate)
                {
                    knot.Id = _nextKnotId++;
                    _knots[i] = knot;
                }
                if (knot.Id >= _nextKnotId) _nextKnotId = knot.Id + 1;
            }
        }

        /// <summary>
        /// Flips the direction of travel, swapping each knot's tangent handles so the shape is unchanged.
        /// Positions along the spline are mirrored: what was at 0 is now at 1.
        /// </summary>
        public void Reverse()
        {
            _knots.Reverse();
            for (int i = 0; i < _knots.Count; i++)
            {
                TbsKnot knot = _knots[i];
                Vector3 tin = knot.TangentIn;
                knot.TangentIn = knot.TangentOut;
                knot.TangentOut = tin;
                _knots[i] = knot;
            }
            OnExternalMutation();
        }

        /// <summary>
        /// Removes the knot at the given index and re-smooths its former neighbours.
        /// </summary>
        public void RemoveKnotAt(int index)
        {
            _knots.RemoveAt(index);
            if (_knots.Count > 0) ApplyModesAround(Mathf.Clamp(index, 0, _knots.Count - 1));
            Notify(index, TbsSplineModification.KnotRemoved);
        }

        /// <summary>
        /// Replaces the knot at the given index and notifies listeners. A knot read through the indexer is a
        /// copy, so write it back through this method after editing it.
        /// </summary>
        public void SetKnot(int index, TbsKnot knot)
        {
            SetKnotNoNotify(index, knot);
            Notify(index, TbsSplineModification.KnotModified);
        }

        /// <summary>
        /// Replaces a knot and bumps <see cref="Version"/> without raising <see cref="Changed"/>. Use it when
        /// dragging a knot every frame, then call <see cref="OnExternalMutation"/> once the gesture ends.
        /// </summary>
        public void SetKnotNoNotify(int index, TbsKnot knot)
        {
            knot.NormalizeRotation();
            if (knot.Id <= 0) knot.Id = _knots[index].Id;
            _knots[index] = knot;
            ApplyModesAround(index);
            _version = ++_globalVersion;
        }

        /// <summary>
        /// Moves a knot's outgoing handle and drags the incoming one along according to the knot's tangent
        /// mode. An auto-smoothed or linear knot is switched to mirrored so the edit can take effect.
        /// </summary>
        /// <param name="index">Index of the knot.</param>
        /// <param name="localTangentOut">New handle, relative to the knot and before its rotation.</param>
        public void SetTangentOut(int index, Vector3 localTangentOut)
        {
            TbsKnot knot = _knots[index];
            if (knot.Mode == TbsTangentMode.AutoSmooth || knot.Mode == TbsTangentMode.Linear) knot.Mode = TbsTangentMode.Mirrored;
            knot.TangentOut = localTangentOut;
            switch (knot.Mode)
            {
                case TbsTangentMode.Mirrored:
                    knot.TangentIn = -localTangentOut;
                    break;
                case TbsTangentMode.Continuous:
                {
                    float mag = knot.TangentIn.magnitude;
                    Vector3 dir = localTangentOut.sqrMagnitude > TbsSplineMath.Epsilon ? localTangentOut.normalized : Vector3.forward;
                    knot.TangentIn = -dir * mag;
                    break;
                }
            }
            _knots[index] = knot;
            Notify(index, TbsSplineModification.KnotModified);
        }

        /// <summary>
        /// Moves a knot's incoming handle and drags the outgoing one along according to the knot's tangent
        /// mode. An auto-smoothed or linear knot is switched to mirrored so the edit can take effect.
        /// </summary>
        /// <param name="index">Index of the knot.</param>
        /// <param name="localTangentIn">New handle, relative to the knot and before its rotation.</param>
        public void SetTangentIn(int index, Vector3 localTangentIn)
        {
            TbsKnot knot = _knots[index];
            if (knot.Mode == TbsTangentMode.AutoSmooth || knot.Mode == TbsTangentMode.Linear) knot.Mode = TbsTangentMode.Mirrored;
            knot.TangentIn = localTangentIn;
            switch (knot.Mode)
            {
                case TbsTangentMode.Mirrored:
                    knot.TangentOut = -localTangentIn;
                    break;
                case TbsTangentMode.Continuous:
                {
                    float mag = knot.TangentOut.magnitude;
                    Vector3 dir = localTangentIn.sqrMagnitude > TbsSplineMath.Epsilon ? localTangentIn.normalized : Vector3.back;
                    knot.TangentOut = -dir * mag;
                    break;
                }
            }
            _knots[index] = knot;
            Notify(index, TbsSplineModification.KnotModified);
        }

        /// <summary>
        /// Returns one segment of the spline as a cubic Bezier, whatever the spline type: Catmull-Rom, B-spline
        /// and linear segments are all converted to their Bezier equivalent.
        /// </summary>
        /// <param name="segment">Index of the segment, from 0 to <see cref="SegmentCount"/> minus 1.</param>
        /// <exception cref="ArgumentOutOfRangeException">The segment index is outside the spline.</exception>
        public TbsCurve GetCurve(int segment)
        {
            if (segment < 0 || segment >= SegmentCount)
                throw new ArgumentOutOfRangeException(nameof(segment), $"Segment {segment} is out of range for a spline with {SegmentCount} segments.");
            int b = (segment + 1) % _knots.Count;
            switch (_type)
            {
                case TbsSplineType.Bezier:
                    return TbsCurve.FromKnots(_knots[segment], _knots[b]);
                case TbsSplineType.Linear:
                    return LinearSegment(_knots[segment].Position, _knots[b].Position);
                case TbsSplineType.CatmullRom:
                {
                    GetSegmentNeighbors(segment, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3);
                    return CatmullRomSegment(p0, p1, p2, p3, _knotParametrization);
                }
                default:
                {
                    GetSegmentNeighbors(segment, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3);
                    return BSplineSegment(p0, p1, p2, p3);
                }
            }
        }

        void GetSegmentNeighbors(int segment, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
        {
            int count = _knots.Count;
            p1 = _knots[segment].Position;
            p2 = _knots[(segment + 1) % count].Position;
            if (_closed)
            {
                p0 = _knots[(segment - 1 + count) % count].Position;
                p3 = _knots[(segment + 2) % count].Position;
                return;
            }
            p0 = segment - 1 >= 0 ? _knots[segment - 1].Position : p1 + (p1 - p2);
            p3 = segment + 2 < count ? _knots[segment + 2].Position : p2 + (p2 - p1);
        }

        static TbsCurve LinearSegment(Vector3 p1, Vector3 p2)
        {
            Vector3 delta = (p2 - p1) / 3f;
            return new TbsCurve(p1, p1 + delta, p2 - delta, p2);
        }

        static TbsCurve CatmullRomSegment(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float parametrization)
        {
            float dt0 = KnotInterval(p0, p1, parametrization);
            float dt1 = KnotInterval(p1, p2, parametrization);
            float dt2 = KnotInterval(p2, p3, parametrization);
            Vector3 m1 = (p1 - p0) / dt0 - (p2 - p0) / (dt0 + dt1) + (p2 - p1) / dt1;
            Vector3 m2 = (p2 - p1) / dt1 - (p3 - p1) / (dt1 + dt2) + (p3 - p2) / dt2;
            m1 *= dt1;
            m2 *= dt1;
            return new TbsCurve(p1, p1 + m1 / 3f, p2 - m2 / 3f, p2);
        }

        static TbsCurve BSplineSegment(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            Vector3 b0 = (p0 + 4f * p1 + p2) / 6f;
            Vector3 b3 = (p1 + 4f * p2 + p3) / 6f;
            Vector3 b1 = (2f * p1 + p2) / 3f;
            Vector3 b2 = (p1 + 2f * p2) / 3f;
            return new TbsCurve(b0, b1, b2, b3);
        }

        static float KnotInterval(Vector3 a, Vector3 b, float parametrization)
        {
            float d = Mathf.Pow((a - b).sqrMagnitude, parametrization * 0.5f);
            return d < 1e-5f ? 1e-5f : d;
        }

        /// <summary>
        /// Re-validates the whole spline and raises a full change notification. Call it after editing the knot
        /// data through some other route, such as a batch of <see cref="SetKnotNoNotify"/> calls or an undo.
        /// </summary>
        public void OnExternalMutation()
        {
            EnsureKnotIds();
            for (int i = 0; i < _knots.Count; i++)
            {
                TbsKnot knot = _knots[i];
                knot.NormalizeRotation();
                _knots[i] = knot;
                ApplyMode(i);
            }
            Notify(-1, TbsSplineModification.Full);
        }

        /// <summary>
        /// Re-applies the tangent modes of a knot and its immediate neighbours, which is what keeps
        /// auto-smoothed knots correct after an edit nearby. Does not notify listeners.
        /// </summary>
        public void ApplyModesAround(int index)
        {
            if (_knots.Count == 0) return;
            for (int i = index - 1; i <= index + 1; i++)
            {
                int resolved = ResolveIndex(i);
                if (resolved >= 0) ApplyMode(resolved);
            }
        }

        int ResolveIndex(int index)
        {
            int count = _knots.Count;
            if (_closed && count > 0) return (index % count + count) % count;
            return index >= 0 && index < count ? index : -1;
        }

        void ApplyMode(int index)
        {
            TbsKnot knot = _knots[index];
            switch (knot.Mode)
            {
                case TbsTangentMode.AutoSmooth:
                {
                    GetNeighborPositions(index, out Vector3 prev, out Vector3 next);
                    TbsSplineMath.AutoSmoothTangents(prev, knot.Position, next, out Vector3 tin, out Vector3 tout);
                    Quaternion inverse = Quaternion.Inverse(knot.Rotation);
                    knot.TangentIn = inverse * tin;
                    knot.TangentOut = inverse * tout;
                    break;
                }
                case TbsTangentMode.Linear:
                {
                    int prevIndex = ResolveIndex(index - 1);
                    int nextIndex = ResolveIndex(index + 1);
                    Quaternion inverse = Quaternion.Inverse(knot.Rotation);
                    knot.TangentIn = prevIndex >= 0 ? inverse * ((_knots[prevIndex].Position - knot.Position) / 3f) : Vector3.zero;
                    knot.TangentOut = nextIndex >= 0 ? inverse * ((_knots[nextIndex].Position - knot.Position) / 3f) : Vector3.zero;
                    break;
                }
                case TbsTangentMode.Mirrored:
                    knot.TangentIn = -knot.TangentOut;
                    break;
                case TbsTangentMode.Continuous:
                {
                    float mag = knot.TangentIn.magnitude;
                    Vector3 dir = knot.TangentOut.sqrMagnitude > TbsSplineMath.Epsilon ? knot.TangentOut.normalized : Vector3.forward;
                    knot.TangentIn = -dir * mag;
                    break;
                }
            }
            _knots[index] = knot;
        }

        void GetNeighborPositions(int index, out Vector3 prev, out Vector3 next)
        {
            int prevIndex = ResolveIndex(index - 1);
            int nextIndex = ResolveIndex(index + 1);
            Vector3 position = _knots[index].Position;
            Vector3 nextPos = nextIndex >= 0 ? _knots[nextIndex].Position : position;
            Vector3 prevPos = prevIndex >= 0 ? _knots[prevIndex].Position : position;
            if (prevIndex < 0) prevPos = position + (position - nextPos);
            if (nextIndex < 0) nextPos = position + (position - prevPos);
            prev = prevPos;
            next = nextPos;
        }

        void Notify(int index, TbsSplineModification modification)
        {
            _version = ++_globalVersion;
            if (_batchDepth > 0)
            {
                _batchPending = true;
                return;
            }
            Changed?.Invoke(this, index, modification);
        }

        /// <summary>Trigger groups stored on this spline.</summary>
        public IReadOnlyList<TbsTriggerGroup> TriggerGroups => _triggerGroups;

        /// <summary>
        /// Adds an empty trigger group and returns it.
        /// </summary>
        /// <param name="name">Label for the group.</param>
        public TbsTriggerGroup AddTriggerGroup(string name = "Group")
        {
            var group = new TbsTriggerGroup { Name = name };
            _triggerGroups.Add(group);
            return group;
        }

        /// <summary>
        /// Adds a trigger to one of the groups.
        /// </summary>
        /// <param name="groupIndex">Index of the group to add to.</param>
        /// <param name="position">Placement along the spline, from 0 to 1. Values are clamped.</param>
        /// <param name="type">Travel direction that fires the trigger.</param>
        /// <param name="name">Label for the trigger.</param>
        /// <returns>The new trigger, or null when the group index is out of range.</returns>
        public TbsSplineTrigger AddTrigger(int groupIndex, float position, TbsTriggerType type = TbsTriggerType.Double, string name = "Trigger")
        {
            if (groupIndex < 0 || groupIndex >= _triggerGroups.Count) return null;
            var trigger = new TbsSplineTrigger { Position = Mathf.Clamp01(position), Type = type, Name = name };
            _triggerGroups[groupIndex].Triggers.Add(trigger);
            return trigger;
        }

        /// <summary>
        /// Offers a move between two positions to every trigger group, firing those that are crossed.
        /// Followers call this for you.
        /// </summary>
        /// <param name="fromT">Normalized position before the move.</param>
        /// <param name="toT">Normalized position after the move.</param>
        public void CheckTriggers(float fromT, float toT)
        {
            for (int i = 0; i < _triggerGroups.Count; i++) _triggerGroups[i].Check(fromT, toT);
        }

        /// <summary>
        /// Re-arms every once-only trigger on this spline, for example when restarting a lap.
        /// </summary>
        public void ResetTriggers()
        {
            for (int i = 0; i < _triggerGroups.Count; i++) _triggerGroups[i].ResetState();
        }

        /// <inheritdoc/>
        public void OnBeforeSerialize()
        {
            _dataVersion = CurrentDataVersion;
        }

        /// <inheritdoc/>
        public void OnAfterDeserialize()
        {
            EnsureKnotIds();
            bool migrate = _dataVersion < CurrentDataVersion;
            for (int i = 0; i < _knots.Count; i++)
            {
                TbsKnot knot = _knots[i];
                knot.NormalizeRotation();
                if (migrate)
                {
                    if (knot.Size <= 0f) knot.Size = 1f;
                    if (knot.Color.a <= 0f && knot.Color.r <= 0f && knot.Color.g <= 0f && knot.Color.b <= 0f)
                        knot.Color = Color.white;
                }
                _knots[i] = knot;
            }
            _dataVersion = CurrentDataVersion;
            _version = ++_globalVersion;
        }
    }
}
