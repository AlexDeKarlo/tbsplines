using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using TBSplineS;

namespace TBSplineS.Editor
{
    public enum TbsPlacementMode
    {
        PlaneXZ,
        Collider
    }

    public enum TbsTool
    {
        Select,
        Move,
        Rotate,
        Scale,
        Point,
        Draw
    }

    public enum TbsPrimitiveKind
    {
        Circle,
        Ngon,
        Star,
        Rectangle,
        Spiral
    }

    [System.Serializable]
    public sealed class TbsSplinePreset
    {
        public TbsSplineType Type;
        public bool Closed;
        public float Parametrization;
        public System.Collections.Generic.List<TBSplineS.TbsKnot> Knots =
            new System.Collections.Generic.List<TBSplineS.TbsKnot>();
    }

    public enum TbsEditorMode
    {
        Edit,
        Object
    }

    public enum TbsLastOp
    {
        None,
        Move,
        MoveHandle,
        MoveSpline,
        Scale,
        Add,
        Rotate,
        RotateSpline
    }

    public enum TbsPivotMode
    {
        Cursor,
        Median
    }

    public enum TbsAddMode
    {
        End,
        Start,
        Insert,
        Delete,
        Merge
    }

    public enum TbsHeightAlign
    {
        First,
        Last,
        Average
    }

    public sealed class TbsMenuEntry
    {
        public Texture2D Icon;
        public string Label;
        public Action Action;
        public bool Separator;
        public bool On;
        public bool Enabled = true;
    }

    public static class TbsSplineEditorState
    {
        const string PlacementKey = "TBSplineS.PlacementMode";

        static bool _placementLoaded;
        static TbsPlacementMode _placement;

        public static event Action Changed;

        public static TbsSplineComputer ActiveComputer { get; private set; }

        public static int SelectedSpline { get; private set; } = -1;

        public static int SelectedKnot { get; private set; } = -1;

        public static int SelectedSplineId { get; private set; } = -1;

        public static int SelectedKnotId { get; private set; } = -1;

        public static int SelectedHandle;

        public static bool HasHandleSelection => HasKnotSelection && SelectedHandle != 0;

        public static bool HoverValid;
        public static int HoverSpline = -1;
        public static int HoverSegment = -1;
        public static float HoverT;
        public static Vector3 HoverPoint;

        public static TbsTool ActiveTool = TbsTool.Select;
        public static int DrawSpline = -1;

        public static bool DrawMode
        {
            get => ActiveTool == TbsTool.Draw;
            set
            {
                if (value) ActiveTool = TbsTool.Draw;
                else if (ActiveTool == TbsTool.Draw) ActiveTool = TbsTool.Select;
            }
        }

        public static bool MoveMode => ActiveTool == TbsTool.Move;
        public static bool RotateMode => ActiveTool == TbsTool.Rotate;
        public static bool ScaleMode => ActiveTool == TbsTool.Scale;
        public static bool PointMode => ActiveTool == TbsTool.Point;

        const string ModeKey = "TBSplineS.EditorMode";
        static int _mode = -1;

        public static TbsEditorMode ActiveMode
        {
            get { if (_mode < 0) _mode = Mathf.Clamp(EditorPrefs.GetInt(ModeKey, 0), 0, 1); return (TbsEditorMode)_mode; }
            set { _mode = (int)value; EditorPrefs.SetInt(ModeKey, _mode); Changed?.Invoke(); }
        }

        public static bool EditModeActive => ActiveMode == TbsEditorMode.Edit;
        public static bool ObjectModeActive => ActiveMode == TbsEditorMode.Object;

        public static bool ToolValidInMode(TbsTool tool, TbsEditorMode mode)
        {
            if (mode == TbsEditorMode.Edit)
                return tool == TbsTool.Move || tool == TbsTool.Rotate || tool == TbsTool.Scale || tool == TbsTool.Point;
            return tool == TbsTool.Move || tool == TbsTool.Rotate || tool == TbsTool.Draw;
        }

        public static bool GhostValid;
        public static Vector3 GhostPoint;
        public static Vector3 GhostAnchor;

        public static int ActionKnotSpline = -1;
        public static int ActionKnotA = -1;
        public static int ActionKnotB = -1;
        public static Color ActionKnotColor = Color.white;

        public static void SetActionKnots(int splineIndex, Color color, int a, int b)
        {
            ActionKnotSpline = splineIndex;
            ActionKnotColor = color;
            ActionKnotA = a;
            ActionKnotB = b;
        }

        public static void ClearActionKnots()
        {
            ActionKnotSpline = -1;
            ActionKnotA = -1;
            ActionKnotB = -1;
        }

        public static bool DragLabelValid;
        public static string DragLabel = string.Empty;
        public static Vector3 DragLabelWorld;

        public static readonly HashSet<int> MultiKnots = new HashSet<int>();

        public static readonly HashSet<int> SelectedSplineIds = new HashSet<int>();
        public static bool ObjectCursorValid;
        public static Vector3 ObjectCursor;

        public static void SetObjectCursor(Vector3 world) { ObjectCursor = world; ObjectCursorValid = true; }
        public static void ClearObjectCursor() { ObjectCursorValid = false; }

        public static void ToggleSplineInSelection(int splineIndex)
        {
            if (ActiveComputer == null || splineIndex < 0 || splineIndex >= ActiveComputer.SplineCount) return;
            int id = ActiveComputer[splineIndex].Id;
            SelectedKnot = -1;
            SelectedKnotId = -1;
            MultiKnots.Clear();
            if (SelectedSplineIds.Contains(id))
            {
                if (SelectedSplineIds.Count > 1)
                {
                    SelectedSplineIds.Remove(id);
                    if (SelectedSpline == splineIndex)
                        foreach (int other in SelectedSplineIds) { SelectedSpline = ActiveComputer.IndexOfSplineId(other); break; }
                }
            }
            else
            {
                SelectedSplineIds.Add(id);
                SelectedSpline = splineIndex;
            }
            SelectedSplineId = SelectedSpline >= 0 && SelectedSpline < ActiveComputer.SplineCount
                ? ActiveComputer[SelectedSpline].Id
                : -1;
            Changed?.Invoke();
        }

        public static bool ConnectTargetValid;
        public static int ConnectTargetSpline = -1;
        public static int ConnectTargetKnot = -1;
        public static int ConnectTargetSegment = -1;
        public static float ConnectTargetT;
        public static Vector3 ConnectTargetWorld;

        const string GridKey = "TBSplineS.ShowGrid";
        const string LabelsKey = "TBSplineS.ShowLabels";
        const string SnapKey = "TBSplineS.SnapToGrid";
        static int _showGrid = -1;
        static int _showLabels = -1;
        static int _snap = -1;
        public static bool HelpVisible;

        public static bool SnapToGrid
        {
            get { if (_snap < 0) _snap = EditorPrefs.GetBool(SnapKey, false) ? 1 : 0; return _snap == 1; }
            set { _snap = value ? 1 : 0; EditorPrefs.SetBool(SnapKey, value); Changed?.Invoke(); }
        }

        public static bool ShowGrid
        {
            get { if (_showGrid < 0) _showGrid = EditorPrefs.GetBool(GridKey, false) ? 1 : 0; return _showGrid == 1; }
            set { _showGrid = value ? 1 : 0; EditorPrefs.SetBool(GridKey, value); Changed?.Invoke(); }
        }

        public static bool ShowLabels
        {
            get { if (_showLabels < 0) _showLabels = EditorPrefs.GetBool(LabelsKey, false) ? 1 : 0; return _showLabels == 1; }
            set { _showLabels = value ? 1 : 0; EditorPrefs.SetBool(LabelsKey, value); Changed?.Invoke(); }
        }

        public static bool HasMultiSelection => HasSplineSelection && MultiKnots.Count > 1;

        public static bool MarqueeActive;
        public static Rect MarqueeRect;

        public static bool DragInfoValid;
        public static Vector3 DragInfoOrigin;
        public static Vector3 DragInfoCurrent;

        public static bool LastActionValid;
        public static TbsLastOp LastOp = TbsLastOp.None;
        public static string LastOpLabel = "";
        public static Vector3 LastDelta;
        public static Vector3 LastRotEuler;
        public static Vector3 LastPivot;
        public static int LastSpline = -1;
        public static int LastHandleSide;
        public static readonly List<int> LastKnotIds = new List<int>();
        public static Vector3 LastScale = Vector3.one;
        public static Vector3 LastScaleCenterLocal;
        public static readonly List<(int id, Vector3 pos, Vector3 tin, Vector3 tout)> LastScaleBase =
            new List<(int id, Vector3 pos, Vector3 tin, Vector3 tout)>();

        public static bool LastIsRotation => LastOp == TbsLastOp.Rotate || LastOp == TbsLastOp.RotateSpline;

        const string PivotKey = "TBSplineS.Pivot";
        const string OrientKey = "TBSplineS.OrientGlobal";
        const string AddModeKey = "TBSplineS.AddMode";
        static int _pivot = -1;
        static int _orient = -1;
        static int _addSub = -1;

        public static TbsPivotMode PivotMode
        {
            get { if (_pivot < 0) _pivot = Mathf.Clamp(EditorPrefs.GetInt(PivotKey, 0), 0, 1); return (TbsPivotMode)_pivot; }
            set { _pivot = (int)value; EditorPrefs.SetInt(PivotKey, _pivot); Changed?.Invoke(); }
        }

        public static bool OrientGlobal
        {
            get { if (_orient < 0) _orient = EditorPrefs.GetBool(OrientKey, true) ? 1 : 0; return _orient == 1; }
            set { _orient = value ? 1 : 0; EditorPrefs.SetBool(OrientKey, value); Changed?.Invoke(); }
        }

