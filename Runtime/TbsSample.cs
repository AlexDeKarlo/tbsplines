using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// A single evaluated point along a spline, carrying everything needed to place an object on it.
    /// </summary>
    public struct TbsSample
    {
        /// <summary>World position of the sample.</summary>
        public Vector3 Position;

        /// <summary>Normalized forward direction along the spline.</summary>
        public Vector3 Tangent;

        /// <summary>Normal direction used as the up axis for orientation.</summary>
        public Vector3 Up;

        /// <summary>Distance from the start of the spline, in world units.</summary>
        public float Distance;

        /// <summary>Position along the spline in the 0..1 range.</summary>
        public float T;

        /// <summary>Scale multiplier at this point, driven by the spline's size modifiers.</summary>
        public float Size;

        /// <summary>Vertex color at this point, driven by the spline's color modifiers.</summary>
        public Color Color;

        /// <summary>
        /// Right-hand side direction, derived from <see cref="Up"/> and <see cref="Tangent"/>.
        /// Falls back to <see cref="Vector3.right"/> when the two are degenerate.
        /// </summary>
        public Vector3 Right
        {
            get
            {
                Vector3 cross = Vector3.Cross(Up, Tangent);
                return cross.sqrMagnitude > TbsSplineMath.Epsilon ? cross.normalized : Vector3.right;
            }
        }

        /// <summary>
        /// Orientation looking along <see cref="Tangent"/> with <see cref="Up"/> as the up axis.
        /// Returns <see cref="Quaternion.identity"/> when the tangent is degenerate.
        /// </summary>
        public Quaternion Rotation => Tangent.sqrMagnitude > TbsSplineMath.Epsilon
            ? Quaternion.LookRotation(Tangent, Up)
            : Quaternion.identity;
    }
}
