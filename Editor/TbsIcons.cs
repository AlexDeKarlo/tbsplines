using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TBSplineS;

namespace TBSplineS.Editor
{
    public static class TbsIcons
    {
        const string DefaultRoot = "Assets/TBSplineS/Editor/Icons/";

        static string _root;

        static string Root => _root ??= ResolveRoot();

        static string ResolveRoot()
        {
            if (System.IO.File.Exists(DefaultRoot + "knot.png")) return DefaultRoot;
            string[] guids = AssetDatabase.FindAssets("TbsIcons t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                int index = path.LastIndexOf("/TbsIcons.cs", System.StringComparison.Ordinal);
                if (index < 0) continue;
                return path.Substring(0, index) + "/Icons/";
            }
            return DefaultRoot;
        }

        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        static readonly HashSet<string> Missing = new HashSet<string>();

        internal static void ClearMissing()
        {
            Missing.Clear();
            _root = null;
        }

        static GUIStyle _panel;
        static GUIStyle _label;
        static GUIStyle _title;

        public static Texture2D Knot => Load("knot");
        public static Texture2D KnotHover => Load("knot-hover");
        public static Texture2D KnotSelected => Load("knot-selected");
        public static Texture2D Tangent => Load("tangent");
        public static Texture2D PanelTexture => Load("panel");
        public static Texture2D GlyphPlus => Load("glyph-plus");
        public static Texture2D GlyphInsert => Load("glyph-insert");
        public static Texture2D ModeAuto => Load("mode-auto");
        public static Texture2D ModeMirrored => Load("mode-mirrored");
        public static Texture2D ModeContinuous => Load("mode-continuous");
        public static Texture2D ModeBroken => Load("mode-broken");
        public static Texture2D ModeLinear => Load("mode-linear");
        public static Texture2D GlyphClosed => Load("glyph-closed");
        public static Texture2D GlyphTrash => Load("glyph-trash");
        public static Texture2D GlyphPen => Load("glyph-pen");
        public static Texture2D GlyphExit => Load("glyph-exit");
        public static Texture2D PlaceXZ => Load("place-xz");
        public static Texture2D Logo => Load("logo");
        public static Texture2D Junction => Load("junction");
        public static Texture2D Merge => Load("merge");
        public static Texture2D Disconnect => Load("disconnect");
        public static Texture2D Reverse => Load("reverse");
        public static Texture2D Duplicate => Load("duplicate");
        public static Texture2D Grid => Load("grid");
        public static Texture2D Help => Load("help");
        public static Texture2D ToolSelect => Load("tool-select");
        public static Texture2D ToolMove => Load("tool-move");
        public static Texture2D ToolRotate => Load("tool-rotate");

        public static Texture2D ModeIcon(TbsTangentMode mode)
        {
            switch (mode)
            {
                case TbsTangentMode.AutoSmooth: return ModeAuto;
                case TbsTangentMode.Mirrored: return ModeMirrored;
                case TbsTangentMode.Continuous: return ModeContinuous;
                case TbsTangentMode.Broken: return ModeBroken;
                default: return ModeLinear;
            }
        }

        public static GUIStyle Panel
        {
            get
            {
                _panel ??= new GUIStyle
                {
                    border = new RectOffset(20, 20, 20, 20),
                    padding = new RectOffset(10, 10, 8, 8)
                };
                _panel.normal.background = PanelTexture;
                return _panel;
            }
        }

        public static GUIStyle Label
        {
            get
            {
                _label ??= new GUIStyle(EditorStyles.miniLabel);
                _label.normal.textColor = new Color(0.82f, 0.85f, 0.9f);
                return _label;
            }
        }

        public static GUIStyle Title
        {
            get
            {
                _title ??= new GUIStyle(EditorStyles.miniBoldLabel);
                _title.normal.textColor = new Color(1f, 0.72f, 0.3f);
                return _title;
            }
        }

        public static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        public static readonly Color ColInk = Hex(0xC8C8C8);
        public static readonly Color ColInkDim = Hex(0x8C8C8C);
        public static readonly Color ColInkHi = Hex(0xF3F3F3);
        public static readonly Color ColAccent = Hex(0x4C8FF0);
        public static readonly Color ColAccentInk = Color.white;
        public static readonly Color ColSel = Hex(0xFF9838);
        public static readonly Color ColLine = Hex(0x202020);
        public static readonly Color ColPanelHi = Hex(0x484848);
        public static readonly Color ColHxAuto = Hex(0xE6BD45);
        public static readonly Color ColHxAligned = Hex(0xCF72DE);
        public static readonly Color ColHxMirror = Hex(0x4FCCBB);
        public static readonly Color ColHxFree = Hex(0xEAEAEA);

        static Font _uiFont;
        static Font _monoFont;