        public static TbsAddMode AddSubMode
        {
            get { if (_addSub < 0) _addSub = Mathf.Clamp(EditorPrefs.GetInt(AddModeKey, 0), 0, 4); return (TbsAddMode)_addSub; }
            set { _addSub = (int)value; EditorPrefs.SetInt(AddModeKey, _addSub); Changed?.Invoke(); }
        }

        static float _pointSize = -1f, _handleSize = -1f, _lineWidth = -1f, _previewWidth = -1f;
        public static float PointSize { get { if (_pointSize < 0f) _pointSize = EditorPrefs.GetFloat("TBSplineS.PointSize", 1f); return _pointSize; } set { _pointSize = Mathf.Clamp(value, 0.3f, 4f); EditorPrefs.SetFloat("TBSplineS.PointSize", _pointSize); Changed?.Invoke(); } }
        public static float HandleSize { get { if (_handleSize < 0f) _handleSize = EditorPrefs.GetFloat("TBSplineS.HandleSize", 2.5f); return _handleSize; } set { _handleSize = Mathf.Clamp(value, 0.3f, 4f); EditorPrefs.SetFloat("TBSplineS.HandleSize", _handleSize); Changed?.Invoke(); } }
        public static float LineWidth { get { if (_lineWidth < 0f) _lineWidth = EditorPrefs.GetFloat("TBSplineS.LineWidth", 1f); return _lineWidth; } set { _lineWidth = Mathf.Clamp(value, 0.5f, 4f); EditorPrefs.SetFloat("TBSplineS.LineWidth", _lineWidth); Changed?.Invoke(); } }
        public static float PreviewLineWidth { get { if (_previewWidth < 0f) _previewWidth = EditorPrefs.GetFloat("TBSplineS.PreviewWidth", 3f); return _previewWidth; } set { _previewWidth = Mathf.Clamp(value, 1f, 6f); EditorPrefs.SetFloat("TBSplineS.PreviewWidth", _previewWidth); Changed?.Invoke(); } }

        public static Color PreviewLineColor
        {
            get { return ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("TBSplineS.PreviewColor", "6BA1FAD9"), out Color c) ? c : new Color(0.42f, 0.63f, 0.98f, 0.85f); }
            set { EditorPrefs.SetString("TBSplineS.PreviewColor", ColorUtility.ToHtmlStringRGBA(value)); Changed?.Invoke(); }
        }

