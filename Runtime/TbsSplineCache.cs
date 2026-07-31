using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// Precomputed sampling of a spline: positions, tangents, up vectors and an arc-length table. It is what
    /// makes evenly spaced travel and nearest-point queries cheap, since the raw curve is not parameterized by
    /// distance. The cache tracks the spline's version and refreshes only the segments that actually changed,
    /// so reading from it is safe at any time. Obtain one from
    /// <see cref="TbsSplineComputer.GetCache"/> rather than constructing it yourself.
    /// </summary>
    public sealed class TbsSplineCache
    {
        /// <summary>Number of steps sampled per curve segment.</summary>
        public const int SamplesPerSegment = 32;

        readonly TbsSpline _spline;

        int _syncedVersion = -1;
        int _segmentCount = -1;
        bool[] _segmentDirty;
        Vector3[] _positions;
        Vector3[] _tangents;
        Vector3[] _ups;
        float[] _cumulative;
        float[] _segmentLength;
        float[] _segmentStart;
        float _totalLength;
        bool _lengthsDirty;
        Bounds _bounds;

        /// <summary>
        /// Creates a cache bound to a spline and subscribes to its changes.
        /// </summary>
        public TbsSplineCache(TbsSpline spline)
        {
            _spline = spline;
            _spline.Changed += OnSplineChanged;
        }

        /// <summary>
        /// Unsubscribes from the spline. Call it before dropping a cache you built yourself, or the spline will
        /// keep it alive.
        /// </summary>
        public void Dispose()
        {
            _spline.Changed -= OnSplineChanged;
        }

        /// <summary>The spline this cache samples.</summary>
        public TbsSpline Spline => _spline;

        /// <summary>Arc length of the whole spline in world units, measured across the sampled points.</summary>
        public float TotalLength
        {
            get
            {
                Prepare();
                return _totalLength;
            }
        }

        /// <summary>Total number of cached sample points across every segment.</summary>
        public int SampleCount
        {
            get
            {
                Prepare();
                return _segmentCount * (SamplesPerSegment + 1);
            }
        }

        /// <summary>Bounding box enclosing every sampled point, in the computer's local space.</summary>
        public Bounds LocalBounds
        {
            get
            {
                Prepare();
                return _bounds;
            }
        }

        /// <summary>Returns a cached sample position by index. Read <see cref="SampleCount"/> for the range.</summary>
        public Vector3 GetSamplePosition(int index) => _positions[index];

        /// <summary>Returns a cached sample up vector by index.</summary>
        public Vector3 GetSampleUp(int index) => _ups[index];

        /// <summary>
        /// Forces the cache to refresh now. Use it to move the cost off a frame where it would be felt, such as
        /// before starting a race rather than on the first lap.
        /// </summary>
        public void Warmup() => Prepare();

        /// <summary>
        /// Samples the spline at a normalized position, spaced by real distance so travel is even.
        /// </summary>
        /// <param name="t">Position from 0 to 1. Clamped on an open spline, wrapped on a closed one.</param>
        /// <param name="sample">Receives the result.</param>
        public void EvaluateAtT(float t, ref TbsSample sample)
        {
            Prepare();
            float wrapped = _spline.Closed ? Mathf.Repeat(t, 1f) : Mathf.Clamp01(t);
            EvaluateAtDistance(wrapped * _totalLength, ref sample);
        }

        /// <summary>
        /// Samples the spline at an arc length from its start.
        /// </summary>
        /// <param name="distance">Distance in world units, clamped to <see cref="TotalLength"/>.</param>
        /// <param name="sample">Receives the result.</param>
        public void EvaluateAtDistance(float distance, ref TbsSample sample)
        {
            Prepare();
            if (_segmentCount == 0 || _totalLength <= TbsSplineMath.Epsilon)
            {
                EvaluateDegenerate(ref sample);
                return;
            }
            distance = _spline.Closed ? Mathf.Repeat(distance, _totalLength) : Mathf.Clamp(distance, 0f, _totalLength);
            int segment = FindSegmentByDistance(distance);
            float t = SegmentDistanceToT(segment, distance - _segmentStart[segment]);
            EvaluateSegment(segment, t, ref sample);
        }

        /// <summary>
        /// Samples one segment at a curve parameter. Unlike the distance-based methods this is spaced by the
        /// curve's own parameterization, so steps are uneven where the curve bends.
        /// </summary>
        /// <param name="segment">Index of the segment.</param>
        /// <param name="t">Parameter within the segment, from 0 to 1.</param>
        /// <param name="sample">Receives the result.</param>
        public void EvaluateSegment(int segment, float t, ref TbsSample sample)
        {
            Prepare();
            if (_segmentCount == 0)
            {
                EvaluateDegenerate(ref sample);
                return;
            }
            TbsCurve curve = _spline.GetCurve(segment);
            sample.Position = curve.EvaluatePosition(t);
            sample.Tangent = SafeTangent(curve, t);
            sample.Up = SampleUp(segment, t, sample.Tangent);
            float distance = _segmentStart[segment] + SegmentTToDistance(segment, t);
            sample.Distance = distance;
            sample.T = _totalLength > TbsSplineMath.Epsilon ? distance / _totalLength : 0f;
            TbsKnot a = _spline[segment];
            TbsKnot b = _spline[(segment + 1) % _spline.Count];
            float lerp = Mathf.Clamp01(t);
            sample.Size = Mathf.Lerp(a.Size, b.Size, lerp);
            sample.Color = Color.Lerp(a.Color, b.Color, lerp);
        }

        /// <summary>
        /// Finds the point on the spline closest to a world position, searching the cached samples and then
        /// refining between the two best.
        /// </summary>
        /// <param name="point">World position to search from.</param>
        /// <param name="sample">Receives the nearest point on the spline.</param>
        /// <returns>Distance along the spline to that point, in world units.</returns>
        public float GetNearestPoint(Vector3 point, ref TbsSample sample)
        {
            Prepare();
            if (_segmentCount == 0)
            {
                EvaluateDegenerate(ref sample);
                return 0f;
            }
            int rows = SamplesPerSegment + 1;
            int total = _segmentCount * rows;
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < total; i++)
            {
                float sqr = (point - _positions[i]).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            int segment = best / rows;
            int row = best % rows;
            float windowStart = Mathf.Max(0f, (row - 1f) / SamplesPerSegment);
            float windowEnd = Mathf.Min(1f, (row + 1f) / SamplesPerSegment);
            int bestSegment = segment;
            float bestT = RefineNearest(segment, point, windowStart, windowEnd, out float bestDistanceSqr);
            if (row == 0)
            {
                int previous = segment > 0 ? segment - 1 : _spline.Closed ? _segmentCount - 1 : -1;
                if (previous >= 0 && previous != segment)
                {
                    float t = RefineNearest(previous, point, 1f - 1f / SamplesPerSegment, 1f, out float distanceSqr);
                    if (distanceSqr < bestDistanceSqr)
                    {
                        bestDistanceSqr = distanceSqr;
                        bestSegment = previous;
                        bestT = t;
                    }
                }
            }
            else if (row == SamplesPerSegment)
            {
                int next = segment < _segmentCount - 1 ? segment + 1 : _spline.Closed ? 0 : -1;
                if (next >= 0 && next != segment)
                {
                    float t = RefineNearest(next, point, 0f, 1f / SamplesPerSegment, out float distanceSqr);
                    if (distanceSqr < bestDistanceSqr)
                    {
                        bestDistanceSqr = distanceSqr;
                        bestSegment = next;
                        bestT = t;
                    }
                }
            }
            EvaluateSegment(bestSegment, bestT, ref sample);
            return sample.T;
        }

        float RefineNearest(int segment, Vector3 point, float t0, float t1, out float distanceSqr)
        {
            TbsCurve curve = _spline.GetCurve(segment);
            for (int iteration = 0; iteration < 24; iteration++)
            {
                float m1 = Mathf.Lerp(t0, t1, 1f / 3f);
                float m2 = Mathf.Lerp(t0, t1, 2f / 3f);
                float d1 = (point - curve.EvaluatePosition(m1)).sqrMagnitude;
                float d2 = (point - curve.EvaluatePosition(m2)).sqrMagnitude;
                if (d1 < d2) t1 = m2;
                else t0 = m1;
            }
            float t = 0.5f * (t0 + t1);
            distanceSqr = (point - curve.EvaluatePosition(t)).sqrMagnitude;
            return t;
        }

        /// <summary>
        /// Converts a position along the spline between knot index, normalized and distance units.
        /// </summary>
        /// <param name="value">Position to convert.</param>
        /// <param name="from">Unit <paramref name="value"/> is currently in.</param>
        /// <param name="to">Unit to convert to.</param>
        public float ConvertUnit(float value, TbsPathUnit from, TbsPathUnit to)
        {
            if (from == to) return value;
            Prepare();
            float distance = from switch
            {
                TbsPathUnit.Distance => value,
                TbsPathUnit.Normalized => value * _totalLength,
                _ => KnotToDistance(value)
            };
            return to switch
            {
                TbsPathUnit.Distance => distance,
                TbsPathUnit.Normalized => _totalLength > TbsSplineMath.Epsilon ? distance / _totalLength : 0f,
                _ => DistanceToKnot(distance)
            };
        }

        /// <summary>
        /// Converts a knot index, fractional part included, into an arc length from the start of the spline.
        /// </summary>
        public float KnotToDistance(float knot)
        {
            Prepare();
            if (_segmentCount == 0) return 0f;
            float clamped = Mathf.Clamp(knot, 0f, _segmentCount);
            int segment = Mathf.Min((int)clamped, _segmentCount - 1);
            return _segmentStart[segment] + SegmentTToDistance(segment, clamped - segment);
        }

        /// <summary>
        /// Converts an arc length from the start of the spline into a knot index with a fractional part.
        /// </summary>
        public float DistanceToKnot(float distance)
        {
            Prepare();
            if (_segmentCount == 0) return 0f;
            distance = Mathf.Clamp(distance, 0f, _totalLength);
            int segment = FindSegmentByDistance(distance);
            return segment + SegmentDistanceToT(segment, distance - _segmentStart[segment]);
        }

        void OnSplineChanged(TbsSpline spline, int knotIndex, TbsSplineModification modification)
        {
            if (modification == TbsSplineModification.KnotModified && knotIndex >= 0 && _segmentDirty != null && _segmentCount == spline.SegmentCount)
            {
                for (int s = knotIndex - 2; s <= knotIndex + 1; s++)
                {
                    int resolved = ResolveSegment(s);
                    if (resolved >= 0) _segmentDirty[resolved] = true;
                }
                _lengthsDirty = true;
            }
            else
            {
                MarkAllDirty();
            }
            _syncedVersion = spline.Version;
        }

        int ResolveSegment(int segment)
        {
            if (_segmentCount <= 0) return -1;
            if (_spline.Closed) return (segment % _segmentCount + _segmentCount) % _segmentCount;
            return segment >= 0 && segment < _segmentCount ? segment : -1;
        }

        void MarkAllDirty()
        {
            if (_segmentDirty != null)
            {
                for (int i = 0; i < _segmentDirty.Length; i++) _segmentDirty[i] = true;
            }
            _lengthsDirty = true;
        }

        void Prepare()
        {
            int segments = _spline.SegmentCount;
            if (segments != _segmentCount || _positions == null)
            {
                _segmentCount = segments;
                int rows = segments * (SamplesPerSegment + 1);
                _segmentDirty = new bool[Mathf.Max(1, segments)];
                _positions = new Vector3[rows];
                _tangents = new Vector3[rows];
                _ups = new Vector3[rows];
                _cumulative = new float[rows];
                _segmentLength = new float[Mathf.Max(1, segments)];
                _segmentStart = new float[Mathf.Max(1, segments)];
                MarkAllDirty();
                _syncedVersion = _spline.Version;
            }
            else if (_syncedVersion != _spline.Version)
            {
                MarkAllDirty();
                _syncedVersion = _spline.Version;
            }
            RebuildDirty();
        }

        void RebuildDirty()
        {
            if (_segmentCount == 0)
            {
                _totalLength = 0f;
                _lengthsDirty = false;
                return;
            }
            bool rebuilt = false;
            for (int s = 0; s < _segmentCount; s++)
            {
                if (!_segmentDirty[s]) continue;
                RebuildSegment(s);
                _segmentDirty[s] = false;
                rebuilt = true;
            }
            if (rebuilt || _lengthsDirty)
            {
                double accumulated = 0;
                for (int s = 0; s < _segmentCount; s++)
                {
                    _segmentStart[s] = (float)accumulated;
                    accumulated += _segmentLength[s];
                }
                _totalLength = (float)accumulated;
                _lengthsDirty = false;
                RecalculateBounds();
            }
        }

        void RecalculateBounds()
        {
            int total = _segmentCount * (SamplesPerSegment + 1);
            if (total == 0)
            {
                Vector3 center = _spline.Count > 0 ? _spline[0].Position : Vector3.zero;
                _bounds = new Bounds(center, Vector3.zero);
                return;
            }
            Vector3 min = _positions[0];
            Vector3 max = _positions[0];
            for (int i = 1; i < total; i++)
            {
                Vector3 p = _positions[i];
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            _bounds = bounds;
        }

        void RebuildSegment(int segment)
        {
            TbsCurve curve = _spline.GetCurve(segment);
            TbsKnot startKnot = _spline[segment];
            TbsKnot endKnot = _spline[(segment + 1) % _spline.Count];
            int baseIndex = segment * (SamplesPerSegment + 1);
            Vector3 prevPosition = curve.EvaluatePosition(0f);
            Vector3 prevTangent = SafeTangent(curve, 0f);
            Vector3 up = TbsSplineMath.OrthonormalUp(prevTangent, startKnot.Up);
            _positions[baseIndex] = prevPosition;
            _tangents[baseIndex] = prevTangent;
            _ups[baseIndex] = up;
            _cumulative[baseIndex] = 0f;
            double accumulated = 0;
            for (int i = 1; i <= SamplesPerSegment; i++)
            {
                float t = (float)i / SamplesPerSegment;
                Vector3 position = curve.EvaluatePosition(t);
                Vector3 tangent = SafeTangent(curve, t);
                TbsSplineMath.RmfStep(prevPosition, prevTangent, up, position, tangent, out Vector3 nextUp);
                up = TbsSplineMath.OrthonormalUp(tangent, nextUp);
                accumulated += Vector3.Distance(prevPosition, position);
                int index = baseIndex + i;
                _positions[index] = position;
                _tangents[index] = tangent;
                _ups[index] = up;
                _cumulative[index] = (float)accumulated;
                prevPosition = position;
                prevTangent = tangent;
            }
            _segmentLength[segment] = (float)accumulated;
            ApplyTwistCorrection(segment, endKnot);
        }

        void ApplyTwistCorrection(int segment, TbsKnot endKnot)
        {
            int baseIndex = segment * (SamplesPerSegment + 1);
            int last = baseIndex + SamplesPerSegment;
            Vector3 endTangent = _tangents[last];
            Vector3 target = TbsSplineMath.OrthonormalUp(endTangent, endKnot.Up);
            float angle = Vector3.SignedAngle(_ups[last], target, endTangent);
            if (Mathf.Abs(angle) < 1e-3f)
            {
                _ups[last] = target;
                return;
            }
            float inverseLength = _segmentLength[segment] > TbsSplineMath.Epsilon ? 1f / _segmentLength[segment] : 0f;
            for (int i = 1; i < SamplesPerSegment; i++)
            {
                int index = baseIndex + i;
                float weight = _cumulative[index] * inverseLength;
                _ups[index] = Quaternion.AngleAxis(angle * weight, _tangents[index]) * _ups[index];
            }
            _ups[last] = target;
        }

        Vector3 SampleUp(int segment, float t, Vector3 tangent)
        {
            int baseIndex = segment * (SamplesPerSegment + 1);
            float scaled = Mathf.Clamp01(t) * SamplesPerSegment;
            int index = Mathf.Min((int)scaled, SamplesPerSegment - 1);
            Vector3 up = Vector3.Slerp(_ups[baseIndex + index], _ups[baseIndex + index + 1], scaled - index);
            return TbsSplineMath.OrthonormalUp(tangent, up);
        }

        float SegmentTToDistance(int segment, float t)
        {
            int baseIndex = segment * (SamplesPerSegment + 1);
            float scaled = Mathf.Clamp01(t) * SamplesPerSegment;
            int index = Mathf.Min((int)scaled, SamplesPerSegment - 1);
            return Mathf.Lerp(_cumulative[baseIndex + index], _cumulative[baseIndex + index + 1], scaled - index);
        }

        float SegmentDistanceToT(int segment, float localDistance)
        {
            int baseIndex = segment * (SamplesPerSegment + 1);
            int lo = 0;
            int hi = SamplesPerSegment;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_cumulative[baseIndex + mid] <= localDistance) lo = mid;
                else hi = mid;
            }
            float a = _cumulative[baseIndex + lo];
            float b = _cumulative[baseIndex + hi];
            float fraction = b - a > TbsSplineMath.Epsilon ? (localDistance - a) / (b - a) : 0f;
            return (lo + fraction) / SamplesPerSegment;
        }

        int FindSegmentByDistance(float distance)
        {
            int lo = 0;
            int hi = _segmentCount - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (_segmentStart[mid] <= distance) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        void EvaluateDegenerate(ref TbsSample sample)
        {
            bool hasKnot = _spline.Count > 0;
            TbsKnot knot = hasKnot ? _spline[0] : default;
            sample.Position = hasKnot ? knot.Position : Vector3.zero;
            sample.Tangent = hasKnot ? knot.Rotation * Vector3.forward : Vector3.forward;
            sample.Up = hasKnot ? knot.Up : Vector3.up;
            sample.Distance = 0f;
            sample.T = 0f;
            sample.Size = hasKnot ? knot.Size : 1f;
            sample.Color = hasKnot ? knot.Color : Color.white;
        }

        static Vector3 SafeTangent(in TbsCurve curve, float t)
        {
            Vector3 derivative = curve.EvaluateDerivative(t);
            if (derivative.sqrMagnitude > TbsSplineMath.Epsilon) return derivative.normalized;
            Vector3 chord = curve.P3 - curve.P0;
            return chord.sqrMagnitude > TbsSplineMath.Epsilon ? chord.normalized : Vector3.forward;
        }
    }
}
