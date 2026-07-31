namespace TBSplineS
{
    /// <summary>
    /// Kind of change reported when a spline is modified, letting listeners rebuild only what is affected.
    /// </summary>
    public enum TbsSplineModification
    {
        /// <summary>A knot was inserted.</summary>
        KnotAdded,

        /// <summary>A knot was removed.</summary>
        KnotRemoved,

        /// <summary>An existing knot changed position, tangents or properties.</summary>
        KnotModified,

        /// <summary>The spline was opened or closed into a loop.</summary>
        ClosedChanged,

        /// <summary>The spline changed in a way that requires a complete rebuild.</summary>
        Full
    }
}