        public static Color IdleCurveColor
        {
            get { return ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("TBSplineS.IdleColor", "7A93B8FF"), out Color c) ? c : new Color(0.48f, 0.58f, 0.72f); }
            set { EditorPrefs.SetString("TBSplineS.IdleColor", ColorUtility.ToHtmlStringRGBA(value)); Changed?.Invoke(); }
        }

        public static Color SelectedCurveColor
        {
            get { return ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("TBSplineS.SelColor", "4C8FF0FF"), out Color c) ? c : new Color(0.30f, 0.56f, 0.94f); }
            set { EditorPrefs.SetString("TBSplineS.SelColor", ColorUtility.ToHtmlStringRGBA(value)); Changed?.Invoke(); }
        }

        public static Color HoverCurveColor
        {
            get { return ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString("TBSplineS.HovColor", "6BA1FAE6"), out Color c) ? c : new Color(0.42f, 0.63f, 0.98f, 0.9f); }
            set { EditorPrefs.SetString("TBSplineS.HovColor", ColorUtility.ToHtmlStringRGBA(value)); Changed?.Invoke(); }
        }

        public static void RecordLast(TbsLastOp op, string label, Vector3 worldDelta, int splineIndex, List<int> knotIds)
        {
            LastOp = op;
            LastOpLabel = label;
            LastDelta = worldDelta;
            LastRotEuler = Vector3.zero;
            LastSpline = splineIndex;
            LastKnotIds.Clear();
            if (knotIds != null) LastKnotIds.AddRange(knotIds);
            LastActionValid = true;
            Changed?.Invoke();
        }

        public static void RecordLastRotation(TbsLastOp op, string label, Vector3 euler, int splineIndex, List<int> knotIds)
        {
            LastOp = op;
            LastOpLabel = label;
            LastRotEuler = euler;
            LastDelta = Vector3.zero;
            LastSpline = splineIndex;
            LastKnotIds.Clear();
            if (knotIds != null) LastKnotIds.AddRange(knotIds);
            LastActionValid = true;
            Changed?.Invoke();
        }

        public static void RecordLastScale(string label, Vector3 scale, Vector3 centerLocal, int splineIndex,
            List<(int id, Vector3 pos, Vector3 tin, Vector3 tout)> baseKnots)
        {
            LastOp = TbsLastOp.Scale;
            LastOpLabel = label;
            LastDelta = Vector3.zero;
            LastRotEuler = Vector3.zero;
            LastScale = scale;
            LastScaleCenterLocal = centerLocal;
            LastSpline = splineIndex;
            LastKnotIds.Clear();
            LastScaleBase.Clear();
            if (baseKnots != null)
            {
                LastScaleBase.AddRange(baseKnots);
                for (int i = 0; i < baseKnots.Count; i++) LastKnotIds.Add(baseKnots[i].id);
            }
            LastActionValid = true;
            Changed?.Invoke();
        }

        public static void InvalidateLast()
        {
            LastActionValid = false;
            LastOp = TbsLastOp.None;
            LastKnotIds.Clear();
            LastScaleBase.Clear();
            LastScale = Vector3.one;
        }

        const int ShortcutSchema = 2;

        [InitializeOnLoadMethod]
        static void MigrateShortcuts()
        {
            if (EditorPrefs.GetInt("TBSplineS.Sc.Schema", 1) >= ShortcutSchema) return;
            foreach (string action in new[] { "Move", "Rotate", "Scale" })
                EditorPrefs.DeleteKey("TBSplineS.Sc." + action);
            EditorPrefs.SetInt("TBSplineS.Sc.Schema", ShortcutSchema);
        }

        public static string GetShortcut(string action)
        {
            string def;
            switch (action)
            {
                case "Move": def = "Ctrl+G"; break;
                case "Rotate": def = "Ctrl+R"; break;
                case "Scale": def = "Ctrl+E"; break;
                case "Add": def = "Shift+A"; break;
                case "New": def = "Shift+A"; break;
                case "Mode": def = "Tab"; break;
                default: def = ""; break;
            }
            return EditorPrefs.GetString("TBSplineS.Sc." + action, def);
        }

        public static void SetShortcut(string action, string value)
        {
            EditorPrefs.SetString("TBSplineS.Sc." + action, value);
            Changed?.Invoke();
        }

        public static bool ShortcutMatches(string action, Event e)
        {
            string b = GetShortcut(action);
            if (string.IsNullOrEmpty(b)) return false;
            bool shift = b.IndexOf("shift", StringComparison.OrdinalIgnoreCase) >= 0;
            bool ctrl = b.IndexOf("ctrl", StringComparison.OrdinalIgnoreCase) >= 0;
            int plus = b.LastIndexOf('+');
            string keyPart = (plus >= 0 ? b.Substring(plus + 1) : b).Trim();
            if (keyPart.Length == 1) keyPart = keyPart.ToUpperInvariant();
            if (!Enum.TryParse(keyPart, true, out KeyCode key)) return false;
            bool eCtrl = e.control || e.command;
            return e.keyCode == key && e.shift == shift && eCtrl == ctrl;
        }

        public static bool MenuOpen;
        public static Vector2 MenuPosition;
        public static readonly List<TbsMenuEntry> MenuItems = new List<TbsMenuEntry>();

        public static void OpenMenu(Vector2 position)
        {
            MenuOpen = true;
            MenuPosition = position;
            MenuItems.Clear();
            SceneView.RepaintAll();
        }

        public static void CloseMenu()
        {
            if (!MenuOpen && MenuItems.Count == 0) return;
            MenuOpen = false;
            MenuItems.Clear();
            SceneView.RepaintAll();
        }

        public static void AddMenuItem(Texture2D icon, string label, Action action, bool on = false, bool enabled = true)
        {
            MenuItems.Add(new TbsMenuEntry { Icon = icon, Label = label, Action = action, On = on, Enabled = enabled });
        }

        public static void AddMenuSeparator()
        {
            MenuItems.Add(new TbsMenuEntry { Separator = true });
        }

        public static TbsPlacementMode Placement
        {
            get
            {
                if (!_placementLoaded)
                {
                    _placement = (TbsPlacementMode)EditorPrefs.GetInt(PlacementKey, 0);
                    _placementLoaded = true;
                }
                return _placement;
            }
            set
            {
                if (Placement == value) return;
                _placement = value;
                EditorPrefs.SetInt(PlacementKey, (int)value);
                Changed?.Invoke();
            }
        }

        public static bool HasSplineSelection =>
            ActiveComputer != null && SelectedSpline >= 0 && SelectedSpline < ActiveComputer.SplineCount;

        public static bool HasKnotSelection =>
            HasSplineSelection && SelectedKnot >= 0 && SelectedKnot < ActiveComputer[SelectedSpline].Count;

        public static void SetComputer(TbsSplineComputer computer)
        {
            if (ActiveComputer == computer) return;
            ActiveComputer = computer;
            SelectedSpline = -1;
            SelectedKnot = -1;
            SelectedSplineId = -1;
            SelectedKnotId = -1;
            MultiKnots.Clear();
            SelectedSplineIds.Clear();
            ActiveTool = TbsTool.Select;
            DrawSpline = -1;
            GhostValid = false;
            ClearHover();
            Changed?.Invoke();
        }

        public static void SelectSpline(int splineIndex)
        {
            SelectedSpline = splineIndex;
            SelectedKnot = -1;
            SelectedKnotId = -1;
            SelectedHandle = 0;
            MultiKnots.Clear();
            SelectedSplineIds.Clear();
            SelectedSplineId = -1;
            if (ActiveComputer != null && splineIndex >= 0 && splineIndex < ActiveComputer.SplineCount)
            {
                SelectedSplineId = ActiveComputer[splineIndex].Id;
                SelectedSplineIds.Add(SelectedSplineId);
            }
            Changed?.Invoke();
        }

        public static void SelectKnot(int splineIndex, int knotIndex)
        {
            SelectedSpline = splineIndex;
            SelectedKnot = knotIndex;
            SelectedHandle = 0;
            MultiKnots.Clear();
            SelectedSplineId = -1;
            SelectedKnotId = -1;
            if (ActiveComputer != null && splineIndex >= 0 && splineIndex < ActiveComputer.SplineCount &&
                knotIndex >= 0 && knotIndex < ActiveComputer[splineIndex].Count)
            {
                TbsSpline spline = ActiveComputer[splineIndex];
                SelectedSplineId = spline.Id;
                SelectedKnotId = spline[knotIndex].Id;
                MultiKnots.Add(SelectedKnotId);
            }
            Changed?.Invoke();
        }

        public static void SelectHandle(int splineIndex, int knotIndex, int side)
        {
            SelectedSpline = splineIndex;
            SelectedKnot = knotIndex;
            MultiKnots.Clear();
            SelectedSplineId = -1;
            SelectedKnotId = -1;
            if (ActiveComputer != null && splineIndex >= 0 && splineIndex < ActiveComputer.SplineCount &&
                knotIndex >= 0 && knotIndex < ActiveComputer[splineIndex].Count)
            {
                TbsSpline spline = ActiveComputer[splineIndex];
                SelectedSplineId = spline.Id;
                SelectedKnotId = spline[knotIndex].Id;
                MultiKnots.Add(SelectedKnotId);
            }
            SelectedHandle = side;
            Changed?.Invoke();
        }

        public static void ToggleKnotInSelection(int splineIndex, int knotIndex)
        {
            if (ActiveComputer == null || splineIndex < 0 || splineIndex >= ActiveComputer.SplineCount) return;
            if (SelectedSpline != splineIndex) { SelectKnot(splineIndex, knotIndex); return; }
            TbsSpline spline = ActiveComputer[splineIndex];
            int id = spline[knotIndex].Id;
            int primaryId = SelectedKnot >= 0 && SelectedKnot < spline.Count ? spline[SelectedKnot].Id : -1;
            if (MultiKnots.Remove(id))
            {
                if (id == primaryId)
                    SelectedKnot = MultiKnots.Count > 0 ? spline.IndexOfKnotId(SmallestMulti()) : -1;
            }
            else
            {
                MultiKnots.Add(id);
                SelectedKnot = knotIndex;
            }
            SelectedSplineId = spline.Id;
            SelectedKnotId = SelectedKnot >= 0 && SelectedKnot < spline.Count ? spline[SelectedKnot].Id : -1;
            Changed?.Invoke();
        }

        static int SmallestMulti()
        {
            int min = int.MaxValue;
            foreach (int id in MultiKnots) if (id < min) min = id;
            return min == int.MaxValue ? -1 : min;
        }

        public static void SelectAllKnots()
        {
            if (!HasSplineSelection) return;
            MultiKnots.Clear();
            TbsSpline spline = ActiveComputer[SelectedSpline];
            for (int i = 0; i < spline.Count; i++) MultiKnots.Add(spline[i].Id);
            if (spline.Count > 0 && SelectedKnot < 0) SelectedKnot = 0;
            SelectedSplineId = spline.Id;
            SelectedKnotId = SelectedKnot >= 0 && SelectedKnot < spline.Count ? spline[SelectedKnot].Id : -1;
            Changed?.Invoke();
        }

        public static void SetPrimaryKnotKeepMulti(int splineIndex, int knotIndex)
        {
            SelectedSpline = splineIndex;
            SelectedKnot = knotIndex;
            if (ActiveComputer != null && splineIndex >= 0 && splineIndex < ActiveComputer.SplineCount)
            {
                TbsSpline spline = ActiveComputer[splineIndex];
                SelectedSplineId = spline.Id;
                SelectedKnotId = knotIndex >= 0 && knotIndex < spline.Count ? spline[knotIndex].Id : -1;
            }
            else
            {
                SelectedSplineId = -1;
                SelectedKnotId = -1;
            }
            Changed?.Invoke();
        }

        public static void SelectNearKnots(int splineIndex, int knotIndex)
        {
            if (ActiveComputer == null || splineIndex < 0 || splineIndex >= ActiveComputer.SplineCount) return;
            TbsSpline spline = ActiveComputer[splineIndex];
            if (knotIndex < 0 || knotIndex >= spline.Count) return;
            int n = spline.Count;
            var ids = new List<int>();
            for (int off = -1; off <= 1; off++)
            {
                int idx = knotIndex + off;
                if (spline.Closed) idx = ((idx % n) + n) % n;
                if (idx < 0 || idx >= n) continue;
                int id = spline[idx].Id;
                if (!ids.Contains(id)) ids.Add(id);
            }
            SetMultiSelection(splineIndex, ids);
        }

        public static void SelectSegmentKnots(int splineIndex, int segment)
        {
            if (ActiveComputer == null || splineIndex < 0 || splineIndex >= ActiveComputer.SplineCount) return;
            TbsSpline spline = ActiveComputer[splineIndex];
            int a = segment;
            int b = segment + 1;
            if (spline.Closed && b >= spline.Count) b = 0;
            var ids = new List<int>();
            if (a >= 0 && a < spline.Count) ids.Add(spline[a].Id);
            if (b >= 0 && b < spline.Count && !ids.Contains(spline[b].Id)) ids.Add(spline[b].Id);
            if (ids.Count > 0) SetMultiSelection(splineIndex, ids);
        }

        public static void SetMultiSelection(int splineIndex, List<int> knotIds)
        {
            SelectedSpline = splineIndex;
            MultiKnots.Clear();
            for (int i = 0; i < knotIds.Count; i++) MultiKnots.Add(knotIds[i]);
            bool validSpline = ActiveComputer != null && splineIndex >= 0 && splineIndex < ActiveComputer.SplineCount;
            SelectedKnot = MultiKnots.Count > 0 && validSpline
                ? ActiveComputer[splineIndex].IndexOfKnotId(SmallestMulti())
                : -1;
            SelectedSplineId = validSpline ? ActiveComputer[splineIndex].Id : -1;
            SelectedKnotId = validSpline && SelectedKnot >= 0 && SelectedKnot < ActiveComputer[splineIndex].Count
                ? ActiveComputer[splineIndex][SelectedKnot].Id
                : -1;
            Changed?.Invoke();
        }

        public static void ClearKnot()
        {
            if (SelectedKnot < 0 && MultiKnots.Count == 0) return;
            SelectedKnot = -1;
            SelectedKnotId = -1;
            SelectedHandle = 0;
            MultiKnots.Clear();
            Changed?.Invoke();
        }

        public static void ClearSelection()
        {
            if (SelectedSpline < 0 && SelectedKnot < 0 && MultiKnots.Count == 0 && SelectedSplineIds.Count == 0) return;
            SelectedSpline = -1;
            SelectedKnot = -1;
            SelectedSplineId = -1;
            SelectedKnotId = -1;
            SelectedHandle = 0;
            MultiKnots.Clear();
            SelectedSplineIds.Clear();
            ObjectCursorValid = false;
            Changed?.Invoke();
        }

        public static void RevalidateSelection()
        {
            TbsSplineComputer computer = ActiveComputer;
            if (computer == null)
            {
                ClearSelection();
                return;
            }
            int splineIndex = SelectedSplineId > 0 ? computer.IndexOfSplineId(SelectedSplineId) : -1;
            if (splineIndex < 0 && SelectedSpline >= 0 && SelectedSpline < computer.SplineCount)
                splineIndex = SelectedSpline;
            if (splineIndex < 0)
            {
                ClearSelection();
                return;
            }
            TbsSpline spline = computer[splineIndex];
            SelectedSpline = splineIndex;
            SelectedSplineId = spline.Id;
            SelectedSplineIds.RemoveWhere(id => computer.IndexOfSplineId(id) < 0);
            if (SelectedSplineIds.Count == 0) SelectedSplineIds.Add(spline.Id);
            MultiKnots.RemoveWhere(id => spline.IndexOfKnotId(id) < 0);
            int knotIndex = SelectedKnotId > 0 ? spline.IndexOfKnotId(SelectedKnotId) : -1;
            if (knotIndex < 0 && MultiKnots.Count > 0) knotIndex = spline.IndexOfKnotId(SmallestMulti());
            SelectedKnot = knotIndex;
            SelectedKnotId = knotIndex >= 0 ? spline[knotIndex].Id : -1;
            if (knotIndex < 0)
            {
                SelectedHandle = 0;
                MultiKnots.Clear();
            }
            else if (!MultiKnots.Contains(SelectedKnotId))
            {
                MultiKnots.Add(SelectedKnotId);
            }
            Changed?.Invoke();
        }

        public static void SetHover(int splineIndex, int segment, float t, Vector3 worldPoint)
        {
            HoverValid = true;
            HoverSpline = splineIndex;
            HoverSegment = segment;
            HoverT = t;
            HoverPoint = worldPoint;
        }

        public static void ClearHover()
        {
            HoverValid = false;
            HoverSpline = -1;
            HoverSegment = -1;
        }

        public static void RaiseChanged() => Changed?.Invoke();
    }

    public static class TbsSplineEditorActions
    {
        public static TbsSplineComputer ResolveComputer()
        {
            if (TbsSplineEditorState.ActiveComputer != null) return TbsSplineEditorState.ActiveComputer;
            GameObject active = Selection.activeGameObject;
            var fromSelection = active != null ? active.GetComponent<TbsSplineComputer>() : null;
            if (fromSelection != null) return fromSelection;
            return UnityEngine.Object.FindFirstObjectByType<TbsSplineComputer>();
        }

        public static void ActivateEditTool()
        {
            var computer = ResolveComputer();
            if (computer == null) return;
            if (Selection.activeGameObject != computer.gameObject)
            {
                Selection.activeGameObject = computer.gameObject;
                EditorApplication.delayCall += TryActivateTool;
                return;
            }
            TryActivateTool();
        }

        static void TryActivateTool()
        {
            GameObject active = Selection.activeGameObject;
            if (active == null || active.GetComponent<TbsSplineComputer>() == null) return;
            if (ToolManager.activeToolType == typeof(TbsSplineComputerTool)) return;
            try { ToolManager.SetActiveTool<TbsSplineComputerTool>(); }
            catch (System.InvalidOperationException) { }
        }

        public static void ExitEditor()
        {
            TbsSelectionWatcher.SuppressUntilSelectionChange();
            ToolManager.RestorePreviousPersistentTool();
        }

        public static void RecordChange(TbsSplineComputer computer, string label)
        {
            Undo.RecordObject(computer, label);
        }

        public static void MarkChanged(TbsSplineComputer computer)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(computer);
        }

        public static void DeleteKnot(TbsSplineComputer computer, int splineIndex, int knotIndex)
        {
            if (!ValidKnot(computer, splineIndex, knotIndex)) return;
            RecordChange(computer, "Delete Knot");
            TbsKnotRef reference = computer.MakeRef(splineIndex, knotIndex);
            computer[splineIndex].RemoveKnotAt(knotIndex);
            computer.RemoveKnotFromJunctions(reference);
            computer.ValidateJunctions();
            MarkChanged(computer);
            TbsSplineEditorState.ClearKnot();
            TbsSplineEditorState.ClearHover();
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            SceneView.RepaintAll();
        }

        public static bool CanMergeKnots(TbsSpline spline)
        {
            return spline.Closed ? spline.Count >= 4 : spline.Count >= 3;
        }

        public static void MergeKnots(TbsSplineComputer computer, int splineIndex, int keepIndex, int removeIndex, Vector3 worldPosition, out int mergedIndex)
        {
            mergedIndex = -1;
            if (!ValidKnot(computer, splineIndex, keepIndex) || !ValidKnot(computer, splineIndex, removeIndex)) return;
            if (keepIndex == removeIndex) return;
            TbsSpline spline = computer[splineIndex];
            if (!CanMergeKnots(spline)) return;
            RecordChange(computer, "Merge Points");
            TbsKnot kept = spline[keepIndex];
            TbsKnot removed = spline[removeIndex];
            kept.Position = computer.transform.InverseTransformPoint(worldPosition);
            kept.Size = (kept.Size + removed.Size) * 0.5f;
            kept.Color = Color.Lerp(kept.Color, removed.Color, 0.5f);
            spline.BeginChange();
            spline.SetKnot(keepIndex, kept);
            TbsKnotRef removedRef = computer.MakeRef(splineIndex, removeIndex);
            spline.RemoveKnotAt(removeIndex);
            spline.EndChange();
            computer.RemoveKnotFromJunctions(removedRef);
            computer.ValidateJunctions();
            MarkChanged(computer);
            mergedIndex = removeIndex < keepIndex ? keepIndex - 1 : keepIndex;
            TbsSplineEditorState.ClearHover();
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            SceneView.RepaintAll();
        }

        public static void DeleteSelectedKnots(TbsSplineComputer computer)
        {
            if (!TbsSplineEditorState.HasSplineSelection) return;
            int splineIndex = TbsSplineEditorState.SelectedSpline;
            if (TbsSplineEditorState.MultiKnots.Count <= 1)
            {
                if (TbsSplineEditorState.HasKnotSelection) DeleteKnot(computer, splineIndex, TbsSplineEditorState.SelectedKnot);
                return;
            }
            RecordChange(computer, "Delete Knots");
            TbsSpline spline = computer[splineIndex];
            var ids = new List<int>(TbsSplineEditorState.MultiKnots);
            for (int i = 0; i < ids.Count; i++)
            {
                int index = spline.IndexOfKnotId(ids[i]);
                if (index < 0) continue;
                computer.RemoveKnotFromJunctions(new TbsKnotRef(spline.Id, ids[i]));
                spline.RemoveKnotAt(index);
            }
            computer.ValidateJunctions();
            MarkChanged(computer);
            TbsSplineEditorState.ClearKnot();
            TbsSplineEditorState.ClearHover();
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            SceneView.RepaintAll();
        }

        public static void SetSelectedKnotsMode(TbsSplineComputer computer, TbsTangentMode mode)
        {
            if (!TbsSplineEditorState.HasSplineSelection) return;
            int splineIndex = TbsSplineEditorState.SelectedSpline;
            TbsSpline spline = computer[splineIndex];
            RecordChange(computer, "Change Tangent Mode");
            spline.BeginChange();
            foreach (int id in TbsSplineEditorState.MultiKnots)
            {
                int index = spline.IndexOfKnotId(id);
                if (index < 0) continue;
                TbsKnot knot = spline[index];
                knot.Mode = mode;
                spline.SetKnot(index, knot);
            }
            spline.EndChange();
            MarkChanged(computer);
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }

        public static void DeleteSpline(TbsSplineComputer computer, int splineIndex)
        {
            if (splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            RecordChange(computer, "Delete Spline");
            computer.RemoveSplineAt(splineIndex);
            computer.ValidateJunctions();
            MarkChanged(computer);
            TbsSplineEditorState.ClearSelection();
            TbsSplineEditorState.ClearHover();
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            SceneView.RepaintAll();
        }

        public static bool ValidKnot(TbsSplineComputer computer, int splineIndex, int knotIndex)
        {
            return computer != null && splineIndex >= 0 && splineIndex < computer.SplineCount &&
                   knotIndex >= 0 && knotIndex < computer[splineIndex].Count;
        }

        public static void SetKnotMode(TbsSplineComputer computer, int splineIndex, int knotIndex, TbsTangentMode mode)
        {
            if (!ValidKnot(computer, splineIndex, knotIndex)) return;
            RecordChange(computer, "Change Tangent Mode");
            TbsSpline spline = computer[splineIndex];
            TbsKnot knot = spline[knotIndex];
            knot.Mode = mode;
            spline.SetKnot(knotIndex, knot);
            MarkChanged(computer);
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }

        public static void SetAllKnotsMode(TbsSplineComputer computer, int splineIndex, TbsTangentMode mode)
        {
            if (splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            TbsSpline spline = computer[splineIndex];
            RecordChange(computer, "Change Tangent Mode");
            spline.BeginChange();
            for (int i = 0; i < spline.Count; i++)
            {
                TbsKnot knot = spline[i];
                knot.Mode = mode;
                spline.SetKnot(i, knot);
            }
            spline.EndChange();
            MarkChanged(computer);
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }

        public static void ToggleClosed(TbsSplineComputer computer, int splineIndex)
        {
            if (splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            RecordChange(computer, "Toggle Closed");
            computer[splineIndex].Closed = !computer[splineIndex].Closed;
            MarkChanged(computer);
            SceneView.RepaintAll();
        }

        public static void SetSplineType(TbsSplineComputer computer, int splineIndex, TbsSplineType type)
        {
            if (computer == null || splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            if (computer[splineIndex].Type == type) return;
            RecordChange(computer, "Set Spline Type");
            computer[splineIndex].Type = type;
            MarkChanged(computer);
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }

        public static void SetSplineParametrization(TbsSplineComputer computer, int splineIndex, float value)
        {
            if (computer == null || splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            RecordChange(computer, "Set Parametrization");
            computer[splineIndex].KnotParametrization = value;
            MarkChanged(computer);
            SceneView.RepaintAll();
        }

        public static void SetPointType(TbsSplineComputer computer, int splineIndex, TbsSplineType chip)
        {
            if (computer == null || splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            if (chip == TbsSplineType.BSpline) return;
            bool hasSelection = TbsSplineEditorState.SelectedSpline == splineIndex
                && (TbsSplineEditorState.HasMultiSelection || TbsSplineEditorState.HasKnotSelection);
            if (!hasSelection) return;
            TbsSpline spline = computer[splineIndex];
            RecordChange(computer, "Set Point Type");
            spline.BeginChange();
            if (spline.Type != TbsSplineType.Bezier)
            {
                for (int i = 0; i < spline.Count; i++)
                {
                    TbsKnot knot = spline[i];
                    knot.Mode = TbsTangentMode.AutoSmooth;
                    spline.SetKnot(i, knot);
                }
                spline.Type = TbsSplineType.Bezier;
            }
            TbsTangentMode mode = chip == TbsSplineType.Linear ? TbsTangentMode.Linear
                : chip == TbsSplineType.CatmullRom ? TbsTangentMode.AutoSmooth
                : TbsTangentMode.Mirrored;
            if (TbsSplineEditorState.HasMultiSelection)
            {
                for (int i = 0; i < spline.Count; i++)
                {
                    if (!TbsSplineEditorState.MultiKnots.Contains(spline[i].Id)) continue;
                    TbsKnot knot = spline[i];
                    knot.Mode = mode;
                    spline.SetKnot(i, knot);
                }
            }
            else
            {
                int ki = TbsSplineEditorState.SelectedKnot;
                TbsKnot knot = spline[ki];
                knot.Mode = mode;
                spline.SetKnot(ki, knot);
            }
            spline.EndChange();
            MarkChanged(computer);
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }

        static List<int> SelectedKnotIndices(TbsSplineComputer computer, int splineIndex)
        {
            var result = new List<int>();
            if (computer == null || splineIndex < 0 || splineIndex >= computer.SplineCount) return result;
            TbsSpline spline = computer[splineIndex];
            if (TbsSplineEditorState.SelectedSpline != splineIndex) return result;
            if (TbsSplineEditorState.HasMultiSelection)
            {
                for (int i = 0; i < spline.Count; i++)
                    if (TbsSplineEditorState.MultiKnots.Contains(spline[i].Id)) result.Add(i);
            }
            else if (TbsSplineEditorState.HasKnotSelection)
            {
                result.Add(TbsSplineEditorState.SelectedKnot);
            }
            return result;
        }

        static void CommitSelectionEdit(TbsSplineComputer computer, int splineIndex, List<int> indices)
        {
            TbsSpline spline = computer[splineIndex];
            for (int i = 0; i < indices.Count; i++)
                computer.PropagateFromKnot(new TbsKnotRef(spline.Id, spline[indices[i]].Id));
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }

        public static float GetPrimarySize(TbsSplineComputer computer)
        {
            if (!TbsSplineEditorState.HasKnotSelection || computer == null) return 1f;
            return computer[TbsSplineEditorState.SelectedSpline][TbsSplineEditorState.SelectedKnot].Size;
        }

        public static Color GetPrimaryColor(TbsSplineComputer computer)
        {
            if (!TbsSplineEditorState.HasKnotSelection || computer == null) return Color.white;
            return computer[TbsSplineEditorState.SelectedSpline][TbsSplineEditorState.SelectedKnot].Color;
        }

        public static void SetSelectedSize(TbsSplineComputer computer, float size)
        {
            int si = TbsSplineEditorState.SelectedSpline;
            var indices = SelectedKnotIndices(computer, si);
            if (indices.Count == 0) return;
            TbsSpline spline = computer[si];
            RecordChange(computer, "Set Point Size");
            spline.BeginChange();
            for (int i = 0; i < indices.Count; i++)
            {
                TbsKnot knot = spline[indices[i]];
                knot.Size = Mathf.Max(0.01f, size);
                spline.SetKnot(indices[i], knot);
            }
            spline.EndChange();
            CommitSelectionEdit(computer, si, indices);
        }

        public static void SetSelectedColor(TbsSplineComputer computer, Color color)
        {
            int si = TbsSplineEditorState.SelectedSpline;
            var indices = SelectedKnotIndices(computer, si);
            if (indices.Count == 0) return;
            TbsSpline spline = computer[si];
            RecordChange(computer, "Set Point Color");
            spline.BeginChange();
            for (int i = 0; i < indices.Count; i++)
            {
                TbsKnot knot = spline[indices[i]];
                knot.Color = color;
                spline.SetKnot(indices[i], knot);
            }
            spline.EndChange();
            CommitSelectionEdit(computer, si, indices);
        }

        public static void DuplicateSelectedKnotsInPlace(TbsSplineComputer computer)
        {
            int si = TbsSplineEditorState.SelectedSpline;
            var indices = SelectedKnotIndices(computer, si);
            if (indices.Count == 0) return;
            TbsSpline spline = computer[si];
            RecordChange(computer, "Duplicate Points");
            spline.BeginChange();
            var newIds = new List<int>();
            for (int n = indices.Count - 1; n >= 0; n--)
            {
                int idx = indices[n];
                TbsKnot copy = spline[idx];
                copy.Id = 0;
                Vector3 along = copy.Rotation * copy.TangentOut;
                if (along.sqrMagnitude < 1e-6f) along = copy.Rotation * Vector3.forward;
                copy.Position += along.normalized * 0.75f;
                spline.InsertKnot(idx + 1, copy);
                newIds.Add(spline[idx + 1].Id);
            }
            spline.EndChange();
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            TbsSplineEditorState.SetMultiSelection(si, newIds);
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }

        public static void FlattenSelected(TbsSplineComputer computer, int axis)
        {
            int si = TbsSplineEditorState.SelectedSpline;
            var indices = SelectedKnotIndices(computer, si);
            if (indices.Count < 2 || axis < 0 || axis > 2) return;
            TbsSpline spline = computer[si];
            Transform trs = computer.transform;
            float sum = 0f;
            for (int i = 0; i < indices.Count; i++) sum += trs.TransformPoint(spline[indices[i]].Position)[axis];
            float average = sum / indices.Count;
            RecordChange(computer, "Flatten Points");
            spline.BeginChange();
            for (int i = 0; i < indices.Count; i++)
            {
                Vector3 world = trs.TransformPoint(spline[indices[i]].Position);
                world[axis] = average;
                TbsKnot knot = spline[indices[i]];
                knot.Position = trs.InverseTransformPoint(world);
                spline.SetKnot(indices[i], knot);
            }
            spline.EndChange();
            CommitSelectionEdit(computer, si, indices);
        }

        public static void DistributeSelectedEvenly(TbsSplineComputer computer)
        {
            int si = TbsSplineEditorState.SelectedSpline;
            var indices = SelectedKnotIndices(computer, si);
            if (indices.Count < 3) return;
            TbsSpline spline = computer[si];
            TbsSplineCache cache = computer.GetCache(si);
            float d0 = cache.KnotToDistance(indices[0]);
            float d1 = cache.KnotToDistance(indices[indices.Count - 1]);
            if (d1 - d0 < 1e-4f) return;
            var targets = new Vector3[indices.Count];
            TbsSample sample = default;
            for (int k = 1; k < indices.Count - 1; k++)
            {
                float target = Mathf.Lerp(d0, d1, (float)k / (indices.Count - 1));
                cache.EvaluateAtDistance(target, ref sample);
                targets[k] = sample.Position;
            }
            RecordChange(computer, "Distribute Points");
            spline.BeginChange();
            for (int k = 1; k < indices.Count - 1; k++)
            {
                TbsKnot knot = spline[indices[k]];
                knot.Position = targets[k];
                spline.SetKnot(indices[k], knot);
            }
            spline.EndChange();
            CommitSelectionEdit(computer, si, indices);
        }

        public static void CreatePrimitive(TbsSplineComputer computer, TbsPrimitiveKind kind, Vector3 worldCenter, out int splineIndex)
        {
            splineIndex = -1;
            if (computer == null) return;
            RecordChange(computer, "New " + kind);
            var spline = new TbsSpline();
            Vector3 c = computer.transform.InverseTransformPoint(worldCenter);
            switch (kind)
            {
                case TbsPrimitiveKind.Circle:
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2f;
                        spline.AddKnot(new TbsKnot(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4f));
                    }
                    spline.Closed = true;
                    break;
                case TbsPrimitiveKind.Ngon:
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f;
                        spline.AddKnot(LinearKnotAt(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4f));
                    }
                    spline.Closed = true;
                    break;
                case TbsPrimitiveKind.Star:
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i / 10f * Mathf.PI * 2f;
                        float r = i % 2 == 0 ? 4.5f : 1.9f;
                        spline.AddKnot(LinearKnotAt(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r));
                    }
                    spline.Closed = true;
                    break;
                case TbsPrimitiveKind.Rectangle:
                    spline.AddKnot(LinearKnotAt(c + new Vector3(-3f, 0f, -2f)));
                    spline.AddKnot(LinearKnotAt(c + new Vector3(3f, 0f, -2f)));
                    spline.AddKnot(LinearKnotAt(c + new Vector3(3f, 0f, 2f)));
                    spline.AddKnot(LinearKnotAt(c + new Vector3(-3f, 0f, 2f)));
                    spline.Closed = true;
                    break;
                default:
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i * 0.62f;
                        float r = Mathf.Lerp(1.2f, 5f, i / 11f);
                        spline.AddKnot(new TbsKnot(c + new Vector3(Mathf.Cos(a) * r, i * 0.35f, Mathf.Sin(a) * r)));
                    }
                    break;
            }
            computer.AddSpline(spline);
            computer.EnsureIds();
            splineIndex = computer.SplineCount - 1;
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            TbsSplineEditorState.SelectSpline(splineIndex);
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }

        static TbsKnot LinearKnotAt(Vector3 position)
        {
            var knot = new TbsKnot(position);
            knot.Mode = TbsTangentMode.Linear;
            return knot;
        }

        public static void SaveSplinePreset(TbsSplineComputer computer, int splineIndex)
        {
            if (computer == null || splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            string path = EditorUtility.SaveFilePanel("Save Spline Preset", Application.dataPath, "spline-preset", "json");
            if (string.IsNullOrEmpty(path)) return;
            TbsSpline spline = computer[splineIndex];
            var preset = new TbsSplinePreset
            {
                Type = spline.Type,
                Closed = spline.Closed,
                Parametrization = spline.KnotParametrization
            };
            for (int i = 0; i < spline.Count; i++) preset.Knots.Add(spline[i]);
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(preset, true));
        }

        public static void LoadSplinePresetAsNew(TbsSplineComputer computer, Vector3 worldCenter, out int splineIndex)
        {
            splineIndex = -1;
            if (computer == null) return;
            string path = EditorUtility.OpenFilePanel("Load Spline Preset", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;
            TbsSplinePreset preset;
            try { preset = JsonUtility.FromJson<TbsSplinePreset>(System.IO.File.ReadAllText(path)); }
            catch (System.Exception) { preset = null; }
            if (preset == null || preset.Knots == null || preset.Knots.Count == 0)
            {
                Debug.LogWarning("TBSplineS: preset file is empty or invalid");
                return;
            }
            RecordChange(computer, "Load Preset");
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < preset.Knots.Count; i++) centroid += preset.Knots[i].Position;
            centroid /= preset.Knots.Count;
            Vector3 offset = computer.transform.InverseTransformPoint(worldCenter) - centroid;
            var spline = new TbsSpline();
            for (int i = 0; i < preset.Knots.Count; i++)
            {
                TbsKnot knot = preset.Knots[i];
                knot.Id = 0;
                knot.Position += offset;
                spline.AddKnot(knot);
            }
            spline.Type = preset.Type;
            spline.KnotParametrization = preset.Parametrization;
            spline.Closed = preset.Closed;
            computer.AddSpline(spline);
            computer.EnsureIds();
            splineIndex = computer.SplineCount - 1;
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            TbsSplineEditorState.SelectSpline(splineIndex);
            SceneView.RepaintAll();
        }

        public static TbsSplineTrigger AddTriggerAt(TbsSplineComputer computer, int splineIndex, float t)
        {
            if (computer == null || splineIndex < 0 || splineIndex >= computer.SplineCount) return null;
            RecordChange(computer, "Add Trigger");
            TbsSpline spline = computer[splineIndex];
            if (spline.TriggerGroups.Count == 0) spline.AddTriggerGroup("triggers");
            TbsSplineTrigger trigger = spline.AddTrigger(0, Mathf.Clamp01(t));
            MarkChanged(computer);
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
            return trigger;
        }

        public static void RemoveTrigger(TbsSplineComputer computer, int splineIndex, int groupIndex, int triggerIndex)
        {
            if (computer == null || splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            TbsSpline spline = computer[splineIndex];
            if (groupIndex < 0 || groupIndex >= spline.TriggerGroups.Count) return;
            var triggers = spline.TriggerGroups[groupIndex].Triggers;
            if (triggerIndex < 0 || triggerIndex >= triggers.Count) return;
            RecordChange(computer, "Delete Trigger");
            triggers.RemoveAt(triggerIndex);
            MarkChanged(computer);
            SceneView.RepaintAll();
        }

        public static void SetTriggerPosition(TbsSplineComputer computer, int splineIndex, int groupIndex, int triggerIndex, float t)
        {
            if (computer == null || splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            TbsSpline spline = computer[splineIndex];
            if (groupIndex < 0 || groupIndex >= spline.TriggerGroups.Count) return;
            var triggers = spline.TriggerGroups[groupIndex].Triggers;
            if (triggerIndex < 0 || triggerIndex >= triggers.Count) return;
            RecordChange(computer, "Move Trigger");
            triggers[triggerIndex].Position = Mathf.Clamp01(t);
            MarkChanged(computer);
            SceneView.RepaintAll();
        }

        public static void AppendKnot(TbsSplineComputer computer, int splineIndex, Vector3 worldPosition, bool prepend, out int newIndex)
        {
            RecordChange(computer, "Add Knot");
            TbsSpline spline = computer[splineIndex];
            Vector3 local = computer.transform.InverseTransformPoint(worldPosition);
            newIndex = prepend ? 0 : spline.Count;
            spline.InsertKnot(newIndex, new TbsKnot(local));
            MarkChanged(computer);
            TbsSplineEditorState.RecordLast(TbsLastOp.Add, "Add Point", Vector3.zero, splineIndex,
                new List<int> { spline[newIndex].Id });
        }

        public static void StartSpline(TbsSplineComputer computer, Vector3 worldPosition, out int splineIndex)
        {
            RecordChange(computer, "New Spline");
            var spline = new TbsSpline();
            spline.AddKnot(new TbsKnot(computer.transform.InverseTransformPoint(worldPosition)));
            computer.AddSpline(spline);
            MarkChanged(computer);
            splineIndex = computer.SplineCount - 1;
            TbsSplineEditorState.RecordLast(TbsLastOp.Add, "New Spline", Vector3.zero, splineIndex,
                new List<int> { spline[0].Id });
            TbsSplineSceneRenderer.Get(computer).SetDirty();
        }

        public static void InsertKnotOnSegment(TbsSplineComputer computer, int splineIndex, int segment, float t, out int newIndex)
        {
            RecordChange(computer, "Insert Knot");
            newIndex = computer.InsertKnotOnSegment(splineIndex, segment, t);
            if (newIndex >= 0)
            {
                MarkChanged(computer);
                TbsSplineEditorState.RecordLast(TbsLastOp.Add, "Insert Point", Vector3.zero, splineIndex,
                    new List<int> { computer[splineIndex][newIndex].Id });
            }
        }

        public static void MoveSpline(TbsSplineComputer computer, int splineIndex, Vector3 localDelta)
        {
            if (splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            TbsSpline spline = computer[splineIndex];
            RecordChange(computer, "Move Spline");
            spline.BeginChange();
            for (int i = 0; i < spline.Count; i++)
            {
                TbsKnot knot = spline[i];
                knot.Position += localDelta;
                spline.SetKnot(i, knot);
            }
            spline.EndChange();
            for (int i = 0; i < spline.Count; i++)
                computer.PropagateFromKnot(new TbsKnotRef(spline.Id, spline[i].Id));
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
        }

        public static void RotateSpline(TbsSplineComputer computer, int splineIndex, Quaternion deltaWorld, Vector3 pivotWorld)
        {
            if (splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            TbsSpline spline = computer[splineIndex];
            Transform trs = computer.transform;
            Quaternion localDelta = Quaternion.Inverse(trs.rotation) * deltaWorld * trs.rotation;
            RecordChange(computer, "Rotate Spline");
            spline.BeginChange();
            for (int i = 0; i < spline.Count; i++)
            {
                TbsKnot knot = spline[i];
                Vector3 world = trs.TransformPoint(knot.Position);
                Vector3 rotated = pivotWorld + deltaWorld * (world - pivotWorld);
                knot.Position = trs.InverseTransformPoint(rotated);
                knot.Rotation = localDelta * knot.Rotation;
                spline.SetKnot(i, knot);
            }
            spline.EndChange();
            for (int i = 0; i < spline.Count; i++)
                computer.PropagateFromKnot(new TbsKnotRef(spline.Id, spline[i].Id));
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
        }

        public static void MoveSelectedKnots(TbsSplineComputer computer, Vector3 worldDelta)
        {
            if (!TbsSplineEditorState.HasSplineSelection) return;
            int splineIndex = TbsSplineEditorState.SelectedSpline;
            TbsSpline spline = computer[splineIndex];
            Vector3 localDelta = computer.transform.InverseTransformVector(worldDelta);
            if (localDelta.sqrMagnitude < 1e-12f) return;
            RecordChange(computer, "Move Knots");
            spline.BeginChange();
            if (TbsSplineEditorState.MultiKnots.Count > 1)
            {
                foreach (int id in TbsSplineEditorState.MultiKnots)
                {
                    int index = spline.IndexOfKnotId(id);
                    if (index < 0) continue;
                    TbsKnot knot = spline[index];
                    knot.Position += localDelta;
                    spline.SetKnot(index, knot);
                }
            }
            else if (TbsSplineEditorState.HasKnotSelection)
            {
                TbsKnot knot = spline[TbsSplineEditorState.SelectedKnot];
                knot.Position += localDelta;
                spline.SetKnot(TbsSplineEditorState.SelectedKnot, knot);
            }
            spline.EndChange();
            foreach (int id in TbsSplineEditorState.MultiKnots)
                computer.PropagateFromKnot(new TbsKnotRef(spline.Id, id));
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
        }

        public static void ApplyScaleFromBase(TbsSplineComputer computer, int splineIndex,
            List<(int id, Vector3 pos, Vector3 tin, Vector3 tout)> baseKnots, Vector3 centerLocal, Vector3 scale)
        {
            if (splineIndex < 0 || splineIndex >= computer.SplineCount || baseKnots.Count == 0) return;
            TbsSpline spline = computer[splineIndex];
            RecordChange(computer, "Scale Knots");
            spline.BeginChange();
            for (int i = 0; i < baseKnots.Count; i++)
            {
                var b = baseKnots[i];
                int idx = spline.IndexOfKnotId(b.id);
                if (idx < 0) continue;
                TbsKnot knot = spline[idx];
                knot.Position = centerLocal + Vector3.Scale(b.pos - centerLocal, scale);
                knot.TangentIn = Vector3.Scale(b.tin, scale);
                knot.TangentOut = Vector3.Scale(b.tout, scale);
                spline.SetKnot(idx, knot);
            }
            spline.EndChange();
            for (int i = 0; i < baseKnots.Count; i++)
                computer.PropagateFromKnot(new TbsKnotRef(spline.Id, baseKnots[i].id));
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
        }

        public static void SetLastDeltaWorld(TbsSplineComputer computer, Vector3 newWorldDelta)
        {
            if (!TbsSplineEditorState.LastActionValid) return;
            Vector3 diff = newWorldDelta - TbsSplineEditorState.LastDelta;
            TbsSplineEditorState.LastDelta = newWorldDelta;
            if (diff.sqrMagnitude < 1e-12f) return;
            NudgeLast(computer, diff);
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }

        public static void SetLastRotation(TbsSplineComputer computer, Vector3 newEuler)
        {
            if (!TbsSplineEditorState.LastActionValid || computer == null) return;
            int si = TbsSplineEditorState.LastSpline;
            if (TbsSplineEditorState.LastOp == TbsLastOp.RotateSpline)
            {
                Vector3 diff = newEuler - TbsSplineEditorState.LastRotEuler;
                TbsSplineEditorState.LastRotEuler = newEuler;
                if (diff.sqrMagnitude < 1e-6f) return;
                if (si >= 0 && si < computer.SplineCount)
                    RotateSpline(computer, si, Quaternion.Euler(diff), TbsSplineEditorState.LastPivot);
            }
            else if (TbsSplineEditorState.LastOp == TbsLastOp.Rotate)
            {
                TbsSplineEditorState.LastRotEuler = new Vector3(newEuler.x, 0f, 0f);
                if (si >= 0 && si < computer.SplineCount)
                {
                    int ki = TbsSplineEditorState.LastKnotIds.Count > 0
                        ? computer[si].IndexOfKnotId(TbsSplineEditorState.LastKnotIds[0])
                        : TbsSplineEditorState.SelectedKnot;
                    if (ki >= 0 && ki < computer[si].Count) SetKnotRoll(computer, si, ki, newEuler.x);
                }
            }
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }

        public static void SetLastScale(TbsSplineComputer computer, Vector3 newScale)
        {
            if (!TbsSplineEditorState.LastActionValid || TbsSplineEditorState.LastOp != TbsLastOp.Scale) return;
            if (computer == null || TbsSplineEditorState.LastScaleBase.Count == 0) return;
            TbsSplineEditorState.LastScale = newScale;
            ApplyScaleFromBase(computer, TbsSplineEditorState.LastSpline, TbsSplineEditorState.LastScaleBase,
                TbsSplineEditorState.LastScaleCenterLocal, newScale);
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }

        public static bool CanRepeatLast =>
            TbsSplineEditorState.LastActionValid && TbsSplineEditorState.LastOp != TbsLastOp.Rotate;

        public static void RepeatLast(TbsSplineComputer computer)
        {
            if (!TbsSplineEditorState.LastActionValid || computer == null) return;
            int si = TbsSplineEditorState.LastSpline;
            switch (TbsSplineEditorState.LastOp)
            {
                case TbsLastOp.Move:
                case TbsLastOp.MoveSpline:
                case TbsLastOp.MoveHandle:
                case TbsLastOp.Add:
                    if (TbsSplineEditorState.LastDelta.sqrMagnitude > 1e-12f)
                        NudgeLast(computer, TbsSplineEditorState.LastDelta);
                    break;
                case TbsLastOp.RotateSpline:
                    if (si >= 0 && si < computer.SplineCount)
                        RotateSpline(computer, si, Quaternion.Euler(TbsSplineEditorState.LastRotEuler), TbsSplineEditorState.LastPivot);
                    break;
                case TbsLastOp.Scale:
                    if (si >= 0 && si < computer.SplineCount && RecaptureScaleBase(computer, si))
                        ApplyScaleFromBase(computer, si, TbsSplineEditorState.LastScaleBase,
                            TbsSplineEditorState.LastScaleCenterLocal, TbsSplineEditorState.LastScale);
                    break;
            }
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }

        public static void ResetLast(TbsSplineComputer computer)
        {
            if (!TbsSplineEditorState.LastActionValid || computer == null) return;
            int si = TbsSplineEditorState.LastSpline;
            switch (TbsSplineEditorState.LastOp)
            {
                case TbsLastOp.Move:
                case TbsLastOp.MoveSpline:
                case TbsLastOp.MoveHandle:
                case TbsLastOp.Add:
                    if (TbsSplineEditorState.LastDelta.sqrMagnitude > 1e-12f)
                        NudgeLast(computer, -TbsSplineEditorState.LastDelta);
                    TbsSplineEditorState.LastDelta = Vector3.zero;
                    break;
                case TbsLastOp.RotateSpline:
                    if (si >= 0 && si < computer.SplineCount)
                        RotateSpline(computer, si, Quaternion.Euler(-TbsSplineEditorState.LastRotEuler), TbsSplineEditorState.LastPivot);
                    TbsSplineEditorState.LastRotEuler = Vector3.zero;
                    break;
                case TbsLastOp.Rotate:
                    SetLastRotation(computer, Vector3.zero);
                    break;
                case TbsLastOp.Scale:
                    if (TbsSplineEditorState.LastScaleBase.Count > 0)
                        ApplyScaleFromBase(computer, si, TbsSplineEditorState.LastScaleBase,
                            TbsSplineEditorState.LastScaleCenterLocal, Vector3.one);
                    TbsSplineEditorState.LastScale = Vector3.one;
                    break;
            }
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }

        static bool RecaptureScaleBase(TbsSplineComputer computer, int splineIndex)
        {
            TbsSpline spline = computer[splineIndex];
            var ids = new List<int>(TbsSplineEditorState.LastKnotIds);
            TbsSplineEditorState.LastScaleBase.Clear();
            for (int i = 0; i < ids.Count; i++)
            {
                int idx = spline.IndexOfKnotId(ids[i]);
                if (idx < 0) continue;
                TbsKnot k = spline[idx];
                TbsSplineEditorState.LastScaleBase.Add((k.Id, k.Position, k.TangentIn, k.TangentOut));
            }
            return TbsSplineEditorState.LastScaleBase.Count > 0;
        }

        static void NudgeLast(TbsSplineComputer computer, Vector3 worldDiff)
        {
            int si = TbsSplineEditorState.LastSpline;
            if (si < 0 || si >= computer.SplineCount) return;
            if (TbsSplineEditorState.LastOp == TbsLastOp.MoveHandle)
            {
                TbsSpline hsp = computer[si];
                int hid = TbsSplineEditorState.LastKnotIds.Count > 0 ? TbsSplineEditorState.LastKnotIds[0] : -1;
                int hidx = hsp.IndexOfKnotId(hid);
                if (hidx < 0) return;
                bool inSide = TbsSplineEditorState.LastHandleSide == 1;
                TbsKnot hk = hsp[hidx];
                Vector3 curTip = computer.transform.TransformPoint(inSide ? hk.TangentInPosition : hk.TangentOutPosition);
                SetTangentWorld(computer, si, hidx, inSide, curTip + worldDiff);
                return;
            }
            Vector3 localDelta = computer.transform.InverseTransformVector(worldDiff);
            if (TbsSplineEditorState.LastOp == TbsLastOp.MoveSpline)
            {
                MoveSpline(computer, si, localDelta);
                return;
            }
            TbsSpline spline = computer[si];
            RecordChange(computer, "Adjust " + TbsSplineEditorState.LastOpLabel);
            spline.BeginChange();
            foreach (int id in TbsSplineEditorState.LastKnotIds)
            {
                int idx = spline.IndexOfKnotId(id);
                if (idx < 0) continue;
                TbsKnot k = spline[idx];
                k.Position += localDelta;
                spline.SetKnot(idx, k);
            }
            spline.EndChange();
            foreach (int id in TbsSplineEditorState.LastKnotIds)
                computer.PropagateFromKnot(new TbsKnotRef(spline.Id, id));
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
        }

        public static void AlignSelectedHeights(TbsSplineComputer computer, TbsHeightAlign mode)
        {
            if (!TbsSplineEditorState.HasSplineSelection) return;
            int splineIndex = TbsSplineEditorState.SelectedSpline;
            TbsSpline spline = computer[splineIndex];
            Transform trs = computer.transform;
            var indices = new List<int>();
            for (int i = 0; i < spline.Count; i++)
                if (TbsSplineEditorState.MultiKnots.Contains(spline[i].Id)) indices.Add(i);
            if (indices.Count < 2) return;
            float refY;
            if (mode == TbsHeightAlign.First) refY = trs.TransformPoint(spline[indices[0]].Position).y;
            else if (mode == TbsHeightAlign.Last) refY = trs.TransformPoint(spline[indices[indices.Count - 1]].Position).y;
            else
            {
                float sum = 0f;
                for (int i = 0; i < indices.Count; i++) sum += trs.TransformPoint(spline[indices[i]].Position).y;
                refY = sum / indices.Count;
            }
            RecordChange(computer, "Align Heights");
            spline.BeginChange();
            for (int i = 0; i < indices.Count; i++)
            {
                int idx = indices[i];
                Vector3 world = trs.TransformPoint(spline[idx].Position);
                world.y = refY;
                TbsKnot knot = spline[idx];
                knot.Position = trs.InverseTransformPoint(world);
                spline.SetKnot(idx, knot);
            }
            spline.EndChange();
            for (int i = 0; i < indices.Count; i++)
                computer.PropagateFromKnot(new TbsKnotRef(spline.Id, spline[indices[i]].Id));
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            SceneView.RepaintAll();
        }

        public static void DuplicateSelectedToNewSpline(TbsSplineComputer computer)
        {
            if (!TbsSplineEditorState.HasSplineSelection) return;
            int splineIndex = TbsSplineEditorState.SelectedSpline;
            TbsSpline spline = computer[splineIndex];
            var ids = new List<int>();
            if (TbsSplineEditorState.MultiKnots.Count > 0) ids.AddRange(TbsSplineEditorState.MultiKnots);
            else if (TbsSplineEditorState.HasKnotSelection) ids.Add(spline[TbsSplineEditorState.SelectedKnot].Id);
            if (ids.Count == 0) return;
            RecordChange(computer, "Duplicate Knots");
            int newIndex = computer.DuplicateKnotsToNewSpline(splineIndex, ids, new Vector3(0f, 0f, 1f));
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            if (newIndex >= 0)
            {
                TbsSpline created = computer[newIndex];
                var newIds = new List<int>();
                for (int i = 0; i < created.Count; i++) newIds.Add(created[i].Id);
                TbsSplineEditorState.SetMultiSelection(newIndex, newIds);
                TbsSplineEditorState.ActiveTool = TbsTool.Move;
            }
            SceneView.RepaintAll();
        }

        public static void ReverseSpline(TbsSplineComputer computer, int splineIndex)
        {
            if (splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            RecordChange(computer, "Reverse Spline");
            computer[splineIndex].Reverse();
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            TbsSplineEditorState.ClearKnot();
            SceneView.RepaintAll();
        }

        public static void DuplicateSpline(TbsSplineComputer computer, int splineIndex)
        {
            if (splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            RecordChange(computer, "Duplicate Spline");
            int newIndex = computer.DuplicateSpline(splineIndex);
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            if (newIndex >= 0) TbsSplineEditorState.SelectSpline(newIndex);
            SceneView.RepaintAll();
        }

        public static void SplitSplineAtKnot(TbsSplineComputer computer, int splineIndex, int knotIndex)
        {
            if (splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            TbsSpline spline = computer[splineIndex];
            if (knotIndex <= 0 || knotIndex >= spline.Count - 1) return;
            int knotId = spline[knotIndex].Id;
            RecordChange(computer, "Split Spline");
            int newIndex = computer.SplitSplineAtKnot(splineIndex, knotId);
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            if (newIndex >= 0) TbsSplineEditorState.SelectSpline(newIndex);
            SceneView.RepaintAll();
        }

        public static void ConnectKnots(TbsSplineComputer computer, TbsKnotRef a, TbsKnotRef b)
        {
            RecordChange(computer, "Connect Splines");
            computer.ConnectKnots(a, b);
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            SceneView.RepaintAll();
        }

        public static void ConnectEndpointToCurve(TbsSplineComputer computer, TbsKnotRef incoming, int targetSplineIndex, int segment, float t)
        {
            RecordChange(computer, "Connect Splines");
            computer.ConnectEndpointToCurve(incoming, targetSplineIndex, segment, t);
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            SceneView.RepaintAll();
        }

        public static void MergeJunction(TbsSplineComputer computer, int junctionId)
        {
            RecordChange(computer, "Merge Splines");
            computer.MergeEndpointJunction(junctionId);
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            TbsSplineEditorState.ClearSelection();
            SceneView.RepaintAll();
        }

        public static void DisconnectKnot(TbsSplineComputer computer, int splineIndex, int knotIndex)
        {
            if (!ValidKnot(computer, splineIndex, knotIndex)) return;
            TbsKnotRef reference = computer.MakeRef(splineIndex, knotIndex);
            TbsJunction junction = computer.GetJunctionOfKnot(reference);
            if (junction == null) return;
            RecordChange(computer, "Disconnect Splines");
            computer.Disconnect(junction.Id);
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            SceneView.RepaintAll();
        }

        public static void PropagateFromKnot(TbsSplineComputer computer, int splineIndex, int knotIndex)
        {
            if (splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            if (knotIndex < 0 || knotIndex >= computer[splineIndex].Count) return;
            computer.PropagateFromKnot(computer.MakeRef(splineIndex, knotIndex));
        }

        public static bool CanEditRoll(TbsSplineComputer computer, int splineIndex, int knotIndex)
        {
            if (!ValidKnot(computer, splineIndex, knotIndex)) return false;
            Vector3 axis = KnotAxis(computer.transform, computer[splineIndex][knotIndex]);
            return Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.99f;
        }

        public static float GetKnotRoll(TbsSplineComputer computer, int splineIndex, int knotIndex)
        {
            if (!ValidKnot(computer, splineIndex, knotIndex)) return 0f;
            TbsKnot knot = computer[splineIndex][knotIndex];
            Transform trs = computer.transform;
            Vector3 axis = KnotAxis(trs, knot);
            if (Mathf.Abs(Vector3.Dot(axis, Vector3.up)) >= 0.99f) return 0f;
            Vector3 reference = TbsSplineMath.OrthonormalUp(axis, Vector3.up);
            Vector3 up = TbsSplineMath.OrthonormalUp(axis, trs.TransformDirection(knot.Up));
            return Vector3.SignedAngle(reference, up, axis);
        }

        public static void SetKnotRoll(TbsSplineComputer computer, int splineIndex, int knotIndex, float degrees)
        {
            if (!ValidKnot(computer, splineIndex, knotIndex)) return;
            TbsSpline spline = computer[splineIndex];
            TbsKnot knot = spline[knotIndex];
            Transform trs = computer.transform;
            Vector3 axis = KnotAxis(trs, knot);
            if (Mathf.Abs(Vector3.Dot(axis, Vector3.up)) >= 0.99f) return;
            Vector3 reference = TbsSplineMath.OrthonormalUp(axis, Vector3.up);
            Quaternion worldRotation = Quaternion.AngleAxis(degrees, axis) * Quaternion.LookRotation(axis, reference);
            Quaternion newLocal = Quaternion.Inverse(trs.rotation) * worldRotation;
            Quaternion keepTangents = Quaternion.Inverse(newLocal) * knot.Rotation;
            RecordChange(computer, "Roll Knot");
            knot.TangentIn = keepTangents * knot.TangentIn;
            knot.TangentOut = keepTangents * knot.TangentOut;
            knot.Rotation = newLocal;
            spline.SetKnot(knotIndex, knot);
            MarkChanged(computer);
            SceneView.RepaintAll();
        }

        public static void SetKnotHeight(TbsSplineComputer computer, int splineIndex, int knotIndex, float worldY)
        {
            if (!ValidKnot(computer, splineIndex, knotIndex)) return;
            TbsSpline spline = computer[splineIndex];
            TbsKnot knot = spline[knotIndex];
            Transform trs = computer.transform;
            Vector3 world = trs.TransformPoint(knot.Position);
            world.y = worldY;
            SetKnotWorld(computer, splineIndex, knotIndex, world);
        }

        public static void SetKnotWorld(TbsSplineComputer computer, int splineIndex, int knotIndex, Vector3 world)
        {
            if (!ValidKnot(computer, splineIndex, knotIndex)) return;
            TbsSpline spline = computer[splineIndex];
            TbsKnot knot = spline[knotIndex];
            RecordChange(computer, "Move Knot");
            knot.Position = computer.transform.InverseTransformPoint(world);
            spline.SetKnot(knotIndex, knot);
            computer.PropagateFromKnot(new TbsKnotRef(spline.Id, knot.Id));
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            SceneView.RepaintAll();
        }

        public static void SetTangentWorld(TbsSplineComputer computer, int splineIndex, int knotIndex, bool inSide, Vector3 world)
        {
            if (!ValidKnot(computer, splineIndex, knotIndex)) return;
            TbsSpline spline = computer[splineIndex];
            Transform trs = computer.transform;
            RecordChange(computer, "Edit Tangent");
            TbsKnot knot = spline[knotIndex];
            if (knot.Mode == TbsTangentMode.AutoSmooth) { knot.Mode = TbsTangentMode.Broken; spline.SetKnot(knotIndex, knot); knot = spline[knotIndex]; }
            Vector3 local = Quaternion.Inverse(knot.Rotation) * (trs.InverseTransformPoint(world) - knot.Position);
            if (inSide) spline.SetTangentIn(knotIndex, local); else spline.SetTangentOut(knotIndex, local);
            MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
            SceneView.RepaintAll();
        }

        static Vector3 KnotAxis(Transform trs, in TbsKnot knot)
        {
            Vector3 tangentLocal = knot.Rotation * knot.TangentOut;
            if (tangentLocal.sqrMagnitude <= TbsSplineMath.Epsilon)
                tangentLocal = knot.Rotation * -knot.TangentIn;
            return tangentLocal.sqrMagnitude > TbsSplineMath.Epsilon
                ? trs.TransformDirection(tangentLocal).normalized
                : trs.TransformDirection(knot.Rotation * Vector3.forward).normalized;
        }

        public static void SetTool(TbsSplineComputer computer, TbsTool tool)
        {
            if (TbsSplineEditorState.ActiveTool == tool) return;
            if (TbsSplineEditorState.DrawMode && tool != TbsTool.Draw) FinishDraw(computer);
            TbsSplineEditorState.ActiveTool = tool;
            if (tool == TbsTool.Draw)
            {
                TbsSplineEditorState.DrawSpline = -1;
                TbsSplineEditorState.GhostValid = false;
            }
            TbsSplineEditorState.CloseMenu();
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }

        public static void SetMode(TbsSplineComputer computer, TbsEditorMode mode)
        {
            if (TbsSplineEditorState.ActiveMode == mode) return;
            if (TbsSplineEditorState.DrawMode) FinishDraw(computer);
            TbsSplineEditorState.ActiveMode = mode;
            if (mode == TbsEditorMode.Object) TbsSplineEditorState.ClearKnot();
            if (!TbsSplineEditorState.ToolValidInMode(TbsSplineEditorState.ActiveTool, mode))
                TbsSplineEditorState.ActiveTool = TbsTool.Move;
            TbsSplineEditorState.CloseMenu();
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }

        public static void ToggleMode(TbsSplineComputer computer) =>
            SetMode(computer, TbsSplineEditorState.ActiveMode == TbsEditorMode.Edit ? TbsEditorMode.Object : TbsEditorMode.Edit);

        public static void ToggleDrawMode(TbsSplineComputer computer) =>
            SetTool(computer, TbsSplineEditorState.DrawMode ? TbsTool.Select : TbsTool.Draw);

        public static void FinishDraw(TbsSplineComputer computer)
        {
            int splineIndex = TbsSplineEditorState.DrawSpline;
            if (computer != null && splineIndex >= 0 && splineIndex < computer.SplineCount && computer[splineIndex].Count < 2)
                DeleteSpline(computer, splineIndex);
            TbsSplineEditorState.DrawMode = false;
            TbsSplineEditorState.DrawSpline = -1;
            TbsSplineEditorState.GhostValid = false;
            TbsSplineEditorState.RaiseChanged();
            SceneView.RepaintAll();
        }
    }
}
