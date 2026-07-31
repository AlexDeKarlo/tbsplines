using TBSplineS;

namespace TBSplineS.Editor
{
    public enum TbsPointType
    {
        Smooth,
        Corner
    }

    public enum TbsHandleType
    {
        Auto,
        Aligned,
        Mirrored,
        Free
    }

    public static class TbsTangentModeView
    {
        public static TbsPointType GetPointType(TbsTangentMode mode) =>
            mode == TbsTangentMode.Linear ? TbsPointType.Corner : TbsPointType.Smooth;

        public static TbsHandleType GetHandleType(TbsTangentMode mode)
        {
            switch (mode)
            {
                case TbsTangentMode.AutoSmooth: return TbsHandleType.Auto;
                case TbsTangentMode.Continuous: return TbsHandleType.Aligned;
                case TbsTangentMode.Mirrored: return TbsHandleType.Mirrored;
                case TbsTangentMode.Broken: return TbsHandleType.Free;
                default: return TbsHandleType.Auto;
            }
        }

        public static TbsTangentMode Compose(TbsPointType point, TbsHandleType handle)
        {
            if (point == TbsPointType.Corner) return TbsTangentMode.Linear;
            switch (handle)
            {
                case TbsHandleType.Auto: return TbsTangentMode.AutoSmooth;
                case TbsHandleType.Aligned: return TbsTangentMode.Continuous;
                case TbsHandleType.Mirrored: return TbsTangentMode.Mirrored;
                default: return TbsTangentMode.Broken;
            }
        }

        public static bool ShowHandles(TbsTangentMode mode) => mode != TbsTangentMode.Linear;
    }
}
