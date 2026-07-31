using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using TBSplineS;

namespace TBSplineS.Editor
{
    [EditorTool("TBSplineS Editor", typeof(TbsSplineComputer))]
    public sealed class TbsSplineComputerTool : EditorTool
    {
        internal const float CurvePickPixels = 10f;
        internal const float KnotPickPixels = 14f;

        static readonly Color HoverColor = new Color(1f, 0.78f, 0.35f, 0.9f);
        static readonly Color SelectedColor = new Color(1f, 0.72f, 0.3f);
        static readonly Color TangentLineColor = new Color(0.85f, 0.85f, 0.9f, 0.65f);
        static readonly Color DeleteHighlightColor = new Color(1f, 0.32f, 0.28f);
        static readonly Color MergeHighlightColor = new Color(1f, 0.85f, 0.25f);

        enum DragKind
        {
            None,
            Knot,
            MoveY,
            Roll,
            Spline,
            Marquee
        }

        Vector3 _splineDragStart;
        Vector2 _marqueeStart;
        Vector3 _dragOriginWorld;
        bool _moveGizmoDragging;
        Vector3 _moveGizmoOrigin;
        bool _magnetValid;
        Vector3 _magnetWorld;
        bool _scaleActive;
        Vector3 _scaleCenterLocal;
        readonly List<(int id, Vector3 pos, Vector3 tin, Vector3 tout)> _scaleBase = new List<(int, Vector3, Vector3, Vector3)>();
        bool _splineRotActive;
        Quaternion _splineRotLast;
        Vector3 _splineRotPivot;
        int _tanDragId = -1;
        Plane _tanDragPlane;
        Vector3 _tanDragStartTip;
        Vector3 _tanDragHitStart;
        Vector3 _tanConstrainA, _tanConstrainB;
        int _tanDragSpline, _tanDragKnot;
        bool _tanDragInSide;
        bool _tanFree;
        bool _handleDragging;
        Vector3 _handleDragOrigin;
        int _handleDragSpline = -1, _handleDragKnot = -1, _handleDragSide;

        GUIContent _icon;
        TbsSplineHud _hud;
        DragKind _drag;
        int _dragSpline = -1;
        int _dragKnot = -1;
        Plane _dragPlane;
        TbsKnot _rollStartKnot;
        Vector3 _rollAxis;
        Vector3 _rollStartDirection;
        Vector2 _rmbDown;
        bool _rmbMoved;
        readonly Vector3[] _ringBuffer = new Vector3[49];

        public override GUIContent toolbarIcon
        {
            get
            {
                Texture icon = TbsIcons.Logo != null ? (Texture)TbsIcons.Logo : EditorGUIUtility.IconContent("EditCollider").image;
                _icon ??= new GUIContent(icon, "TBSplineS Editor (Alt+E)");
                return _icon;
            }
        }

        public override void OnActivated()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            var computer = target as TbsSplineComputer;
            TbsSplineEditorState.SetComputer(computer);
            if (computer != null) CleanupOrphanSplines(computer);
            SceneView.RepaintAll();
        }

