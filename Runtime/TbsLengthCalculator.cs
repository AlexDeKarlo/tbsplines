using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TBSplineS
{
    /// <summary>
    /// Direction of length change that fires a <see cref="TbsLengthEvent"/>.
    /// </summary>
    public enum TbsLengthEventType
    {
        /// <summary>Fires only when the spline gets longer past the threshold.</summary>
        Growing,

        /// <summary>Fires only when the spline gets shorter past the threshold.</summary>
        Shrinking,

        /// <summary>Fires in both directions.</summary>
        Both
    }

    /// <summary>
    /// Fires a callback when the measured length of a spline crosses a threshold.
    /// </summary>
    [Serializable]
    public sealed class TbsLengthEvent
    {
        /// <summary>Whether this event is evaluated at all.</summary>
        public bool Enabled = true;

        /// <summary>Length threshold to watch, in world units.</summary>
        public float TargetLength = 1f;

        /// <summary>Direction of change the event reacts to.</summary>
        public TbsLengthEventType Type = TbsLengthEventType.Both;

        /// <summary>Invoked when the threshold is crossed.</summary>
        public UnityEvent OnCross = new UnityEvent();

        /// <summary>
        /// Returns whether a change from one length to another crosses this event's threshold in a watched
        /// direction. Does not invoke <see cref="OnCross"/>.
        /// </summary>
        /// <param name="previous">Length measured on the previous build.</param>
        /// <param name="current">Length measured now.</param>
        public bool Check(float previous, float current)
        {
            if (!Enabled) return false;
            bool grew = previous < TargetLength && current >= TargetLength;
            bool shrank = previous > TargetLength && current <= TargetLength;
            if (grew && (Type == TbsLengthEventType.Growing || Type == TbsLengthEventType.Both)) return true;
            if (shrank && (Type == TbsLengthEventType.Shrinking || Type == TbsLengthEventType.Both)) return true;
            return false;
        }
    }

    /// <summary>
    /// Measures the length of the spline it follows and raises events when that length crosses configured
    /// thresholds. Useful for progress bars, pacing and gating gameplay on how far a path has been extended.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("TBSplineS/Length Calculator")]
    public sealed class TbsLengthCalculator : TbsSplineUser
    {
        [SerializeField] List<TbsLengthEvent> _events = new List<TbsLengthEvent>();

        [NonSerialized] float _lastLength;
        [NonSerialized] bool _hasBaseline;

        /// <summary>Length measured on the most recent build, in world units.</summary>
        public float CurrentLength { get; private set; }

        /// <summary>Thresholds watched by this component. Mutating the list changes them directly.</summary>
        public List<TbsLengthEvent> Events => _events;

        protected override void PostBuild()
        {
            CurrentLength = Length;
            if (_hasBaseline && !Mathf.Approximately(_lastLength, CurrentLength))
            {
                for (int i = 0; i < _events.Count; i++)
                {
                    TbsLengthEvent lengthEvent = _events[i];
                    if (lengthEvent != null && lengthEvent.Check(_lastLength, CurrentLength))
                        lengthEvent.OnCross.Invoke();
                }
            }
            _lastLength = CurrentLength;
            _hasBaseline = true;
        }
    }
}
