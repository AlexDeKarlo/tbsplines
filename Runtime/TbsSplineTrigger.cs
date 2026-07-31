using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TBSplineS
{
    /// <summary>
    /// Travel direction that fires a trigger.
    /// </summary>
    public enum TbsTriggerType
    {
        /// <summary>Fires when crossed in either direction.</summary>
        Double,

        /// <summary>Fires only when crossed while moving towards the end of the spline.</summary>
        Forward,

        /// <summary>Fires only when crossed while moving towards the start of the spline.</summary>
        Backward
    }

    /// <summary>
    /// A point on a spline that raises an event when something passes it. Stored on the spline itself rather
    /// than as a scene object; see <see cref="TbsSplineTriggerZone"/> for the component-based alternative.
    /// </summary>
    [Serializable]
    public sealed class TbsSplineTrigger
    {
        /// <summary>Label shown in the editor.</summary>
        public string Name = "Trigger";

        /// <summary>Marker color used when drawing the trigger in the scene view.</summary>
        public Color Color = Color.yellow;

        /// <summary>Placement along the spline, from 0 at the start to 1 at the end.</summary>
        [Range(0f, 1f)] public float Position = 0.5f;

        /// <summary>Travel direction that fires this trigger.</summary>
        public TbsTriggerType Type = TbsTriggerType.Double;

        /// <summary>Fires at most once until <see cref="ResetState"/> is called.</summary>
        public bool WorkOnce;

        /// <summary>Whether this trigger is evaluated at all.</summary>
        public bool Enabled = true;

        /// <summary>Invoked when the trigger fires.</summary>
        public UnityEvent OnCross = new UnityEvent();

        [NonSerialized] bool _worked;

        /// <summary>
        /// Reports whether a move between two positions fires this trigger, and marks it as used when it does.
        /// Does not invoke <see cref="OnCross"/>.
        /// </summary>
        /// <param name="from">Normalized position before the move.</param>
        /// <param name="to">Normalized position after the move.</param>
        public bool Check(float from, float to)
        {
            if (!Enabled) return false;
            if (WorkOnce && _worked) return false;
            bool forward = to > from;
            bool crossed = forward
                ? from < Position && to >= Position
                : from > Position && to <= Position;
            if (!crossed) return false;
            if (Type == TbsTriggerType.Forward && !forward) return false;
            if (Type == TbsTriggerType.Backward && forward) return false;
            _worked = true;
            return true;
        }

        /// <summary>Clears the fired flag so a once-only trigger can fire again.</summary>
        public void ResetState() => _worked = false;
    }

    /// <summary>
    /// A named set of triggers that can be enabled or disabled together, for example to arm a checkpoint line
    /// only during a particular lap.
    /// </summary>
    [Serializable]
    public sealed class TbsTriggerGroup
    {
        /// <summary>Label shown in the editor.</summary>
        public string Name = "Group";

        /// <summary>Marker color used when drawing this group's triggers.</summary>
        public Color Color = Color.cyan;

        /// <summary>Whether the triggers in this group are evaluated at all.</summary>
        public bool Enabled = true;

        /// <summary>Triggers belonging to this group. Mutating the list changes the group directly.</summary>
        public List<TbsSplineTrigger> Triggers = new List<TbsSplineTrigger>();

        /// <summary>
        /// Evaluates every trigger in the group for a move between two positions and invokes those that fire.
        /// </summary>
        /// <param name="from">Normalized position before the move.</param>
        /// <param name="to">Normalized position after the move.</param>
        public void Check(float from, float to)
        {
            if (!Enabled) return;
            for (int i = 0; i < Triggers.Count; i++)
                if (Triggers[i].Check(from, to)) Triggers[i].OnCross.Invoke();
        }

        /// <summary>Clears the fired flag on every trigger in the group.</summary>
        public void ResetState()
        {
            for (int i = 0; i < Triggers.Count; i++) Triggers[i].ResetState();
        }
    }
}