        public override void OnWillBeDeactivated()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            CancelDrag();
            TbsSplineEditorState.InvalidateLast();
            TbsSplineEditorState.CloseMenu();
            TbsSplineEditorState.ActiveTool = TbsTool.Select;
            TbsSplineEditorState.DrawSpline = -1;
            TbsSplineEditorState.GhostValid = false;
            TbsSplineEditorState.ClearActionKnots();
            TbsSplineEditorState.ClearHover();
            TbsSplineEditorState.ClearSelection();
            SceneView.RepaintAll();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView sceneView)) return;
            var computer = target as TbsSplineComputer;
            if (computer == null) return;
            if (TbsSplineEditorState.ActiveComputer != computer) TbsSplineEditorState.SetComputer(computer);
            var renderer = TbsSplineSceneRenderer.Get(computer);
            _hud ??= new TbsSplineHud();
            Event evt = Event.current;

            int defaultControl = GUIUtility.GetControlID(FocusType.Passive);
            if (evt.type == EventType.Layout) HandleUtility.AddDefaultControl(defaultControl);

            _hud.PrepareLayout(computer, sceneView);
            bool inputBlocked = (TbsSplineEditorState.MenuOpen || _hud.MouseOver) && _drag == DragKind.None;

            HandleCommands(computer, evt);
            HandleRightClick(computer, renderer, evt, inputBlocked);
            UpdateHover(renderer, evt, inputBlocked);
            DrawCurves(computer, renderer, evt);
            DoActiveDrag(computer, renderer, evt, sceneView);
            if (TbsSplineEditorState.DrawMode)
            {
                DoDrawMode(computer, renderer, evt, sceneView, inputBlocked);
            }
            else
            {
                if (TbsSplineEditorState.EditModeActive && TbsSplineEditorState.MoveMode) DoMoveGizmo(computer, renderer, inputBlocked);
                if (TbsSplineEditorState.EditModeActive && TbsSplineEditorState.ScaleMode) DoScaleGizmo(computer, renderer, inputBlocked);
                if (TbsSplineEditorState.ObjectModeActive && TbsSplineEditorState.MoveMode) DoSplineMoveGizmo(computer, inputBlocked);
                if (TbsSplineEditorState.ObjectModeActive && TbsSplineEditorState.RotateMode) DoSplineRotateGizmo(computer, inputBlocked);
                if (TbsSplineEditorState.PointMode && TbsSplineEditorState.HasSplineSelection)
                {
                    DoPointTool(computer, renderer, evt, sceneView, inputBlocked, defaultControl);
                }
                else
                {
                    if (TbsSplineEditorState.PointMode)
                    {
                        TbsSplineEditorState.GhostValid = false;
                        TbsSplineEditorState.ClearActionKnots();
                    }
                    DoSelectedSplineHandles(computer, evt, sceneView, inputBlocked);
                }
                DoTriggerHandles(computer, renderer, evt, inputBlocked);
                DoSceneMouse(computer, renderer, evt, sceneView, defaultControl, inputBlocked);
            }
            HandleKeys(computer, evt, sceneView);
            _hud.DoGUI(computer, sceneView);
            if (evt.type == EventType.MouseMove) sceneView.Repaint();
        }

        void OnUndoRedo()
        {
            CancelDrag();
            TbsSplineEditorState.InvalidateLast();
            var computer = TbsSplineEditorState.ActiveComputer;
            if (computer != null)
            {
                TbsSplineSceneRenderer.Get(computer).SetDirty();
                TbsSplineEditorState.RevalidateSelection();
                if (TbsSplineEditorState.DrawMode && (TbsSplineEditorState.DrawSpline < 0 || TbsSplineEditorState.DrawSpline >= computer.SplineCount))
                {
                    TbsSplineEditorState.DrawMode = false;
                    TbsSplineEditorState.DrawSpline = -1;
                    TbsSplineEditorState.GhostValid = false;
                }
            }
            TbsSplineEditorState.ClearHover();
            SceneView.RepaintAll();
        }

        void CancelDrag()
        {
            TbsSplineEditorState.DragLabelValid = false;
            TbsSplineEditorState.MarqueeActive = false;
            TbsSplineEditorState.DragInfoValid = false;
            _magnetValid = false;
            if (_tanDragId != -1 || _handleDragging)
            {
                _tanDragId = -1;
                _handleDragging = false;
                GUIUtility.hotControl = 0;
            }
            if (_drag == DragKind.None) return;
            _drag = DragKind.None;
            _dragSpline = -1;
            _dragKnot = -1;
            GUIUtility.hotControl = 0;
        }

        void SetDragInfo(Vector3 origin, Vector3 current)
        {
            TbsSplineEditorState.DragInfoValid = true;
            TbsSplineEditorState.DragInfoOrigin = origin;
            TbsSplineEditorState.DragInfoCurrent = current;
        }

        bool MagnetizeToKnot(TbsSplineComputer computer, Vector2 gui, out Vector3 world)
        {
            world = default;
            Transform trs = computer.transform;
            Camera cam = Camera.current;
            float best = KnotPickPixels;
            bool found = false;
            for (int s = 0; s < computer.SplineCount; s++)
            {
                TbsSpline sp = computer[s];
                for (int i = 0; i < sp.Count; i++)
                {
                    if (s == TbsSplineEditorState.SelectedSpline && TbsSplineEditorState.MultiKnots.Contains(sp[i].Id)) continue;
                    Vector3 w = trs.TransformPoint(sp[i].Position);
                    if (cam != null && cam.WorldToViewportPoint(w).z <= 0f) continue;
                    float d = Vector2.Distance(HandleUtility.WorldToGUIPoint(w), gui);
                    if (d < best) { best = d; world = w; found = true; }
                }
            }
            return found;
        }

        void DrawDragGuides(TbsSplineComputer computer)
        {
            if (!TbsSplineEditorState.DragInfoValid) return;
            Vector3 o = TbsSplineEditorState.DragInfoOrigin;
            Vector3 c = TbsSplineEditorState.DragInfoCurrent;
            Vector3 corner = new Vector3(c.x, o.y, c.z);
            Handles.color = new Color(1f, 1f, 1f, 0.5f);
            Handles.DrawDottedLine(o, corner, 3f);
            Handles.color = new Color(0.45f, 0.9f, 1f, 0.8f);
            Handles.DrawDottedLine(corner, c, 3f);
            Handles.color = new Color(1f, 0.72f, 0.3f, 0.9f);
            float os = HandleUtility.GetHandleSize(o) * 0.06f;
            Vector3 face = Camera.current != null ? Camera.current.transform.forward : Vector3.up;
            Handles.DrawWireDisc(o, face, os);
            if (_magnetValid)
            {
                Handles.color = new Color(0.4f, 1f, 0.6f, 0.95f);
                float ms = HandleUtility.GetHandleSize(_magnetWorld) * 0.12f;
                Handles.DrawWireDisc(_magnetWorld, face, ms);
                Handles.DrawWireDisc(_magnetWorld, face, ms * 0.55f);
            }
        }

        static Vector3 SplineCenter(TbsSplineComputer computer, int splineIndex)
        {
            TbsSample sample = default;
            computer.Evaluate(splineIndex, 0.5f, ref sample);
            return sample.Position;
        }

        static Vector3 SelectionCenter(TbsSplineComputer computer)
        {
            int splineIndex = TbsSplineEditorState.SelectedSpline;
            TbsSpline spline = computer[splineIndex];
            Transform trs = computer.transform;
            if (TbsSplineEditorState.MultiKnots.Count > 1)
            {
                Vector3 sum = Vector3.zero;
                int n = 0;
                foreach (int id in TbsSplineEditorState.MultiKnots)
                {
                    int index = spline.IndexOfKnotId(id);
                    if (index < 0) continue;
                    sum += trs.TransformPoint(spline[index].Position);
                    n++;
                }
                if (n > 0) return sum / n;
            }
            return trs.TransformPoint(spline[TbsSplineEditorState.SelectedKnot].Position);
        }

        void DoMoveGizmo(TbsSplineComputer computer, TbsSplineSceneRenderer renderer, bool inputBlocked)
        {
            if (!TbsSplineEditorState.HasKnotSelection)
            {
                if (_moveGizmoDragging) { _moveGizmoDragging = false; TbsSplineEditorState.DragInfoValid = false; }
                return;
            }
            Vector3 center = SelectionCenter(computer);
            bool disable = inputBlocked && GUIUtility.hotControl == 0;
            using (new EditorGUI.DisabledScope(disable))
            {
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(center, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    if (!_moveGizmoDragging) { _moveGizmoDragging = true; _moveGizmoOrigin = center; }
                    if (TbsSplineEditorState.SnapToGrid)
                    {
                        Vector3 snapped = GridSnap(computer, moved);
                        if (Mathf.Abs(moved.x - center.x) > 1e-5f) moved.x = snapped.x;
                        if (Mathf.Abs(moved.z - center.z) > 1e-5f) moved.z = snapped.z;
                    }
                    TbsSplineEditorActions.MoveSelectedKnots(computer, moved - center);
                    SetDragInfo(_moveGizmoOrigin, moved);
                    UpdateGizmoConnectTarget(computer, renderer);
                    SceneView.RepaintAll();
                }
            }
            if (_moveGizmoDragging && GUIUtility.hotControl == 0)
            {
                _moveGizmoDragging = false;
                TbsSplineEditorState.DragInfoValid = false;
                Vector3 moveDelta = SelectionCenter(computer) - _moveGizmoOrigin;
                if (moveDelta.sqrMagnitude > 1e-8f)
                    TbsSplineEditorState.RecordLast(TbsLastOp.Move, "Move", moveDelta,
                        TbsSplineEditorState.SelectedSpline, new List<int>(TbsSplineEditorState.MultiKnots));
                if (TbsSplineEditorState.ConnectTargetValid && IsSelectedEndpoint(computer))
                    OfferConnect(computer, TbsSplineEditorState.SelectedSpline, TbsSplineEditorState.SelectedKnot);
                else
                    TbsSplineEditorState.ConnectTargetValid = false;
            }
        }

        void DoScaleGizmo(TbsSplineComputer computer, TbsSplineSceneRenderer renderer, bool inputBlocked)
        {
            if (!TbsSplineEditorState.HasKnotSelection)
            {
                _scaleActive = false;
                return;
            }
            Vector3 center = SelectionCenter(computer);
            float size = HandleUtility.GetHandleSize(center);
            bool disable = inputBlocked && GUIUtility.hotControl == 0;
            using (new EditorGUI.DisabledScope(disable))
            {
                EditorGUI.BeginChangeCheck();
                Vector3 scale = Handles.ScaleHandle(Vector3.one, center, Quaternion.identity, size);
                if (EditorGUI.EndChangeCheck())
                {
                    if (!_scaleActive)
                    {
                        _scaleActive = true;
                        _scaleCenterLocal = computer.transform.InverseTransformPoint(center);
                        CaptureScaleBase(computer);
                    }
                    TbsSplineEditorActions.ApplyScaleFromBase(computer, TbsSplineEditorState.SelectedSpline, _scaleBase, _scaleCenterLocal, scale);
                    _lastScaleRatio = scale;
                    SetDragInfo(computer.transform.TransformPoint(_scaleCenterLocal), center);
                    SceneView.RepaintAll();
                }
            }
            if (_scaleActive && GUIUtility.hotControl == 0)
            {
                _scaleActive = false;
                TbsSplineEditorState.DragInfoValid = false;
                TbsSplineEditorState.RecordLastScale("Scale", _lastScaleRatio, _scaleCenterLocal, TbsSplineEditorState.SelectedSpline, _scaleBase);
            }
        }

        Vector3 _lastScaleRatio = Vector3.one;

        void CaptureScaleBase(TbsSplineComputer computer)
        {
            _scaleBase.Clear();
            int splineIndex = TbsSplineEditorState.SelectedSpline;
            TbsSpline spline = computer[splineIndex];
            if (TbsSplineEditorState.MultiKnots.Count > 0)
            {
                foreach (int id in TbsSplineEditorState.MultiKnots)
                {
                    int idx = spline.IndexOfKnotId(id);
                    if (idx < 0) continue;
                    TbsKnot k = spline[idx];
                    _scaleBase.Add((k.Id, k.Position, k.TangentIn, k.TangentOut));
                }
            }
            else if (TbsSplineEditorState.HasKnotSelection)
            {
                TbsKnot k = spline[TbsSplineEditorState.SelectedKnot];
                _scaleBase.Add((k.Id, k.Position, k.TangentIn, k.TangentOut));
            }
        }

        void DoSplineRotateGizmo(TbsSplineComputer computer, bool inputBlocked)
        {
            if (!TbsSplineEditorState.HasSplineSelection)
            {
                _splineRotActive = false;
                return;
            }
            if (!_splineRotActive)
            {
                _splineRotLast = Quaternion.identity;
                _splineRotPivot = SplineGizmoPivot(computer);
            }
            bool disable = inputBlocked && GUIUtility.hotControl == 0;
            using (new EditorGUI.DisabledScope(disable))
            {
                EditorGUI.BeginChangeCheck();
                Quaternion rot = Handles.RotationHandle(_splineRotLast, _splineRotPivot);
                if (EditorGUI.EndChangeCheck())
                {
                    _splineRotActive = true;
                    Quaternion delta = rot * Quaternion.Inverse(_splineRotLast);
                    _splineRotLast = rot;
                    foreach (int id in TbsSplineEditorState.SelectedSplineIds)
                    {
                        int idx = computer.IndexOfSplineId(id);
                        if (idx >= 0) TbsSplineEditorActions.RotateSpline(computer, idx, delta, _splineRotPivot);
                    }
                    SceneView.RepaintAll();
                }
            }
            if (_splineRotActive && GUIUtility.hotControl == 0)
            {
                _splineRotActive = false;
                Vector3 euler = _splineRotLast.eulerAngles;
                euler = new Vector3(Mathf.DeltaAngle(0f, euler.x), Mathf.DeltaAngle(0f, euler.y), Mathf.DeltaAngle(0f, euler.z));
                if (euler.sqrMagnitude > 1e-4f)
                {
                    TbsSplineEditorState.LastPivot = _splineRotPivot;
                    TbsSplineEditorState.RecordLastRotation(TbsLastOp.RotateSpline, "Rotate Spline", euler, TbsSplineEditorState.SelectedSpline, null);
                }
            }
        }

        Vector3 SplineGizmoPivot(TbsSplineComputer computer)
        {
            if (TbsSplineEditorState.PivotMode == TbsPivotMode.Cursor && TbsSplineEditorState.ObjectCursorValid)
                return TbsSplineEditorState.ObjectCursor;
            return SelectedSplinesMedian(computer);
        }

        static Vector3 SelectedSplinesMedian(TbsSplineComputer computer)
        {
            Transform trs = computer.transform;
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (int id in TbsSplineEditorState.SelectedSplineIds)
            {
                int idx = computer.IndexOfSplineId(id);
                if (idx < 0) continue;
                TbsSpline sp = computer[idx];
                for (int i = 0; i < sp.Count; i++) { sum += trs.TransformPoint(sp[i].Position); n++; }
            }
            if (n == 0)
            {
                int si = TbsSplineEditorState.SelectedSpline;
                return si >= 0 && si < computer.SplineCount ? SplineCenter(computer, si) : trs.position;
            }
            return sum / n;
        }

        void DoSplineMoveGizmo(TbsSplineComputer computer, bool inputBlocked)
        {
            if (!TbsSplineEditorState.HasSplineSelection)
            {
                _moveGizmoDragging = false;
                return;
            }
            Vector3 center = SplineGizmoPivot(computer);
            bool disable = inputBlocked && GUIUtility.hotControl == 0;
            using (new EditorGUI.DisabledScope(disable))
            {
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(center, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    if (!_moveGizmoDragging) { _moveGizmoDragging = true; _moveGizmoOrigin = center; }
                    Vector3 worldDelta = moved - center;
                    if (TbsSplineEditorState.SnapToGrid)
                    {
                        Vector3 snapped = GridSnap(computer, moved);
                        if (Mathf.Abs(moved.x - center.x) > 1e-5f) worldDelta.x = snapped.x - center.x;
                        if (Mathf.Abs(moved.z - center.z) > 1e-5f) worldDelta.z = snapped.z - center.z;
                    }
                    Vector3 localDelta = computer.transform.InverseTransformVector(worldDelta);
                    foreach (int id in TbsSplineEditorState.SelectedSplineIds)
                    {
                        int idx = computer.IndexOfSplineId(id);
                        if (idx >= 0) TbsSplineEditorActions.MoveSpline(computer, idx, localDelta);
                    }
                    if (TbsSplineEditorState.ObjectCursorValid) TbsSplineEditorState.ObjectCursor += worldDelta;
                    SetDragInfo(_moveGizmoOrigin, SplineGizmoPivot(computer));
                    SceneView.RepaintAll();
                }
            }
            if (_moveGizmoDragging && GUIUtility.hotControl == 0)
            {
                _moveGizmoDragging = false;
                TbsSplineEditorState.DragInfoValid = false;
                Vector3 d = SplineGizmoPivot(computer) - _moveGizmoOrigin;
                if (d.sqrMagnitude > 1e-8f)
                    TbsSplineEditorState.RecordLast(TbsLastOp.MoveSpline, "Move Spline", d, TbsSplineEditorState.SelectedSpline, null);
            }
        }

        bool IsSelectedEndpoint(TbsSplineComputer computer)
        {
            if (TbsSplineEditorState.MultiKnots.Count > 1 || !TbsSplineEditorState.HasKnotSelection) return false;
            int si = TbsSplineEditorState.SelectedSpline;
            TbsSpline spline = computer[si];
            return !spline.Closed && spline.IsEndpointIndex(TbsSplineEditorState.SelectedKnot);
        }

        void UpdateGizmoConnectTarget(TbsSplineComputer computer, TbsSplineSceneRenderer renderer)
        {
            if (!IsSelectedEndpoint(computer))
            {
                TbsSplineEditorState.ConnectTargetValid = false;
                return;
            }
            Vector3 tip = computer.transform.TransformPoint(computer[TbsSplineEditorState.SelectedSpline][TbsSplineEditorState.SelectedKnot].Position);
            Vector2 gui = HandleUtility.WorldToGUIPoint(tip);
            UpdateConnectTarget(computer, renderer, gui, TbsSplineEditorState.SelectedSpline);
        }

        void DoTangentMoveGizmo(TbsSplineComputer computer, int splineIndex, int knotIndex, bool inSide, SceneView sceneView, bool inputBlocked)
        {
            TbsSpline spline = computer[splineIndex];
            Transform trs = computer.transform;
            TbsKnot knot = spline[knotIndex];
            Vector3 tip = trs.TransformPoint(inSide ? knot.TangentInPosition : knot.TangentOutPosition);
            float size = HandleUtility.GetHandleSize(tip) * 0.28f * TbsSplineEditorState.HandleSize;
            Event evt = Event.current;

            TangentPlane(computer, splineIndex, knotIndex, inSide, tip, size, Vector3.right, Vector3.forward, new Color(0.4f, 0.66f, 1f), evt, inputBlocked);
            TangentPlane(computer, splineIndex, knotIndex, inSide, tip, size, Vector3.right, Vector3.up, new Color(0.55f, 0.95f, 0.5f), evt, inputBlocked);
            TangentPlane(computer, splineIndex, knotIndex, inSide, tip, size, Vector3.up, Vector3.forward, new Color(1f, 0.5f, 0.5f), evt, inputBlocked);
            TangentFreeMove(computer, splineIndex, knotIndex, inSide, tip, size, sceneView, evt, inputBlocked);

            bool disable = inputBlocked && GUIUtility.hotControl == 0;
            using (new EditorGUI.DisabledScope(disable))
            {
                EditorGUI.BeginChangeCheck();
                Vector3 np = tip;
                Handles.color = new Color(0.95f, 0.4f, 0.4f);
                np = Handles.Slider(np, Vector3.right, size, Handles.ArrowHandleCap, 0f);
                Handles.color = new Color(0.55f, 0.95f, 0.5f);
                np = Handles.Slider(np, Vector3.up, size, Handles.ArrowHandleCap, 0f);
                Handles.color = new Color(0.4f, 0.66f, 1f);
                np = Handles.Slider(np, Vector3.forward, size, Handles.ArrowHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    if (!_handleDragging)
                    {
                        _handleDragging = true;
                        _handleDragOrigin = tip;
                        _handleDragSpline = splineIndex;
                        _handleDragKnot = knotIndex;
                        _handleDragSide = inSide ? 1 : 2;
                    }
                    if (TbsSplineEditorState.SelectedHandle != (inSide ? 1 : 2)) TbsSplineEditorState.SelectHandle(splineIndex, knotIndex, inSide ? 1 : 2);
                    ApplyTangent(computer, splineIndex, knotIndex, inSide, SnapMovedAxes(computer, tip, np));
                }
            }
        }

        void TangentPlane(TbsSplineComputer computer, int si, int ki, bool inSide, Vector3 tip, float size, Vector3 axA, Vector3 axB, Color col, Event evt, bool inputBlocked)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            Vector3 normal = Vector3.Cross(axA, axB).normalized;
            float off = size * 0.34f;
            float sq = size * 0.15f;
            Vector3 center = tip + (axA + axB) * off;
            if (evt.type == EventType.Layout)
            {
                if (!inputBlocked)
                    HandleUtility.AddControl(id, HandleUtility.DistanceToRectangle(center, Quaternion.LookRotation(normal, axA), sq));
            }
            else if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt && !inputBlocked &&
                     HandleUtility.nearestControl == id && _tanDragId == -1 && _drag == DragKind.None)
            {
                _tanDragId = id;
                _tanFree = false;
                _tanDragPlane = new Plane(normal, tip);
                _tanConstrainA = axA;
                _tanConstrainB = axB;
                _tanDragSpline = si;
                _tanDragKnot = ki;
                _tanDragInSide = inSide;
                _tanDragStartTip = tip;
                TbsSplineEditorState.SelectHandle(si, ki, inSide ? 1 : 2);
                _handleDragging = true;
                _handleDragOrigin = tip;
                _handleDragSpline = si;
                _handleDragKnot = ki;
                _handleDragSide = inSide ? 1 : 2;
                Ray ray0 = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                _tanDragHitStart = _tanDragPlane.Raycast(ray0, out float e0) ? ray0.GetPoint(e0) : tip;
                GUIUtility.hotControl = id;
                evt.Use();
            }
            else if (evt.type == EventType.Repaint)
            {
                bool hot = _tanDragId == id;
                Vector3 a = center - axA * sq - axB * sq;
                Vector3 b = center + axA * sq - axB * sq;
                Vector3 c = center + axA * sq + axB * sq;
                Vector3 d = center - axA * sq + axB * sq;
                Handles.DrawSolidRectangleWithOutline(new[] { a, b, c, d },
                    new Color(col.r, col.g, col.b, hot ? 0.4f : 0.22f),
                    hot ? Color.white : new Color(col.r, col.g, col.b, 0.9f));
            }
            TangentDragUpdate(computer, id, evt);
        }

        void TangentFreeMove(TbsSplineComputer computer, int si, int ki, bool inSide, Vector3 tip, float size, SceneView sceneView, Event evt, bool inputBlocked)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            Transform cam = sceneView != null && sceneView.camera != null ? sceneView.camera.transform : null;
            Vector3 normal = cam != null ? cam.forward : Vector3.up;
            Vector3 right = cam != null ? cam.right : Vector3.right;
            Vector3 up = cam != null ? cam.up : Vector3.forward;
            float radius = size * 0.18f;
            if (evt.type == EventType.Layout)
            {
                if (!inputBlocked)
                    HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(tip, radius));
            }
            else if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt && !inputBlocked &&
                     HandleUtility.nearestControl == id && _tanDragId == -1 && _drag == DragKind.None)
            {
                _tanDragId = id;
                _tanFree = true;
                _tanDragPlane = new Plane(normal, tip);
                _tanConstrainA = right;
                _tanConstrainB = up;
                _tanDragSpline = si;
                _tanDragKnot = ki;
                _tanDragInSide = inSide;
                _tanDragStartTip = tip;
                TbsSplineEditorState.SelectHandle(si, ki, inSide ? 1 : 2);
                _handleDragging = true;
                _handleDragOrigin = tip;
                _handleDragSpline = si;
                _handleDragKnot = ki;
                _handleDragSide = inSide ? 1 : 2;
                Ray ray0 = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                _tanDragHitStart = _tanDragPlane.Raycast(ray0, out float e0) ? ray0.GetPoint(e0) : tip;
                GUIUtility.hotControl = id;
                evt.Use();
            }
            else if (evt.type == EventType.Repaint)
            {
                bool hot = _tanDragId == id;
                Handles.color = hot ? Color.white : new Color(1f, 1f, 1f, 0.9f);
                Handles.DrawWireDisc(tip, normal, radius * 0.5f);
                for (int a = 0; a < 4; a++)
                {
                    Vector3 dir = a == 0 ? right : a == 1 ? -right : a == 2 ? up : -up;
                    Vector3 side = a < 2 ? up : right;
                    Vector3 t = tip + dir * radius;
                    Handles.DrawAAPolyLine(2.5f, tip + dir * (radius * 0.55f), t);
                    Handles.DrawAAPolyLine(2f, t, t - dir * (radius * 0.3f) + side * (radius * 0.18f));
                    Handles.DrawAAPolyLine(2f, t, t - dir * (radius * 0.3f) - side * (radius * 0.18f));
                }
                if (hot)
                {
                    Handles.color = new Color(1f, 1f, 1f, 0.18f);
                    Handles.DrawSolidDisc(tip, normal, radius * 0.5f);
                }
            }
            TangentDragUpdate(computer, id, evt);
        }

        void TangentDragUpdate(TbsSplineComputer computer, int id, Event evt)
        {
            if (_tanDragId != id) return;
            if (evt.rawType == EventType.MouseDrag && evt.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                if (_tanDragPlane.Raycast(ray, out float ent))
                {
                    Vector3 delta = ray.GetPoint(ent) - _tanDragHitStart;
                    Vector3 worldTip = _tanDragStartTip
                        + _tanConstrainA * Vector3.Dot(delta, _tanConstrainA)
                        + _tanConstrainB * Vector3.Dot(delta, _tanConstrainB);
                    if (TbsSplineEditorState.SnapToGrid)
                    {
                        float step = computer.EditorGridSize;
                        if (_tanFree)
                        {
                            worldTip.x = SnapValue(worldTip.x, step);
                            worldTip.y = SnapValue(worldTip.y, step);
                            worldTip.z = SnapValue(worldTip.z, step);
                        }
                        else
                        {
                            worldTip = SnapAlong(worldTip, _tanConstrainA, step);
                            worldTip = SnapAlong(worldTip, _tanConstrainB, step);
                        }
                    }
                    ApplyTangent(computer, _tanDragSpline, _tanDragKnot, _tanDragInSide, worldTip);
                    SceneView.RepaintAll();
                }
                evt.Use();
            }
            else if (evt.rawType == EventType.MouseUp && evt.button == 0)
            {
                _tanDragId = -1;
                GUIUtility.hotControl = 0;
                evt.Use();
            }
            else if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                Undo.RevertAllInCurrentGroup();
                _tanDragId = -1;
                _handleDragging = false;
                GUIUtility.hotControl = 0;
                TbsSplineSceneRenderer.Get(computer).SetDirty();
                evt.Use();
                SceneView.RepaintAll();
            }
        }

        Vector3 SnapMovedAxes(TbsSplineComputer computer, Vector3 from, Vector3 to)
        {
            if (!TbsSplineEditorState.SnapToGrid) return to;
            float step = computer.EditorGridSize;
            Vector3 d = to - from;
            if (Mathf.Abs(d.x) > 1e-6f) to.x = SnapValue(to.x, step);
            if (Mathf.Abs(d.y) > 1e-6f) to.y = SnapValue(to.y, step);
            if (Mathf.Abs(d.z) > 1e-6f) to.z = SnapValue(to.z, step);
            return to;
        }

        static float SnapValue(float v, float step) => step > 0f ? Mathf.Round(v / step) * step : v;

        static Vector3 SnapAlong(Vector3 p, Vector3 axis, float step)
        {
            if (step <= 0f) return p;
            float d = Vector3.Dot(p, axis);
            return p + axis * (Mathf.Round(d / step) * step - d);
        }

        void ApplyTangent(TbsSplineComputer computer, int si, int ki, bool inSide, Vector3 worldTip)
        {
            if (si < 0 || si >= computer.SplineCount) return;
            TbsSpline spline = computer[si];
            if (ki < 0 || ki >= spline.Count) return;
            Transform trs = computer.transform;
            TbsSplineEditorActions.RecordChange(computer, "Edit Tangent");
            TbsKnot k = spline[ki];
            if (k.Mode == TbsTangentMode.AutoSmooth) { k.Mode = TbsTangentMode.Broken; spline.SetKnot(ki, k); k = spline[ki]; }
            Vector3 local = Quaternion.Inverse(k.Rotation) * (trs.InverseTransformPoint(worldTip) - k.Position);
            if (inSide) spline.SetTangentIn(ki, local); else spline.SetTangentOut(ki, local);
            TbsSplineEditorActions.MarkChanged(computer);
            TbsSplineSceneRenderer.Get(computer).SetDirty();
        }

        void FinishHandleDrag(TbsSplineComputer computer)
        {
            if (!_handleDragging || GUIUtility.hotControl != 0) return;
            _handleDragging = false;
            if (computer == null || _handleDragSpline < 0 || _handleDragSpline >= computer.SplineCount) return;
            TbsSpline spline = computer[_handleDragSpline];
            if (_handleDragKnot < 0 || _handleDragKnot >= spline.Count) return;
            TbsKnot k = spline[_handleDragKnot];
            bool inSide = _handleDragSide == 1;
            Vector3 tip = computer.transform.TransformPoint(inSide ? k.TangentInPosition : k.TangentOutPosition);
            Vector3 d = tip - _handleDragOrigin;
            if (d.sqrMagnitude > 1e-8f)
            {
                TbsSplineEditorState.RecordLast(TbsLastOp.MoveHandle, "Move Handle", d, _handleDragSpline, new List<int> { k.Id });
                TbsSplineEditorState.LastHandleSide = _handleDragSide;
            }
        }

        Rect MarqueeRect(Vector2 current)
        {
            float x = Mathf.Min(_marqueeStart.x, current.x);
            float y = Mathf.Min(_marqueeStart.y, current.y);
            return new Rect(x, y, Mathf.Abs(current.x - _marqueeStart.x), Mathf.Abs(current.y - _marqueeStart.y));
        }

        void ApplyMarquee(TbsSplineComputer computer, Event evt)
        {
            Rect r = MarqueeRect(evt.mousePosition);
            if (r.width < 4f && r.height < 4f)
            {
                if (TbsSplineEditorState.HasKnotSelection) TbsSplineEditorState.ClearKnot();
                else TbsSplineEditorState.ClearSelection();
                return;
            }
            if (!TbsSplineEditorState.HasSplineSelection) return;
            int splineIndex = TbsSplineEditorState.SelectedSpline;
            TbsSpline spline = computer[splineIndex];
            Transform trs = computer.transform;
            Camera camera = Camera.current;
            var ids = new List<int>();
            for (int i = 0; i < spline.Count; i++)
            {
                Vector3 world = trs.TransformPoint(spline[i].Position);
                if (camera != null && camera.WorldToViewportPoint(world).z <= 0f) continue;
                Vector2 gui = HandleUtility.WorldToGUIPoint(world);
                if (r.Contains(gui)) ids.Add(spline[i].Id);
            }
            if (ids.Count > 0) TbsSplineEditorState.SetMultiSelection(splineIndex, ids);
            else TbsSplineEditorState.ClearKnot();
        }

        void DrawSplineMoveGizmo(TbsSplineComputer computer, int splineIndex)
        {
            Vector3 c = SplineCenter(computer, splineIndex);
            float s = HandleUtility.GetHandleSize(c) * 0.18f;
            Handles.color = new Color(1f, 0.72f, 0.3f, 0.9f);
            Handles.DrawWireDisc(c, Vector3.up, s * 0.5f);
            for (int a = 0; a < 4; a++)
            {
                Vector3 dir = a switch { 0 => Vector3.right, 1 => Vector3.left, 2 => Vector3.forward, _ => Vector3.back };
                Vector3 side = new Vector3(-dir.z, 0f, dir.x);
                Vector3 tip = c + dir * s;
                Handles.DrawAAPolyLine(2.5f, c + dir * (s * 0.55f), tip);
                Handles.DrawAAPolyLine(2f, tip, tip - dir * (s * 0.3f) + side * (s * 0.18f));
                Handles.DrawAAPolyLine(2f, tip, tip - dir * (s * 0.3f) - side * (s * 0.18f));
            }
        }

        internal static float GroundY(TbsSplineComputer computer) => computer.EditorGridHeight;

        static Vector3 GridSnap(TbsSplineComputer computer, Vector3 position)
        {
            float step = computer.EditorGridSize;
            position.x = Mathf.Round(position.x / step) * step;
            position.z = Mathf.Round(position.z / step) * step;
            return position;
        }

        static Vector3 SnapPosition(Vector3 position)
        {
            Vector3 grid = EditorSnapSettings.move;
            if (grid.x > 0f) position.x = Mathf.Round(position.x / grid.x) * grid.x;
            if (grid.y > 0f) position.y = Mathf.Round(position.y / grid.y) * grid.y;
            if (grid.z > 0f) position.z = Mathf.Round(position.z / grid.z) * grid.z;
            return position;
        }

        static void SetDragLabel(Vector3 world, string text)
        {
            TbsSplineEditorState.DragLabelValid = true;
            TbsSplineEditorState.DragLabelWorld = world;
            TbsSplineEditorState.DragLabel = text;
        }

        static void CleanupOrphanSplines(TbsSplineComputer computer)
        {
            for (int i = computer.SplineCount - 1; i >= 0; i--)
            {
                if (computer[i].Count >= 2) continue;
                if (computer.SplineCount <= 1) break;
                TbsSplineEditorActions.RecordChange(computer, "Remove Empty Spline");
                computer.RemoveSplineAt(i);
                TbsSplineEditorActions.MarkChanged(computer);
            }
        }

        void HandleCommands(TbsSplineComputer computer, Event evt)
        {
            if (evt.type != EventType.ValidateCommand && evt.type != EventType.ExecuteCommand) return;
            if (evt.commandName == "Duplicate")
            {
                if (!TbsSplineEditorState.EditModeActive) return;
                if (!TbsSplineEditorState.HasKnotSelection && TbsSplineEditorState.MultiKnots.Count == 0) return;
                if (evt.type == EventType.ExecuteCommand) TbsSplineEditorActions.DuplicateSelectedKnotsInPlace(computer);
                evt.Use();
                return;
            }
            if (evt.commandName != "SoftDelete" && evt.commandName != "Delete") return;
            if (evt.type == EventType.ExecuteCommand) DeleteSelection(computer);
            evt.Use();
        }

        void HandleRightClick(TbsSplineComputer computer, TbsSplineSceneRenderer renderer, Event evt, bool inputBlocked)
        {
            if (evt.alt || _drag != DragKind.None) return;
            if (evt.type == EventType.MouseDown && evt.button == 1)
            {
                _rmbDown = evt.mousePosition;
                _rmbMoved = false;
            }
            else if (evt.type == EventType.MouseDrag && evt.button == 1)
            {
                if ((evt.mousePosition - _rmbDown).sqrMagnitude > 16f) _rmbMoved = true;
            }
            else if (evt.type == EventType.MouseUp && evt.button == 1 && !_rmbMoved && !inputBlocked)
            {
                if (TbsSplineEditorState.DrawMode) TbsSplineEditorActions.FinishDraw(computer);
                else HandleContextClick(computer, renderer, evt);
            }
        }

        static readonly TbsSpline _previewSpline = new TbsSpline();
        static readonly List<Vector3> _previewLines = new List<Vector3>();
        static float _dashPhase;

        static void BeginDashes() => _dashPhase = 0f;

        static void SkipDashPhase(float length)
        {
            _dashPhase += length;
            if (_dashPhase > 1e5f) _dashPhase = 0f;
        }

        static bool ClipParam(float p, float q, ref float s0, ref float s1)
        {
            if (Mathf.Abs(p) < 1e-9f) return q >= 0f;
            float r = q / p;
            if (p < 0f)
            {
                if (r > s1) return false;
                if (r > s0) s0 = r;
            }
            else
            {
                if (r < s0) return false;
                if (r < s1) s1 = r;
            }
            return true;
        }

        static float ScreenToWorldClipParam(float s, float wA, float wB)
        {
            float d = s * wA + (1f - s) * wB;
            return d > 1e-9f ? s * wA / d : s;
        }

        static void DrawDashedSegment(Vector3 a, Vector3 b, float width)
        {
            float length = Vector3.Distance(a, b);
            if (length < 1e-6f) return;
            float t0 = 0f, t1 = 1f;
            float da = 0f, db = 0f;
            Camera cam = Camera.current;
            if (cam != null)
            {
                Vector3 cp = cam.transform.position;
                Vector3 cf = cam.transform.forward;
                da = Vector3.Dot(a - cp, cf);
                db = Vector3.Dot(b - cp, cf);
                const float near = 0.01f;
                if (da <= near && db <= near)
                {
                    SkipDashPhase(length);
                    return;
                }
                if (da < near) t0 = (near - da) / (db - da);
                else if (db < near) t1 = (near - da) / (db - da);
            }
            Vector2 ga = HandleUtility.WorldToGUIPoint(Vector3.LerpUnclamped(a, b, t0));
            Vector2 gb = HandleUtility.WorldToGUIPoint(Vector3.LerpUnclamped(a, b, t1));
            float viewW = cam != null ? cam.pixelWidth / EditorGUIUtility.pixelsPerPoint : 4000f;
            float viewH = cam != null ? cam.pixelHeight / EditorGUIUtility.pixelsPerPoint : 4000f;
            const float margin = 40f;
            float s0 = 0f, s1 = 1f;
            float dx = gb.x - ga.x, dy = gb.y - ga.y;
            if (!ClipParam(-dx, ga.x + margin, ref s0, ref s1) ||
                !ClipParam(dx, viewW + margin - ga.x, ref s0, ref s1) ||
                !ClipParam(-dy, ga.y + margin, ref s0, ref s1) ||
                !ClipParam(dy, viewH + margin - ga.y, ref s0, ref s1))
            {
                SkipDashPhase(length);
                return;
            }
            float u0 = s0, u1 = s1;
            if (cam != null && !cam.orthographic)
            {
                float wA = Mathf.LerpUnclamped(da, db, t0);
                float wB = Mathf.LerpUnclamped(da, db, t1);
                u0 = ScreenToWorldClipParam(s0, wA, wB);
                u1 = ScreenToWorldClipParam(s1, wA, wB);
            }
            float tA = Mathf.LerpUnclamped(t0, t1, u0);
            float tB = Mathf.LerpUnclamped(t0, t1, u1);
            float visWorld = length * (tB - tA);
            float visGui = Vector2.Distance(Vector2.LerpUnclamped(ga, gb, s0), Vector2.LerpUnclamped(ga, gb, s1));
            if (visWorld < 1e-6f || visGui < 1f)
            {
                SkipDashPhase(length);
                return;
            }
            float worldPerPixel = visWorld / visGui;
            float dash = Mathf.Max((6f + width * 3f) * worldPerPixel, 1e-6f);
            float period = dash * 1.6f;
            Vector3 dir = (b - a) / length;
            Vector3 start = Vector3.LerpUnclamped(a, b, tA);
            float phase0 = Mathf.Repeat(_dashPhase + length * tA, period);
            float t = 0f;
            int guard = 0;
            while (t < visWorld && guard++ < 4096)
            {
                float p = Mathf.Repeat(phase0 + t, period);
                if (p < dash)
                {
                    float run = Mathf.Min(dash - p, visWorld - t);
                    Handles.DrawAAPolyLine(width, start + dir * t, start + dir * (t + run));
                    t += Mathf.Max(run, period * 1e-3f);
                }
                else
                {
                    t += Mathf.Max(Mathf.Min(period - p, visWorld - t), period * 1e-3f);
                }
            }
            SkipDashPhase(length);
        }

        static void CopyPreview(TbsSpline source)
        {
            while (_previewSpline.Count > 0) _previewSpline.RemoveKnotAt(_previewSpline.Count - 1);
            _previewSpline.Closed = false;
            _previewSpline.Type = source.Type;
            _previewSpline.KnotParametrization = source.KnotParametrization;
            _previewSpline.LinearAverageDirection = source.LinearAverageDirection;
            for (int i = 0; i < source.Count; i++) _previewSpline.AddKnot(source[i]);
            _previewSpline.Closed = source.Closed;
            _previewSpline.OnExternalMutation();
        }

        static int SplitPreviewSegment(int segment, float t)
        {
            if (segment < 0 || segment >= _previewSpline.SegmentCount) return -1;
            t = Mathf.Clamp01(t);
            TbsCurve curve = _previewSpline.GetCurve(segment);
            curve.Split(t, out TbsCurve left, out TbsCurve right);
            int startIndex = segment;
            int endIndex = (segment + 1) % _previewSpline.Count;
            TbsKnot start = _previewSpline[startIndex];
            if (start.Mode != TbsTangentMode.Linear && start.Mode != TbsTangentMode.Broken) start.Mode = TbsTangentMode.Broken;
            start.TangentOut = Quaternion.Inverse(start.Rotation) * (left.P1 - left.P0);
            _previewSpline.SetKnot(startIndex, start);
            TbsKnot end = _previewSpline[endIndex];
            if (end.Mode != TbsTangentMode.Linear && end.Mode != TbsTangentMode.Broken) end.Mode = TbsTangentMode.Broken;
            end.TangentIn = Quaternion.Inverse(end.Rotation) * (right.P2 - right.P3);
            _previewSpline.SetKnot(endIndex, end);
            var middle = new TbsKnot(left.P3, left.P2 - left.P3, right.P1 - right.P0, Quaternion.identity, TbsTangentMode.Broken);
            _previewSpline.InsertKnot(segment + 1, middle);
            return segment + 1;
        }

        static void DrawSplinePreview(TbsSplineComputer computer)
        {
            _previewLines.Clear();
            Transform trs = computer.transform;
            int segs = _previewSpline.SegmentCount;
            const int steps = 16;
            for (int s = 0; s < segs; s++)
            {
                TbsCurve curve = _previewSpline.GetCurve(s);
                Vector3 prev = trs.TransformPoint(curve.EvaluatePosition(0f));
                for (int i = 1; i <= steps; i++)
                {
                    Vector3 next = trs.TransformPoint(curve.EvaluatePosition(i / (float)steps));
                    _previewLines.Add(prev);
                    _previewLines.Add(next);
                    prev = next;
                }
            }
            if (_previewLines.Count == 0) return;
            Handles.color = TbsSplineEditorState.PreviewLineColor;
            float width = TbsSplineEditorState.PreviewLineWidth;
            BeginDashes();
            for (int i = 0; i + 1 < _previewLines.Count; i += 2)
                DrawDashedSegment(_previewLines[i], _previewLines[i + 1], width);
        }

        static void DrawAffectedMark(Vector3 world)
        {
            float s = HandleUtility.GetHandleSize(world) * 0.11f;
            Vector3 face = Camera.current != null ? -Camera.current.transform.forward : Vector3.up;
            Handles.color = new Color(1f, 0.82f, 0.25f, 0.95f);
            Handles.DrawWireDisc(world, face, s);
            Handles.color = new Color(1f, 0.82f, 0.25f, 0.35f);
            Handles.DrawSolidDisc(world, face, s * 0.55f);
        }

        static void DrawRemoveMark(Vector3 world)
        {
            float s = HandleUtility.GetHandleSize(world) * 0.14f;
            Camera cam = Camera.current;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            Vector3 up = cam != null ? cam.transform.up : Vector3.forward;
            Vector3 d1 = (right + up).normalized * s;
            Vector3 d2 = (right - up).normalized * s;
            Handles.color = new Color(1f, 0.32f, 0.28f, 0.95f);
            Handles.DrawAAPolyLine(3f, world - d1, world + d1);
            Handles.DrawAAPolyLine(3f, world - d2, world + d2);
        }

        static bool OnScreen(Vector3 world, SceneView sceneView)
        {
            const float margin = 40f;
            Vector2 gui = HandleUtility.WorldToGUIPoint(world);
            return gui.x >= -margin && gui.y >= -margin &&
                   gui.x <= sceneView.position.width + margin &&
                   gui.y <= sceneView.position.height + margin;
        }

        void DoDeleteMode(TbsSplineComputer computer, TbsSplineSceneRenderer renderer, Event evt, SceneView sceneView, int si, int defaultControl)
        {
            TbsSpline spline = computer[si];
            if (!renderer.FindNearestKnot(evt.mousePosition, si, float.MaxValue, out int kIdx)) return;
            Transform trs = computer.transform;
            Vector3 world = trs.TransformPoint(spline[kIdx].Position);
            if (!OnScreen(world, sceneView)) return;
            TbsSplineEditorState.SetActionKnots(si, DeleteHighlightColor, kIdx, -1);
            if (evt.type == EventType.Repaint)
            {
                CopyPreview(spline);
                _previewSpline.RemoveKnotAt(kIdx);
                DrawSplinePreview(computer);
                DrawRemoveMark(world);
            }
            if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt && HandleUtility.nearestControl == defaultControl)
            {
                TbsSplineEditorActions.DeleteKnot(computer, si, kIdx);
                renderer.SetDirty();
                evt.Use();
            }
        }

        void DoMergeMode(TbsSplineComputer computer, TbsSplineSceneRenderer renderer, Event evt, SceneView sceneView, int si, int defaultControl)
        {
            TbsSpline spline = computer[si];
            if (!TbsSplineEditorActions.CanMergeKnots(spline)) return;
            if (!renderer.FindNearestKnot(evt.mousePosition, si, float.MaxValue, out int kIdx)) return;
            int partner = FindMergePartner(computer, spline, kIdx, evt.mousePosition);
            if (partner < 0) return;
            Transform trs = computer.transform;
            Vector3 worldA = trs.TransformPoint(spline[kIdx].Position);
            Vector3 worldB = trs.TransformPoint(spline[partner].Position);
            if (!OnScreen(worldA, sceneView)) return;
            Vector3 mid = (worldA + worldB) * 0.5f;
            Plane plane = MakeDragPlane(mid, sceneView);
            Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            Vector3 cursor = plane.Raycast(ray, out float enter) && enter <= 100000f ? ray.GetPoint(enter) : mid;
            TbsSplineEditorState.SetActionKnots(si, MergeHighlightColor, kIdx, partner);
            TbsSplineEditorState.GhostValid = true;
            TbsSplineEditorState.GhostPoint = cursor;
            if (evt.type == EventType.Repaint)
            {
                CopyPreview(spline);
                TbsKnot kept = _previewSpline[kIdx];
                kept.Position = trs.InverseTransformPoint(cursor);
                kept.Size = (kept.Size + _previewSpline[partner].Size) * 0.5f;
                kept.Color = Color.Lerp(kept.Color, _previewSpline[partner].Color, 0.5f);
                _previewSpline.SetKnot(kIdx, kept);
                _previewSpline.RemoveKnotAt(partner);
                DrawSplinePreview(computer);
                float width = TbsSplineEditorState.PreviewLineWidth;
                Handles.color = new Color(MergeHighlightColor.r, MergeHighlightColor.g, MergeHighlightColor.b, 0.8f);
                BeginDashes();
                DrawDashedSegment(worldA, cursor, width);
                BeginDashes();
                DrawDashedSegment(worldB, cursor, width);
            }
            if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt && HandleUtility.nearestControl == defaultControl)
            {
                TbsSplineEditorActions.MergeKnots(computer, si, kIdx, partner, cursor, out int mergedIndex);
                if (mergedIndex >= 0) TbsSplineEditorState.SelectKnot(si, mergedIndex);
                renderer.SetDirty();
                evt.Use();
            }
        }

        static int FindMergePartner(TbsSplineComputer computer, TbsSpline spline, int index, Vector2 gui)
        {
            int prev = spline.Closed ? (index - 1 + spline.Count) % spline.Count : index - 1;
            int next = spline.Closed ? (index + 1) % spline.Count : index + 1;
            bool hasPrev = prev >= 0 && prev < spline.Count && prev != index;
            bool hasNext = next >= 0 && next < spline.Count && next != index;
            if (!hasPrev && !hasNext) return -1;
            Transform trs = computer.transform;
            float dPrev = hasPrev ? GuiKnotDistance(trs, spline, prev, gui) : float.MaxValue;
            float dNext = hasNext ? GuiKnotDistance(trs, spline, next, gui) : float.MaxValue;
            if (dPrev == float.MaxValue && dNext == float.MaxValue) return -1;
            return dPrev <= dNext ? prev : next;
        }

        static float GuiKnotDistance(Transform trs, TbsSpline spline, int index, Vector2 gui)
        {
            Vector3 world = trs.TransformPoint(spline[index].Position);
            Camera cam = Camera.current;
            if (cam != null && Vector3.Dot(world - cam.transform.position, cam.transform.forward) <= 0f) return float.MaxValue;
            return Vector2.Distance(HandleUtility.WorldToGUIPoint(world), gui);
        }

        void DoTriggerHandles(TbsSplineComputer computer, TbsSplineSceneRenderer renderer, Event evt, bool inputBlocked)
        {
            if (!TbsSplineEditorState.HasSplineSelection || !TbsSplineEditorState.EditModeActive) return;
            int si = TbsSplineEditorState.SelectedSpline;
            TbsSpline spline = computer[si];
            var groups = spline.TriggerGroups;
            if (groups.Count == 0) return;
            TbsSplineCache cache = computer.GetCache(si);
            Transform trs = computer.transform;
            TbsSample sample = default;
            for (int g = 0; g < groups.Count; g++)
            {
                var triggers = groups[g].Triggers;
                for (int t = 0; t < triggers.Count; t++)
                {
                    TbsSplineTrigger trigger = triggers[t];
                    cache.EvaluateAtT(trigger.Position, ref sample);
                    Vector3 world = trs.TransformPoint(sample.Position);
                    Vector3 tangent = trs.TransformDirection(sample.Tangent).normalized;
                    float size = HandleUtility.GetHandleSize(world) * 0.14f;
                    int id = GUIUtility.GetControlID(FocusType.Passive);
                    switch (evt.GetTypeForControl(id))
                    {
                        case EventType.Layout:
                            if (!inputBlocked) HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(world, size * 1.6f));
                            break;
                        case EventType.MouseDown:
                            if (!inputBlocked && evt.button == 0 && !evt.alt && HandleUtility.nearestControl == id &&
                                _drag == DragKind.None && GUIUtility.hotControl == 0)
                            {
                                GUIUtility.hotControl = id;
                                evt.Use();
                            }
                            break;
                        case EventType.MouseDrag:
                            if (GUIUtility.hotControl == id)
                            {
                                if (renderer.HitTest(evt.mousePosition, 100000f, out int hs, out _, out _, out Vector3 hitWorld) && hs == si)
                                {
                                    TbsSample near = default;
                                    float nt = computer.GetNearestPoint(si, hitWorld, ref near);
                                    TbsSplineEditorActions.SetTriggerPosition(computer, si, g, t, nt);
                                }
                                evt.Use();
                            }
                            break;
                        case EventType.MouseUp:
                            if (GUIUtility.hotControl == id)
                            {
                                GUIUtility.hotControl = 0;
                                evt.Use();
                            }
                            break;
                        case EventType.Repaint:
                        {
                            bool hot = GUIUtility.hotControl == id;
                            bool hover = HandleUtility.nearestControl == id && GUIUtility.hotControl == 0;
                            Vector3 face = Camera.current != null ? -Camera.current.transform.forward : Vector3.up;
                            Handles.color = new Color(0.07f, 0.08f, 0.1f, 0.9f);
                            Handles.DrawSolidDisc(world, face, size * (hot || hover ? 1.5f : 1.25f));
                            Handles.color = groups[g].Enabled ? trigger.Color : new Color(0.5f, 0.5f, 0.5f);
                            Handles.DrawSolidDisc(world, face, size * (hot || hover ? 1.15f : 0.9f));
                            Handles.color = Color.white;
                            if (trigger.Type != TbsTriggerType.Backward)
                                Handles.DrawAAPolyLine(2.5f, world + tangent * size * 1.8f, world + tangent * size * 3.2f);
                            if (trigger.Type != TbsTriggerType.Forward)
                                Handles.DrawAAPolyLine(2.5f, world - tangent * size * 1.8f, world - tangent * size * 3.2f);
                            break;
                        }
                    }
                }
            }
        }

        void DeleteSelection(TbsSplineComputer computer)
        {
            if (_drag != DragKind.None || GUIUtility.hotControl != 0) return;
            if (EditorGUIUtility.editingTextField) return;
            if (TbsSplineEditorState.MultiKnots.Count > 1)
                TbsSplineEditorActions.DeleteSelectedKnots(computer);
            else if (TbsSplineEditorState.HasKnotSelection)
                TbsSplineEditorActions.DeleteKnot(computer, TbsSplineEditorState.SelectedSpline, TbsSplineEditorState.SelectedKnot);
        }

        void UpdateHover(TbsSplineSceneRenderer renderer, Event evt, bool inputBlocked)
        {
            if (evt.type != EventType.MouseMove) return;
            if (_drag != DragKind.None || GUIUtility.hotControl != 0) return;
            if (inputBlocked)
            {
                TbsSplineEditorState.ClearHover();
                return;
            }
            if (renderer.HitTest(evt.mousePosition, CurvePickPixels, out int spline, out int segment, out float t, out Vector3 point))
                TbsSplineEditorState.SetHover(spline, segment, t, point);
            else
                TbsSplineEditorState.ClearHover();
        }

        void DrawCurves(TbsSplineComputer computer, TbsSplineSceneRenderer renderer, Event evt)
        {
            if (evt.type != EventType.Repaint) return;
            if (TbsSplineEditorState.ShowGrid) DrawGrid(computer);
            renderer.DrawIdle();
            float lw = TbsSplineEditorState.LineWidth;
            Color selCol = TbsSplineEditorState.SelectedCurveColor;
            if (TbsSplineEditorState.HoverValid && TbsSplineEditorState.HoverSpline != TbsSplineEditorState.SelectedSpline)
                renderer.DrawSplineHighlight(TbsSplineEditorState.HoverSpline, TbsSplineEditorState.HoverCurveColor, 4f * lw);
            if (TbsSplineEditorState.SelectedSplineIds.Count > 0)
            {
                foreach (int id in TbsSplineEditorState.SelectedSplineIds)
                {
                    int hidx = computer.IndexOfSplineId(id);
                    if (hidx >= 0) renderer.DrawSplineHighlight(hidx, selCol, 5.5f * lw);
                }
            }
            else if (TbsSplineEditorState.HasSplineSelection)
                renderer.DrawSplineHighlight(TbsSplineEditorState.SelectedSpline, selCol, 5.5f * lw);
            if (computer.EditorShowHeightGuides || computer.EditorRenderAll) DrawHeightGuides(computer);
            if (TbsSplineEditorState.HasSplineSelection)
            {
                DrawDirectionChevrons(computer, TbsSplineEditorState.SelectedSpline);
                if (!TbsSplineEditorState.HasKnotSelection && !TbsSplineEditorState.ObjectModeActive)
                    DrawSplineMoveGizmo(computer, TbsSplineEditorState.SelectedSpline);
            }
            DrawSelectedKnotLines(computer);
            DrawHoverCursor(computer);
            DrawConnectTarget(computer);
            DrawDragGuides(computer);
        }

        void DrawHeightGuides(TbsSplineComputer computer)
        {
            if (computer.EditorRenderAll)
            {
                for (int s = 0; s < computer.SplineCount; s++) DrawSplineHeightGuides(computer, s);
                return;
            }
            DrawSplineHeightGuides(computer, TbsSplineEditorState.SelectedSpline);
            if (TbsSplineEditorState.HoverValid && TbsSplineEditorState.HoverSpline != TbsSplineEditorState.SelectedSpline)
                DrawSplineHeightGuides(computer, TbsSplineEditorState.HoverSpline);
        }

        void DrawSplineHeightGuides(TbsSplineComputer computer, int splineIndex)
        {
            if (splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            Transform trs = computer.transform;
            TbsSpline spline = computer[splineIndex];
            float y = computer.EditorGridHeight;
            Handles.color = new Color(0.55f, 0.75f, 1f, 0.32f);
            for (int i = 0; i < spline.Count; i++)
            {
                Vector3 world = trs.TransformPoint(spline[i].Position);
                if (Mathf.Abs(world.y - y) < 1e-4f) continue;
                Vector3 ground = new Vector3(world.x, y, world.z);
                Handles.DrawDottedLine(world, ground, 3f);
            }
        }

        void DrawDirectionChevrons(TbsSplineComputer computer, int splineIndex)
        {
            TbsSplineCache cache = computer.GetCache(splineIndex);
            float length = cache.TotalLength;
            if (length < 0.5f) return;
            Transform trs = computer.transform;
            int count = Mathf.Clamp(Mathf.RoundToInt(length / 2.5f), 1, 40);
            Handles.color = new Color(1f, 0.82f, 0.4f, 0.9f);
            TbsSample sample = default;
            for (int i = 0; i < count; i++)
            {
                float d = length * (i + 0.5f) / count;
                cache.EvaluateAtDistance(d, ref sample);
                Vector3 p = trs.TransformPoint(sample.Position);
                Vector3 fwd = trs.TransformDirection(sample.Tangent).normalized;
                Vector3 up = TbsSplineMath.OrthonormalUp(fwd, trs.TransformDirection(sample.Up));
                Vector3 side = Vector3.Cross(up, fwd).normalized;
                float sz = HandleUtility.GetHandleSize(p) * 0.09f;
                Vector3 tip = p + fwd * sz;
                Handles.DrawAAPolyLine(2.5f, tip, p - fwd * sz + side * sz);
                Handles.DrawAAPolyLine(2.5f, tip, p - fwd * sz - side * sz);
            }
        }

        void DrawHoverCursor(TbsSplineComputer computer)
        {
            if (!TbsSplineEditorState.HoverValid || _drag != DragKind.None || TbsSplineEditorState.DrawMode) return;
            int splineIndex = TbsSplineEditorState.HoverSpline;
            if (splineIndex < 0 || splineIndex >= computer.SplineCount) return;
            TbsSample sample = default;
            computer.GetCache(splineIndex).EvaluateSegment(TbsSplineEditorState.HoverSegment, TbsSplineEditorState.HoverT, ref sample);
            Transform trs = computer.transform;
            Vector3 world = trs.TransformPoint(sample.Position);
            Vector3 tangent = trs.TransformDirection(sample.Tangent).normalized;
            Vector3 up = TbsSplineMath.OrthonormalUp(tangent, trs.TransformDirection(sample.Up));
            float size = HandleUtility.GetHandleSize(world) * 0.16f;
            Handles.color = HoverColor;
            Handles.DrawWireDisc(world, tangent, size);
            Handles.DrawAAPolyLine(3f, world, world + tangent * size * 2.2f);
        }

        void DrawConnectTarget(TbsSplineComputer computer)
        {
            if (!TbsSplineEditorState.ConnectTargetValid) return;
            Vector3 face = Camera.current != null ? Camera.current.transform.forward : Vector3.up;
            int ts = TbsSplineEditorState.ConnectTargetSpline;
            if (ts >= 0 && ts < computer.SplineCount)
            {
                Transform trs = computer.transform;
                TbsSpline targetSpline = computer[ts];
                Handles.color = new Color(0.32f, 0.78f, 1f, 0.85f);
                for (int i = 0; i < targetSpline.Count; i++)
                {
                    Vector3 kw = trs.TransformPoint(targetSpline[i].Position);
                    if (Camera.current != null && Camera.current.WorldToViewportPoint(kw).z <= 0f) continue;
                    Handles.DrawSolidDisc(kw, face, HandleUtility.GetHandleSize(kw) * 0.09f);
                }
            }
            Vector3 world = TbsSplineEditorState.ConnectTargetWorld;
            float size = HandleUtility.GetHandleSize(world) * 0.22f;
            Handles.color = new Color(0.32f, 0.78f, 1f, 0.95f);
            Handles.DrawWireDisc(world, face, size);
            Handles.DrawWireDisc(world, face, size * 0.6f);
        }

        void DrawGrid(TbsSplineComputer computer)
        {
            float y = computer.EditorGridHeight;
            float step = computer.EditorGridSize;
            SceneView view = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView : SceneView.lastActiveSceneView;
            Vector3 pivot = view != null ? view.pivot : computer.transform.position;
            float reach = view != null ? Mathf.Clamp(view.size * 2.4f, 20f, 6000f) : 100f;
            while (reach / step > 140f) step *= 2f;
            int half = Mathf.Clamp(Mathf.CeilToInt(reach / step), 8, 160);
            float cx = Mathf.Round(pivot.x / step) * step;
            float cz = Mathf.Round(pivot.z / step) * step;
            float span = half * step;
            for (int i = -half; i <= half; i++)
            {
                float fade = 1f - Mathf.Abs(i) / (float)half;
                fade = fade * fade;
                float a = fade * (i % 10 == 0 ? 0.28f : 0.09f);
                if (a < 0.01f) continue;
                Handles.color = i % 10 == 0 ? new Color(0.53f, 0.78f, 1f, a) : new Color(1f, 1f, 1f, a);
                float gx = cx + i * step;
                Handles.DrawLine(new Vector3(gx, y, cz - span), new Vector3(gx, y, cz + span));
                float gz = cz + i * step;
                Handles.DrawLine(new Vector3(cx - span, y, gz), new Vector3(cx + span, y, gz));
            }
        }

        void DrawSelectedKnotLines(TbsSplineComputer computer)
        {
            if (!TbsSplineEditorState.HasKnotSelection || !TbsSplineEditorState.EditModeActive) return;
            TbsSpline spline = computer[TbsSplineEditorState.SelectedSpline];
            TbsKnot knot = spline[TbsSplineEditorState.SelectedKnot];
            Transform trs = computer.transform;
            Vector3 world = trs.TransformPoint(knot.Position);
            if (ShowTangents(spline, knot))
            {
                Vector3 inTip = trs.TransformPoint(knot.TangentInPosition);
                Vector3 outTip = trs.TransformPoint(knot.TangentOutPosition);
                Handles.color = TangentLineColor;
                Handles.DrawDottedLine(world, inTip, 4f);
                Handles.DrawDottedLine(world, outTip, 4f);
                if (computer.EditorShowHeightGuides || computer.EditorRenderAll)
                {
                    float gy = computer.EditorGridHeight;
                    Handles.color = new Color(0.55f, 0.75f, 1f, 0.32f);
                    if (Mathf.Abs(inTip.y - gy) > 1e-4f) Handles.DrawDottedLine(inTip, new Vector3(inTip.x, gy, inTip.z), 3f);
                    if (Mathf.Abs(outTip.y - gy) > 1e-4f) Handles.DrawDottedLine(outTip, new Vector3(outTip.x, gy, outTip.z), 3f);
                }
            }
            if (TbsSplineEditorState.RotateMode && TbsSplineEditorActions.CanEditRoll(computer, TbsSplineEditorState.SelectedSpline, TbsSplineEditorState.SelectedKnot))
            {
                FillRing(computer, knot, world, out _, out _);
                Handles.color = SelectedColor;
                Handles.DrawAAPolyLine(3f, _ringBuffer);
            }
        }

        float FillRing(TbsSplineComputer computer, in TbsKnot knot, Vector3 world, out Vector3 axis, out Vector3 upDirection)
        {
            Transform trs = computer.transform;
            Vector3 tangentLocal = knot.Rotation * knot.TangentOut;
            if (tangentLocal.sqrMagnitude <= TbsSplineMath.Epsilon)
                tangentLocal = knot.Rotation * -knot.TangentIn;
            axis = tangentLocal.sqrMagnitude > TbsSplineMath.Epsilon
                ? trs.TransformDirection(tangentLocal).normalized
                : trs.TransformDirection(knot.Rotation * Vector3.forward).normalized;
            upDirection = TbsSplineMath.OrthonormalUp(axis, trs.TransformDirection(knot.Up));
            Vector3 right = Vector3.Cross(upDirection, axis).normalized;
            float radius = HandleUtility.GetHandleSize(world) * 0.8f;
            for (int i = 0; i < _ringBuffer.Length; i++)
            {
                float angle = i / (float)(_ringBuffer.Length - 1) * Mathf.PI * 2f;
                _ringBuffer[i] = world + (upDirection * Mathf.Cos(angle) + right * Mathf.Sin(angle)) * radius;
            }
            return radius;
        }

        static bool ShowTangents(TbsSpline spline, in TbsKnot knot) =>
            spline.Type == TbsSplineType.Bezier && TbsTangentModeView.ShowHandles(knot.Mode);

        bool ValidateDragTarget(TbsSplineComputer computer)
        {
            if (_drag == DragKind.Marquee) return true;
            if (_dragSpline < 0 || _dragSpline >= computer.SplineCount) return false;
            if (_drag == DragKind.Spline) return true;
            return _dragKnot >= 0 && _dragKnot < computer[_dragSpline].Count;
        }

        void DoActiveDrag(TbsSplineComputer computer, TbsSplineSceneRenderer renderer, Event evt, SceneView sceneView)
        {
            if (_drag == DragKind.None) return;
            if (_drag == DragKind.Marquee)
            {
                if (evt.rawType == EventType.MouseDrag && evt.button == 0)
                {
                    TbsSplineEditorState.MarqueeActive = true;
                    TbsSplineEditorState.MarqueeRect = MarqueeRect(evt.mousePosition);
                    SceneView.RepaintAll();
                    evt.Use();
                }
                else if (evt.rawType == EventType.MouseUp && evt.button == 0)
                {
                    ApplyMarquee(computer, evt);
                    CancelDrag();
                    evt.Use();
                }
                else if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape) { CancelDrag(); evt.Use(); }
                return;
            }
            if (!ValidateDragTarget(computer))
            {
                CancelDrag();
                return;
            }
            if (evt.rawType == EventType.MouseDrag && evt.button == 0)
            {
                ApplyDrag(computer, renderer, evt, sceneView);
                evt.Use();
            }
            else if (evt.rawType == EventType.MouseUp && evt.button == 0)
            {
                if (_drag == DragKind.Roll && _dragSpline >= 0 && _dragSpline < computer.SplineCount &&
                    _dragKnot >= 0 && _dragKnot < computer[_dragSpline].Count)
                {
                    float roll = TbsSplineEditorActions.GetKnotRoll(computer, _dragSpline, _dragKnot);
                    TbsSplineEditorState.RecordLastRotation(TbsLastOp.Rotate, "Roll Knot", new Vector3(roll, 0f, 0f), _dragSpline,
                        new List<int> { computer[_dragSpline][_dragKnot].Id });
                }
                if (_drag == DragKind.Knot && _dragSpline >= 0 && _dragSpline < computer.SplineCount &&
                    _dragKnot >= 0 && _dragKnot < computer[_dragSpline].Count)
                {
                    Vector3 endWorld = computer.transform.TransformPoint(computer[_dragSpline][_dragKnot].Position);
                    Vector3 dragDelta = endWorld - _dragOriginWorld;
                    if (dragDelta.sqrMagnitude > 1e-8f)
                    {
                        var movedIds = new List<int>();
                        if (TbsSplineEditorState.MultiKnots.Count > 1) movedIds.AddRange(TbsSplineEditorState.MultiKnots);
                        else movedIds.Add(computer[_dragSpline][_dragKnot].Id);
                        TbsSplineEditorState.RecordLast(TbsLastOp.Move, movedIds.Count > 1 ? "Move Points" : "Move Point",
                            dragDelta, _dragSpline, movedIds);
                    }
                }
                if (_drag == DragKind.Knot && TbsSplineEditorState.ConnectTargetValid && IsDragEndpoint(computer))
                {
                    int splineIndex = _dragSpline;
                    int knotIndex = _dragKnot;
                    CancelDrag();
                    OfferConnect(computer, splineIndex, knotIndex);
                }
                else
                {
                    TbsSplineEditorState.ConnectTargetValid = false;
                    CancelDrag();
                }
                evt.Use();
            }
            else if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                Undo.RevertAllInCurrentGroup();
                TbsSplineEditorState.ConnectTargetValid = false;
                CancelDrag();
                renderer.SetDirty();
                evt.Use();
                SceneView.RepaintAll();
            }
        }

        bool IsDragEndpoint(TbsSplineComputer computer)
        {
            if (_dragSpline < 0 || _dragSpline >= computer.SplineCount) return false;
            TbsSpline spline = computer[_dragSpline];
            return !spline.Closed && spline.IsEndpointIndex(_dragKnot);
        }

        void ApplyDrag(TbsSplineComputer computer, TbsSplineSceneRenderer renderer, Event evt, SceneView sceneView)
        {
            TbsSpline spline = computer[_dragSpline];
            Transform trs = computer.transform;
            if (_drag == DragKind.Spline)
            {
                if (TryPlacementPoint(evt.mousePosition, _dragPlane, out Vector3 target))
                {
                    if (evt.control || evt.command) target = SnapPosition(target);
                    Vector3 delta = trs.InverseTransformPoint(target) - trs.InverseTransformPoint(_splineDragStart);
                    _splineDragStart = target;
                    TbsSplineEditorActions.MoveSpline(computer, _dragSpline, delta);
                    SetDragLabel(target, "move spline");
                }
                return;
            }
            TbsKnot knot = spline[_dragKnot];
            Vector3 knotWorld = trs.TransformPoint(knot.Position);
            switch (_drag)
            {
                case DragKind.Knot:
                    _magnetValid = false;
                    if (TryPlacementPoint(evt.mousePosition, _dragPlane, out Vector3 knotPosition))
                    {
                        bool ctrl = evt.control || evt.command;
                        bool endpoint = TbsSplineEditorState.MultiKnots.Count <= 1 && IsDragEndpoint(computer);
                        if (endpoint)
                            UpdateConnectTarget(computer, renderer, evt.mousePosition, _dragSpline);
                        else
                            TbsSplineEditorState.ConnectTargetValid = false;
                        if (!ctrl && TbsSplineEditorState.SnapToGrid && endpoint && TbsSplineEditorState.ConnectTargetValid)
                        {
                            knotPosition = TbsSplineEditorState.ConnectTargetWorld;
                            _magnetValid = true;
                            _magnetWorld = knotPosition;
                        }
                        else if (!ctrl && TbsSplineEditorState.SnapToGrid && !endpoint && MagnetizeToKnot(computer, evt.mousePosition, out Vector3 magnet))
                        {
                            knotPosition = magnet;
                            _magnetValid = true;
                            _magnetWorld = magnet;
                        }
                        else if (TbsSplineEditorState.SnapToGrid && !ctrl) knotPosition = GridSnap(computer, knotPosition);
                        else if (ctrl) knotPosition = SnapPosition(knotPosition);
                        TbsSplineEditorActions.RecordChange(computer, "Move Knot");
                        Vector3 newLocal = trs.InverseTransformPoint(knotPosition);
                        Vector3 delta = newLocal - knot.Position;
                        if (TbsSplineEditorState.MultiKnots.Count > 1 && TbsSplineEditorState.MultiKnots.Contains(knot.Id))
                        {
                            spline.BeginChange();
                            foreach (int id in TbsSplineEditorState.MultiKnots)
                            {
                                int index = spline.IndexOfKnotId(id);
                                if (index < 0) continue;
                                TbsKnot other = spline[index];
                                other.Position += delta;
                                spline.SetKnot(index, other);
                            }
                            spline.EndChange();
                            foreach (int id in TbsSplineEditorState.MultiKnots)
                                computer.PropagateFromKnot(new TbsKnotRef(spline.Id, id));
                        }
                        else
                        {
                            knot.Position = newLocal;
                            spline.SetKnot(_dragKnot, knot);
                            computer.PropagateFromKnot(new TbsKnotRef(spline.Id, knot.Id));
                        }
                        TbsSplineEditorActions.MarkChanged(computer);
                        TbsSplineSceneRenderer.Get(computer).SetDirty();
                        SetDragInfo(_dragOriginWorld, knotPosition);
                    }
                    break;
                case DragKind.MoveY:
                {
                    float delta = HandleUtility.CalcLineTranslation(evt.mousePosition - evt.delta, evt.mousePosition, knotWorld, Vector3.up);
                    Vector3 target = knotWorld + Vector3.up * delta;
                    if (evt.control || evt.command)
                    {
                        float step = EditorSnapSettings.move.y;
                        if (step > 0f) target.y = Mathf.Round(target.y / step) * step;
                    }
                    TbsSplineEditorActions.RecordChange(computer, "Move Knot");
                    knot.Position = trs.InverseTransformPoint(target);
                    spline.SetKnot(_dragKnot, knot);
                    computer.PropagateFromKnot(new TbsKnotRef(spline.Id, knot.Id));
                    TbsSplineEditorActions.MarkChanged(computer);
                    TbsSplineSceneRenderer.Get(computer).SetDirty();
                    SetDragLabel(target, $"h = {target.y - GroundY(computer):F2}");
                    break;
                }
                case DragKind.Roll:
                {
                    Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                    if (_dragPlane.Raycast(ray, out float enter))
                    {
                        Vector3 direction = ray.GetPoint(enter) - knotWorld;
                        if (direction.sqrMagnitude > 1e-6f)
                        {
                            float angle = Vector3.SignedAngle(_rollStartDirection, direction.normalized, _rollAxis);
                            if (evt.control || evt.command) angle = Mathf.Round(angle / 15f) * 15f;
                            TbsKnot start = _rollStartKnot;
                            Quaternion newLocal = Quaternion.Inverse(trs.rotation) * (Quaternion.AngleAxis(angle, _rollAxis) * (trs.rotation * start.Rotation));
                            Quaternion keepTangents = Quaternion.Inverse(newLocal) * start.Rotation;
                            TbsSplineEditorActions.RecordChange(computer, "Roll Knot");
                            knot = start;
                            knot.Rotation = newLocal;
                            knot.TangentIn = keepTangents * start.TangentIn;
                            knot.TangentOut = keepTangents * start.TangentOut;
                            spline.SetKnot(_dragKnot, knot);
                            TbsSplineEditorActions.MarkChanged(computer);
                            SetDragLabel(knotWorld, $"roll {(angle >= 0f ? "+" : "")}{angle:F0}°");
                        }
                    }
                    break;
                }
            }
        }

        void BeginDrag(DragKind kind, int splineIndex, int knotIndex, int controlId, Plane plane)
        {
            _drag = kind;
            _dragSpline = splineIndex;
            _dragKnot = knotIndex;
            _dragPlane = plane;
            GUIUtility.hotControl = controlId;
        }

        void DoSelectedSplineHandles(TbsSplineComputer computer, Event evt, SceneView sceneView, bool inputBlocked)
        {
            if (!TbsSplineEditorState.HasSplineSelection) return;
            if (TbsSplineEditorState.ObjectModeActive) return;
            int splineIndex = TbsSplineEditorState.SelectedSpline;
            TbsSpline spline = computer[splineIndex];
            Transform trs = computer.transform;

            int moveSplineId = GUIUtility.GetControlID(FocusType.Passive);
            bool moveGizmo = !TbsSplineEditorState.HasKnotSelection && !evt.shift;
            Vector3 center = SplineCenter(computer, splineIndex);
            float mhs = HandleUtility.GetHandleSize(center) * 0.16f;
            if (evt.type == EventType.Layout && !inputBlocked)
            {
                HandleUtility.AddControl(moveSplineId, moveGizmo ? HandleUtility.DistanceToCircle(center, mhs) : 1e6f);
            }
            else if (moveGizmo && evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt && !inputBlocked && HandleUtility.nearestControl == moveSplineId && _drag == DragKind.None)
            {
                _drag = DragKind.Spline;
                _dragSpline = splineIndex;
                _dragKnot = -1;
                _dragPlane = MakeDragPlane(center, sceneView);
                _splineDragStart = TryPlacementPoint(evt.mousePosition, _dragPlane, out Vector3 grab) ? grab : center;
                GUIUtility.hotControl = moveSplineId;
                evt.Use();
                return;
            }

            for (int i = 0; i < spline.Count; i++)
            {
                int id = GUIUtility.GetControlID(FocusType.Passive);
                Vector3 world = trs.TransformPoint(spline[i].Position);
                float radius = HandleUtility.GetHandleSize(world) * 0.12f;
                if (evt.type == EventType.Layout && !inputBlocked)
                {
                    HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(world, radius));
                }
                else if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt && !inputBlocked && HandleUtility.nearestControl == id && _drag == DragKind.None)
                {
                    if (evt.control || evt.command)
                    {
                        TbsSplineEditorActions.DeleteKnot(computer, splineIndex, i);
                        evt.Use();
                        return;
                    }
                    if (evt.shift)
                    {
                        if (TbsSplineEditorState.SelectedSpline == splineIndex && TbsSplineEditorState.SelectedKnot == i)
                        {
                            _dragOriginWorld = world;
                            BeginDrag(DragKind.MoveY, splineIndex, i, id, MakeDragPlane(world, sceneView));
                            evt.Use();
                            return;
                        }
                        TbsSplineEditorState.ToggleKnotInSelection(splineIndex, i);
                        evt.Use();
                        return;
                    }
                    if (!(TbsSplineEditorState.MultiKnots.Count > 1 && TbsSplineEditorState.MultiKnots.Contains(spline[i].Id)))
                        TbsSplineEditorState.SelectKnot(splineIndex, i);
                    _dragOriginWorld = world;
                    BeginDrag(DragKind.Knot, splineIndex, i, id, MakeDragPlane(world, sceneView));
                    evt.Use();
                    return;
                }
            }
            DoSelectedKnotManipulators(computer, evt, sceneView, inputBlocked);
        }

        void DoSelectedKnotManipulators(TbsSplineComputer computer, Event evt, SceneView sceneView, bool inputBlocked)
        {
            if (!TbsSplineEditorState.HasKnotSelection) return;
            int splineIndex = TbsSplineEditorState.SelectedSpline;
            int knotIndex = TbsSplineEditorState.SelectedKnot;
            TbsSpline spline = computer[splineIndex];
            Transform trs = computer.transform;
            TbsKnot knot = spline[knotIndex];
            Vector3 world = trs.TransformPoint(knot.Position);

            if (ShowTangents(spline, knot) && !TbsSplineEditorState.RotateMode)
            {
                DoTangentMoveGizmo(computer, splineIndex, knotIndex, true, sceneView, inputBlocked);
                DoTangentMoveGizmo(computer, splineIndex, knotIndex, false, sceneView, inputBlocked);
            }
            FinishHandleDrag(computer);

            int rollId = GUIUtility.GetControlID(FocusType.Passive);
            if (!TbsSplineEditorState.RotateMode || !TbsSplineEditorActions.CanEditRoll(computer, splineIndex, knotIndex))
            {
                if (evt.type == EventType.Layout && !inputBlocked) HandleUtility.AddControl(rollId, 1e6f);
                return;
            }
            FillRing(computer, knot, world, out Vector3 rollAxis, out Vector3 rollUp);
            if (evt.type == EventType.Layout && !inputBlocked)
            {
                HandleUtility.AddControl(rollId, HandleUtility.DistanceToPolyLine(_ringBuffer));
            }
            else if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt && !inputBlocked && HandleUtility.nearestControl == rollId && _drag == DragKind.None)
            {
                _rollStartKnot = knot;
                _rollAxis = rollAxis;
                var plane = new Plane(rollAxis, world);
                Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                _rollStartDirection = plane.Raycast(ray, out float enter) ? (ray.GetPoint(enter) - world).normalized : rollUp;
                BeginDrag(DragKind.Roll, splineIndex, knotIndex, rollId, plane);
                evt.Use();
            }
        }

        enum AddKind { Append, Prepend, InsertOnCurve, Reroute, NewFromKnot }

        void DoPointTool(TbsSplineComputer computer, TbsSplineSceneRenderer renderer, Event evt, SceneView sceneView, bool inputBlocked, int defaultControl)
        {
            TbsSplineEditorState.GhostValid = false;
            TbsSplineEditorState.ClearActionKnots();
            if (inputBlocked || _drag != DragKind.None) return;
            int si = TbsSplineEditorState.SelectedSpline;
            TbsSpline spline = computer[si];
            Transform trs = computer.transform;

            if (evt.type == EventType.ScrollWheel && evt.shift)
            {
                int dir = evt.delta.y > 0f ? 1 : -1;
                TbsSplineEditorState.AddSubMode = (TbsAddMode)((((int)TbsSplineEditorState.AddSubMode + dir) % 5 + 5) % 5);
                evt.Use();
                SceneView.RepaintAll();
                return;
            }

            TbsAddMode pointSub = TbsSplineEditorState.AddSubMode;
            if (pointSub == TbsAddMode.Delete)
            {
                DoDeleteMode(computer, renderer, evt, sceneView, si, defaultControl);
                return;
            }
            if (pointSub == TbsAddMode.Merge)
            {
                DoMergeMode(computer, renderer, evt, sceneView, si, defaultControl);
                return;
            }

            if (!TryPlacementPoint(evt.mousePosition, MakePlacementPlane(computer), out Vector3 cursor)) return;

            int overKnot = renderer.FindNearestKnot(evt.mousePosition, si, KnotPickPixels, out int kIdx) ? kIdx : -1;
            bool onCurve = TbsSplineEditorState.HoverValid && TbsSplineEditorState.HoverSpline == si && TbsSplineEditorState.HoverSegment >= 0;

            TbsAddMode sub = TbsSplineEditorState.AddSubMode;
            AddKind kind;
            Vector3 ghost = cursor;
            Vector3 a = default, b = default;
            bool hasA = false, hasB = false;
            int insertSeg = -1;

            if (sub == TbsAddMode.End)
            {
                kind = AddKind.Append;
                if (spline.Count > 0) { a = trs.TransformPoint(spline[spline.Count - 1].Position); hasA = true; }
            }
            else if (sub == TbsAddMode.Start)
            {
                kind = AddKind.Prepend;
                if (spline.Count > 0) { a = trs.TransformPoint(spline[0].Position); hasA = true; }
            }
            else if (overKnot >= 0)
            {
                kind = AddKind.NewFromKnot;
                ghost = trs.TransformPoint(spline[overKnot].Position);
            }
            else if (onCurve)
            {
                kind = AddKind.InsertOnCurve;
                insertSeg = TbsSplineEditorState.HoverSegment;
                TbsSample cs = default;
                computer.GetCache(si).EvaluateSegment(insertSeg, TbsSplineEditorState.HoverT, ref cs);
                ghost = trs.TransformPoint(cs.Position);
                a = trs.TransformPoint(spline[insertSeg].Position);
                hasA = true;
                int nb = insertSeg + 1;
                if (spline.Closed && nb >= spline.Count) nb = 0;
                if (nb < spline.Count) { b = trs.TransformPoint(spline[nb].Position); hasB = true; }
            }
            else if (spline.Count >= 2)
            {
                kind = AddKind.Reroute;
                insertSeg = NearestSegment(computer, spline, cursor);
                a = trs.TransformPoint(spline[insertSeg].Position);
                hasA = true;
                int nb = insertSeg + 1;
                if (spline.Closed && nb >= spline.Count) nb = 0;
                if (nb < spline.Count) { b = trs.TransformPoint(spline[nb].Position); hasB = true; }
            }
            else
            {
                kind = AddKind.Append;
                if (spline.Count > 0) { a = trs.TransformPoint(spline[spline.Count - 1].Position); hasA = true; }
            }

            TbsSplineEditorState.GhostValid = kind != AddKind.NewFromKnot;
            TbsSplineEditorState.GhostPoint = ghost;
            if (evt.type == EventType.Repaint)
            {
                if (kind == AddKind.NewFromKnot)
                {
                    Handles.color = new Color(1f, 0.72f, 0.3f, 0.9f);
                    float hs = HandleUtility.GetHandleSize(ghost) * 0.16f;
                    Vector3 face = Camera.current != null ? Camera.current.transform.forward : Vector3.up;
                    Handles.DrawWireDisc(ghost, face, hs);
                }
                else
                {
                    CopyPreview(spline);
                    Vector3 local = trs.InverseTransformPoint(cursor);
                    switch (kind)
                    {
                        case AddKind.Append:
                            _previewSpline.InsertKnot(_previewSpline.Count, new TbsKnot(local));
                            break;
                        case AddKind.Prepend:
                            _previewSpline.InsertKnot(0, new TbsKnot(local));
                            break;
                        case AddKind.InsertOnCurve:
                            SplitPreviewSegment(insertSeg, TbsSplineEditorState.HoverT);
                            break;
                        case AddKind.Reroute:
                            int ri = SplitPreviewSegment(insertSeg, 0.5f);
                            if (ri >= 0)
                            {
                                TbsKnot rk = _previewSpline[ri];
                                rk.Position = local;
                                _previewSpline.SetKnot(ri, rk);
                            }
                            break;
                    }
                    DrawSplinePreview(computer);
                    if (hasA) DrawAffectedMark(a);
                    if (hasB) DrawAffectedMark(b);
                }
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt && HandleUtility.nearestControl == defaultControl)
            {
                switch (kind)
                {
                    case AddKind.Append:
                        TbsSplineEditorActions.AppendKnot(computer, si, cursor, false, out int ai);
                        TbsSplineEditorState.SelectKnot(si, ai);
                        break;
                    case AddKind.Prepend:
                        TbsSplineEditorActions.AppendKnot(computer, si, cursor, true, out int pi);
                        TbsSplineEditorState.SelectKnot(si, pi);
                        break;
                    case AddKind.InsertOnCurve:
                        TbsSplineEditorActions.InsertKnotOnSegment(computer, si, insertSeg, TbsSplineEditorState.HoverT, out int ii);
                        if (ii >= 0) TbsSplineEditorState.SelectKnot(si, ii);
                        break;
                    case AddKind.Reroute:
                        TbsSplineEditorActions.InsertKnotOnSegment(computer, si, insertSeg, 0.5f, out int ri);
                        if (ri >= 0)
                        {
                            TbsSplineEditorActions.SetKnotWorld(computer, si, ri, cursor);
                            TbsSplineEditorState.SelectKnot(si, ri);
                        }
                        break;
                    case AddKind.NewFromKnot:
                        if (EditorUtility.DisplayDialog("Point already exists",
                                "A point already exists here.\n\nCreate a NEW spline starting from this point?", "New spline", "Cancel"))
                        {
                            Vector3 kw = trs.TransformPoint(spline[overKnot].Position);
                            TbsSplineEditorActions.StartSpline(computer, kw, out int nsi);
                            TbsSplineEditorState.SelectSpline(nsi);
                        }
                        break;
                }
                renderer.SetDirty();
                TbsSplineEditorState.GhostValid = false;
                evt.Use();
            }
        }

        static int NearestSegment(TbsSplineComputer computer, TbsSpline spline, Vector3 worldCursor)
        {
            Transform trs = computer.transform;
            int segs = spline.Count - 1 + (spline.Closed ? 1 : 0);
            int best = 0;
            float bestD = float.MaxValue;
            for (int s = 0; s < segs; s++)
            {
                int ia = s;
                int ib = (s + 1) % spline.Count;
                Vector3 pa = trs.TransformPoint(spline[ia].Position);
                Vector3 pb = trs.TransformPoint(spline[ib].Position);
                float d = DistancePointSegment(worldCursor, pa, pb);
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        static float DistancePointSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) return Vector3.Distance(p, a);
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
            return Vector3.Distance(p, a + ab * t);
        }

        void DoSceneMouse(TbsSplineComputer computer, TbsSplineSceneRenderer renderer, Event evt, SceneView sceneView, int defaultControl, bool inputBlocked)
        {
            if (_drag != DragKind.None) return;

            if (TbsSplineEditorState.GhostValid && !TbsSplineEditorState.DrawMode && !TbsSplineEditorState.PointMode) TbsSplineEditorState.GhostValid = false;
            if (TbsSplineEditorState.ConnectTargetValid && !TbsSplineEditorState.DrawMode && !_moveGizmoDragging) TbsSplineEditorState.ConnectTargetValid = false;

            if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt && !inputBlocked && HandleUtility.nearestControl == defaultControl)
            {
                if (TbsSplineEditorState.HoverValid && TbsSplineEditorState.HoverSpline < computer.SplineCount)
                {
                    int hoverSpline = TbsSplineEditorState.HoverSpline;
                    if (TbsSplineEditorState.ObjectModeActive)
                    {
                        if (evt.shift) TbsSplineEditorState.ToggleSplineInSelection(hoverSpline);
                        else TbsSplineEditorState.SelectSpline(hoverSpline);
                        TbsSplineEditorState.SetObjectCursor(TbsSplineEditorState.HoverPoint);
                    }
                    else if (renderer.FindNearestKnot(evt.mousePosition, hoverSpline, KnotPickPixels, out int knotIndex))
                        TbsSplineEditorState.SelectKnot(hoverSpline, knotIndex);
                    else
                        TbsSplineEditorState.SelectSpline(hoverSpline);
                }
                else
                {
                    _drag = DragKind.Marquee;
                    _marqueeStart = evt.mousePosition;
                    _dragSpline = TbsSplineEditorState.SelectedSpline;
                    _dragKnot = -1;
                    TbsSplineEditorState.MarqueeActive = false;
                    GUIUtility.hotControl = defaultControl;
                }
                evt.Use();
            }
        }

        bool TryFindTriggerNear(TbsSplineComputer computer, Vector2 gui, out int groupIndex, out int triggerIndex)
        {
            groupIndex = -1;
            triggerIndex = -1;
            if (!TbsSplineEditorState.HasSplineSelection) return false;
            int si = TbsSplineEditorState.SelectedSpline;
            TbsSpline spline = computer[si];
            if (spline.TriggerGroups.Count == 0) return false;
            TbsSplineCache cache = computer.GetCache(si);
            Transform trs = computer.transform;
            TbsSample sample = default;
            float best = KnotPickPixels;
            for (int g = 0; g < spline.TriggerGroups.Count; g++)
            {
                var triggers = spline.TriggerGroups[g].Triggers;
                for (int t = 0; t < triggers.Count; t++)
                {
                    cache.EvaluateAtT(triggers[t].Position, ref sample);
                    Vector3 world = trs.TransformPoint(sample.Position);
                    float d = Vector2.Distance(HandleUtility.WorldToGUIPoint(world), gui);
                    if (d < best)
                    {
                        best = d;
                        groupIndex = g;
                        triggerIndex = t;
                    }
                }
            }
            return groupIndex >= 0;
        }

        void ShowTriggerMenu(TbsSplineComputer computer, int splineIndex, int groupIndex, int triggerIndex)
        {
            TbsSpline spline = computer[splineIndex];
            TbsSplineTrigger trigger = spline.TriggerGroups[groupIndex].Triggers[triggerIndex];
            TbsSplineEditorState.OpenMenu(Event.current.mousePosition);
            TbsSplineEditorState.AddMenuItem(null, "Trigger · Both Directions", () => { trigger.Type = TbsTriggerType.Double; SceneView.RepaintAll(); }, trigger.Type == TbsTriggerType.Double);
            TbsSplineEditorState.AddMenuItem(null, "Trigger · Forward Only", () => { trigger.Type = TbsTriggerType.Forward; SceneView.RepaintAll(); }, trigger.Type == TbsTriggerType.Forward);
            TbsSplineEditorState.AddMenuItem(null, "Trigger · Backward Only", () => { trigger.Type = TbsTriggerType.Backward; SceneView.RepaintAll(); }, trigger.Type == TbsTriggerType.Backward);
            TbsSplineEditorState.AddMenuSeparator();
            TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphTrash, "Delete Trigger", () => TbsSplineEditorActions.RemoveTrigger(computer, splineIndex, groupIndex, triggerIndex));
        }

        static float SegmentGlobalT(TbsSplineComputer computer, int splineIndex, int segment, float t)
        {
            TbsSample sample = default;
            computer.GetCache(splineIndex).EvaluateSegment(segment, t, ref sample);
            return sample.T;
        }

        void HandleContextClick(TbsSplineComputer computer, TbsSplineSceneRenderer renderer, Event evt)
        {
            if (TryFindTriggerNear(computer, evt.mousePosition, out int triggerGroup, out int triggerIdx))
            {
                ShowTriggerMenu(computer, TbsSplineEditorState.SelectedSpline, triggerGroup, triggerIdx);
                return;
            }
            if (TbsSplineEditorState.HasSplineSelection &&
                renderer.FindNearestKnot(evt.mousePosition, TbsSplineEditorState.SelectedSpline, KnotPickPixels, out int selectedKnot))
            {
                int id = computer[TbsSplineEditorState.SelectedSpline][selectedKnot].Id;
                if (TbsSplineEditorState.MultiKnots.Count > 1 && TbsSplineEditorState.MultiKnots.Contains(id))
                    TbsSplineEditorState.SetPrimaryKnotKeepMulti(TbsSplineEditorState.SelectedSpline, selectedKnot);
                else
                    TbsSplineEditorState.SelectKnot(TbsSplineEditorState.SelectedSpline, selectedKnot);
                ShowKnotMenu(computer);
                return;
            }
            if (renderer.HitTest(evt.mousePosition, CurvePickPixels, out int splineIndex, out int segment, out float t, out Vector3 _))
            {
                if (renderer.FindNearestKnot(evt.mousePosition, splineIndex, KnotPickPixels, out int knotIndex))
                {
                    int id = computer[splineIndex][knotIndex].Id;
                    if (TbsSplineEditorState.MultiKnots.Count > 1 && TbsSplineEditorState.SelectedSpline == splineIndex && TbsSplineEditorState.MultiKnots.Contains(id))
                        TbsSplineEditorState.SetPrimaryKnotKeepMulti(splineIndex, knotIndex);
                    else
                        TbsSplineEditorState.SelectKnot(splineIndex, knotIndex);
                    ShowKnotMenu(computer);
                }
                else
                {
                    ShowCurveMenu(computer, splineIndex, segment, t);
                }
                return;
            }
            ShowSceneMenu(computer);
        }

        void ShowKnotMenu(TbsSplineComputer computer)
        {
            int splineIndex = TbsSplineEditorState.SelectedSpline;
            int knotIndex = TbsSplineEditorState.SelectedKnot;
            TbsSpline spline = computer[splineIndex];
            TbsTangentMode current = spline[knotIndex].Mode;
            TbsSplineEditorState.OpenMenu(Event.current.mousePosition);
            if (TbsSplineEditorState.MultiKnots.Count > 1)
            {
                int n = TbsSplineEditorState.MultiKnots.Count;
                TbsSplineEditorState.AddMenuItem(TbsIcons.Duplicate, $"Duplicate {n} points → new spline", () => TbsSplineEditorActions.DuplicateSelectedToNewSpline(computer));
                TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphTrash, $"Delete {n} points", () => TbsSplineEditorActions.DeleteSelectedKnots(computer));
                AddAlignItems(computer);
                TbsSplineEditorState.AddMenuSeparator();
            }
            AddModeItem(computer, splineIndex, knotIndex, current, TbsTangentMode.AutoSmooth, "Auto Smooth");
            AddModeItem(computer, splineIndex, knotIndex, current, TbsTangentMode.Mirrored, "Mirrored");
            AddModeItem(computer, splineIndex, knotIndex, current, TbsTangentMode.Continuous, "Continuous");
            AddModeItem(computer, splineIndex, knotIndex, current, TbsTangentMode.Broken, "Broken");
            AddModeItem(computer, splineIndex, knotIndex, current, TbsTangentMode.Linear, "Linear");
            TbsSplineEditorState.AddMenuSeparator();
            TbsSplineEditorState.AddMenuItem(TbsIcons.ToolSelect, "Select All Points", () => TbsSplineEditorState.SelectAllKnots(), false, spline.Count > 1);
            TbsSplineEditorState.AddMenuItem(TbsIcons.ToolSelect, "Select Near (±1)", () => TbsSplineEditorState.SelectNearKnots(splineIndex, knotIndex), false, spline.Count > 1);
            TbsSplineEditorState.AddMenuSeparator();
            bool canSplit = knotIndex > 0 && knotIndex < spline.Count - 1 && !spline.Closed;
            TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphInsert, "Split Spline Here", () => TbsSplineEditorActions.SplitSplineAtKnot(computer, splineIndex, knotIndex), false, canSplit);
            bool connected = computer.GetJunctionOfKnot(computer.MakeRef(splineIndex, knotIndex)) != null;
            TbsSplineEditorState.AddMenuItem(TbsIcons.Disconnect, "Disconnect", () => TbsSplineEditorActions.DisconnectKnot(computer, splineIndex, knotIndex), false, connected);
            TbsSplineEditorState.AddMenuSeparator();
            TbsSplineEditorState.AddMenuItem(TbsIcons.Reverse, "Reverse Spline", () => TbsSplineEditorActions.ReverseSpline(computer, splineIndex));
            TbsSplineEditorState.AddMenuItem(TbsIcons.Duplicate, "Duplicate Spline", () => TbsSplineEditorActions.DuplicateSpline(computer, splineIndex));
            TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphClosed, spline.Closed ? "Open Spline" : "Close Spline", () => TbsSplineEditorActions.ToggleClosed(computer, splineIndex), spline.Closed);
            TbsSplineEditorState.AddMenuSeparator();
            TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphTrash, "Delete Knot", () => TbsSplineEditorActions.DeleteKnot(computer, splineIndex, knotIndex));
        }

        static void AddModeItem(TbsSplineComputer computer, int splineIndex, int knotIndex, TbsTangentMode current, TbsTangentMode mode, string label)
        {
            TbsSplineEditorState.AddMenuItem(TbsIcons.ModeIcon(mode), label, () => TbsSplineEditorActions.SetKnotMode(computer, splineIndex, knotIndex, mode), current == mode);
        }

        static void AddAlignItems(TbsSplineComputer computer)
        {
            TbsSplineEditorState.AddMenuItem(TbsIcons.PlaceXZ, "Align height → first", () => TbsSplineEditorActions.AlignSelectedHeights(computer, TbsHeightAlign.First));
            TbsSplineEditorState.AddMenuItem(TbsIcons.PlaceXZ, "Align height → last", () => TbsSplineEditorActions.AlignSelectedHeights(computer, TbsHeightAlign.Last));
            TbsSplineEditorState.AddMenuItem(TbsIcons.PlaceXZ, "Align height → average", () => TbsSplineEditorActions.AlignSelectedHeights(computer, TbsHeightAlign.Average));
            TbsSplineEditorState.AddMenuItem(TbsIcons.PlaceXZ, "Flatten X (average)", () => TbsSplineEditorActions.FlattenSelected(computer, 0));
            TbsSplineEditorState.AddMenuItem(TbsIcons.PlaceXZ, "Flatten Z (average)", () => TbsSplineEditorActions.FlattenSelected(computer, 2));
            TbsSplineEditorState.AddMenuItem(null, "Distribute Evenly", () => TbsSplineEditorActions.DistributeSelectedEvenly(computer));
            TbsSplineEditorState.AddMenuItem(TbsIcons.ModeIcon(TbsTangentMode.AutoSmooth), "Smooth Selected (Auto)", () => TbsSplineEditorActions.SetSelectedKnotsMode(computer, TbsTangentMode.AutoSmooth));
            TbsSplineEditorState.AddMenuItem(TbsIcons.Duplicate, "Duplicate Points In Place", () => TbsSplineEditorActions.DuplicateSelectedKnotsInPlace(computer));
        }

        void ShowCurveMenu(TbsSplineComputer computer, int splineIndex, int segment, float t)
        {
            TbsSplineEditorState.OpenMenu(Event.current.mousePosition);
            TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphInsert, "Insert Knot Here", () =>
            {
                TbsSplineEditorActions.InsertKnotOnSegment(computer, splineIndex, segment, t, out int newIndex);
                if (newIndex >= 0) TbsSplineEditorState.SelectKnot(splineIndex, newIndex);
            });
            TbsSplineEditorState.AddMenuItem(TbsIcons.LedDotTex, "Add Trigger Here", () =>
                TbsSplineEditorActions.AddTriggerAt(computer, splineIndex, SegmentGlobalT(computer, splineIndex, segment, t)));
            TbsSplineEditorState.AddMenuItem(null, "Select Spline", () => TbsSplineEditorState.SelectSpline(splineIndex));
            TbsSplineEditorState.AddMenuItem(TbsIcons.ToolSelect, "Select All Points", () => { TbsSplineEditorState.SelectSpline(splineIndex); TbsSplineEditorState.SelectAllKnots(); });
            TbsSplineEditorState.AddMenuItem(TbsIcons.ToolSelect, "Select Near (segment)", () => TbsSplineEditorState.SelectSegmentKnots(splineIndex, segment));
            if (TbsSplineEditorState.HasMultiSelection)
            {
                TbsSplineEditorState.AddMenuSeparator();
                AddAlignItems(computer);
            }
            TbsSplineEditorState.AddMenuSeparator();
            TbsSplineEditorState.AddMenuItem(TbsIcons.Reverse, "Reverse Spline", () => TbsSplineEditorActions.ReverseSpline(computer, splineIndex));
            TbsSplineEditorState.AddMenuItem(TbsIcons.Duplicate, "Duplicate Spline", () => TbsSplineEditorActions.DuplicateSpline(computer, splineIndex));
            TbsSplineEditorState.AddMenuItem(null, "Save Spline As Preset…", () => TbsSplineEditorActions.SaveSplinePreset(computer, splineIndex));
            TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphClosed, computer[splineIndex].Closed ? "Open Spline" : "Close Spline", () => TbsSplineEditorActions.ToggleClosed(computer, splineIndex), computer[splineIndex].Closed);
            TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphTrash, "Delete Spline", () => TbsSplineEditorActions.DeleteSpline(computer, splineIndex));
        }

        void ShowSceneMenu(TbsSplineComputer computer)
        {
            bool hasPlace = TryPlacementPoint(Event.current.mousePosition, MakePlacementPlane(computer), out Vector3 place);
            TbsSplineEditorState.OpenMenu(Event.current.mousePosition);
            TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphPen, "New Spline", () => TbsSplineEditorActions.ToggleDrawMode(computer));
            if (hasPlace)
            {
                TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphPlus, "New Circle", () => TbsSplineEditorActions.CreatePrimitive(computer, TbsPrimitiveKind.Circle, place, out _));
                TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphPlus, "New Ngon", () => TbsSplineEditorActions.CreatePrimitive(computer, TbsPrimitiveKind.Ngon, place, out _));
                TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphPlus, "New Star", () => TbsSplineEditorActions.CreatePrimitive(computer, TbsPrimitiveKind.Star, place, out _));
                TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphPlus, "New Rectangle", () => TbsSplineEditorActions.CreatePrimitive(computer, TbsPrimitiveKind.Rectangle, place, out _));
                TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphPlus, "New Spiral", () => TbsSplineEditorActions.CreatePrimitive(computer, TbsPrimitiveKind.Spiral, place, out _));
                TbsSplineEditorState.AddMenuItem(null, "New From Preset…", () => TbsSplineEditorActions.LoadSplinePresetAsNew(computer, place, out _));
            }
            TbsSplineEditorState.AddMenuItem(null, "Frame Selection", () =>
            {
                if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Frame(GetFocusBounds(computer), false);
            }, false, TbsSplineEditorState.HasSplineSelection);
            TbsSplineEditorState.AddMenuSeparator();
            TbsSplineEditorState.AddMenuItem(TbsIcons.Help, "Shortcuts", () =>
            {
                TbsSplineEditorState.HelpVisible = !TbsSplineEditorState.HelpVisible;
                TbsSplineEditorState.RaiseChanged();
            }, TbsSplineEditorState.HelpVisible);
            TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphExit, "Exit Spline Editor", TbsSplineEditorActions.ExitEditor);
        }

        void UpdateConnectTarget(TbsSplineComputer computer, TbsSplineSceneRenderer renderer, Vector2 mouse, int excludeSpline)
        {
            TbsSplineEditorState.ConnectTargetValid = false;
            Transform trs = computer.transform;
            float best = KnotPickPixels;
            Camera cam = Camera.current;
            for (int s = 0; s < computer.SplineCount; s++)
            {
                if (s == excludeSpline) continue;
                TbsSpline spline = computer[s];
                for (int knotIndex = 0; knotIndex < spline.Count; knotIndex++)
                {
                    Vector3 world = trs.TransformPoint(spline[knotIndex].Position);
                    if (cam != null && cam.WorldToViewportPoint(world).z <= 0f) continue;
                    float d = Vector2.Distance(HandleUtility.WorldToGUIPoint(world), mouse);
                    if (d < best)
                    {
                        best = d;
                        TbsSplineEditorState.ConnectTargetValid = true;
                        TbsSplineEditorState.ConnectTargetSpline = s;
                        TbsSplineEditorState.ConnectTargetKnot = knotIndex;
                        TbsSplineEditorState.ConnectTargetSegment = -1;
                        TbsSplineEditorState.ConnectTargetWorld = world;
                    }
                }
            }
            if (TbsSplineEditorState.ConnectTargetValid) return;
            if (renderer.HitTest(mouse, CurvePickPixels, out int hs, out int seg, out float t, out Vector3 point, excludeSpline) && hs != excludeSpline)
            {
                TbsSplineEditorState.ConnectTargetValid = true;
                TbsSplineEditorState.ConnectTargetSpline = hs;
                TbsSplineEditorState.ConnectTargetKnot = -1;
                TbsSplineEditorState.ConnectTargetSegment = seg;
                TbsSplineEditorState.ConnectTargetT = t;
                TbsSplineEditorState.ConnectTargetWorld = point;
            }
        }

        void OfferConnect(TbsSplineComputer computer, int incomingSpline, int incomingKnot)
        {
            if (!TbsSplineEditorState.ConnectTargetValid) return;
            TbsKnotRef incoming = computer.MakeRef(incomingSpline, incomingKnot);
            int targetSpline = TbsSplineEditorState.ConnectTargetSpline;
            int targetKnot = TbsSplineEditorState.ConnectTargetKnot;
            int targetSegment = TbsSplineEditorState.ConnectTargetSegment;
            float targetT = TbsSplineEditorState.ConnectTargetT;
            TbsSplineEditorState.OpenMenu(Event.current.mousePosition);
            if (targetKnot >= 0)
            {
                TbsKnotRef target = computer.MakeRef(targetSpline, targetKnot);
                TbsSpline targetSp = computer[targetSpline];
                bool targetEndpoint = !targetSp.Closed && targetSp.IsEndpointIndex(targetKnot);
                TbsSplineEditorState.AddMenuItem(TbsIcons.Junction, "Connect (junction)", () => TbsSplineEditorActions.ConnectKnots(computer, target, incoming));
                TbsSplineEditorState.AddMenuItem(TbsIcons.Merge, "Merge into one spline", () =>
                {
                    TbsSplineEditorActions.ConnectKnots(computer, target, incoming);
                    TbsJunction junction = computer.GetJunctionOfKnot(incoming);
                    if (junction != null) TbsSplineEditorActions.MergeJunction(computer, junction.Id);
                }, false, targetEndpoint);
            }
            else
            {
                TbsSplineEditorState.AddMenuItem(TbsIcons.Junction, "Connect (on-ramp)", () => TbsSplineEditorActions.ConnectEndpointToCurve(computer, incoming, targetSpline, targetSegment, targetT));
            }
            TbsSplineEditorState.AddMenuSeparator();
            TbsSplineEditorState.AddMenuItem(TbsIcons.GlyphExit, "Cancel", () => { });
            TbsSplineEditorState.ConnectTargetValid = false;
        }

        void DoDrawMode(TbsSplineComputer computer, TbsSplineSceneRenderer renderer, Event evt, SceneView sceneView, bool inputBlocked)
        {
            Transform trs = computer.transform;
            int splineIndex = TbsSplineEditorState.DrawSpline;
            TbsSpline spline = splineIndex >= 0 && splineIndex < computer.SplineCount ? computer[splineIndex] : null;
            Vector3 anchor = spline != null && spline.Count > 0
                ? trs.TransformPoint(spline[spline.Count - 1].Position)
                : trs.position;
            Vector3 point = default;
            bool valid = !inputBlocked && TryPlacementPoint(evt.mousePosition, MakePlacementPlane(computer), out point);
            if (!inputBlocked && spline != null && spline.Count > 0)
            {
                UpdateConnectTarget(computer, renderer, evt.mousePosition, splineIndex);
                if (TbsSplineEditorState.ConnectTargetValid)
                {
                    point = TbsSplineEditorState.ConnectTargetWorld;
                    valid = true;
                }
            }
            else
            {
                TbsSplineEditorState.ConnectTargetValid = false;
            }
            TbsSplineEditorState.GhostValid = valid;
            if (valid)
            {
                TbsSplineEditorState.GhostPoint = point;
                TbsSplineEditorState.GhostAnchor = anchor;
            }
            if (evt.type == EventType.Repaint && valid && spline != null && spline.Count > 0)
            {
                Handles.color = TbsSplineEditorState.PreviewLineColor;
                BeginDashes();
                DrawDashedSegment(anchor, TbsSplineEditorState.GhostPoint, TbsSplineEditorState.PreviewLineWidth);
            }
            if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt && valid)
            {
                if (spline == null)
                {
                    TbsSplineEditorActions.StartSpline(computer, TbsSplineEditorState.GhostPoint, out splineIndex);
                    TbsSplineEditorState.DrawSpline = splineIndex;
                    TbsSplineEditorState.SelectSpline(splineIndex);
                }
                else if (TbsSplineEditorState.ConnectTargetValid)
                {
                    TbsSplineEditorActions.AppendKnot(computer, splineIndex, TbsSplineEditorState.GhostPoint, false, out int newIndex);
                    OfferConnect(computer, splineIndex, newIndex);
                    TbsSplineEditorActions.FinishDraw(computer);
                }
                else
                {
                    TbsSplineEditorActions.AppendKnot(computer, splineIndex, TbsSplineEditorState.GhostPoint, false, out _);
                }
                evt.Use();
            }
        }

        void HandleKeys(TbsSplineComputer computer, Event evt, SceneView sceneView)
        {
            if (evt.type != EventType.KeyDown) return;
            if (EditorGUIUtility.editingTextField) return;
            if (_hud != null && _hud.IsCapturingShortcut) return;

            if (TbsSplineEditorState.ShortcutMatches("Mode", evt))
            {
                TbsSplineEditorActions.ToggleMode(computer);
                evt.Use();
                return;
            }
            if (TbsSplineEditorState.EditModeActive)
            {
                if (TbsSplineEditorState.ShortcutMatches("Move", evt)) { TbsSplineEditorActions.SetTool(computer, TbsTool.Move); evt.Use(); return; }
                if (TbsSplineEditorState.ShortcutMatches("Rotate", evt)) { TbsSplineEditorActions.SetTool(computer, TbsTool.Rotate); evt.Use(); return; }
                if (TbsSplineEditorState.ShortcutMatches("Scale", evt)) { TbsSplineEditorActions.SetTool(computer, TbsTool.Scale); evt.Use(); return; }
                if (TbsSplineEditorState.ShortcutMatches("Add", evt)) { TbsSplineEditorActions.SetTool(computer, TbsTool.Point); evt.Use(); return; }
            }
            else
            {
                if (TbsSplineEditorState.ShortcutMatches("Move", evt)) { TbsSplineEditorActions.SetTool(computer, TbsTool.Move); evt.Use(); return; }
                if (TbsSplineEditorState.ShortcutMatches("Rotate", evt)) { TbsSplineEditorActions.SetTool(computer, TbsTool.Rotate); evt.Use(); return; }
                if (TbsSplineEditorState.ShortcutMatches("New", evt)) { TbsSplineEditorActions.ToggleDrawMode(computer); evt.Use(); return; }
            }

            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                    if (TbsSplineEditorState.MenuOpen) TbsSplineEditorState.CloseMenu();
                    else if (TbsSplineEditorState.DrawMode) TbsSplineEditorActions.FinishDraw(computer);
                    else if (TbsSplineEditorState.HasKnotSelection) TbsSplineEditorState.ClearKnot();
                    else if (TbsSplineEditorState.HasSplineSelection) TbsSplineEditorState.ClearSelection();
                    else TbsSplineEditorActions.ExitEditor();
                    evt.Use();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (TbsSplineEditorState.DrawMode)
                    {
                        TbsSplineEditorActions.FinishDraw(computer);
                        evt.Use();
                    }
                    break;
                case KeyCode.A:
                    if ((evt.control || evt.command) && TbsSplineEditorState.HasSplineSelection)
                    {
                        TbsSplineEditorState.SelectAllKnots();
                        evt.Use();
                    }
                    break;
                case KeyCode.F:
                    sceneView.Frame(GetFocusBounds(computer), false);
                    evt.Use();
                    break;
            }
        }

        Bounds GetFocusBounds(TbsSplineComputer computer)
        {
            Transform trs = computer.transform;
            if (TbsSplineEditorState.HasKnotSelection)
            {
                Vector3 world = trs.TransformPoint(computer[TbsSplineEditorState.SelectedSpline][TbsSplineEditorState.SelectedKnot].Position);
                return new Bounds(world, Vector3.one * 4f);
            }
            if (TbsSplineEditorState.HasSplineSelection)
                return WorldBounds(computer, TbsSplineEditorState.SelectedSpline);
            if (computer.SplineCount > 0)
            {
                Bounds bounds = WorldBounds(computer, 0);
                for (int i = 1; i < computer.SplineCount; i++) bounds.Encapsulate(WorldBounds(computer, i));
                return bounds;
            }
            return new Bounds(trs.position, Vector3.one * 4f);
        }

        static Bounds WorldBounds(TbsSplineComputer computer, int splineIndex)
        {
            Bounds local = computer.GetCache(splineIndex).LocalBounds;
            Transform trs = computer.transform;
            Vector3 center = trs.TransformPoint(local.center);
            Vector3 extents = local.extents;
            Vector3 axisX = trs.TransformVector(new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = trs.TransformVector(new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = trs.TransformVector(new Vector3(0f, 0f, extents.z));
            Vector3 worldExtents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(center, Vector3.Max(worldExtents * 2f, Vector3.one));
        }

        internal bool TryPlacementPoint(Vector2 guiPosition, Plane fallbackPlane, out Vector3 position)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
            if (TbsSplineEditorState.Placement == TbsPlacementMode.Collider && Physics.Raycast(ray, out RaycastHit hit, 100000f))
            {
                position = hit.point + hit.normal * 0.01f;
                return true;
            }
            if (fallbackPlane.Raycast(ray, out float enter) && enter <= 100000f)
            {
                position = ray.GetPoint(enter);
                return true;
            }
            position = default;
            return false;
        }

        internal static Plane MakeDragPlane(Vector3 anchor, SceneView sceneView)
        {
            Camera cam = sceneView != null ? sceneView.camera : Camera.current;
            if (cam != null && Mathf.Abs(Vector3.Dot(cam.transform.forward, Vector3.up)) < 0.12f)
                return new Plane(-cam.transform.forward, anchor);
            return new Plane(Vector3.up, anchor);
        }

        internal static Plane MakePlacementPlane(TbsSplineComputer computer)
        {
            return new Plane(Vector3.up, new Vector3(0f, computer.EditorGridHeight, 0f));
        }
    }
}
