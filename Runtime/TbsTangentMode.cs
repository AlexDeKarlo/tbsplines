namespace TBSplineS
{
    /// <summary>
    /// How a knot's two tangent handles relate to each other when either one is moved.
    /// </summary>
    public enum TbsTangentMode
    {
        /// <summary>Both handles are recomputed from the neighbouring knots and cannot be edited by hand.</summary>
        AutoSmooth,

        /// <summary>Handles stay opposite and equal in length, giving a smooth, symmetric curve.</summary>
        Mirrored,

        /// <summary>Handles stay opposite but keep independent lengths, so the curve is smooth but can change pace.</summary>
        Continuous,

        /// <summary>Handles move independently, allowing a sharp corner at the knot.</summary>
        Broken,

        /// <summary>Handles collapse onto the knot, making both adjoining segments straight.</summary>
        Linear
    }
}
