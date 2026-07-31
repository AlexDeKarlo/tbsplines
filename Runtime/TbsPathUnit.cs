namespace TBSplineS
{
    /// <summary>
    /// How a position along a spline is expressed.
    /// </summary>
    public enum TbsPathUnit
    {
        /// <summary>Index of a knot, with the fractional part interpolating towards the next one.</summary>
        Knot,

        /// <summary>Fraction of the whole spline, from 0 at the start to 1 at the end.</summary>
        Normalized,

        /// <summary>Arc length from the start of the spline, in world units.</summary>
        Distance
    }
}
