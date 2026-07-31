using UnityEditor;
using UnityEngine;

namespace TBSplineS.Editor
{
    public static class TbsInspectorGUI
    {
        static GUIStyle _wordmark, _sub, _section, _primary, _secondary;

        public static void Header(string subtitle)
        {
            Rect r = GUILayoutUtility.GetRect(0f, 48f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, TbsIcons.Hex(0x2E2E2E));
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2f, r.width, 2f), TbsIcons.ColAccent);
            if (TbsIcons.Logo != null)
            {
                Color c = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(r.x + 12f, r.y + 13f, 22f, 22f), TbsIcons.Logo, ScaleMode.ScaleToFit, true);
                GUI.color = c;
            }
            GUI.Label(new Rect(r.x + 42f, r.y + 8f, r.width - 52f, 18f), "TB<color=#4c8ff0>Spline</color>S", Wordmark);
            if (!string.IsNullOrEmpty(subtitle))
                GUI.Label(new Rect(r.x + 42f, r.y + 26f, r.width - 52f, 14f), subtitle, Sub);
            GUILayout.Space(6f);
        }

        public static bool PrimaryButton(string label, float height = 32f) =>
            GUILayout.Button(label, Primary, GUILayout.Height(height));

        public static bool SecondaryButton(string label, float height = 26f) =>
            GUILayout.Button(label, Secondary, GUILayout.Height(height));

        public static void Section(string label)
        {
            GUILayout.Space(6f);
            GUILayout.Label(label.ToUpperInvariant(), SectionStyle);
        }

        static GUIStyle Wordmark
        {
            get
            {
                if (_wordmark == null)
                    _wordmark = new GUIStyle { font = TbsIcons.UiFont, fontSize = 14, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleLeft };
                _wordmark.normal.textColor = TbsIcons.ColInkHi;
                return _wordmark;
            }
        }

        static GUIStyle Sub
        {
            get
            {
                if (_sub == null)
                    _sub = new GUIStyle { font = TbsIcons.UiFont, fontSize = 11, alignment = TextAnchor.MiddleLeft };
                _sub.normal.textColor = TbsIcons.ColInkDim;
                return _sub;
            }
        }

        static GUIStyle SectionStyle
        {
            get
            {
                if (_section == null)
                    _section = new GUIStyle { font = TbsIcons.UiFont, fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, margin = new RectOffset(2, 2, 4, 2) };
                _section.normal.textColor = TbsIcons.ColInkDim;
                return _section;
            }
        }

        static GUIStyle Primary
        {
            get
            {
                if (_primary == null)
                    _primary = new GUIStyle { border = new RectOffset(9, 9, 9, 11), padding = new RectOffset(4, 4, 0, 2), fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, font = TbsIcons.UiFont };
                _primary.normal.background = TbsIcons.SegBtnActiveTex;
                _primary.hover.background = TbsIcons.SegBtnActiveTex;
                _primary.active.background = TbsIcons.SegBtnActiveTex;
                _primary.normal.textColor = Color.white;
                _primary.hover.textColor = Color.white;
                _primary.active.textColor = Color.white;
                return _primary;
            }
        }

        static GUIStyle Secondary
        {
            get
            {
                if (_secondary == null)
                    _secondary = new GUIStyle { border = new RectOffset(9, 9, 9, 9), padding = new RectOffset(4, 4, 0, 0), fontSize = 12, alignment = TextAnchor.MiddleCenter, font = TbsIcons.UiFont };
                _secondary.normal.background = TbsIcons.SummaryTex;
                _secondary.hover.background = TbsIcons.SummaryHoverTex;
                _secondary.active.background = TbsIcons.SummaryHoverTex;
                _secondary.normal.textColor = TbsIcons.ColInk;
                _secondary.hover.textColor = TbsIcons.ColInkHi;
                _secondary.active.textColor = TbsIcons.ColInkHi;
                return _secondary;
            }
        }
    }
}
