using System;
using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// Which way along the spline an object faces.
    /// </summary>
    public enum TbsMotionDirection
    {
        /// <summary>Faces along the spline tangent.</summary>
        Forward,

        /// <summary>Faces against the spline tangent.</summary>
        Backward
    }

    /// <summary>
    /// Turns a spline sample into a transform placement. Shared by the positioner, follower and projector so
    /// they all offer the same offset, rotation and scale options.
    /// </summary>
    [Serializable]
    public sealed class TbsMotionModule
    {
        /// <summary>Sideways and vertical shift from the spline, scaled by the sample's size.</summary>
        public Vector2 Offset;

        /// <summary>Extra rotation applied on top of the spline orientation, in euler degrees.</summary>
        public Vector3 RotationOffset;

        /// <summary>Scale used when <see cref="ApplyScale"/> is on, before the sample's size multiplier.</summary>
        public Vector3 BaseScale = Vector3.one;

        /// <summary>Whether the world X position is driven by the spline.</summary>
        public bool ApplyPositionX = true;

        /// <summary>Whether the world Y position is driven by the spline.</summary>
        public bool ApplyPositionY = true;

        /// <summary>Whether the world Z position is driven by the spline.</summary>
        public bool ApplyPositionZ = true;

        /// <summary>Whether the object is rotated to match the spline.</summary>
        public bool ApplyRotation = true;

        /// <summary>Whether the object is scaled by <see cref="BaseScale"/> and the sample's size.</summary>
        public bool ApplyScale;

        /// <summary>Orients around the Z axis only, for 2D projects.</summary>
        public bool Is2D;

        /// <summary>Which way along the spline the object faces.</summary>
        public TbsMotionDirection Direction = TbsMotionDirection.Forward;

        /// <summary>
        /// Returns the world position for a sample, with <see cref="Offset"/> applied. Axis masks are ignored.
        /// </summary>
        public Vector3 ComputePosition(in TbsSample sample)
        {
            return sample.Position + sample.Right * (Offset.x * sample.Size) + sample.Up * (Offset.y * sample.Size);
        }

        /// <summary>
        /// Returns the world rotation for a sample, honouring <see cref="Direction"/>, <see cref="Is2D"/> and
        /// <see cref="RotationOffset"/>. Falls back to identity when the tangent is degenerate.
        /// </summary>
        public Quaternion ComputeRotation(in TbsSample sample)
        {
            Vector3 forward = Direction == TbsMotionDirection.Backward ? -sample.Tangent : sample.Tangent;
            Quaternion baseRotation;
            if (forward.sqrMagnitude < TbsSplineMath.Epsilon)
            {
                baseRotation = Quaternion.identity;
            }
            else if (Is2D)
            {
                float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
                baseRotation = Quaternion.Euler(0f, 0f, angle);
            }
            else
            {
                baseRotation = Quaternion.LookRotation(forward, sample.Up);
            }
            return baseRotation * Quaternion.Euler(RotationOffset);
        }

        /// <summary>
        /// Moves a transform to the sample, writing only the axes and channels this module is set to apply.
        /// </summary>
        public void ApplyTo(Transform target, in TbsSample sample)
        {
            Vector3 position = ComputePosition(sample);
            Vector3 current = target.position;
            if (!ApplyPositionX) position.x = current.x;
            if (!ApplyPositionY) position.y = current.y;
            if (!ApplyPositionZ) position.z = current.z;
            target.position = position;

            if (ApplyRotation) target.rotation = ComputeRotation(sample);

            if (ApplyScale)
                target.localScale = new Vector3(BaseScale.x * sample.Size, BaseScale.y * sample.Size, BaseScale.z * sample.Size);
        }

        /// <summary>
        /// Moves a rigidbody to the sample through the physics engine, so collisions are resolved on the way.
        /// Call this from FixedUpdate. Scale is not applied.
        /// </summary>
        public void ApplyTo(Rigidbody body, in TbsSample sample)
        {
            body.MovePosition(ApplyMask(ComputePosition(sample), body.position));
            if (ApplyRotation) body.MoveRotation(ComputeRotation(sample));
        }

        Vector3 ApplyMask(Vector3 position, Vector3 current)
        {
            if (!ApplyPositionX) position.x = current.x;
            if (!ApplyPositionY) position.y = current.y;
            if (!ApplyPositionZ) position.z = current.z;
            return position;
        }
    }
}
