using System;
using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// One control point of a spline, holding its placement, tangent handles and per-point appearance.
    /// </summary>
    [Serializable]
    public struct TbsKnot
    {
        /// <summary>Identifier that stays stable while knots around it are inserted or removed.</summary>
        public int Id;

        /// <summary>Position of the knot, in the spline computer's local space.</summary>
        public Vector3 Position;

        /// <summary>Incoming tangent handle, relative to <see cref="Position"/> and before <see cref="Rotation"/>.</summary>
        public Vector3 TangentIn;

        /// <summary>Outgoing tangent handle, relative to <see cref="Position"/> and before <see cref="Rotation"/>.</summary>
        public Vector3 TangentOut;

        /// <summary>Orientation of the knot, which rolls the surface and rotates both tangent handles.</summary>
        public Quaternion Rotation;

        /// <summary>How the two tangent handles follow each other when either one is moved.</summary>
        public TbsTangentMode Mode;

        /// <summary>Scale multiplier applied by generators that support per-point width.</summary>
        public float Size;

        /// <summary>Vertex color applied by generators that support per-point color.</summary>
        public Color Color;

        /// <summary>
        /// Creates an auto-smoothed knot at the given position, with unit size and white color.
        /// </summary>
        public TbsKnot(Vector3 position)
            : this(position, Vector3.zero, Vector3.zero, Quaternion.identity, TbsTangentMode.AutoSmooth)
        {
        }

        /// <summary>
        /// Creates a knot with explicit tangents and orientation, with unit size and white color.
        /// </summary>
        public TbsKnot(Vector3 position, Vector3 tangentIn, Vector3 tangentOut, Quaternion rotation, TbsTangentMode mode)
            : this(position, tangentIn, tangentOut, rotation, mode, 1f, Color.white)
        {
        }

        /// <summary>
        /// Creates a fully specified knot.
        /// </summary>
        public TbsKnot(Vector3 position, Vector3 tangentIn, Vector3 tangentOut, Quaternion rotation, TbsTangentMode mode, float size, Color color)
        {
            Id = 0;
            Position = position;
            TangentIn = tangentIn;
            TangentOut = tangentOut;
            Rotation = rotation;
            Mode = mode;
            Size = size;
            Color = color;
        }

        /// <summary>Incoming handle as an absolute point, with <see cref="Rotation"/> applied.</summary>
        public Vector3 TangentInPosition => Position + Rotation * TangentIn;

        /// <summary>Outgoing handle as an absolute point, with <see cref="Rotation"/> applied.</summary>
        public Vector3 TangentOutPosition => Position + Rotation * TangentOut;

        /// <summary>Up axis of the knot, used to orient the surface built along the spline.</summary>
        public Vector3 Up => Rotation * Vector3.up;

        /// <summary>
        /// Renormalizes <see cref="Rotation"/>, falling back to identity if it has degenerated to zero.
        /// Call this after writing the quaternion by hand.
        /// </summary>
        public void NormalizeRotation()
        {
            float mag = Rotation.x * Rotation.x + Rotation.y * Rotation.y + Rotation.z * Rotation.z + Rotation.w * Rotation.w;
            Rotation = mag < 1e-6f ? Quaternion.identity : Quaternion.Normalize(Rotation);
        }
    }
}
