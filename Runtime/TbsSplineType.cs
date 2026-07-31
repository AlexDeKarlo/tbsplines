namespace TBSplineS
{
    /// <summary>
    /// Interpolation used to build the curve running through a spline's knots.
    /// </summary>
    public enum TbsSplineType
    {
        /// <summary>Cubic Bezier driven by each knot's tangent handles. The only type with directly editable handles.</summary>
        Bezier,

        /// <summary>Catmull-Rom. Passes through every knot, tangents derived from the neighbouring knots.</summary>
        CatmullRom,

        /// <summary>Uniform B-spline. Smoothest of the set, but the curve does not pass through the knots.</summary>
        BSpline,

        /// <summary>Straight segments between knots.</summary>
        Linear
    }
}
