using System;
using UnityEditor;
using UnityEngine;
using TBSplineS;

namespace TBSplineS.Editor
{
    public sealed class TbsSplineHud
    {
        const float BarHeight = 50f;
        const float MenuRow = 24f;

        Rect _topBar;
        Rect _panelRect;
        Rect _menuRect;
        bool _panelVisible;

        bool _modeOpen;
        bool _gridOpen;
        bool _orientOpen;
        bool _pivotOpen;
        Rect _modeBtnRect;
        Rect _gridBtnRect;
        Rect _orientBtnRect;
        Rect _pivotBtnRect;
        Rect _modePopRect;
        Rect _gridPopRect;
        Rect _orientPopRect;
        Rect _pivotPopRect;

        Rect _redoRect;
        bool _redoCollapsed;
        string _scCapture;

        public bool IsCapturingShortcut => _scCapture != null;

        static GUIStyle _wordmark;

        public bool MouseOver { get; private set; }

        public void PrepareLayout(TbsSplineComputer computer, SceneView sceneView)
        {
            _topBar = new Rect(0f, 0f, sceneView.position.width, BarHeight);
            _panelVisible = !TbsSplineEditorState.DrawMode && TryCardsLayout(computer, sceneView, out _, out _, out _, out _, out _);
            _panelRect = _panelVisible ? CardsBounds(computer, sceneView) : new Rect(0f, 0f, 0f, 0f);
            if (TbsSplineEditorState.MenuOpen)
            {
                float w = 214f;
                float h = 8f;
                var items = TbsSplineEditorState.MenuItems;
                for (int i = 0; i < items.Count; i++) h += items[i].Separator ? 7f : MenuRow;
                Vector2 p = TbsSplineEditorState.MenuPosition;
                _menuRect = new Rect(p.x, p.y, w, h);
                _menuRect.x = Mathf.Clamp(_menuRect.x, 4f, Mathf.Max(4f, sceneView.position.width - w - 4f));
                _menuRect.y = Mathf.Clamp(_menuRect.y, 4f, Mathf.Max(4f, sceneView.position.height - h - 4f));
            }
            Vector2 mouse = Event.current.mousePosition;
            MouseOver = _topBar.Contains(mouse)
                        || (_panelVisible && _panelRect.Contains(mouse))
                        || (_modeOpen && _modePopRect.Contains(mouse))
                        || (_gridOpen && _gridPopRect.Contains(mouse))
                        || (_orientOpen && _orientPopRect.Contains(mouse))
                        || (_pivotOpen && _pivotPopRect.Contains(mouse))
                        || _redoRect.Contains(mouse)
                        || (TbsSplineEditorState.MenuOpen && _menuRect.Contains(mouse));
        }

        public void DoGUI(TbsSplineComputer computer, SceneView sceneView)
        {
            Handles.BeginGUI();
            if (Event.current.type == EventType.Repaint)
            {
                DrawJunctions(computer, sceneView);
                DrawKnotSprites(computer, sceneView);
                if (TbsSplineEditorState.ShowLabels || computer.EditorShowNumbers || computer.EditorRenderAll) DrawLabels(computer, sceneView);
                DrawGhost(sceneView);
                DrawHoverBadge(computer, sceneView);
                if (TbsSplineEditorState.DragLabelValid)
                    DrawChip(HandleUtility.WorldToGUIPoint(TbsSplineEditorState.DragLabelWorld) + new Vector2(18f, -34f), TbsSplineEditorState.DragLabel);
            }
            DrawTopBar(computer, sceneView);
            if (_panelVisible) DrawTypeCards(computer, sceneView);
            DrawRedoPanel(computer, sceneView);
            DrawHoverChip(computer);
            DrawStatusPill(computer, sceneView);
            DrawAddModeLabel(computer, sceneView);
            if (TbsSplineEditorState.HelpVisible) DrawHelp(sceneView);
            DrawDragInfo(sceneView);
            DrawMarquee();
            DrawModePopover(computer);
            DrawGridPopover(computer);
            DrawOrientPopover();
            DrawPivotPopover(computer);
            DrawMenu();
            Handles.EndGUI();
        }

        void DrawTopBar(TbsSplineComputer computer, SceneView sceneView)
        {
            GUI.Box(_topBar, GUIContent.none, TbsIcons.HBar);
            float midY = _topBar.y + BarHeight * 0.5f;
            float viewW = sceneView.position.width;
            bool showWordmark = viewW >= 1260f;
            bool compactTools = viewW < 1240f;
            bool compactSelectors = viewW < 1140f;
            bool showTransform = viewW >= 930f;
            bool compactSettings = viewW < 700f;
            float x = _topBar.x + 14f;

            DrawIcon(new Rect(x, midY - 11f, 22f, 22f), TbsIcons.Logo, Color.white);
            x += 28f;
            if (showWordmark)
            {
                x += DrawWordmark(x, midY);
                x += 10f;
            }
            DrawBarDivider(ref x, midY);

            x += DrawModeSelector(x, midY, compactSelectors);
            x += 10f;
            DrawBarDivider(ref x, midY);
            x += 6f;

            DrawToolGroup(ref x, midY, computer, compactTools);
            x += 8f;

            x += DrawMiniSelector(ref _orientBtnRect, x, midY, 2,
                TbsSplineEditorState.OrientGlobal ? TbsIcons.Orient : TbsIcons.OrientLocal,
                compactSelectors ? null : TbsSplineEditorState.OrientGlobal ? "Global" : "Local", ref _orientOpen);
            if (TbsSplineEditorState.ObjectModeActive)
            {
                x += 6f;
                x += DrawMiniSelector(ref _pivotBtnRect, x, midY, 3,
                    TbsSplineEditorState.PivotMode == TbsPivotMode.Cursor ? TbsIcons.PivotCursor : TbsIcons.Pivot,
                    compactSelectors ? null : TbsSplineEditorState.PivotMode == TbsPivotMode.Cursor ? "Cursor" : "Median", ref _pivotOpen);
            }
            x += 8f;
            DrawBarDivider(ref x, midY);
            x += 4f;

            float gridW = compactSettings ? 44f : 108f;
            float gridX = _topBar.xMax - 14f - gridW;
            if (showTransform) DrawTransformInline(x, midY, computer, gridX - x - 10f);

            DrawGridSelector(gridX, midY, gridW, compactSettings);

            if (TbsSplineEditorState.DrawMode)
            {
                var hint = new Rect(_topBar.x + 14f, _topBar.yMax + 10f, 470f, 26f);
                GUI.Box(hint, GUIContent.none, TbsIcons.PanelLarge);
                GUI.Label(new Rect(hint.x + 14f, hint.y + 5f, hint.width - 28f, 16f),
                    "New spline: LMB add · RMB / Enter finish · Esc cancel", TbsIcons.InkLabel);
            }
        }

        float DrawWordmark(float x, float midY)
        {
            if (_wordmark == null)
                _wordmark = new GUIStyle
                {
                    font = TbsIcons.UiFont,
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    richText = true
                };
            _wordmark.normal.textColor = TbsIcons.ColInkHi;
            float w = _wordmark.CalcSize(new GUIContent("TBSplineS")).x;
            GUI.Label(new Rect(x, midY - 10f, w + 8f, 20f), "TB<color=#4c8ff0>Spline</color>S", _wordmark);
            return w;
        }

        void DrawBarDivider(ref float x, float midY)
        {
            EditorGUI.DrawRect(new Rect(x, midY - 13f, 1f, 26f), TbsIcons.ColLine);
            x += 7f;
        }

        float DrawModeSelector(float x, float midY, bool compact)
        {
            float w = compact ? 62f : 138f;
            _modeBtnRect = new Rect(x, midY - 17f, w, 34f);
            EditorGUI.BeginChangeCheck();
            bool open = GUI.Toggle(_modeBtnRect, _modeOpen, GUIContent.none, TbsIcons.SummaryButton);
            if (EditorGUI.EndChangeCheck()) { _modeOpen = open; if (open) CloseDropsExcept(0); }
            DrawIcon(new Rect(x + 11f, midY - 8f, 16f, 16f), TbsIcons.ModeSummary, TbsIcons.ColAccent);
            if (!compact)
            {
                string label = TbsSplineEditorState.EditModeActive ? "Edit Mode" : "Object Mode";
                GUI.Label(new Rect(x + 32f, midY - 9f, w - 52f, 18f), label, TbsIcons.InkLabel);
            }
            DrawIcon(new Rect(x + w - 18f, midY - 6f, 11f, 11f), TbsIcons.Chevron, TbsIcons.ColInkDim);
            return w;
        }

        static string ToolTip(string label, string action)
        {
            string key = TbsSplineEditorState.GetShortcut(action);
            return string.IsNullOrEmpty(key) ? label : label + " (" + key + ")";
        }

