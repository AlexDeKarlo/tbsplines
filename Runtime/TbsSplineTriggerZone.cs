using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TBSplineS
{
    /// <summary>
    /// A checkpoint on a spline, authored as a scene object so its events can be wired in the inspector and its
    /// position tweaked with gizmos. Fires when a <see cref="TbsSplineFollower"/> passes it, with optional
    /// direction filtering, cooldown and per-follower targeting.
    /// </summary>
    [AddComponentMenu("TBSplineS/Spline Trigger")]
    public sealed class TbsSplineTriggerZone : MonoBehaviour
    {
        static readonly List<TbsSplineTriggerZone> Active = new List<TbsSplineTriggerZone>();

        [Tooltip("Spline Computer that owns the target spline")]
        [SerializeField] TbsSplineComputer _computer;
        [Tooltip("Stable id of the spline inside the computer")]
        [SerializeField] int _splineId = 1;
        [Tooltip("Normalized position of the trigger on the spline")]
        [SerializeField, Range(0f, 1f)] float _position = 0.5f;
        [Tooltip("Which travel direction fires the trigger")]
        [SerializeField] TbsTriggerType _direction = TbsTriggerType.Double;
        [Tooltip("Fire only once until ResetState is called")]
        [SerializeField] bool _fireOnce;
        [Tooltip("Minimum seconds between firings, 0 = no cooldown")]
        [SerializeField, Min(0f)] float _cooldown;
        [Tooltip("Only this follower fires the trigger, empty = any follower")]
        [SerializeField] TbsSplineFollower _onlyFollower;
        [Tooltip("Marker color on the spline")]
        [SerializeField] Color _markerColor = new Color(1f, 0.75f, 0.25f);
        [Tooltip("Invoked on every crossing")]
        [SerializeField] UnityEvent _onCrossed = new UnityEvent();
        [Tooltip("Invoked only on the first crossing")]
        [SerializeField] UnityEvent _onFirstCross = new UnityEvent();
        [Tooltip("Invoked on the second and later crossings")]
        [SerializeField] UnityEvent _onRepeatCross = new UnityEvent();

        [NonSerialized] int _crossCount;
        [NonSerialized] float _lastFireTime = float.NegativeInfinity;

        /// <summary>Raised on every firing, carrying the trigger and the follower that crossed it.</summary>
        public event Action<TbsSplineTriggerZone, TbsSplineFollower> Crossed;

        /// <summary>Spline computer that owns the target spline.</summary>
        public TbsSplineComputer Computer
        {
            get => _computer;
            set
            {
                _computer = value;
                if (_computer != null && _computer.IndexOfSplineId(_splineId) < 0 && _computer.SplineCount > 0)
                    _splineId = _computer[0].Id;
            }
        }

        /// <summary>Identifier of the spline this trigger sits on. Falls back to the first spline when unmatched.</summary>
        public int SplineId
        {
            get => _splineId;
            set => _splineId = value;
        }

        /// <summary>Placement along the spline, from 0 at the start to 1 at the end. Values are clamped.</summary>
        public float Position
        {
            get => _position;
            set => _position = Mathf.Clamp01(value);
        }

        /// <summary>Travel direction that fires this trigger.</summary>
        public TbsTriggerType Direction
        {
            get => _direction;
            set => _direction = value;
        }

        /// <summary>Color of the scene-view marker.</summary>
        public Color MarkerColor => _markerColor;

        /// <summary>Inspector-assignable event invoked on every firing.</summary>
        public UnityEvent OnCrossed => _onCrossed;

        /// <summary>Inspector-assignable event invoked on the first firing only.</summary>
        public UnityEvent OnFirstCross => _onFirstCross;

        /// <summary>Inspector-assignable event invoked on the second and later firings.</summary>
        public UnityEvent OnRepeatCross => _onRepeatCross;

        /// <summary>How many times this trigger has fired since the last reset.</summary>
        public int CrossCount => _crossCount;

        /// <summary>
        /// Clears the crossing count and cooldown, re-arming a fire-once trigger.
        /// </summary>
        public void ResetState()
        {
            _crossCount = 0;
            _lastFireTime = float.NegativeInfinity;
        }

        void OnEnable()
        {
            Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
        }

        /// <summary>
        /// Evaluates a follower's move against this trigger and fires it if every condition passes. Followers
        /// call this for you; use it directly only when driving movement yourself.
        /// </summary>
        /// <param name="follower">Follower that moved.</param>
        /// <param name="fromT">Normalized position before the move.</param>
        /// <param name="toT">Normalized position after the move.</param>
        /// <returns>True when the trigger fired.</returns>
        public bool ProcessCrossing(TbsSplineFollower follower, float fromT, float toT)
        {
            if (_onlyFollower != null && follower != _onlyFollower) return false;
            if (_fireOnce && _crossCount > 0) return false;
            bool forward = toT > fromT;
            bool crossed = forward
                ? fromT < _position && toT >= _position
                : fromT > _position && toT <= _position;
            if (!crossed) return false;
            if (_direction == TbsTriggerType.Forward && !forward) return false;
            if (_direction == TbsTriggerType.Backward && forward) return false;
            if (_cooldown > 0f && Time.time - _lastFireTime < _cooldown) return false;
            _lastFireTime = Time.time;
            _crossCount++;
            if (_crossCount == 1) _onFirstCross.Invoke();
            else _onRepeatCross.Invoke();
            _onCrossed.Invoke();
            Crossed?.Invoke(this, follower);
            return true;
        }

        /// <summary>
        /// Resolves where this trigger sits in the world.
        /// </summary>
        /// <param name="world">Receives the world position of the trigger.</param>
        /// <param name="tangent">Receives the spline tangent there.</param>
        /// <returns>False when no spline is assigned, in which case the outputs hold defaults.</returns>
        public bool TryGetWorldPosition(out Vector3 world, out Vector3 tangent)
        {
            world = Vector3.zero;
            tangent = Vector3.forward;
            if (_computer == null) return false;
            int index = _computer.IndexOfSplineId(_splineId);
            if (index < 0 && _computer.SplineCount > 0) index = 0;
            if (index < 0) return false;
            TbsSample sample = default;
            _computer.Evaluate(index, _position, ref sample);
            world = sample.Position;
            tangent = sample.Tangent;
            return true;
        }

        /// <summary>
        /// Offers a follower's move to every enabled trigger on the given spline. Called by followers as they
        /// advance.
        /// </summary>
        /// <param name="follower">Follower that moved.</param>
        /// <param name="computer">Computer the follower is bound to.</param>
        /// <param name="spline">Spline the follower is travelling on.</param>
        /// <param name="fromT">Normalized position before the move.</param>
        /// <param name="toT">Normalized position after the move.</param>
        public static void NotifyCrossing(TbsSplineFollower follower, TbsSplineComputer computer, TbsSpline spline, float fromT, float toT)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                TbsSplineTriggerZone zone = Active[i];
                if (zone._computer != computer) continue;
                int index = computer.IndexOfSplineId(zone._splineId);
                if (index < 0 && computer.SplineCount > 0) index = 0;
                if (index < 0 || computer[index] != spline) continue;
                zone.ProcessCrossing(follower, fromT, toT);
            }
        }

        void OnDrawGizmos()
        {
            if (!TryGetWorldPosition(out Vector3 world, out Vector3 tangent)) return;
            Gizmos.color = _markerColor;
            Gizmos.DrawSphere(world, 0.35f);
            Vector3 dir = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : Vector3.forward;
            if (_direction != TbsTriggerType.Backward) Gizmos.DrawLine(world + dir * 0.5f, world + dir * 1.2f);
            if (_direction != TbsTriggerType.Forward) Gizmos.DrawLine(world - dir * 0.5f, world - dir * 1.2f);
            Gizmos.color = new Color(_markerColor.r, _markerColor.g, _markerColor.b, 0.35f);
            Gizmos.DrawLine(transform.position, world);
        }
    }
}
