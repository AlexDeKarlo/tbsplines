using System;
using UnityEngine;
using UnityEngine.Events;

namespace TBSplineS
{
    /// <summary>
    /// Finds the point on the spline nearest to a source transform and snaps an object to it. Use it to keep a
    /// character on a rail, to read how far along a path something is, or to trigger logic at the path ends.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("TBSplineS/Spline Projector")]
    public sealed class TbsSplineProjector : MonoBehaviour
    {
        [Tooltip("Spline Computer that owns the target spline")]
        [SerializeField] TbsSplineComputer _computer;
        [Tooltip("Stable id of the spline inside the computer")]
        [SerializeField] int _splineId = 1;
        [Tooltip("Transform whose position is projected onto the spline, empty means this object")]
        [SerializeField] Transform _source;
        [Tooltip("Transform that gets moved to the projected point, empty means this object")]
        [SerializeField] Transform _moveTarget;
        [Tooltip("Snap to the nearest spline point every LateUpdate")]
        [SerializeField] bool _follow = true;
        [Tooltip("Rotate the moved object along the spline at the projected point")]
        [SerializeField] bool _alignRotation;
        [Tooltip("Offset in the spline frame applied after projection")]
        [SerializeField] Vector3 _positionOffset;
        [Tooltip("Invoked when the projected point reaches the spline end")]
        [SerializeField] UnityEvent _onReachedEnd = new UnityEvent();
        [Tooltip("Invoked when the projected point reaches the spline start")]
        [SerializeField] UnityEvent _onReachedStart = new UnityEvent();

        [NonSerialized] int _cachedIndex = -1;
        [NonSerialized] bool _hasLast;
        [NonSerialized] Vector3 _lastSourcePosition;
        [NonSerialized] int _lastVersion;
        [NonSerialized] bool _wasAtEnd;
        [NonSerialized] bool _wasAtStart;
        TbsSample _sample;

        /// <summary>Raised the first frame the projected point settles at the end of the spline.</summary>
        public event Action<TbsSplineProjector> ReachedEnd;

        /// <summary>Raised the first frame the projected point settles at the start of the spline.</summary>
        public event Action<TbsSplineProjector> ReachedStart;

        /// <summary>Spline computer that owns the target spline. Setting it re-resolves the spline.</summary>
        public TbsSplineComputer Computer
        {
            get => _computer;
            set
            {
                _computer = value;
                _cachedIndex = -1;
                _hasLast = false;
                if (_computer != null && _computer.IndexOfSplineId(_splineId) < 0 && _computer.SplineCount > 0)
                    _splineId = _computer[0].Id;
            }
        }

        /// <summary>
        /// Identifier of the spline to project onto. Falls back to the first spline when no spline carries it.
        /// </summary>
        public int SplineId
        {
            get => _splineId;
            set
            {
                _splineId = value;
                _cachedIndex = -1;
                _hasLast = false;
            }
        }

        /// <summary>Transform whose position is projected. Falls back to this component's transform when empty.</summary>
        public Transform Source
        {
            get => _source;
            set { _source = value; _hasLast = false; }
        }

        /// <summary>Transform moved to the projected point. Falls back to this component's transform when empty.</summary>
        public Transform MoveTarget
        {
            get => _moveTarget;
            set => _moveTarget = value;
        }

        /// <summary>Inspector-assignable counterpart of <see cref="ReachedEnd"/>.</summary>
        public UnityEvent OnReachedEnd => _onReachedEnd;

        /// <summary>Inspector-assignable counterpart of <see cref="ReachedStart"/>.</summary>
        public UnityEvent OnReachedStart => _onReachedStart;

        /// <summary>The spline sample produced by the most recent projection.</summary>
        public TbsSample LastSample => _sample;

        /// <summary>Position along the spline of the most recent projection, from 0 at the start to 1 at the end.</summary>
        public float LastT => _sample.T;

        /// <summary>
        /// Projects an arbitrary world point onto the spline without moving anything.
        /// </summary>
        /// <param name="worldPoint">Point to project.</param>
        /// <param name="sample">Receives the nearest point on the spline.</param>
        /// <returns>False when no spline is assigned, in which case <paramref name="sample"/> is untouched.</returns>
        public bool Project(Vector3 worldPoint, ref TbsSample sample)
        {
            if (!ResolveIndex()) return false;
            _computer.GetNearestPoint(_cachedIndex, worldPoint, ref sample);
            return true;
        }

        /// <summary>
        /// Projects the source and moves the target onto the spline. Runs automatically every LateUpdate while
        /// following is on; call it directly to snap within the current frame. Recomputes only when the source
        /// or the spline actually changed.
        /// </summary>
        /// <returns>False when no spline is assigned.</returns>
        public bool Snap()
        {
            Transform sourceTransform = _source != null ? _source : transform;
            Vector3 sourcePosition = sourceTransform.position;
            if (!ResolveIndex()) return false;
            int version = _computer[_cachedIndex].Version;
            if (!_hasLast || sourcePosition != _lastSourcePosition || version != _lastVersion)
            {
                _computer.GetNearestPoint(_cachedIndex, sourcePosition, ref _sample);
                _lastSourcePosition = sourcePosition;
                _lastVersion = version;
                _hasLast = true;
                CheckEdges();
            }
            Transform moved = _moveTarget != null ? _moveTarget : transform;
            Vector3 targetPosition = _sample.Position + _sample.Rotation * _positionOffset;
            if ((moved.position - targetPosition).sqrMagnitude > 1e-10f) moved.position = targetPosition;
            if (_alignRotation && Quaternion.Angle(moved.rotation, _sample.Rotation) > 1e-3f) moved.rotation = _sample.Rotation;
            return true;
        }

        void CheckEdges()
        {
            bool atEnd = _sample.T >= 1f - 1e-3f;
            bool atStart = _sample.T <= 1e-3f;
            if (atEnd && !_wasAtEnd)
            {
                ReachedEnd?.Invoke(this);
                _onReachedEnd.Invoke();
            }
            if (atStart && !_wasAtStart)
            {
                ReachedStart?.Invoke(this);
                _onReachedStart.Invoke();
            }
            _wasAtEnd = atEnd;
            _wasAtStart = atStart;
        }

        void LateUpdate()
        {
            if (_follow) Snap();
        }

        bool ResolveIndex()
        {
            if (_computer == null) return false;
            if (_cachedIndex < 0 || _cachedIndex >= _computer.SplineCount || _computer[_cachedIndex].Id != _splineId)
            {
                _cachedIndex = _computer.IndexOfSplineId(_splineId);
                if (_cachedIndex < 0 && _computer.SplineCount > 0) _cachedIndex = 0;
            }
            return _cachedIndex >= 0;
        }

        void OnValidate()
        {
            _cachedIndex = -1;
            _hasLast = false;
        }
    }
}
