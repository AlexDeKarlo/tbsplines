using System;
using System.Collections.Generic;
using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// Points at one knot of one spline within a spline computer.
    /// </summary>
    [Serializable]
    public struct TbsKnotRef : IEquatable<TbsKnotRef>
    {
        /// <summary>Identifier of the spline the knot belongs to.</summary>
        public int SplineId;

        /// <summary>Identifier of the knot within that spline.</summary>
        public int KnotId;

        /// <summary>
        /// Creates a reference to the knot with the given identifiers.
        /// </summary>
        public TbsKnotRef(int splineId, int knotId)
        {
            SplineId = splineId;
            KnotId = knotId;
        }

        /// <inheritdoc/>
        public bool Equals(TbsKnotRef other) => SplineId == other.SplineId && KnotId == other.KnotId;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is TbsKnotRef other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => SplineId * 397 ^ KnotId;
    }

    /// <summary>
    /// How the splines meeting at a junction are kept aligned.
    /// </summary>
    public enum TbsJunctionMode
    {
        /// <summary>Knots share a position, but each spline keeps its own tangents, producing a visible corner.</summary>
        Free,

        /// <summary>Tangents are aligned across the junction so traffic passes through it without a kink.</summary>
        Smooth
    }

    /// <summary>
    /// A group of knots from one or more splines that are tied together, letting followers switch between branches.
    /// </summary>
    [Serializable]
    public sealed class TbsJunction
    {
        [SerializeField] int _id;
        [SerializeField] TbsJunctionMode _mode = TbsJunctionMode.Free;
        [SerializeField] List<TbsKnotRef> _members = new List<TbsKnotRef>();

        /// <summary>Identifier of this junction within its spline computer.</summary>
        public int Id
        {
            get => _id;
            set => _id = value;
        }

        /// <summary>How the connected knots are kept aligned.</summary>
        public TbsJunctionMode Mode
        {
            get => _mode;
            set => _mode = value;
        }

        /// <summary>The knots tied together by this junction. Mutating the list changes the junction directly.</summary>
        public List<TbsKnotRef> Members => _members;

        /// <summary>Number of knots tied together by this junction.</summary>
        public int Count => _members.Count;

        /// <summary>
        /// Returns whether the given knot takes part in this junction.
        /// </summary>
        public bool Contains(TbsKnotRef reference) => _members.Contains(reference);

        /// <summary>
        /// Returns whether the knot with the given spline and knot identifiers takes part in this junction.
        /// </summary>
        public bool ContainsKnot(int splineId, int knotId)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].SplineId == splineId && _members[i].KnotId == knotId) return true;
            }
            return false;
        }
    }
}
