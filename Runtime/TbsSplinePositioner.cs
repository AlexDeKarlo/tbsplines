using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// How a positioner's placement along the spline is expressed.
    /// </summary>
    public enum TbsPositionMode
    {
        /// <summary>Fraction of the spline, from 0 at the start to 1 at the end.</summary>
        Percent,

        /// <summary>Arc length from the start of the spline, in world units.</summary>
        Distance
    }

    /// <summary>
    /// Pins a transform to a fixed point on the spline. Unlike a follower it does not advance on its own, so it
    /// suits markers, cameras and anything you drive by setting <see cref="Position"/> yourself.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("TBSplineS/Spline Positioner")]
    public sealed class TbsSplinePositioner : TbsSplineUser
    {
        [SerializeField] TbsPositionMode _mode = TbsPositionMode.Percent;
        [SerializeField] float _position;
        [SerializeField] Transform _target;
        [SerializeField] TbsMotionModule _motion = new TbsMotionModule();

        TbsSample _sample;

        /// <summary>Unit <see cref="Position"/> is expressed in.</summary>
        public TbsPositionMode PositionMode
        {
            get => _mode;
            set { _mode = value; SetDirty(); }
        }

        /// <summary>Placement along the spline, in <see cref="PositionMode"/> units. Clamped to the spline's extent.</summary>
        public float Position
        {
            get => _position;
            set { _position = value; SetDirty(); }
        }

        /// <summary>Transform to place. Falls back to this component's own transform when left empty.</summary>
        public Transform Target
        {
            get => _target;
            set { _target = value; SetDirty(); }
        }

        /// <summary>The spline sample the target was last placed at.</summary>
        public TbsSample Sample => _sample;

        protected override void PostBuild()
        {
            Apply();
        }

        /// <summary>
        /// Immediately moves the target to <see cref="Position"/>. Called automatically after each build; call it
        /// yourself when you need the placement applied within the current frame.
        /// </summary>
        public void Apply()
        {
            TbsSplineCache cache = ResolveCache();
            if (cache == null) return;
            Transform target = _target != null ? _target : transform;
            float localT;
            if (_mode == TbsPositionMode.Distance)
            {
                float length = Length;
                localT = length > TbsSplineMath.Epsilon ? _position / length : 0f;
            }
            else
            {
                localT = _position;
            }
            Evaluate(Mathf.Clamp01(localT), ref _sample);
            _motion.ApplyTo(target, _sample);
        }

        void OnDidApplyAnimationProperties()
        {
            Apply();
        }
    }
}
