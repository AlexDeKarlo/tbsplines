using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// A single cubic Bezier segment defined by four control points.
    /// </summary>
    public readonly struct TbsCurve
    {
        /// <summary>Start point of the segment.</summary>
        public readonly Vector3 P0;

        /// <summary>Outgoing control point of the start knot.</summary>
        public readonly Vector3 P1;

        /// <summary>Incoming control point of the end knot.</summary>
        public readonly Vector3 P2;

        /// <summary>End point of the segment.</summary>
        public readonly Vector3 P3;

        /// <summary>
        /// Creates a curve from four explicit control points.
        /// </summary>
        public TbsCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            P3 = p3;
        }

        /// <summary>
        /// Builds the segment that connects two adjacent knots, using their tangent handles as control points.
        /// </summary>
        /// <param name="a">Knot the segment starts at.</param>
        /// <param name="b">Knot the segment ends at.</param>
        public static TbsCurve FromKnots(in TbsKnot a, in TbsKnot b)
        {
            return new TbsCurve(a.Position, a.TangentOutPosition, b.TangentInPosition, b.Position);
        }

        /// <summary>
        /// Returns the point on the curve at the given parameter.
        /// </summary>
        /// <param name="t">Curve parameter, normally in the 0..1 range. Values outside the range extrapolate.</param>
        public Vector3 EvaluatePosition(float t)
        {
            float u = 1f - t;
            float uu = u * u;
            float tt = t * t;
            return uu * u * P0 + 3f * uu * t * P1 + 3f * u * tt * P2 + tt * t * P3;
        }

        /// <summary>
        /// Returns the first derivative at the given parameter. Its direction is the curve tangent, its magnitude the
        /// rate of change of position, so it is not normalized.
        /// </summary>
        /// <param name="t">Curve parameter, normally in the 0..1 range.</param>
        public Vector3 EvaluateDerivative(float t)
        {
            float u = 1f - t;
            return 3f * u * u * (P1 - P0) + 6f * u * t * (P2 - P1) + 3f * t * t * (P3 - P2);
        }

        /// <summary>
        /// Returns the second derivative at the given parameter. Useful for curvature and banking calculations.
        /// </summary>
        /// <param name="t">Curve parameter, normally in the 0..1 range.</param>
        public Vector3 EvaluateAcceleration(float t)
        {
            float u = 1f - t;
            return 6f * u * (P2 - 2f * P1 + P0) + 6f * t * (P3 - 2f * P2 + P1);
        }

        /// <summary>
        /// Splits the curve at the given parameter into two segments that together trace the original shape exactly.
        /// </summary>
        /// <param name="t">Curve parameter to split at, in the 0..1 range.</param>
        /// <param name="left">Segment covering 0..<paramref name="t"/>.</param>
        /// <param name="right">Segment covering <paramref name="t"/>..1.</param>
        public void Split(float t, out TbsCurve left, out TbsCurve right)
        {
            Vector3 a = Vector3.LerpUnclamped(P0, P1, t);
            Vector3 b = Vector3.LerpUnclamped(P1, P2, t);
            Vector3 c = Vector3.LerpUnclamped(P2, P3, t);
            Vector3 d = Vector3.LerpUnclamped(a, b, t);
            Vector3 e = Vector3.LerpUnclamped(b, c, t);
            Vector3 p = Vector3.LerpUnclamped(d, e, t);
            left = new TbsCurve(P0, a, d, p);
            right = new TbsCurve(p, e, c, P3);
        }
    }
}
