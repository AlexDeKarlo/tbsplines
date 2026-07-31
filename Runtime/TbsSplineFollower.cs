using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TBSplineS
{
    /// <summary>
    /// What a follower does on reaching the end of the spline.
    /// </summary>
    public enum TbsFollowMode
    {
        /// <summary>Halts at the end.</summary>
        Stop,

        /// <summary>Jumps back to the start and keeps going.</summary>
        Loop,

        /// <summary>Reverses direction and travels back.</summary>
        PingPong
    }

    /// <summary>
    /// What drives a follower's pace.
    /// </summary>
    public enum TbsFollowInput
    {
        /// <summary>Travels at a set speed in world units per second, so a longer path takes longer.</summary>
        Uniform,

        /// <summary>Covers the whole spline in a set time, so a longer path is travelled faster.</summary>
        Time
    }

    /// <summary>
    /// A stretch of spline where a follower's speed is overridden, for slow corners and boost pads.
    /// </summary>
    [Serializable]
    public sealed class TbsSpeedRegion
    {
        /// <summary>Where the region begins, from 0 at the spline start to 1 at its end.</summary>
        [Range(0f, 1f)] public float From;

        /// <summary>Where the region ends.</summary>
        [Range(0f, 1f)] public float To = 1f;

        /// <summary>Speed multiplier or replacement value, depending on <see cref="Multiply"/>.</summary>
        public float Value = 1f;

        /// <summary>Scales the follower's speed when true, replaces it outright when false.</summary>
        public bool Multiply = true;
    }

    /// <summary>
    /// Moves an object along a spline at a controlled pace, firing triggers on the way and taking branches at
    /// junctions. The workhorse for vehicles, patrolling enemies, cameras and moving platforms.
    /// </summary>
    [AddComponentMenu("TBSplineS/Spline Follower")]
    public sealed class TbsSplineFollower : MonoBehaviour
    {
        [Tooltip("Spline Computer that owns the spline to follow")]
        [SerializeField] TbsSplineComputer _computer;
        [Tooltip("Stable id of the spline inside the computer")]
        [SerializeField] int _splineId = 1;
        [Tooltip("Movement speed in meters per second, negative values move backwards")]
        [SerializeField] float _speed = 5f;
        [Tooltip("Behaviour at an open spline end: stop and raise ReachedEnd, loop to the start, or bounce back")]
        [SerializeField] TbsFollowMode _endMode = TbsFollowMode.Loop;
        [Tooltip("Rotate the object along the spline tangent and up vector")]
        [SerializeField] bool _alignRotation = true;
        [Tooltip("Offset in the spline frame: X right, Y up, Z along the tangent")]
        [SerializeField] Vector3 _positionOffset;
        [Tooltip("Distance in meters from the spline start where the follower begins")]
        [SerializeField] float _startDistance;
        [Tooltip("Start moving automatically when Play mode begins")]
        [SerializeField] bool _playOnStart = true;
        [Tooltip("Drive an attached kinematic Rigidbody in FixedUpdate instead of the Transform in Update")]
        [SerializeField] bool _moveRigidbody;
        [Tooltip("Uniform: move at Speed meters per second. Time: traverse the whole spline in Duration seconds")]
        [SerializeField] TbsFollowInput _followInput = TbsFollowInput.Uniform;
        [Tooltip("Seconds to traverse the whole spline in Time mode")]
        [SerializeField, Min(0.01f)] float _duration = 10f;
        [Tooltip("Project this object onto the spline at start instead of using Start Distance")]
        [SerializeField] bool _autoStartPosition;
        [Tooltip("Speed zones along the spline: multiply or add to the base speed inside each region")]
        [SerializeField] List<TbsSpeedRegion> _speedRegions = new List<TbsSpeedRegion>();
        [Tooltip("Invoked when the follower stops at an open spline end in Stop mode")]
        [SerializeField] UnityEvent _onEndReached = new UnityEvent();

        [NonSerialized] float _distance;
        [NonSerialized] float _direction = 1f;
        [NonSerialized] bool _playing;
        [NonSerialized] bool _initialized;
        [NonSerialized] int _cachedIndex = -1;
        [NonSerialized] Rigidbody _rigidbody;
        TbsSample _sample;

        /// <summary>Raised when the follower reaches the end of the spline, whatever the end mode.</summary>
        public event Action<TbsSplineFollower> ReachedEnd;

        /// <summary>
        /// Raised when the follower arrives at a junction, carrying the junction and the knot it arrived on.
        /// Handle this and call <see cref="SwitchToBranch"/> to choose which way to go.
        /// </summary>
        public event Action<TbsSplineFollower, TbsJunction, TbsKnotRef> JunctionReached;

        /// <summary>Spline computer that owns the spline being followed.</summary>
        public TbsSplineComputer Computer
        {
            get => _computer;
            set
            {
                _computer = value;
                _cachedIndex = -1;
                if (_computer != null && _computer.IndexOfSplineId(_splineId) < 0 && _computer.SplineCount > 0)
                    _splineId = _computer[0].Id;
            }
        }

        /// <summary>
        /// Identifier of the spline being followed. Falls back to the first spline when unmatched.
        /// </summary>
        public int SplineId
        {
            get => _splineId;
            set
            {
                _splineId = value;
                _cachedIndex = -1;
            }
        }

        /// <summary>
        /// Travel speed in world units per second. Used when the follow input is uniform; negative values
        /// travel backwards.
        /// </summary>
        public float Speed
        {
            get => _speed;
            set => _speed = value;
        }

        /// <summary>What happens on reaching the end of the spline.</summary>
        public TbsFollowMode EndMode
        {
            get => _endMode;
            set => _endMode = value;
        }

        /// <summary>
        /// Moves through the rigidbody on this object instead of writing the transform, so the follower
        /// collides with the world on the way. Requires a kinematic rigidbody.
        /// </summary>
        public bool MoveRigidbody
        {
            get => _moveRigidbody;
            set => _moveRigidbody = value;
        }

        /// <summary>Rotates the object to face along the spline as it travels.</summary>
        public bool AlignRotation
        {
            get => _alignRotation;
            set => _alignRotation = value;
        }

        /// <summary>Whether the follower is currently moving.</summary>
        public bool IsPlaying => _playing;

        /// <summary>Current direction of travel: 1 towards the end of the spline, -1 towards the start.</summary>
        public float Direction => _direction;

        /// <summary>The spline sample the follower was last placed at.</summary>
        public TbsSample Sample => _sample;

        /// <summary>
        /// Position along the spline as an arc length from its start, in world units. Setting it teleports the
        /// follower without firing the triggers in between.
        /// </summary>
        public float Distance
        {
            get
            {
                EnsureInitialized();
                return _distance;
            }
            set
            {
                EnsureInitialized();
                _distance = value;
                Apply();
            }
        }

        /// <summary>Length of the spline being followed, in world units.</summary>
        public float Length
        {
            get
            {
                TbsSplineCache cache = ResolveCache();
                return cache != null ? cache.TotalLength : 0f;
            }
        }

        /// <summary>Position along the spline from 0 at the start to 1 at the end.</summary>
        public float NormalizedT
        {
            get
            {
                float length = Length;
                return length > TbsSplineMath.Epsilon ? Distance / length : 0f;
            }
            set => Distance = value * Length;
        }

        /// <summary>Starts or resumes travel from the current position.</summary>
        public void Play()
        {
            EnsureInitialized();
            _playing = true;
        }

        /// <summary>Halts travel, keeping the current position.</summary>
        public void Pause()
        {
            _playing = false;
        }

        /// <summary>Whether pace comes from a speed or from a total duration.</summary>
        public TbsFollowInput FollowInput
        {
            get => _followInput;
            set => _followInput = value;
        }

        /// <summary>
        /// Seconds to cover the whole spline. Used when the follow input is time-based. Clamped to a small
        /// positive minimum.
        /// </summary>
        public float Duration
        {
            get => _duration;
            set => _duration = Mathf.Max(0.01f, value);
        }

        /// <summary>Starts from the object's own position, projected onto the spline, instead of from the start.</summary>
        public bool AutoStartPosition
        {
            get => _autoStartPosition;
            set => _autoStartPosition = value;
        }

        /// <summary>Stretches of spline where the speed is overridden. Mutating the list changes them directly.</summary>
        public List<TbsSpeedRegion> SpeedRegions => _speedRegions;

        /// <summary>Inspector-assignable counterpart of <see cref="ReachedEnd"/>.</summary>
        public UnityEvent EndReached => _onEndReached;

        /// <summary>
        /// Advances the follower by a distance right now, firing any triggers crossed on the way and handling
        /// the end of the spline. Use it to drive movement yourself instead of by speed or duration.
        /// </summary>
        /// <param name="distance">Distance in world units. Negative values move backwards.</param>
        public void Move(float distance)
        {
            EnsureInitialized();
            TbsSplineCache cache = ResolveCache();
            if (cache == null) return;
            float length = cache.TotalLength;
            if (length <= TbsSplineMath.Epsilon) return;
            AdvanceDistance(distance, cache, length);
            Apply();
        }

        /// <summary>
        /// Advances the follower by one time step, working out the distance from the speed or duration and any
        /// speed regions in force. Called for you while playing.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds.</param>
        public void Advance(float deltaTime)
        {
            EnsureInitialized();
            TbsSplineCache cache = ResolveCache();
            if (cache == null) return;
            float length = cache.TotalLength;
            if (length <= TbsSplineMath.Epsilon) return;
            float baseSpeed = _followInput == TbsFollowInput.Time ? length / Mathf.Max(_duration, 0.01f) : _speed;
            baseSpeed = ApplySpeedRegions(baseSpeed, length);
            AdvanceDistance(baseSpeed * deltaTime * _direction, cache, length);
        }

        float ApplySpeedRegions(float speed, float length)
        {
            if (_speedRegions == null || _speedRegions.Count == 0) return speed;
            float t = Mathf.Clamp01(_distance / length);
            for (int i = 0; i < _speedRegions.Count; i++)
            {
                TbsSpeedRegion region = _speedRegions[i];
                if (region == null || t < region.From || t > region.To) continue;
                speed = region.Multiply ? speed * region.Value : speed + region.Value;
            }
            return speed;
        }

        void AdvanceDistance(float step, TbsSplineCache cache, float length)
        {
            float oldDistance = _distance;
            _distance += step;
            if (cache.Spline.Closed)
            {
                _distance = Mathf.Repeat(_distance, length);
                CheckTriggers(cache, oldDistance, length);
                return;
            }
            switch (_endMode)
            {
                case TbsFollowMode.Stop:
                    if (step > 0f && _distance >= length)
                    {
                        _distance = length;
                        StopAtEnd();
                    }
                    else if (step < 0f && _distance <= 0f)
                    {
                        _distance = 0f;
                        StopAtEnd();
                    }
                    break;
                case TbsFollowMode.Loop:
                    if (_distance > length || _distance < 0f) _distance = Mathf.Repeat(_distance, length);
                    break;
                case TbsFollowMode.PingPong:
                    while (_distance < 0f || _distance > length)
                    {
                        if (_distance < 0f)
                        {
                            _distance = -_distance;
                            _direction = -_direction;
                        }
                        else
                        {
                            _distance = 2f * length - _distance;
                            _direction = -_direction;
                        }
                    }
                    break;
            }
            CheckTriggers(cache, oldDistance, length);
        }

        void CheckTriggers(TbsSplineCache cache, float oldDistance, float length)
        {
            if (Mathf.Approximately(oldDistance, _distance)) return;
            if (Application.isPlaying)
            {
                float fromT = oldDistance / length;
                float toT = _distance / length;
                cache.Spline.CheckTriggers(fromT, toT);
                TbsSplineTriggerZone.NotifyCrossing(this, _computer, cache.Spline, fromT, toT);
            }
            CheckJunctions(cache, oldDistance);
        }

        void CheckJunctions(TbsSplineCache cache, float oldDistance)
        {
            if (JunctionReached == null || _computer == null) return;
            TbsSpline spline = cache.Spline;
            int splineId = spline.Id;
            float lo = Mathf.Min(oldDistance, _distance);
            float hi = Mathf.Max(oldDistance, _distance);
            IReadOnlyList<TbsJunction> junctions = _computer.Junctions;
            for (int j = 0; j < junctions.Count; j++)
            {
                TbsJunction junction = junctions[j];
                for (int m = 0; m < junction.Members.Count; m++)
                {
                    TbsKnotRef member = junction.Members[m];
                    if (member.SplineId != splineId) continue;
                    int knotIndex = spline.IndexOfKnotId(member.KnotId);
                    if (knotIndex < 0) continue;
                    float knotDistance = cache.KnotToDistance(knotIndex);
                    if (knotDistance > lo + 1e-4f && knotDistance <= hi + 1e-4f)
                        JunctionReached.Invoke(this, junction, member);
                }
            }
        }

        /// <summary>
        /// Moves the follower onto another spline at a junction, keeping it in motion. Call this from a
        /// <see cref="JunctionReached"/> handler to pick which way the follower goes.
        /// </summary>
        /// <param name="branch">Knot to continue from, normally one of the junction's other members.</param>
        /// <returns>False when the knot cannot be resolved.</returns>
        public bool SwitchToBranch(TbsKnotRef branch)
        {
            EnsureInitialized();
            if (_computer == null) return false;
            if (!_computer.ResolveRef(branch, out int splineIndex, out int knotIndex)) return false;
            _splineId = _computer[splineIndex].Id;
            _cachedIndex = splineIndex;
            TbsSplineCache cache = _computer.GetCache(splineIndex);
            _distance = cache.KnotToDistance(knotIndex);
            Apply();
            return true;
        }

        void StopAtEnd()
        {
            if (!_playing) return;
            _playing = false;
            ReachedEnd?.Invoke(this);
            _onEndReached.Invoke();
        }

        /// <summary>
        /// Places the object at the follower's current position immediately, without advancing it. Useful after
        /// setting <see cref="Distance"/> when the result has to be visible this frame.
        /// </summary>
        public void Apply()
        {
            EnsureInitialized();
            if (_computer == null || ResolveCache() == null) return;
            _computer.EvaluateAtDistance(_cachedIndex, _distance, ref _sample);
            Vector3 position = _sample.Position + _sample.Rotation * _positionOffset;
            if (_moveRigidbody && _rigidbody != null && Application.isPlaying)
            {
                _rigidbody.MovePosition(position);
                if (_alignRotation) _rigidbody.MoveRotation(_sample.Rotation);
            }
            else
            {
                transform.position = position;
                if (_alignRotation) transform.rotation = _sample.Rotation;
            }
        }

        void Start()
        {
            EnsureInitialized();
            if (_playOnStart) _playing = true;
            Apply();
        }

        void Update()
        {
            if (_moveRigidbody && _rigidbody != null) return;
            if (!_playing) return;
            Advance(Time.deltaTime);
            Apply();
        }

        void FixedUpdate()
        {
            if (!_moveRigidbody || _rigidbody == null) return;
            if (!_playing) return;
            Advance(Time.fixedDeltaTime);
            Apply();
        }

        void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            _distance = _startDistance;
            _direction = 1f;
            _rigidbody = GetComponent<Rigidbody>();
            if (_autoStartPosition && _computer != null)
            {
                TbsSplineCache cache = ResolveCache();
                if (cache != null)
                {
                    TbsSample projected = default;
                    _computer.GetNearestPoint(_cachedIndex, transform.position, ref projected);
                    _distance = projected.Distance;
                }
            }
        }

        TbsSplineCache ResolveCache()
        {
            if (_computer == null) return null;
            if (_cachedIndex < 0 || _cachedIndex >= _computer.SplineCount || _computer[_cachedIndex].Id != _splineId)
            {
                _cachedIndex = _computer.IndexOfSplineId(_splineId);
                if (_cachedIndex < 0 && _computer.SplineCount > 0) _cachedIndex = 0;
            }
            return _cachedIndex >= 0 ? _computer.GetCache(_cachedIndex) : null;
        }

        void OnValidate()
        {
            _cachedIndex = -1;
        }
    }
}
