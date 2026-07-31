using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// Low-level spline maths shared by the runtime components. Useful when writing your own spline users.
    /// </summary>
    public static class TbsSplineMath
    {
        /// <summary>Tolerance below which lengths and dot products are treated as zero.</summary>
        public const float Epsilon = 1e-6f;

        /// <summary>
        /// Computes smooth tangent handles for a knot from its two neighbours, weighted by the distance to each
        /// so that uneven knot spacing does not produce overshoot.
        /// </summary>
        /// <param name="prev">Position of the previous knot.</param>
        /// <param name="current">Position of the knot being smoothed.</param>
        /// <param name="next">Position of the next knot.</param>
        /// <param name="tangentIn">Incoming handle, relative to <paramref name="current"/>.</param>
        /// <param name="tangentOut">Outgoing handle, relative to <paramref name="current"/>.</param>
        public static void AutoSmoothTangents(Vector3 prev, Vector3 current, Vector3 next, out Vector3 tangentIn, out Vector3 tangentOut)
        {
            float d1 = Mathf.Sqrt(Mathf.Max(Vector3.Distance(prev, current), Epsilon));
            float d2 = Mathf.Sqrt(Mathf.Max(Vector3.Distance(current, next), Epsilon));
            float d1Sq = d1 * d1;
            float d2Sq = d2 * d2;
            Vector3 outControl = (d1Sq * next - d2Sq * prev + (2f * d1Sq + 3f * d1 * d2 + d2Sq) * current) / (3f * d1 * (d1 + d2));
            Vector3 inControl = (d2Sq * prev - d1Sq * next + (2f * d2Sq + 3f * d2 * d1 + d1Sq) * current) / (3f * d2 * (d2 + d1));
            tangentOut = outControl - current;
            tangentIn = inControl - current;
        }

        /// <summary>
        /// Advances an up vector by one step of the rotation minimising frame, propagating orientation along a
        /// curve with as little twist as possible. Carry <paramref name="up1"/> into the next step.
        /// </summary>
        /// <param name="p0">Position at the start of the step.</param>
        /// <param name="t0">Tangent at the start of the step.</param>
        /// <param name="up0">Up vector at the start of the step.</param>
        /// <param name="p1">Position at the end of the step.</param>
        /// <param name="t1">Tangent at the end of the step.</param>
        /// <param name="up1">Resulting up vector at the end of the step.</param>
        public static void RmfStep(Vector3 p0, Vector3 t0, Vector3 up0, Vector3 p1, Vector3 t1, out Vector3 up1)
        {
            Vector3 v1 = p1 - p0;
            float c1 = Vector3.Dot(v1, v1);
            if (c1 < Epsilon)
            {
                up1 = up0;
                return;
            }
            Vector3 upReflected = up0 - 2f / c1 * Vector3.Dot(v1, up0) * v1;
            Vector3 tanReflected = t0 - 2f / c1 * Vector3.Dot(v1, t0) * v1;
            Vector3 v2 = t1 - tanReflected;
            float c2 = Vector3.Dot(v2, v2);
            if (c2 < Epsilon)
            {
                up1 = upReflected;
                return;
            }
            up1 = upReflected - 2f / c2 * Vector3.Dot(v2, upReflected) * v2;
        }

        /// <summary>
        /// Returns a unit up vector perpendicular to the tangent, as close to the requested one as possible.
        /// Picks an arbitrary perpendicular when the two are parallel.
        /// </summary>
        /// <param name="tangent">Direction to be perpendicular to. Need not be normalized.</param>
        /// <param name="up">Preferred up direction.</param>
        public static Vector3 OrthonormalUp(Vector3 tangent, Vector3 up)
        {
            Vector3 t = tangent.normalized;
            Vector3 u = up - Vector3.Dot(up, t) * t;
            float mag = u.magnitude;
            if (mag < Epsilon)
            {
                Vector3 alt = Mathf.Abs(t.y) < 0.99f ? Vector3.up : Vector3.right;
                u = alt - Vector3.Dot(alt, t) * t;
                mag = u.magnitude;
            }
            return u / mag;
        }
    }
}