        void DrawToolGroup(ref float x, float midY, TbsSplineComputer computer, bool iconOnly)
        {
            bool edit = TbsSplineEditorState.EditModeActive;
            Texture2D[] icons = edit
                ? new[] { TbsIcons.ToolMove, TbsIcons.ToolRotate, TbsIcons.ToolScale, TbsIcons.GlyphPlus }
                : new[] { TbsIcons.ToolMove, TbsIcons.ToolRotate, TbsIcons.ToolNew };
            TbsTool[] tools = edit
                ? new[] { TbsTool.Move, TbsTool.Rotate, TbsTool.Scale, TbsTool.Point }
                : new[] { TbsTool.Move, TbsTool.Rotate, TbsTool.Draw };
            string[] labels = edit
                ? new[] { "Move", "Rotate", "Scale", "Point" }
                : new[] { "Move", "Rotate", "New" };
            string[] tips = edit
                ? new[] { ToolTip("Move", "Move"), ToolTip("Rotate", "Rotate"), ToolTip("Scale", "Scale"), ToolTip("Point · Shift+Scroll = Add / Delete / Merge", "Add") }
                : new[] { ToolTip("Move", "Move"), ToolTip("Rotate", "Rotate"), ToolTip("New spline", "New") };

            GUIStyle ls = ToolLabelStyle;
            int n = icons.Length;
            float capW = iconOnly ? 6f : 40f;
            float[] bw = new float[n];
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                bw[i] = iconOnly ? 34f : 30f + ls.CalcSize(new GUIContent(labels[i])).x + 12f;
                sum += bw[i];
            }
            float shellW = 6f + capW + sum + (n - 1) * 3f + 8f;
            var shell = new Rect(x, midY - 17f, shellW, 34f);
            GUI.Box(shell, GUIContent.none, TbsIcons.SegShell);
            float bx = shell.x + 6f;
            if (!iconOnly) GUI.Label(new Rect(bx, midY - 6f, capW, 14f), "Tools", TbsIcons.Caption);
            bx += capW;
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(bx, midY - 15f, bw[i], 30f);
                bool active = TbsSplineEditorState.ActiveTool == tools[i];
                EditorGUI.BeginChangeCheck();
                bool val = GUI.Toggle(r, active, new GUIContent(string.Empty, tips[i]), TbsIcons.SegButton);
                if (EditorGUI.EndChangeCheck() && val && !active) TbsSplineEditorActions.SetTool(computer, tools[i]);
                float ix = iconOnly ? r.center.x - 8f : r.x + 10f;
                DrawIcon(new Rect(ix, r.center.y - 8f, 16f, 16f), icons[i], active ? TbsIcons.ColAccentInk : TbsIcons.ColInk);
                if (!iconOnly)
                {
                    ls.normal.textColor = active ? TbsIcons.ColAccentInk : TbsIcons.ColInk;
                    GUI.Label(new Rect(r.x + 30f, r.y, r.width - 32f, r.height), labels[i], ls);
                }
                bx += bw[i] + 3f;
            }
            x += shellW;
        }

        static GUIStyle _toolLabel;
        static GUIStyle ToolLabelStyle
        {
            get
            {
                if (_toolLabel == null)
                    _toolLabel = new GUIStyle { font = TbsIcons.UiFont, fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                return _toolLabel;
            }
        }

        void CloseDropsExcept(int keep)
        {
            if (keep != 0) _modeOpen = false;
            if (keep != 1) _gridOpen = false;
            if (keep != 2) _orientOpen = false;
            if (keep != 3) _pivotOpen = false;
        }

        float DrawMiniSelector(ref Rect btnRect, float x, float midY, int dropIndex, Texture2D icon, string label, ref bool open)
        {
            float w = label == null ? 46f : 32f + TbsIcons.InkLabel.CalcSize(new GUIContent(label)).x + 32f;
            btnRect = new Rect(x, midY - 17f, w, 34f);
            EditorGUI.BeginChangeCheck();
            bool o = GUI.Toggle(btnRect, open, GUIContent.none, TbsIcons.SummaryButton);
            if (EditorGUI.EndChangeCheck()) { open = o; if (o) CloseDropsExcept(dropIndex); }
            DrawIcon(new Rect(x + 11f, midY - 8f, 16f, 16f), icon, TbsIcons.ColInk);
            if (label != null) GUI.Label(new Rect(x + 32f, midY - 9f, w - 50f, 18f), label, TbsIcons.InkLabel);
            DrawIcon(new Rect(x + w - 18f, midY - 6f, 11f, 11f), TbsIcons.Chevron, TbsIcons.ColInkDim);
            return w;
        }

        void DrawTransformInline(float x, float midY, TbsSplineComputer computer, float maxWidth)
        {
            if (computer == null || !TbsSplineEditorState.HasSplineSelection) return;
            if (maxWidth < 420f) return;
            Transform trs = computer.transform;
            bool hasKnot = TryGetSelectedKnot(computer, out int si, out int ki);
            bool handle = hasKnot && TbsSplineEditorState.SelectedHandle != 0;
            bool global = TbsSplineEditorState.OrientGlobal;

            string who, sub, tag = null;
            Color mark, tagColor = default;
            bool square = false;
            Vector3 shown;
            System.Action<Vector3> apply;

            if (handle)
            {
                bool inSide = TbsSplineEditorState.SelectedHandle == 1;
                var k = computer[si][ki];
                Vector3 world = trs.TransformPoint(inSide ? k.TangentInPosition : k.TangentOutPosition);
                shown = global ? world : trs.InverseTransformPoint(world) - k.Position;
                apply = v => TbsSplineEditorActions.SetTangentWorld(computer, si, ki, inSide, global ? v : trs.TransformPoint(k.Position + v));
                who = "Handle"; sub = "P" + (ki + 1); square = true;
                tag = inSide ? "In" : "Out"; tagColor = HandleColor(k.Mode); mark = tagColor;
            }
            else if (hasKnot)
            {
                Vector3 world = trs.TransformPoint(computer[si][ki].Position);
                Vector3 origin = computer[si][0].Position;
                shown = global ? world : computer[si][ki].Position - origin;
                apply = v => TbsSplineEditorActions.SetKnotWorld(computer, si, ki, global ? v : trs.TransformPoint(origin + v));
                who = "Point " + (ki + 1); sub = "#" + computer[si].Id; mark = TbsIcons.ColSel;
            }
            else
            {
                shown = global ? trs.position : trs.localPosition;
                apply = v => { Undo.RecordObject(trs, "Move Spline Object"); if (global) trs.position = v; else trs.localPosition = v; SceneView.RepaintAll(); };
                who = "Spline #" + computer[si].Id; sub = computer[si].Count + " pts"; mark = TbsIcons.ColAccent;
            }

            float whoW = TbsIcons.InkStrong.CalcSize(new GUIContent(who)).x;
            float subW = TbsIcons.Caption.CalcSize(new GUIContent(sub)).x;
            float tagW = tag != null ? Mathf.Max(32f, TagStyle.CalcSize(new GUIContent(tag)).x + 18f) : 0f;
            float chipW = 16f + whoW + 6f + subW + (tag != null ? 8f + tagW : 0f);
            const float coordsW = 246f;
            const float frameW = 50f;
            bool pointExtras = hasKnot && !handle;
            float extrasW = pointExtras ? 96f : 0f;
            float coordsX = x + 12f + chipW + 12f;
            float frameX = coordsX + coordsW + extrasW + 10f;
            float totalW = frameX + frameW + 12f - x;
            if (totalW > maxWidth) return;
            var shell = new Rect(x, midY - 19f, totalW, 38f);
            GUI.Box(shell, GUIContent.none, TbsIcons.SegShell);

            float cx = shell.x + 12f;
            DrawIcon(new Rect(cx, midY - 5f, 10f, 10f), square ? TbsIcons.SwatchTex : TbsIcons.LedDotTex, mark);
            cx += 16f;
            GUI.Label(new Rect(cx, midY - 9f, whoW + 2f, 18f), who, TbsIcons.InkStrong);
            cx += whoW + 6f;
            GUI.Label(new Rect(cx, midY - 7f, subW + 2f, 16f), sub, TbsIcons.Caption);
            cx += subW + 8f;
            if (tag != null)
            {
                var tr = new Rect(cx, midY - 10f, tagW, 20f);
                GUI.Box(tr, GUIContent.none, TbsIcons.MiniShell);
                var ts = TagStyle; ts.normal.textColor = tagColor;
                GUI.Label(tr, tag, ts);
            }

            EditorGUI.BeginChangeCheck();
            Vector3 nv = DrawInlineVec3(coordsX, midY, shown);
            if (EditorGUI.EndChangeCheck()) apply(nv);

            if (pointExtras)
            {
                float ex = coordsX + coordsW + 2f;
                float sizeValue = TbsSplineEditorActions.GetPrimarySize(computer);
                EditorGUI.BeginChangeCheck();
                if (ScrubLabel(new Rect(ex, midY - 10f, 14f, 20f), "S", CaptionCenter, sizeValue, out float scrubbed))
                    sizeValue = scrubbed;
                sizeValue = EditorGUI.FloatField(new Rect(ex + 15f, midY - 11f, 46f, 22f), sizeValue, NumField);
                if (EditorGUI.EndChangeCheck()) TbsSplineEditorActions.SetSelectedSize(computer, sizeValue);

                EditorGUI.BeginChangeCheck();
                Color knotColor = EditorGUI.ColorField(new Rect(ex + 66f, midY - 9f, 26f, 18f), GUIContent.none,
                    TbsSplineEditorActions.GetPrimaryColor(computer), false, false, false);
                if (EditorGUI.EndChangeCheck()) TbsSplineEditorActions.SetSelectedColor(computer, knotColor);
            }

            var ftr = new Rect(frameX, midY - 9f, frameW, 18f);
            GUI.Box(ftr, GUIContent.none, TbsIcons.MiniShell);
            GUI.Label(ftr, global ? "World" : "Local", CaptionCenter);
        }

        Vector3 DrawInlineVec3(float x, float midY, Vector3 v)
        {
            float[] c = { v.x, v.y, v.z };
            string[] ax = { "X", "Y", "Z" };
            for (int i = 0; i < 3; i++)
            {
                if (ScrubLabel(new Rect(x - 1f, midY - 10f, 14f, 20f), ax[i], AxisStyle(i), c[i], out float scrubbed))
                    c[i] = scrubbed;
                x += 14f;
                c[i] = EditorGUI.FloatField(new Rect(x, midY - 11f, 64f, 22f), c[i], NumField);
                x += 68f;
            }
            return new Vector3(c[0], c[1], c[2]);
        }

        static Color HandleColor(TbsTangentMode m)
        {
            switch (ModeToHandle(m))
            {
                case 0: return TbsIcons.ColHxAuto;
                case 1: return TbsIcons.ColHxAligned;
                case 2: return TbsIcons.ColHxMirror;
                default: return TbsIcons.ColHxFree;
            }
        }

        static GUIStyle[] _axisStyles;
        static GUIStyle AxisStyle(int i)
        {
            if (_axisStyles == null)
            {
                Color[] cols = { TbsIcons.Hex(0xFF7A7A), TbsIcons.Hex(0x8FE08F), TbsIcons.Hex(0x7FB2FF) };
                _axisStyles = new GUIStyle[3];
                for (int k = 0; k < 3; k++)
                {
                    _axisStyles[k] = new GUIStyle { font = TbsIcons.MonoFont, fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                    _axisStyles[k].normal.textColor = cols[k];
                }
            }
            return _axisStyles[i];
        }

        static GUIStyle _tagStyle;
        static GUIStyle TagStyle
        {
            get
            {
                if (_tagStyle == null)
                    _tagStyle = new GUIStyle { font = TbsIcons.UiFont, fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                return _tagStyle;
            }
        }

        static GUIStyle _captionCenter;
        static GUIStyle CaptionCenter
        {
            get
            {
                if (_captionCenter == null)
                    _captionCenter = new GUIStyle { font = TbsIcons.UiFont, fontSize = 9, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _captionCenter.normal.textColor = TbsIcons.ColInkDim;
                return _captionCenter;
            }
        }

        void DrawOrientPopover()
        {
            if (!_orientOpen) return;
            const float w = 236f;
            const float rowH = 46f;
            _orientPopRect = new Rect(_orientBtnRect.x, _orientBtnRect.yMax + 6f, w, 12f + rowH * 2f);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 &&
                !_orientPopRect.Contains(e.mousePosition) && !_orientBtnRect.Contains(e.mousePosition))
            { _orientOpen = false; e.Use(); return; }
            GUI.Box(_orientPopRect, GUIContent.none, TbsIcons.Popover);
            bool g = TbsSplineEditorState.OrientGlobal;
            if (DrawChoiceRow(new Rect(_orientPopRect.x + 6f, _orientPopRect.y + 6f, w - 12f, rowH), TbsIcons.Orient, "Global", "Axes aligned to the world", g))
            { TbsSplineEditorState.OrientGlobal = true; _orientOpen = false; }
            if (DrawChoiceRow(new Rect(_orientPopRect.x + 6f, _orientPopRect.y + 6f + rowH, w - 12f, rowH), TbsIcons.OrientLocal, "Local", "Axes follow the spline", !g))
            { TbsSplineEditorState.OrientGlobal = false; _orientOpen = false; }
        }

        void DrawPivotPopover(TbsSplineComputer computer)
        {
            if (!_pivotOpen) return;
            const float w = 236f;
            const float rowH = 46f;
            _pivotPopRect = new Rect(_pivotBtnRect.x, _pivotBtnRect.yMax + 6f, w, 12f + rowH * 2f);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 &&
                !_pivotPopRect.Contains(e.mousePosition) && !_pivotBtnRect.Contains(e.mousePosition))
            { _pivotOpen = false; e.Use(); return; }
            GUI.Box(_pivotPopRect, GUIContent.none, TbsIcons.Popover);
            bool cur = TbsSplineEditorState.PivotMode == TbsPivotMode.Cursor;
            if (DrawChoiceRow(new Rect(_pivotPopRect.x + 6f, _pivotPopRect.y + 6f, w - 12f, rowH), TbsIcons.PivotCursor, "3D Cursor", "Pivot at the placement cursor", cur))
            { TbsSplineEditorState.PivotMode = TbsPivotMode.Cursor; _pivotOpen = false; }
            if (DrawChoiceRow(new Rect(_pivotPopRect.x + 6f, _pivotPopRect.y + 6f + rowH, w - 12f, rowH), TbsIcons.Pivot, "Median", "Pivot at the selection center", !cur))
            { TbsSplineEditorState.PivotMode = TbsPivotMode.Median; _pivotOpen = false; }
        }

        bool DrawChoiceRow(Rect r, Texture2D icon, string title, string sub, bool active)
        {
            Event e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            if (active) EditorGUI.DrawRect(r, new Color(TbsIcons.ColAccent.r, TbsIcons.ColAccent.g, TbsIcons.ColAccent.b, 0.16f));
            else if (hover) EditorGUI.DrawRect(r, new Color(1f, 1f, 1f, 0.05f));
            DrawIcon(new Rect(r.x + 10f, r.y + r.height * 0.5f - 8f, 16f, 16f), icon, active ? TbsIcons.ColAccent : TbsIcons.ColInkDim);
            GUI.Label(new Rect(r.x + 34f, r.y + 6f, r.width - 60f, 16f), title, TbsIcons.InkStrong);
            GUI.Label(new Rect(r.x + 34f, r.y + 24f, r.width - 60f, 14f), sub, TbsIcons.Caption);
            if (active) DrawIcon(new Rect(r.xMax - 24f, r.y + r.height * 0.5f - 7f, 14f, 14f), TbsIcons.LedDotTex, TbsIcons.ColAccent);
            if (e.type == EventType.MouseDown && e.button == 0 && hover) { e.Use(); return true; }
            return false;
        }

        void DrawGridSelector(float x, float midY, float w, bool compact)
        {
            _gridBtnRect = new Rect(x, midY - 17f, w, 34f);
            EditorGUI.BeginChangeCheck();
            bool open = GUI.Toggle(_gridBtnRect, _gridOpen, GUIContent.none, TbsIcons.SummaryButton);
            if (EditorGUI.EndChangeCheck()) { _gridOpen = open; if (open) CloseDropsExcept(1); }
            DrawIcon(new Rect(x + 10f, midY - 8f, 16f, 16f), TbsIcons.Grid, TbsIcons.ColInk);
            if (!compact) GUI.Label(new Rect(x + 30f, midY - 9f, w - 46f, 18f), "Settings", TbsIcons.InkLabel);
            DrawIcon(new Rect(x + w - 18f, midY - 6f, 11f, 11f), TbsIcons.Chevron, TbsIcons.ColInkDim);
        }

        void DrawModePopover(TbsSplineComputer computer)
        {
            if (!_modeOpen) return;
            const float w = 224f;
            const float rowH = 46f;
            _modePopRect = new Rect(_modeBtnRect.x, _modeBtnRect.yMax + 6f, w, 12f + rowH * 2f);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 &&
                !_modePopRect.Contains(e.mousePosition) && !_modeBtnRect.Contains(e.mousePosition))
            {
                _modeOpen = false; e.Use(); return;
            }
            GUI.Box(_modePopRect, GUIContent.none, TbsIcons.Popover);
            DrawModeRow(new Rect(_modePopRect.x + 6f, _modePopRect.y + 6f, w - 12f, rowH),
                TbsIcons.ModeEdit, "Edit", "Edit points on the spline", TbsSplineEditorState.EditModeActive, computer, TbsEditorMode.Edit);
            DrawModeRow(new Rect(_modePopRect.x + 6f, _modePopRect.y + 6f + rowH, w - 12f, rowH),
                TbsIcons.ModeObject, "Object", "Move whole splines", TbsSplineEditorState.ObjectModeActive, computer, TbsEditorMode.Object);
        }

        void DrawModeRow(Rect r, Texture2D icon, string title, string sub, bool active, TbsSplineComputer computer, TbsEditorMode mode)
        {
            Event e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            if (active) EditorGUI.DrawRect(r, new Color(TbsIcons.ColAccent.r, TbsIcons.ColAccent.g, TbsIcons.ColAccent.b, 0.16f));
            else if (hover) EditorGUI.DrawRect(r, new Color(1f, 1f, 1f, 0.05f));
            DrawIcon(new Rect(r.x + 10f, r.y + r.height * 0.5f - 8f, 16f, 16f), icon, active ? TbsIcons.ColAccent : TbsIcons.ColInkDim);
            GUI.Label(new Rect(r.x + 34f, r.y + 6f, r.width - 60f, 16f), title, TbsIcons.InkStrong);
            GUI.Label(new Rect(r.x + 34f, r.y + 24f, r.width - 60f, 14f), sub, TbsIcons.Caption);
            if (active) DrawIcon(new Rect(r.xMax - 24f, r.y + r.height * 0.5f - 7f, 14f, 14f), TbsIcons.LedDotTex, TbsIcons.ColAccent);
            if (e.type == EventType.MouseDown && e.button == 0 && hover)
            {
                TbsSplineEditorActions.SetMode(computer, mode);
                _modeOpen = false;
                e.Use();
            }
        }

        void DrawGridPopover(TbsSplineComputer computer)
        {
            if (!_gridOpen) return;
            const float w = 252f;
            const float h = 10f
                + 22f + 30f + 30f + 30f + 32f
                + 10f + 22f + 30f + 32f
                + 10f + 22f + 26f * 4f + 24f * 4f
                + 10f + 22f + 24f * 6f
                + 8f;
            _gridPopRect = new Rect(_gridBtnRect.xMax - w, _gridBtnRect.yMax + 6f, w, h);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 &&
                !_gridPopRect.Contains(e.mousePosition) && !_gridBtnRect.Contains(e.mousePosition))
            {
                _gridOpen = false; e.Use(); return;
            }
            GUI.Box(_gridPopRect, GUIContent.none, TbsIcons.Popover);
            float px = _gridPopRect.x + 12f;
            float pw = w - 24f;
            float ry = _gridPopRect.y + 10f;

            GUI.Label(new Rect(px, ry, pw, 14f), "GRID", TbsIcons.Caption); ry += 22f;

            GUI.Label(new Rect(px, ry + 3f, pw - 44f, 16f), "Show grid", TbsIcons.InkLabel);
            bool sg = ToggleSwitch(new Rect(px + pw - 38f, ry, 38f, 22f), TbsSplineEditorState.ShowGrid, "Show grid");
            if (sg != TbsSplineEditorState.ShowGrid) TbsSplineEditorState.ShowGrid = sg;
            ry += 30f;

            GUI.Label(new Rect(px, ry + 3f, pw - 44f, 16f), "Snap to grid", TbsIcons.InkLabel);
            bool sn = ToggleSwitch(new Rect(px + pw - 38f, ry, 38f, 22f), TbsSplineEditorState.SnapToGrid, "Snap to grid");
            if (sn != TbsSplineEditorState.SnapToGrid) TbsSplineEditorState.SnapToGrid = sn;
            ry += 30f;

            GUI.Label(new Rect(px, ry + 3f, pw - 80f, 16f), "Cell size", TbsIcons.InkLabel);
            EditorGUI.BeginChangeCheck();
            float ns = EditorGUI.FloatField(new Rect(px + pw - 74f, ry, 74f, 22f), computer.EditorGridSize, NumField);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(computer, "Grid Cell Size");
                computer.EditorGridSize = Mathf.Max(0.05f, ns);
                EditorUtility.SetDirty(computer);
                SceneView.RepaintAll();
            }
            ry += 30f;

            GUI.Label(new Rect(px, ry + 3f, pw - 80f, 16f), "Grid height", TbsIcons.InkLabel);
            EditorGUI.BeginChangeCheck();
            float nh = EditorGUI.FloatField(new Rect(px + pw - 74f, ry, 74f, 22f), computer.EditorGridHeight, NumField);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(computer, "Grid Height");
                computer.EditorGridHeight = nh;
                EditorUtility.SetDirty(computer);
                SceneView.RepaintAll();
            }
            ry += 32f;

            EditorGUI.DrawRect(new Rect(px, ry, pw, 1f), TbsIcons.ColLine); ry += 10f;
            GUI.Label(new Rect(px, ry, pw, 14f), "PLACEMENT", TbsIcons.Caption); ry += 22f;

            GUI.Label(new Rect(px, ry + 3f, pw - 132f, 16f), "Plane", TbsIcons.InkLabel);
            var segR = new Rect(px + pw - 128f, ry, 128f, 22f);
            GUI.Box(segR, GUIContent.none, TbsIcons.MiniShell);
            if (MiniSegBtn(new Rect(segR.x + 2f, segR.y + 2f, 62f, 18f), "XZ", TbsSplineEditorState.Placement == TbsPlacementMode.PlaneXZ))
                TbsSplineEditorState.Placement = TbsPlacementMode.PlaneXZ;
            if (MiniSegBtn(new Rect(segR.x + 64f, segR.y + 2f, 62f, 18f), "Collider", TbsSplineEditorState.Placement == TbsPlacementMode.Collider))
                TbsSplineEditorState.Placement = TbsPlacementMode.Collider;
            ry += 30f;

            GUI.Label(new Rect(px, ry + 3f, pw - 44f, 16f), "Point labels", TbsIcons.InkLabel);
            bool lb = ToggleSwitch(new Rect(px + pw - 38f, ry, 38f, 22f), TbsSplineEditorState.ShowLabels, "Point labels");
            if (lb != TbsSplineEditorState.ShowLabels) TbsSplineEditorState.ShowLabels = lb;
            ry += 32f;

            EditorGUI.DrawRect(new Rect(px, ry, pw, 1f), TbsIcons.ColLine); ry += 10f;
            GUI.Label(new Rect(px, ry, pw, 14f), "GIZMO", TbsIcons.Caption); ry += 22f;
            ry = GizmoSlider(px, pw, ry, "Handle", () => TbsSplineEditorState.HandleSize, v => TbsSplineEditorState.HandleSize = v, 0.3f, 4f);
            ry = GizmoSlider(px, pw, ry, "Point", () => TbsSplineEditorState.PointSize, v => TbsSplineEditorState.PointSize = v, 0.3f, 3f);
            ry = GizmoSlider(px, pw, ry, "Line", () => TbsSplineEditorState.LineWidth, v => TbsSplineEditorState.LineWidth = v, 0.5f, 3f);
            ry = GizmoSlider(px, pw, ry, "Preview", () => TbsSplineEditorState.PreviewLineWidth, v => TbsSplineEditorState.PreviewLineWidth = v, 1f, 6f);
            ry = GizmoColor(px, pw, ry, "Idle", () => TbsSplineEditorState.IdleCurveColor, c => TbsSplineEditorState.IdleCurveColor = c);
            ry = GizmoColor(px, pw, ry, "Selected", () => TbsSplineEditorState.SelectedCurveColor, c => TbsSplineEditorState.SelectedCurveColor = c);
            ry = GizmoColor(px, pw, ry, "Hover", () => TbsSplineEditorState.HoverCurveColor, c => TbsSplineEditorState.HoverCurveColor = c);
            ry = GizmoColor(px, pw, ry, "Preview", () => TbsSplineEditorState.PreviewLineColor, c => TbsSplineEditorState.PreviewLineColor = c);
            ry += 2f;

            EditorGUI.DrawRect(new Rect(px, ry, pw, 1f), TbsIcons.ColLine); ry += 10f;
            GUI.Label(new Rect(px, ry, pw, 14f), "SHORTCUTS", TbsIcons.Caption); ry += 22f;
            ShortcutRow(px, pw, ref ry, "Move", "Move");
            ShortcutRow(px, pw, ref ry, "Rotate", "Rotate");
            ShortcutRow(px, pw, ref ry, "Scale", "Scale");
            ShortcutRow(px, pw, ref ry, "Point tool", "Add");
            ShortcutRow(px, pw, ref ry, "New spline", "New");
            ShortcutRow(px, pw, ref ry, "Edit / Object", "Mode");

            if (_scCapture != null)
            {
                Event ke = Event.current;
                if (ke.type == EventType.KeyDown)
                {
                    if (ke.keyCode == KeyCode.Escape || ke.keyCode == KeyCode.None) _scCapture = null;
                    else if (!IsModifierKey(ke.keyCode))
                    {
                        string combo = ((ke.control || ke.command) ? "Ctrl+" : "") + (ke.shift ? "Shift+" : "") + ke.keyCode;
                        TbsSplineEditorState.SetShortcut(_scCapture, combo);
                        _scCapture = null;
                    }
                    ke.Use();
                }
            }
        }

        float GizmoSlider(float px, float pw, float ry, string label, Func<float> get, Action<float> set, float min, float max)
        {
            GUI.Label(new Rect(px, ry + 1f, 72f, 16f), label, TbsIcons.InkLabel);
            EditorGUI.BeginChangeCheck();
            float v = GUI.HorizontalSlider(new Rect(px + 74f, ry + 5f, pw - 74f - 36f, 12f), get(), min, max);
            if (EditorGUI.EndChangeCheck()) set(Mathf.Round(v * 20f) / 20f);
            GUI.Label(new Rect(px + pw - 34f, ry + 1f, 34f, 16f), get().ToString("0.0"), TbsIcons.Mono);
            return ry + 26f;
        }

        float GizmoColor(float px, float pw, float ry, string label, Func<Color> get, Action<Color> set)
        {
            GUI.Label(new Rect(px, ry + 1f, 72f, 16f), label, TbsIcons.InkLabel);
            EditorGUI.BeginChangeCheck();
            Color c = EditorGUI.ColorField(new Rect(px + 74f, ry, pw - 74f, 18f), get());
            if (EditorGUI.EndChangeCheck()) set(c);
            return ry + 24f;
        }

        static bool ToggleSwitch(Rect r, bool value, string tip)
        {
            var tr = new Rect(r.x, r.y + (r.height - 20f) * 0.5f, 38f, 20f);
            if (GUI.Button(tr, new GUIContent(string.Empty, tip), GUIStyle.none)) value = !value;
            GUI.DrawTexture(tr, value ? TbsIcons.ToggleOnTex : TbsIcons.ToggleOffTex, ScaleMode.ScaleToFit, true);
            return value;
        }

        static bool MiniSegBtn(Rect r, string label, bool active, string tip = null)
        {
            if (active) EditorGUI.DrawRect(r, TbsIcons.ColAccent);
            GUIStyle style = MiniSegStyle;
            style.normal.textColor = active ? TbsIcons.ColAccentInk : TbsIcons.ColInk;
            style.hover.textColor = active ? TbsIcons.ColAccentInk : TbsIcons.ColInkHi;
            return GUI.Button(r, new GUIContent(label, tip), style);
        }

        void DrawAddModeLabel(TbsSplineComputer computer, SceneView sceneView)
        {
            if (!TbsSplineEditorState.PointMode || !TbsSplineEditorState.HasSplineSelection || MouseOver) return;
            Vector2 m = Event.current.mousePosition;
            string mode = TbsSplineEditorState.AddSubMode switch
            {
                TbsAddMode.End => "Point · Add End",
                TbsAddMode.Start => "Point · Add Start",
                TbsAddMode.Insert => "Point · Insert",
                TbsAddMode.Delete => "Point · Delete",
                _ => "Point · Merge"
            };
            const string hint = "Shift+wheel: mode";
            float w = Mathf.Max(TbsIcons.InkStrong.CalcSize(new GUIContent(mode)).x, TbsIcons.Caption.CalcSize(new GUIContent(hint)).x) + 40f;
            var r = new Rect(m.x + 20f, m.y + 20f, w, 40f);
            if (r.xMax > sceneView.position.width - 6f) r.x = m.x - w - 20f;
            if (r.yMax > sceneView.position.height - 6f) r.y = m.y - 44f;
            GUI.Box(r, GUIContent.none, TbsIcons.SegShell);
            bool removal = TbsSplineEditorState.AddSubMode == TbsAddMode.Delete || TbsSplineEditorState.AddSubMode == TbsAddMode.Merge;
            DrawIcon(new Rect(r.x + 11f, r.y + 12f, 15f, 15f), removal ? TbsIcons.GlyphTrash : TbsIcons.GlyphPlus, TbsIcons.ColAccent);
            GUI.Label(new Rect(r.x + 32f, r.y + 5f, w - 38f, 16f), mode, TbsIcons.InkStrong);
            GUI.Label(new Rect(r.x + 32f, r.y + 21f, w - 38f, 14f), hint, TbsIcons.Caption);
        }

        void DrawStatusPill(TbsSplineComputer computer, SceneView sceneView)
        {
            string modeL = TbsSplineEditorState.EditModeActive ? "Edit" : "Object";
            string toolL = ToolLabel(TbsSplineEditorState.ActiveTool);
            string selL;
            int selSpline = TbsSplineEditorState.SelectedSpline;
            if (TryGetSelectedKnot(computer, out _, out int selKnot)) selL = $"Point {selKnot + 1}";
            else if (computer != null && selSpline >= 0 && selSpline < computer.SplineCount) selL = $"Spline · {computer[selSpline].Count} pts";
            else selL = "Nothing selected";
            string text = $"{modeL}   ·   {toolL}   ·   {selL}";
            float tw = TbsIcons.InkLabel.CalcSize(new GUIContent(text)).x;
            float w = tw + 40f;
            var r = new Rect(sceneView.position.width - w - 16f, sceneView.position.height - 30f - 38f, w, 30f);
            GUI.Box(r, GUIContent.none, TbsIcons.Pill);
            DrawIcon(new Rect(r.x + 13f, r.y + 11f, 8f, 8f), TbsIcons.LedDotTex, TbsIcons.ColAccent);
            GUI.Label(new Rect(r.x + 27f, r.y + 6f, w - 34f, 18f), text, TbsIcons.InkLabel);
        }

        static string ToolLabel(TbsTool t) => t switch
        {
            TbsTool.Move => "Move",
            TbsTool.Rotate => "Rotate",
            TbsTool.Scale => "Scale",
            TbsTool.Point => "Point",
            TbsTool.Draw => "New",
            _ => "Select"
        };

        static GUIStyle _numField;
        static GUIStyle NumField
        {
            get
            {
                if (_numField == null)
                    _numField = new GUIStyle(EditorStyles.numberField)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        font = TbsIcons.MonoFont,
                        fontSize = 11,
                        border = new RectOffset(6, 6, 6, 6)
                    };
                _numField.normal.background = TbsIcons.FieldTex;
                _numField.focused.background = TbsIcons.FieldFocusTex;
                _numField.normal.textColor = TbsIcons.ColInkHi;
                _numField.focused.textColor = TbsIcons.ColInkHi;
                return _numField;
            }
        }

        static GUIStyle _miniSegStyle;
        static GUIStyle MiniSegStyle
        {
            get
            {
                if (_miniSegStyle == null)
                    _miniSegStyle = new GUIStyle
                    {
                        font = TbsIcons.UiFont,
                        fontSize = 11,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                return _miniSegStyle;
            }
        }

        static bool TryGetSelectedKnot(TbsSplineComputer computer, out int splineIndex, out int knotIndex)
        {
            splineIndex = TbsSplineEditorState.SelectedSpline;
            knotIndex = TbsSplineEditorState.SelectedKnot;
            return computer != null
                   && splineIndex >= 0 && splineIndex < computer.SplineCount
                   && knotIndex >= 0 && knotIndex < computer[splineIndex].Count;
        }

        static bool TryGetCardsContext(TbsSplineComputer computer, out TbsTangentMode mode, out bool applyAll, out int splineIndex)
        {
            mode = TbsTangentMode.AutoSmooth;
            applyAll = TbsSplineEditorState.ObjectModeActive;
            splineIndex = TbsSplineEditorState.SelectedSpline;
            if (applyAll)
            {
                if (computer == null || splineIndex < 0 || splineIndex >= computer.SplineCount || computer[splineIndex].Count == 0) return false;
                mode = computer[splineIndex][0].Mode;
                return true;
            }
            if (!TryGetSelectedKnot(computer, out splineIndex, out int ki)) return false;
            mode = computer[splineIndex][ki].Mode;
            return true;
        }

        static void ApplyCardMode(TbsSplineComputer computer, TbsTangentMode mode, bool applyAll, int splineIndex)
        {
            if (applyAll) TbsSplineEditorActions.SetAllKnotsMode(computer, splineIndex, mode);
            else ApplyKnotMode(computer, mode);
        }

        enum SecondCard { None, Handles, Param }

        bool TryCardsLayout(TbsSplineComputer computer, SceneView sceneView, out int splineIndex, out TbsSplineType type, out Rect typeRect, out Rect secondRect, out SecondCard second)
        {
            typeRect = default;
            secondRect = default;
            second = SecondCard.None;
            type = TbsSplineType.Bezier;
            splineIndex = TbsSplineEditorState.SelectedSpline;
            if (computer == null || splineIndex < 0 || splineIndex >= computer.SplineCount) return false;
            type = computer[splineIndex].Type;

            bool knotContext = TryGetCardsContext(computer, out _, out _, out _);
            if (type == TbsSplineType.Bezier && knotContext) second = SecondCard.Handles;
            else if (type == TbsSplineType.CatmullRom) second = SecondCard.Param;

            const float typeW = 328f;
            float secondW = second == SecondCard.Handles ? 428f : second == SecondCard.Param ? 256f : 0f;
            float gap = secondW > 0f ? 10f : 0f;
            float totalW = typeW + gap + secondW;
            float avail = sceneView.position.width - 24f;
            float y = _topBar.yMax + 14f;
            if (secondW > 0f && totalW > avail)
            {
                typeRect = new Rect((sceneView.position.width - typeW) * 0.5f, y, typeW, 42f);
                secondRect = new Rect((sceneView.position.width - secondW) * 0.5f, y + 50f, secondW, 42f);
                return true;
            }
            float startX = (sceneView.position.width - totalW) * 0.5f;
            typeRect = new Rect(startX, y, typeW, 42f);
            if (secondW > 0f) secondRect = new Rect(typeRect.xMax + gap, y, secondW, 42f);
            return true;
        }

        Rect CardsBounds(TbsSplineComputer computer, SceneView sceneView)
        {
            if (!TryCardsLayout(computer, sceneView, out _, out _, out Rect tr, out Rect sr, out SecondCard second)) return new Rect();
            return second == SecondCard.None ? tr : Rect.MinMaxRect(tr.xMin, tr.yMin, sr.xMax, sr.yMax);
        }

        void DrawTypeCards(TbsSplineComputer computer, SceneView sceneView)
        {
            if (!TryCardsLayout(computer, sceneView, out int si, out TbsSplineType type, out Rect typeRect, out Rect secondRect, out SecondCard second)) return;
            DrawSplineTypeCard(typeRect, computer, si, type);
            if (second == SecondCard.Handles)
            {
                TryGetCardsContext(computer, out TbsTangentMode mode, out bool applyAll, out _);
                DrawHandleCard(secondRect, computer, mode, applyAll, si);
            }
            else if (second == SecondCard.Param)
            {
                DrawParamCard(secondRect, computer, si, computer[si].KnotParametrization);
            }
        }

        static readonly (TbsSplineType type, string label)[] SplineTypeChips =
        {
            (TbsSplineType.Bezier, "Bezier"),
            (TbsSplineType.CatmullRom, "Catmull"),
            (TbsSplineType.BSpline, "B-Spline"),
            (TbsSplineType.Linear, "Linear")
        };

        void DrawSplineTypeCard(Rect r, TbsSplineComputer computer, int si, TbsSplineType type)
        {
            int pki = -1;
            bool pointScope = TbsSplineEditorState.EditModeActive
                && TryGetSelectedKnot(computer, out int psi, out pki)
                && psi == si;
            TbsSplineType active = type;
            if (pointScope && type == TbsSplineType.Bezier)
            {
                TbsTangentMode m = computer[si][pki].Mode;
                active = m == TbsTangentMode.Linear ? TbsSplineType.Linear
                    : m == TbsTangentMode.AutoSmooth ? TbsSplineType.CatmullRom
                    : TbsSplineType.Bezier;
            }
            GUI.Box(r, GUIContent.none, TbsIcons.Card);
            float x = r.x + 12f;
            GUI.Label(new Rect(x, r.center.y - 7f, 40f, 14f), pointScope ? "Point" : "Type", TbsIcons.Caption);
            x += 42f;
            var shell = new Rect(x, r.center.y - 15f, r.xMax - x - 11f, 30f);
            GUI.Box(shell, GUIContent.none, TbsIcons.ChipShell);
            float cw = (shell.width - 10f) * 0.25f;
            for (int i = 0; i < SplineTypeChips.Length; i++)
            {
                TbsSplineType chipType = SplineTypeChips[i].type;
                bool enabled = !pointScope || chipType != TbsSplineType.BSpline;
                var chipRect = new Rect(shell.x + 2f + i * (cw + 2f), shell.y + 2f, cw, 26f);
                if (Chip(chipRect, SplineTypeChips[i].label, active == chipType, null, enabled))
                {
                    if (pointScope) TbsSplineEditorActions.SetPointType(computer, si, chipType);
                    else TbsSplineEditorActions.SetSplineType(computer, si, chipType);
                }
            }
        }

        void DrawParamCard(Rect r, TbsSplineComputer computer, int si, float value)
        {
            GUI.Box(r, GUIContent.none, TbsIcons.Card);
            float x = r.x + 12f;
            GUI.Label(new Rect(x, r.center.y - 7f, 42f, 14f), "Curve", TbsIcons.Caption);
            x += 46f;
            GUI.Label(new Rect(x, r.center.y - 7f, 52f, 14f), "Uniform", TbsIcons.Caption);
            float sx = x + 52f;
            float sw = r.xMax - sx - 58f;
            EditorGUI.BeginChangeCheck();
            float nv = GUI.HorizontalSlider(new Rect(sx, r.center.y - 2f, sw, 12f), value, 0f, 1f);
            if (EditorGUI.EndChangeCheck()) TbsSplineEditorActions.SetSplineParametrization(computer, si, nv);
            GUI.Label(new Rect(sx + sw + 6f, r.center.y - 7f, 52f, 14f), "Chordal", TbsIcons.Caption);
        }

        void DrawHandleCard(Rect r, TbsSplineComputer computer, TbsTangentMode mode, bool applyAll, int si)
        {
            GUI.Box(r, GUIContent.none, TbsIcons.Card);
            float x = r.x + 12f;
            GUI.Label(new Rect(x, r.center.y - 7f, 54f, 14f), "Handles", TbsIcons.Caption);
            x += 56f;
            var shell = new Rect(x, r.center.y - 15f, r.xMax - x - 11f, 30f);
            GUI.Box(shell, GUIContent.none, TbsIcons.ChipShell);
            int cur = ModeToHandle(mode);
            float cw = (shell.width - 10f) * 0.25f;
            DrawHandleChip(shell, 0, cw, "Auto", cur == 0, TbsIcons.ColHxAuto, computer, TbsTangentMode.AutoSmooth, applyAll, si);
            DrawHandleChip(shell, 1, cw, "Aligned", cur == 1, TbsIcons.ColHxAligned, computer, TbsTangentMode.Continuous, applyAll, si);
            DrawHandleChip(shell, 2, cw, "Mirror", cur == 2, TbsIcons.ColHxMirror, computer, TbsTangentMode.Mirrored, applyAll, si);
            DrawHandleChip(shell, 3, cw, "Free", cur == 3, TbsIcons.ColHxFree, computer, TbsTangentMode.Broken, applyAll, si);
        }

        void DrawHandleChip(Rect shell, int i, float cw, string label, bool active, Color swatch, TbsSplineComputer computer, TbsTangentMode mode, bool applyAll, int si)
        {
            var r = new Rect(shell.x + 2f + i * (cw + 2f), shell.y + 2f, cw, 26f);
            if (Chip(r, label, active, swatch)) ApplyCardMode(computer, mode, applyAll, si);
        }

        bool Chip(Rect r, string label, bool active, Color? swatch, bool enabled = true)
        {
            bool clicked;
            using (new EditorGUI.DisabledScope(!enabled))
            {
                EditorGUI.BeginChangeCheck();
                bool val = GUI.Toggle(r, active, GUIContent.none, TbsIcons.ChipButton);
                clicked = EditorGUI.EndChangeCheck() && val && !active && enabled;
            }
            float tx = r.x + 12f;
            if (swatch.HasValue)
            {
                DrawIcon(new Rect(r.x + 11f, r.center.y - 5f, 10f, 10f), TbsIcons.SwatchTex, swatch.Value);
                tx = r.x + 25f;
            }
            GUIStyle style = ChipTextStyle(active);
            Color prev = style.normal.textColor;
            if (!enabled) style.normal.textColor = new Color(prev.r, prev.g, prev.b, 0.35f);
            GUI.Label(new Rect(tx, r.y, r.xMax - tx - 4f, r.height), label, style);
            style.normal.textColor = prev;
            return clicked;
        }

        static GUIStyle _chipText;
        static GUIStyle ChipTextStyle(bool active)
        {
            if (_chipText == null)
                _chipText = new GUIStyle { font = TbsIcons.UiFont, fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _chipText.normal.textColor = active ? TbsIcons.ColAccentInk : TbsIcons.ColInkDim;
            return _chipText;
        }

        static int ModeToHandle(TbsTangentMode m) => (int)TbsTangentModeView.GetHandleType(m);

        static void ApplyKnotMode(TbsSplineComputer computer, TbsTangentMode mode)
        {
            if (TbsSplineEditorState.HasMultiSelection)
                TbsSplineEditorActions.SetSelectedKnotsMode(computer, mode);
            else
                TbsSplineEditorActions.SetKnotMode(computer, TbsSplineEditorState.SelectedSpline, TbsSplineEditorState.SelectedKnot, mode);
        }

        static Quaternion SplineFrame(TbsSplineComputer computer)
        {
            if (computer == null) return Quaternion.identity;
            int si = TbsSplineEditorState.LastSpline;
            if (si < 0 || si >= computer.SplineCount) return computer.transform.rotation;
            TbsSpline sp = computer[si];
            int ki = TbsSplineEditorState.SelectedKnot;
            if ((ki < 0 || ki >= sp.Count) && TbsSplineEditorState.LastKnotIds.Count > 0)
                ki = sp.IndexOfKnotId(TbsSplineEditorState.LastKnotIds[0]);
            if (ki < 0 || ki >= sp.Count) return computer.transform.rotation;
            return computer.transform.rotation * sp[ki].Rotation;
        }

        void DrawRedoPanel(TbsSplineComputer computer, SceneView sceneView)
        {
            const float w = 290f;
            const float headH = 44f;
            bool hasAction = TbsSplineEditorState.LastActionValid;
            bool rotation = TbsSplineEditorState.LastIsRotation;
            bool scale = TbsSplineEditorState.LastOp == TbsLastOp.Scale;
            bool editable = hasAction && (rotation || scale
                || TbsSplineEditorState.LastOp == TbsLastOp.Move
                || TbsSplineEditorState.LastOp == TbsLastOp.MoveSpline
                || TbsSplineEditorState.LastOp == TbsLastOp.MoveHandle
                || TbsSplineEditorState.LastOp == TbsLastOp.Add);
            bool showOrient = hasAction && !rotation && !scale;
            const float footerH = 30f;
            float bodyH = (_redoCollapsed || !hasAction) ? 0f : (showOrient ? 92f : 52f) + footerH;
            _redoRect = new Rect(14f, sceneView.position.height - 34f - headH - bodyH, w, headH + bodyH);
            GUI.Box(_redoRect, GUIContent.none, TbsIcons.PanelLarge);

            var header = new Rect(_redoRect.x, _redoRect.y, w, headH);
            GUI.Box(header, GUIContent.none, TbsIcons.HeaderGrad);
            var chip = new Rect(header.x + 12f, header.y + 10f, 24f, 24f);
            GUI.Box(chip, GUIContent.none, TbsIcons.ChipAccentPanel);
            DrawIcon(new Rect(chip.x + 5f, chip.y + 5f, 14f, 14f), rotation ? TbsIcons.ToolRotate : TbsIcons.Reverse, TbsIcons.Hex(0xCFE0FF));
            GUI.Label(new Rect(header.x + 46f, header.y + 6f, w - 80f, 12f), "LAST ACTION", TbsIcons.Caption);
            var prev = GUI.contentColor;
            if (!hasAction) GUI.contentColor = TbsIcons.ColInkDim;
            GUI.Label(new Rect(header.x + 46f, header.y + 20f, w - 80f, 18f), hasAction ? TbsSplineEditorState.LastOpLabel : "No action yet", TbsIcons.InkStrong);
            GUI.contentColor = prev;
            if (hasAction)
                DrawIcon(new Rect(header.xMax - 24f, header.y + headH * 0.5f - 6f, 11f, 11f), TbsIcons.Chevron, TbsIcons.ColInkDim);

            Event e = Event.current;
            if (hasAction && e.type == EventType.MouseDown && e.button == 0 && header.Contains(e.mousePosition))
            {
                _redoCollapsed = !_redoCollapsed;
                e.Use();
            }
            if (_redoCollapsed || !hasAction) return;

            float by = header.yMax + 11f;
            float fw = (w - 24f - 14f) / 3f;

            if (rotation)
            {
                Vector3 rot = TbsSplineEditorState.LastRotEuler;
                using (new EditorGUI.DisabledScope(!editable))
                {
                    if (TbsSplineEditorState.LastOp == TbsLastOp.Rotate)
                    {
                        float nrx = DrawDeltaField(new Rect(_redoRect.x + 12f, by, fw, 38f), "Roll°", rot.x, out bool crx);
                        if (crx && computer != null) TbsSplineEditorActions.SetLastRotation(computer, new Vector3(nrx, 0f, 0f));
                    }
                    else
                    {
                        float rx = DrawDeltaField(new Rect(_redoRect.x + 12f, by, fw, 38f), "Rot X", rot.x, out bool cx);
                        float ry = DrawDeltaField(new Rect(_redoRect.x + 12f + fw + 7f, by, fw, 38f), "Rot Y", rot.y, out bool cy);
                        float rz = DrawDeltaField(new Rect(_redoRect.x + 12f + (fw + 7f) * 2f, by, fw, 38f), "Rot Z", rot.z, out bool cz);
                        if ((cx || cy || cz) && computer != null) TbsSplineEditorActions.SetLastRotation(computer, new Vector3(rx, ry, rz));
                    }
                }
                DrawRedoFooter(computer, by + 44f, w);
                return;
            }

            if (scale)
            {
                Vector3 sc = TbsSplineEditorState.LastScale;
                using (new EditorGUI.DisabledScope(!editable))
                {
                    float sx = DrawDeltaField(new Rect(_redoRect.x + 12f, by, fw, 38f), "× X", sc.x, out bool cx);
                    float sy = DrawDeltaField(new Rect(_redoRect.x + 12f + fw + 7f, by, fw, 38f), "× Y", sc.y, out bool cy);
                    float sz = DrawDeltaField(new Rect(_redoRect.x + 12f + (fw + 7f) * 2f, by, fw, 38f), "× Z", sc.z, out bool cz);
                    if ((cx || cy || cz) && computer != null)
                        TbsSplineEditorActions.SetLastScale(computer, new Vector3(sx, sy, sz));
                }
                DrawRedoFooter(computer, by + 44f, w);
                return;
            }

            bool global = TbsSplineEditorState.OrientGlobal;
            Quaternion frame = global ? Quaternion.identity : SplineFrame(computer);
            Vector3 shown = global ? TbsSplineEditorState.LastDelta : Quaternion.Inverse(frame) * TbsSplineEditorState.LastDelta;
            using (new EditorGUI.DisabledScope(!editable))
            {
                float nx = DrawDeltaField(new Rect(_redoRect.x + 12f, by, fw, 38f), "Δ X", shown.x, out bool cx);
                float ny = DrawDeltaField(new Rect(_redoRect.x + 12f + fw + 7f, by, fw, 38f), "Δ Y", shown.y, out bool cy);
                float nz = DrawDeltaField(new Rect(_redoRect.x + 12f + (fw + 7f) * 2f, by, fw, 38f), "Δ Z", shown.z, out bool cz);
                if ((cx || cy || cz) && editable && computer != null)
                {
                    Vector3 newShown = new Vector3(nx, ny, nz);
                    Vector3 newWorld = global ? newShown : frame * newShown;
                    TbsSplineEditorActions.SetLastDeltaWorld(computer, newWorld);
                }
            }
            float oy = by + 44f;
            GUI.Label(new Rect(_redoRect.x + 12f, oy + 3f, 52f, 16f), "ORIENT", TbsIcons.Caption);
            var seg = new Rect(_redoRect.x + 66f, oy, w - 66f - 12f, 22f);
            GUI.Box(seg, GUIContent.none, TbsIcons.MiniShell);
            float half = seg.width * 0.5f;
            if (MiniSegBtn(new Rect(seg.x + 2f, seg.y + 2f, half - 3f, 18f), "Global", global, "Δ measured along world axes"))
                TbsSplineEditorState.OrientGlobal = true;
            if (MiniSegBtn(new Rect(seg.x + half + 1f, seg.y + 2f, half - 3f, 18f), "Local", !global, "Δ measured along the spline (forward)"))
                TbsSplineEditorState.OrientGlobal = false;
            DrawRedoFooter(computer, oy + 26f, w);
        }

        void DrawRedoFooter(TbsSplineComputer computer, float y, float w)
        {
            var row = new Rect(_redoRect.x + 12f, y + 4f, w - 24f, 20f);
            float half = row.width * 0.5f;
            bool canRepeat = TbsSplineEditorActions.CanRepeatLast;
            using (new EditorGUI.DisabledScope(!canRepeat))
            {
                if (MiniSegBtn(new Rect(row.x, row.y, half - 3f, 20f), "Repeat", false, "Apply the same change once more") && computer != null)
                    TbsSplineEditorActions.RepeatLast(computer);
            }
            if (MiniSegBtn(new Rect(row.x + half + 1f, row.y, half - 3f, 20f), "Reset", false, "Undo this change, keep the record") && computer != null)
                TbsSplineEditorActions.ResetLast(computer);
        }

        float DrawDeltaField(Rect r, string label, float value, out bool changed)
        {
            changed = false;
            var lr = new Rect(r.x, r.y, r.width, 13f);
            if (ScrubLabel(lr, label, TbsIcons.Caption, value, out float scrubbed))
            {
                changed = true;
                value = scrubbed;
            }
            var fr = new Rect(r.x, r.y + 15f, r.width, 22f);
            EditorGUI.BeginChangeCheck();
            float nv = EditorGUI.FloatField(fr, value, NumField);
            if (EditorGUI.EndChangeCheck()) changed = true;
            return nv;
        }

        static float _scrubStartValue;
        static float _scrubAccum;

        static bool ScrubLabel(Rect r, string label, GUIStyle style, float value, out float newValue)
        {
            newValue = value;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            Event e = Event.current;
            EditorGUIUtility.AddCursorRect(r, MouseCursor.SlideArrow);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && r.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        GUIUtility.keyboardControl = 0;
                        _scrubStartValue = value;
                        _scrubAccum = 0f;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        float step = e.shift ? 0.003f : 0.03f;
                        _scrubAccum += e.delta.x * step;
                        float raw = _scrubStartValue + _scrubAccum;
                        if (e.control || e.command)
                        {
                            float snap = EditorSnapSettings.move.x;
                            if (snap > 0f) raw = Mathf.Round(raw / snap) * snap;
                        }
                        newValue = raw;
                        e.Use();
                        GUI.changed = true;
                        GUI.Label(r, label, style);
                        return true;
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }
            GUI.Label(r, label, style);
            return false;
        }

        void ShortcutRow(float px, float pw, ref float ry, string label, string action)
        {
            GUI.Label(new Rect(px, ry + 2f, pw - 96f, 18f), label, TbsIcons.InkLabel);
            var kr = new Rect(px + pw - 88f, ry, 88f, 20f);
            bool capturing = _scCapture == action;
            GUI.Box(kr, GUIContent.none, TbsIcons.FieldBg);
            var prev = GUI.contentColor;
            if (capturing) GUI.contentColor = TbsIcons.ColAccent;
            GUI.Label(kr, capturing ? "press…" : TbsSplineEditorState.GetShortcut(action), TbsIcons.Mono);
            GUI.contentColor = prev;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && kr.Contains(e.mousePosition))
            {
                _scCapture = capturing ? null : action;
                e.Use();
            }
            ry += 24f;
        }

        static bool IsModifierKey(KeyCode k) =>
            k == KeyCode.LeftShift || k == KeyCode.RightShift ||
            k == KeyCode.LeftControl || k == KeyCode.RightControl ||
            k == KeyCode.LeftAlt || k == KeyCode.RightAlt ||
            k == KeyCode.LeftCommand || k == KeyCode.RightCommand;

        void DrawMenu()
        {
            if (!TbsSplineEditorState.MenuOpen) return;
            var items = TbsSplineEditorState.MenuItems;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (!_menuRect.Contains(e.mousePosition))
                {
                    TbsSplineEditorState.CloseMenu();
                    e.Use();
                    return;
                }
                float ry = _menuRect.y + 4f;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].Separator) { ry += 7f; continue; }
                    var row = new Rect(_menuRect.x, ry, _menuRect.width, MenuRow);
                    if (row.Contains(e.mousePosition))
                    {
                        Action action = items[i].Action;
                        bool enabled = items[i].Enabled;
                        TbsSplineEditorState.CloseMenu();
                        if (enabled) action?.Invoke();
                        e.Use();
                        return;
                    }
                    ry += MenuRow;
                }
                e.Use();
                return;
            }
            if (e.type != EventType.Repaint && e.type != EventType.MouseMove) return;
            GUI.Box(_menuRect, GUIContent.none, TbsIcons.MenuPanel);
            Vector2 mouse = e.mousePosition;
            float y = _menuRect.y + 4f;
            for (int i = 0; i < items.Count; i++)
            {
                TbsMenuEntry item = items[i];
                if (item.Separator)
                {
                    EditorGUI.DrawRect(new Rect(_menuRect.x + 8f, y + 3f, _menuRect.width - 16f, 1f), new Color(1f, 1f, 1f, 0.08f));
                    y += 7f;
                    continue;
                }
                var row = new Rect(_menuRect.x + 3f, y, _menuRect.width - 6f, MenuRow);
                bool hover = row.Contains(mouse) && item.Enabled;
                if (hover) EditorGUI.DrawRect(row, new Color(TbsIcons.ColAccent.r, TbsIcons.ColAccent.g, TbsIcons.ColAccent.b, 0.20f));
                if (item.On) EditorGUI.DrawRect(new Rect(row.x + 2f, row.y + 4f, 3f, MenuRow - 8f), TbsIcons.ColAccent);
                if (item.Icon != null)
                {
                    Color old = GUI.color;
                    GUI.color = item.Enabled ? (item.On ? TbsIcons.ColAccent : TbsIcons.ColInk) : TbsIcons.ColInkDim;
                    GUI.DrawTexture(new Rect(row.x + 8f, y + 4f, 15f, 15f), item.Icon, ScaleMode.ScaleToFit, true);
                    GUI.color = old;
                }
                var labelStyle = TbsIcons.InkLabel;
                Color lc = item.Enabled ? TbsIcons.ColInkHi : TbsIcons.ColInkDim;
                var content = new GUIContent(item.Label);
                var prev = GUI.contentColor;
                GUI.contentColor = lc;
                GUI.Label(new Rect(row.x + 30f, y + 3f, row.width - 34f, 18f), content, labelStyle);
                GUI.contentColor = prev;
                y += MenuRow;
            }
        }

        void DrawHoverChip(TbsSplineComputer computer)
        {
            if (MouseOver || TbsSplineEditorState.DrawMode) return;
            if (!TbsSplineEditorState.HoverValid) return;
            if (TbsSplineEditorState.HoverSpline == TbsSplineEditorState.SelectedSpline) return;
            int splineIndex = TbsSplineEditorState.HoverSpline;
            if (splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            Vector2 mouse = Event.current.mousePosition;
            var rect = new Rect(mouse.x + 18f, mouse.y + 18f, 268f, 24f);
            GUI.Box(rect, GUIContent.none, TbsIcons.Panel);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 20f, 16f),
                $"Spline #{computer[splineIndex].Id} · {computer.GetLength(splineIndex):F1} m — LMB select · RMB insert knot", TbsIcons.Label);
        }

        void DrawKnotSprites(TbsSplineComputer computer, SceneView sceneView)
        {
            Camera camera = sceneView.camera;
            if (computer.EditorRenderAll)
            {
                for (int s = 0; s < computer.SplineCount; s++)
                {
                    if (s == TbsSplineEditorState.SelectedSpline || (TbsSplineEditorState.HoverValid && s == TbsSplineEditorState.HoverSpline)) continue;
                    DrawSplineKnots(computer, camera, s, 11f, 0.6f, -1);
                }
            }
            if (TbsSplineEditorState.HoverValid && TbsSplineEditorState.HoverSpline != TbsSplineEditorState.SelectedSpline)
                DrawSplineKnots(computer, camera, TbsSplineEditorState.HoverSpline, 13f, 0.8f, -1);
            if (TbsSplineEditorState.HasSplineSelection)
            {
                DrawSplineKnots(computer, camera, TbsSplineEditorState.SelectedSpline, 20f, 1f, TbsSplineEditorState.SelectedKnot);
                if (TbsSplineEditorState.HasKnotSelection) DrawSelectedKnotUI(computer, camera);
            }
        }

        void DrawSplineKnots(TbsSplineComputer computer, Camera camera, int splineIndex, float size, float alpha, int selectedKnot)
        {
            if (splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            TbsSpline spline = computer[splineIndex];
            Transform trs = computer.transform;
            Vector2 mouse = Event.current.mousePosition;
            float groundY = TbsSplineComputerTool.GroundY(computer);
            bool shadows = computer.EditorShowHeightGuides || computer.EditorRenderAll;
            for (int i = 0; i < spline.Count; i++)
            {
                Vector3 world = trs.TransformPoint(spline[i].Position);
                if (camera != null && camera.WorldToViewportPoint(world).z <= 0f) continue;
                Vector2 gui = HandleUtility.WorldToGUIPoint(world);
                if (shadows && Mathf.Abs(world.y - groundY) > 0.01f)
                {
                    Vector3 ground = new Vector3(world.x, groundY, world.z);
                    if (camera == null || camera.WorldToViewportPoint(ground).z > 0f)
                        DrawIconTinted(HandleUtility.WorldToGUIPoint(ground), 7f, TbsIcons.Knot, new Color(0f, 0f, 0f, 0.4f));
                }
                bool action = splineIndex == TbsSplineEditorState.ActionKnotSpline
                    && TbsSplineEditorState.PointMode
                    && (TbsSplineEditorState.AddSubMode == TbsAddMode.Delete || TbsSplineEditorState.AddSubMode == TbsAddMode.Merge)
                    && (i == TbsSplineEditorState.ActionKnotA || i == TbsSplineEditorState.ActionKnotB);
                if (action)
                {
                    Color hc = TbsSplineEditorState.ActionKnotColor;
                    float hs = (size + 8f) * TbsSplineEditorState.PointSize;
                    DrawIconTinted(gui, hs * 1.9f, TbsIcons.LedDotTex, new Color(hc.r, hc.g, hc.b, 0.3f));
                    DrawIconTinted(gui, hs, TbsIcons.LedDotTex, new Color(hc.r, hc.g, hc.b, 0.95f));
                    continue;
                }
                bool multi = splineIndex == TbsSplineEditorState.SelectedSpline
                    && TbsSplineEditorState.MultiKnots.Count > 1
                    && TbsSplineEditorState.MultiKnots.Contains(spline[i].Id);
                Texture2D icon;
                float drawSize = size;
                if (i == selectedKnot)
                {
                    icon = TbsIcons.KnotSelected;
                    drawSize = size + 6f;
                }
                else if (multi)
                {
                    icon = TbsIcons.KnotSelected;
                    drawSize = size + 3f;
                }
                else if ((gui - mouse).sqrMagnitude < TbsSplineComputerTool.KnotPickPixels * TbsSplineComputerTool.KnotPickPixels)
                {
                    icon = TbsIcons.KnotHover;
                    drawSize = size + 3f;
                }
                else
                {
                    icon = TbsIcons.Knot;
                }
                DrawIconCentered(gui, drawSize * TbsSplineEditorState.PointSize, icon, alpha);
            }
        }

        void DrawSelectedKnotUI(TbsSplineComputer computer, Camera camera)
        {
            TbsSpline spline = computer[TbsSplineEditorState.SelectedSpline];
            TbsKnot knot = spline[TbsSplineEditorState.SelectedKnot];
            Transform trs = computer.transform;
            Vector3 world = trs.TransformPoint(knot.Position);
            if (camera != null && camera.WorldToViewportPoint(world).z <= 0f) return;
            float handleSize = HandleUtility.GetHandleSize(world);
            Vector3 tangentLocal = knot.Rotation * knot.TangentOut;
            Vector3 axis = tangentLocal.sqrMagnitude > TbsSplineMath.Epsilon
                ? trs.TransformDirection(tangentLocal).normalized
                : trs.TransformDirection(knot.Rotation * Vector3.forward).normalized;
            if (TbsSplineEditorState.RotateMode)
            {
                Vector3 upDirection = TbsSplineMath.OrthonormalUp(axis, trs.TransformDirection(knot.Up));
                Vector3 grip = world + upDirection * (handleSize * 0.8f);
                if (camera == null || camera.WorldToViewportPoint(grip).z > 0f)
                    DrawIconCentered(HandleUtility.WorldToGUIPoint(grip), 14f, TbsIcons.KnotSelected, 1f);
            }
            if (TbsTangentModeView.ShowHandles(knot.Mode))
            {
                DrawTangentIcon(computer, trs, camera, knot.TangentInPosition);
                DrawTangentIcon(computer, trs, camera, knot.TangentOutPosition);
            }
            if (!TbsSplineEditorState.DragLabelValid)
            {
                int index = TbsSplineEditorState.SelectedKnot;
                float groundY = TbsSplineComputerTool.GroundY(computer);
                string text = $"{index + 1}    y {world.y - groundY:F2}";
                float w = Mathf.Max(18f, 10f + text.Length * 7f);
                DrawBadge(HandleUtility.WorldToGUIPoint(world) + new Vector2(15f + w * 0.5f, -13f), text);
            }
        }

        void DrawJunctions(TbsSplineComputer computer, SceneView sceneView)
        {
            Camera camera = sceneView.camera;
            var junctions = computer.Junctions;
            for (int i = 0; i < junctions.Count; i++)
            {
                TbsJunction junction = junctions[i];
                if (junction.Count == 0) continue;
                Vector3 world = computer.GetKnotWorld(junction.Members[0]);
                if (camera != null && camera.WorldToViewportPoint(world).z <= 0f) continue;
                float size = junction.Count >= 3 ? 26f : 20f;
                DrawIconCentered(HandleUtility.WorldToGUIPoint(world), size, TbsIcons.Junction, 1f);
            }
        }

        void DrawLabels(TbsSplineComputer computer, SceneView sceneView)
        {
            Camera camera = sceneView.camera;
            if (computer.EditorRenderAll)
            {
                for (int s = 0; s < computer.SplineCount; s++) DrawSplineLabels(computer, camera, s);
            }
            else if (TbsSplineEditorState.HasSplineSelection)
            {
                DrawSplineLabels(computer, camera, TbsSplineEditorState.SelectedSpline);
            }
        }

        void DrawSplineLabels(TbsSplineComputer computer, Camera camera, int splineIndex)
        {
            if (splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            TbsSpline spline = computer[splineIndex];
            Transform trs = computer.transform;
            bool selectedSpline = splineIndex == TbsSplineEditorState.SelectedSpline;
            for (int i = 0; i < spline.Count; i++)
            {
                if (selectedSpline && i == TbsSplineEditorState.SelectedKnot) continue;
                Vector3 world = trs.TransformPoint(spline[i].Position);
                if (camera != null && camera.WorldToViewportPoint(world).z <= 0f) continue;
                Vector2 gui = HandleUtility.WorldToGUIPoint(world);
                DrawNumberBadge(gui + new Vector2(13f, -13f), i + 1);
            }
        }

        static void DrawNumberBadge(Vector2 center, int number) => DrawBadge(center, number.ToString());

        static void DrawBadge(Vector2 center, string text)
        {
            float w = Mathf.Max(18f, 10f + text.Length * 7f);
            var rect = new Rect(center.x - w * 0.5f, center.y - 9f, w, 18f);
            EditorGUI.DrawRect(rect, new Color(0.09f, 0.11f, 0.15f, 0.92f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(1f, 0.72f, 0.3f, 0.5f));
            var prev = GUI.contentColor;
            GUI.contentColor = new Color(1f, 0.85f, 0.55f);
            GUI.Label(rect, text, CenteredMini);
            GUI.contentColor = prev;
        }

        static GUIStyle _centeredMini;
        static GUIStyle CenteredMini
        {
            get
            {
                if (_centeredMini == null)
                {
                    _centeredMini = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
                    _centeredMini.normal.textColor = Color.white;
                }
                return _centeredMini;
            }
        }

        void DrawHoverBadge(TbsSplineComputer computer, SceneView sceneView)
        {
            if (TbsSplineEditorState.DrawMode || _panelVisible && _panelRect.Contains(Event.current.mousePosition)) return;
            if (!TbsSplineEditorState.HoverValid) return;
            int splineIndex = TbsSplineEditorState.HoverSpline;
            if (splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            TbsSample sample = default;
            computer.GetCache(splineIndex).EvaluateSegment(TbsSplineEditorState.HoverSegment, TbsSplineEditorState.HoverT, ref sample);
            Camera camera = sceneView.camera;
            Vector3 world = computer.transform.TransformPoint(sample.Position);
            if (camera != null && camera.WorldToViewportPoint(world).z <= 0f) return;
            DrawBadge(HandleUtility.WorldToGUIPoint(world) + new Vector2(0f, 20f), $"d {sample.Distance:F1}");
        }

        void DrawHelp(SceneView sceneView)
        {
            string move = TbsSplineEditorState.GetShortcut("Move");
            string rotate = TbsSplineEditorState.GetShortcut("Rotate");
            string scale = TbsSplineEditorState.GetShortcut("Scale");
            string add = TbsSplineEditorState.GetShortcut("Add");
            string draw = TbsSplineEditorState.GetShortcut("New");
            string mode = TbsSplineEditorState.GetShortcut("Mode");
            string toggle;
            try { toggle = UnityEditor.ShortcutManagement.ShortcutManager.instance.GetShortcutBinding("TBSplineS/Toggle Spline Editor").ToString(); }
            catch (System.Exception) { toggle = "Alt+E"; }
            string[] lines =
            {
                "TBSplineS — shortcuts",
                $"Edit tools: Move ({move}) · Rotate ({rotate}) · Scale ({scale}) · Add ({add})",
                $"Object tools: Move ({move}) · Rotate ({rotate}) · New spline ({draw})",
                $"{mode} — Edit/Object mode · {toggle} — enter/exit editor",
                "Click — select point · Shift+Click — multi-select · drag empty — box select",
                "Ctrl+Click point — delete · Ctrl+A — select all · Delete — delete selection",
                "RMB curve — insert point / menu · RMB point or scene — context menu",
                "Add: Shift+Scroll — cycle End/Start/Insert · drag endpoint onto spline — connect",
                "F — frame selection · Esc — cancel drag / back · Ctrl — invert grid snap"
            };
            float width = 402f;
            for (int i = 1; i < lines.Length; i++)
                width = Mathf.Max(width, TbsIcons.Label.CalcSize(new GUIContent(lines[i])).x + 28f);
            float height = 16f + lines.Length * 17f;
            var rect = new Rect(12f, _topBar.yMax + 8f, width, height);
            GUI.Box(rect, GUIContent.none, TbsIcons.Panel);
            for (int i = 0; i < lines.Length; i++)
                GUI.Label(new Rect(rect.x + 12f, rect.y + 8f + i * 17f, width - 24f, 15f), lines[i], i == 0 ? TbsIcons.Title : TbsIcons.Label);
        }

        void DrawTangentIcon(TbsSplineComputer computer, Transform trs, Camera camera, Vector3 localPosition)
        {
            Vector3 world = trs.TransformPoint(localPosition);
            if (computer.EditorShowHeightGuides || computer.EditorRenderAll)
            {
                float gy = TbsSplineComputerTool.GroundY(computer);
                if (Mathf.Abs(world.y - gy) > 0.01f)
                {
                    Vector3 ground = new Vector3(world.x, gy, world.z);
                    if (camera == null || camera.WorldToViewportPoint(ground).z > 0f)
                        DrawIconTinted(HandleUtility.WorldToGUIPoint(ground), 6f, TbsIcons.Tangent, new Color(0f, 0f, 0f, 0.4f));
                }
            }
            if (camera != null && camera.WorldToViewportPoint(world).z <= 0f) return;
            DrawIconCentered(HandleUtility.WorldToGUIPoint(world), 15f, TbsIcons.Tangent, 1f);
        }

        void DrawGhost(SceneView sceneView)
        {
            if (!TbsSplineEditorState.GhostValid) return;
            Camera camera = sceneView.camera;
            if (camera != null && camera.WorldToViewportPoint(TbsSplineEditorState.GhostPoint).z <= 0f) return;
            Vector2 gui = HandleUtility.WorldToGUIPoint(TbsSplineEditorState.GhostPoint);
            DrawIconCentered(gui, 20f, TbsIcons.KnotHover, 0.9f);
            DrawIconCentered(gui, 11f, TbsIcons.GlyphPlus, 0.9f);
        }

        static void DrawIcon(Rect rect, Texture2D icon, Color color)
        {
            if (icon == null) return;
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        static void DrawIconCentered(Vector2 center, float size, Texture2D icon, float alpha)
        {
            DrawIconTinted(center, size, icon, new Color(1f, 1f, 1f, alpha));
        }

        static void DrawIconTinted(Vector2 center, float size, Texture2D icon, Color color)
        {
            if (icon == null) return;
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), icon, ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        void DrawDragInfo(SceneView sceneView)
        {
            if (!TbsSplineEditorState.DragInfoValid) return;
            Vector3 o = TbsSplineEditorState.DragInfoOrigin;
            Vector3 c = TbsSplineEditorState.DragInfoCurrent;
            Vector3 d = c - o;
            string text = $"Δ    x {d.x:+0.00;-0.00}     y {d.y:+0.00;-0.00}     z {d.z:+0.00;-0.00}          →    ({c.x:0.00},  {c.y:0.00},  {c.z:0.00})";
            float w = 560f;
            float h = 26f;
            var rect = new Rect((sceneView.position.width - w) * 0.5f, sceneView.position.height - h - 54f, w, h);
            GUI.Box(rect, GUIContent.none, TbsIcons.Panel);
            var prev = GUI.contentColor;
            GUI.contentColor = new Color(1f, 0.9f, 0.7f);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 5f, w - 28f, 16f), text, CenteredMini);
            GUI.contentColor = prev;
        }

        void DrawMarquee()
        {
            if (!TbsSplineEditorState.MarqueeActive) return;
            Rect r = TbsSplineEditorState.MarqueeRect;
            EditorGUI.DrawRect(r, new Color(1f, 0.72f, 0.3f, 0.1f));
            var b = new Color(1f, 0.72f, 0.3f, 0.75f);
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), b);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), b);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), b);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), b);
        }

        void DrawChip(Vector2 position, string text)
        {
            float width = 26f + text.Length * 6.4f;
            var rect = new Rect(position.x, position.y, width, 20f);
            GUI.Box(rect, GUIContent.none, TbsIcons.Panel);
            GUI.Label(new Rect(rect.x + 9f, rect.y + 2f, rect.width - 18f, 16f), text, TbsIcons.Label);
        }

    }
}