        public static Font UiFont
        {
            get { if (_uiFont == null) _uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Inter", "Roboto", "Arial" }, 12); return _uiFont; }
        }

        public static Font MonoFont
        {
            get { if (_monoFont == null) _monoFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Cascadia Mono", "Courier New" }, 12); return _monoFont; }
        }

        public static Texture2D HBarTex => Load("hbar");
        public static Texture2D SegShellTex => Load("seg-shell");
        public static Texture2D ChipShellTex => Load("chip-shell");
        public static Texture2D SegBtnHoverTex => Load("seg-btn-hover");
        public static Texture2D SegBtnActiveTex => Load("seg-btn-active");
        public static Texture2D ChipHoverTex => Load("chip-hover");
        public static Texture2D ChipActiveTex => Load("chip-active");
        public static Texture2D SummaryTex => Load("summary");
        public static Texture2D SummaryHoverTex => Load("summary-hover");
        public static Texture2D SummaryOpenTex => Load("summary-open");
        public static Texture2D PopoverTex => Load("popover");
        public static Texture2D MenuTex => Load("menu");
        public static Texture2D CardTex => Load("card");
        public static Texture2D PanelLgTex => Load("panel-lg");
        public static Texture2D HeaderGradTex => Load("header-grad");
        public static Texture2D HeaderSolidTex => Load("header-solid");
        public static Texture2D ChipAccentTex => Load("chip-accent");
        public static Texture2D PillTex => Load("pill");
        public static Texture2D FieldTex => Load("field");
        public static Texture2D FieldFocusTex => Load("field-focus");
        public static Texture2D TooltipTex => Load("tooltip");
        public static Texture2D ToastTex => Load("toast");
        public static Texture2D MiniShellTex => Load("mini-shell");
        public static Texture2D ToggleOffTex => Load("toggle-off");
        public static Texture2D ToggleOnTex => Load("toggle-on");
        public static Texture2D SwatchTex => Load("swatch");
        public static Texture2D LedDotTex => Load("led-dot");
        public static Texture2D ToolScale => Load("tool-scale");
        public static Texture2D ToolNew => Load("tool-new");
        public static Texture2D ModeEdit => Load("mode-edit");
        public static Texture2D ModeObject => Load("mode-object");
        public static Texture2D ModeSummary => Load("mode-summary");
        public static Texture2D Chevron => Load("chevron");
        public static Texture2D Orient => Load("orient");
        public static Texture2D OrientLocal => Load("orient-local");
        public static Texture2D Pivot => Load("pivot");
        public static Texture2D PivotCursor => Load("pivot-cursor");

        static readonly Dictionary<string, GUIStyle> SkinCache = new Dictionary<string, GUIStyle>();

        static GUIStyle Skin(string tex, RectOffset border, RectOffset overflow = null, RectOffset padding = null)
        {
            if (!SkinCache.TryGetValue(tex, out GUIStyle style))
            {
                style = new GUIStyle
                {
                    border = border,
                    overflow = overflow ?? new RectOffset(),
                    padding = padding ?? new RectOffset()
                };
                SkinCache[tex] = style;
            }
            style.normal.background = Load(tex);
            return style;
        }

        static RectOffset Ro(int l, int r, int t, int b) => new RectOffset(l, r, t, b);

        public static GUIStyle HBar => Skin("hbar", Ro(4, 4, 0, 0), Ro(0, 0, 0, 6));
        public static GUIStyle SegShell => Skin("seg-shell", Ro(12, 12, 12, 12), null, Ro(3, 3, 3, 3));
        public static GUIStyle ChipShell => Skin("chip-shell", Ro(10, 10, 10, 10), null, Ro(2, 2, 2, 2));
        public static GUIStyle Popover => Skin("popover", Ro(44, 44, 38, 54), Ro(32, 32, 26, 42), Ro(8, 8, 8, 8));
        public static GUIStyle MenuPanel => Skin("menu", Ro(39, 39, 33, 49), Ro(32, 32, 26, 42), Ro(6, 6, 6, 6));
        public static GUIStyle Card => Skin("card", Ro(44, 44, 38, 54), Ro(32, 32, 26, 42), Ro(10, 10, 8, 8));
        public static GUIStyle PanelLarge => Skin("panel-lg", Ro(43, 43, 37, 53), Ro(32, 32, 26, 42));
        public static GUIStyle Pill => Skin("pill", Ro(46, 46, 40, 56), Ro(30, 30, 24, 40), Ro(14, 14, 6, 6));
        public static GUIStyle Tooltip => Skin("tooltip", Ro(38, 38, 32, 48), Ro(32, 32, 26, 42), Ro(8, 8, 5, 5));
        public static GUIStyle Toast => Skin("toast", Ro(39, 39, 33, 49), Ro(32, 32, 26, 42), Ro(12, 12, 6, 6));
        public static GUIStyle ChipAccentPanel => Skin("chip-accent", Ro(8, 8, 8, 8));
        public static GUIStyle MiniShell => Skin("mini-shell", Ro(7, 7, 7, 7));
        public static GUIStyle HeaderGrad => Skin("header-grad", Ro(11, 11, 11, 2));
        public static GUIStyle HeaderSolid => Skin("header-solid", Ro(11, 11, 11, 2));
        public static GUIStyle FieldBg => Skin("field", Ro(6, 6, 6, 6));

        static GUIStyle _segButton;
        public static GUIStyle SegButton
        {
            get
            {
                if (_segButton == null)
                    _segButton = new GUIStyle
                    {
                        border = Ro(9, 9, 9, 9),
                        padding = Ro(0, 0, 0, 2),
                        alignment = TextAnchor.MiddleCenter,
                        font = UiFont,
                        fontSize = 12,
                        fontStyle = FontStyle.Bold
                    };
                _segButton.normal.background = null;
                _segButton.hover.background = SegBtnHoverTex;
                _segButton.active.background = SegBtnActiveTex;
                _segButton.onNormal.background = SegBtnActiveTex;
                _segButton.onHover.background = SegBtnActiveTex;
                _segButton.normal.textColor = ColInkDim;
                _segButton.hover.textColor = ColInkHi;
                _segButton.active.textColor = ColAccentInk;
                _segButton.onNormal.textColor = ColAccentInk;
                _segButton.onHover.textColor = ColAccentInk;
                return _segButton;
            }
        }

        static GUIStyle _chipButton;
        public static GUIStyle ChipButton
        {
            get
            {
                if (_chipButton == null)
                    _chipButton = new GUIStyle
                    {
                        border = Ro(8, 8, 8, 8),
                        padding = Ro(10, 10, 0, 0),
                        alignment = TextAnchor.MiddleCenter,
                        font = UiFont,
                        fontSize = 11,
                        fontStyle = FontStyle.Bold
                    };
                _chipButton.normal.background = null;
                _chipButton.hover.background = ChipHoverTex;
                _chipButton.active.background = ChipActiveTex;
                _chipButton.onNormal.background = ChipActiveTex;
                _chipButton.onHover.background = ChipActiveTex;
                _chipButton.normal.textColor = ColInkDim;
                _chipButton.hover.textColor = ColInkHi;
                _chipButton.active.textColor = ColAccentInk;
                _chipButton.onNormal.textColor = ColAccentInk;
                _chipButton.onHover.textColor = ColAccentInk;
                return _chipButton;
            }
        }

        static GUIStyle _summaryButton;
        public static GUIStyle SummaryButton
        {
            get
            {
                if (_summaryButton == null)
                    _summaryButton = new GUIStyle
                    {
                        border = Ro(9, 9, 9, 9),
                        padding = Ro(11, 10, 0, 0),
                        alignment = TextAnchor.MiddleLeft,
                        font = UiFont,
                        fontSize = 12,
                        fontStyle = FontStyle.Bold
                    };
                _summaryButton.normal.background = SummaryTex;
                _summaryButton.hover.background = SummaryHoverTex;
                _summaryButton.active.background = SummaryOpenTex;
                _summaryButton.onNormal.background = SummaryOpenTex;
                _summaryButton.onHover.background = SummaryOpenTex;
                _summaryButton.normal.textColor = ColInk;
                _summaryButton.hover.textColor = ColInkHi;
                _summaryButton.onNormal.textColor = ColInkHi;
                _summaryButton.onHover.textColor = ColInkHi;
                return _summaryButton;
            }
        }

        static GUIStyle _caption, _inkLabel, _inkStrong, _mono;

        public static GUIStyle Caption
        {
            get
            {
                if (_caption == null)
                    _caption = new GUIStyle { font = UiFont, fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                _caption.normal.textColor = ColInkDim;
                return _caption;
            }
        }

        public static GUIStyle InkLabel
        {
            get
            {
                if (_inkLabel == null)
                    _inkLabel = new GUIStyle { font = UiFont, fontSize = 12, alignment = TextAnchor.MiddleLeft };
                _inkLabel.normal.textColor = ColInk;
                return _inkLabel;
            }
        }

        public static GUIStyle InkStrong
        {
            get
            {
                if (_inkStrong == null)
                    _inkStrong = new GUIStyle { font = UiFont, fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                _inkStrong.normal.textColor = ColInkHi;
                return _inkStrong;
            }
        }

        public static GUIStyle Mono
        {
            get
            {
                if (_mono == null)
                    _mono = new GUIStyle { font = MonoFont, fontSize = 11, alignment = TextAnchor.MiddleCenter };
                _mono.normal.textColor = ColInkHi;
                return _mono;
            }
        }

        static Texture2D Load(string name)
        {
            if (Cache.TryGetValue(name, out Texture2D cached) && cached != null) return cached;
            if (Missing.Contains(name)) return null;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + name + ".png");
            if (texture != null)
            {
                Cache[name] = texture;
                return texture;
            }
            Missing.Add(name);
            Debug.LogWarning($"TBSplineS: editor icon '{Root}{name}.png' is missing; the related UI element will not render.");
            return null;
        }
    }
}
