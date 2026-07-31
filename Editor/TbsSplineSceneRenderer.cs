using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TBSplineS;

namespace TBSplineS.Editor
{
    public sealed class TbsSplineSceneRenderer
    {
        const int RefineIterations = 16;

        static readonly Dictionary<int, TbsSplineSceneRenderer> Registry = new Dictionary<int, TbsSplineSceneRenderer>();
        static readonly List<int> StaleKeys = new List<int>();
        static readonly Color IdleOutlineColor = new Color(0.07f, 0.08f, 0.1f, 0.85f);

        readonly TbsSplineComputer _computer;

        Vector3[][] _worldPolylines;
        Bounds[] _worldBounds;
        int[] _lastVersions;
        int[] _lastSampleCounts;
        Matrix4x4 _lastMatrix;
        int _lastSplineCount = -1;
        bool _dirty = true;

        static TbsSplineSceneRenderer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
            Undo.undoRedoPerformed += MarkAllDirty;
        }

        static void MarkAllDirty()
        {
            foreach (KeyValuePair<int, TbsSplineSceneRenderer> pair in Registry) pair.Value._dirty = true;
        }

        TbsSplineSceneRenderer(TbsSplineComputer computer)
        {
            _computer = computer;
        }

        public static TbsSplineSceneRenderer Get(TbsSplineComputer computer)
        {
            StaleKeys.Clear();
            foreach (KeyValuePair<int, TbsSplineSceneRenderer> pair in Registry)
            {
                if (pair.Value._computer == null) StaleKeys.Add(pair.Key);
            }
            for (int i = 0; i < StaleKeys.Count; i++) Registry.Remove(StaleKeys[i]);
            int id = computer.GetInstanceID();
            if (!Registry.TryGetValue(id, out TbsSplineSceneRenderer renderer))
            {
                renderer = new TbsSplineSceneRenderer(computer);
                Registry.Add(id, renderer);
            }
            return renderer;
        }

        public void DrawIdle()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            Validate();
            int count = _lastSplineCount;
            if (count <= 0) return;
            Color previous = Handles.color;
            Color core = TbsSplineEditorState.IdleCurveColor;
            for (int i = 0; i < count; i++)
            {
                Vector3[] polyline = _worldPolylines[i];
                if (polyline == null || polyline.Length < 2) continue;
                Handles.color = IdleOutlineColor;
                Handles.DrawAAPolyLine(4.5f, polyline);
                Handles.color = core;
                Handles.DrawAAPolyLine(2.5f, polyline);
            }
            Handles.color = previous;
        }

        public void DrawSplineHighlight(int splineIndex, Color color, float width)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            Validate();
            if (splineIndex < 0 || splineIndex >= _lastSplineCount) return;
            Vector3[] polyline = _worldPolylines[splineIndex];
            if (polyline == null || polyline.Length < 2) return;
            Color previous = Handles.color;
            Handles.color = color;
            Handles.DrawAAPolyLine(width, polyline);
            Handles.color = previous;
        }

        public bool HitTest(Vector2 guiPosition, float thresholdPixels, out int splineIndex, out int segment, out float segmentT, out Vector3 worldPoint, int excludeSpline = -1)
        {
            splineIndex = -1;
            segment = -1;
            segmentT = 0f;
            worldPoint = Vector3.zero;
            Validate();
            int count = _lastSplineCount;
            if (count <= 0) return false;
            Camera camera = Camera.current;
            bool hasCamera = camera != null;
            Vector3 cameraPosition = hasCamera ? camera.transform.position : Vector3.zero;
            Vector3 cameraForward = hasCamera ? camera.transform.forward : Vector3.forward;
            float bestDistance = float.MaxValue;
            int bestSpline = -1;
            int bestSample = -1;
            for (int i = 0; i < count; i++)
            {
                if (i == excludeSpline) continue;
                Vector3[] polyline = _worldPolylines[i];
                if (polyline == null || polyline.Length < 2) continue;
                if (!BoundsVisible(_worldBounds[i], hasCamera, cameraPosition, cameraForward, guiPosition, thresholdPixels)) continue;
                int coarseBest = -1;
                float coarseDistance = float.MaxValue;
                for (int s = 0; s < polyline.Length; s += 4)
                {
                    if (hasCamera && Vector3.Dot(polyline[s] - cameraPosition, cameraForward) <= 0f) continue;
                    float distance = (HandleUtility.WorldToGUIPoint(polyline[s]) - guiPosition).sqrMagnitude;
                    if (distance < coarseDistance)
                    {
                        coarseDistance = distance;
                        coarseBest = s;
                    }
                }
                if (coarseBest < 0) continue;
                int windowStart = Mathf.Max(0, coarseBest - 4);
                int windowEnd = Mathf.Min(polyline.Length - 1, coarseBest + 4);
                for (int s = windowStart; s <= windowEnd; s++)
                {
                    if (hasCamera && Vector3.Dot(polyline[s] - cameraPosition, cameraForward) <= 0f) continue;
                    float distance = Vector2.Distance(HandleUtility.WorldToGUIPoint(polyline[s]), guiPosition);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestSpline = i;
                        bestSample = s;
                    }
                }
            }
            if (bestSpline < 0) return false;
            int rows = TbsSplineCache.SamplesPerSegment + 1;
            int bestSegment = bestSample / rows;
            int row = bestSample % rows;
            float t0 = Mathf.Max(0f, (row - 1f) / TbsSplineCache.SamplesPerSegment);
            float t1 = Mathf.Min(1f, (row + 1f) / TbsSplineCache.SamplesPerSegment);
            TbsCurve curve = _computer[bestSpline].GetCurve(bestSegment);
            Transform trs = _computer.transform;
            for (int iteration = 0; iteration < RefineIterations; iteration++)
            {
                float m1 = Mathf.Lerp(t0, t1, 1f / 3f);
                float m2 = Mathf.Lerp(t0, t1, 2f / 3f);
                float d1 = GuiSqrDistance(trs, curve, m1, guiPosition);
                float d2 = GuiSqrDistance(trs, curve, m2, guiPosition);
                if (d1 < d2) t1 = m2;
                else t0 = m1;
            }
            float refinedT = 0.5f * (t0 + t1);
            Vector3 refinedWorld = trs.TransformPoint(curve.EvaluatePosition(refinedT));
            float refinedDistance = Vector2.Distance(HandleUtility.WorldToGUIPoint(refinedWorld), guiPosition);
            if (Mathf.Min(bestDistance, refinedDistance) > thresholdPixels) return false;
            splineIndex = bestSpline;
            segment = bestSegment;
            segmentT = refinedT;
            worldPoint = refinedWorld;
            return true;
        }

        public bool FindNearestKnot(Vector2 guiPosition, int splineIndex, float thresholdPixels, out int knotIndex)
        {
            knotIndex = -1;
            Validate();
            if (splineIndex < 0 || splineIndex >= _computer.SplineCount) return false;
            TbsSpline spline = _computer[splineIndex];
            Transform trs = _computer.transform;
            Camera camera = Camera.current;
            bool hasCamera = camera != null;
            Vector3 cameraPosition = hasCamera ? camera.transform.position : Vector3.zero;
            Vector3 cameraForward = hasCamera ? camera.transform.forward : Vector3.forward;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < spline.Count; i++)
            {
                Vector3 world = trs.TransformPoint(spline[i].Position);
                if (hasCamera && Vector3.Dot(world - cameraPosition, cameraForward) <= 0f) continue;
                float distance = Vector2.Distance(HandleUtility.WorldToGUIPoint(world), guiPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    knotIndex = i;
                }
            }
            if (knotIndex >= 0 && bestDistance <= thresholdPixels) return true;
            knotIndex = -1;
            return false;
        }

        public void SetDirty()
        {
            _dirty = true;
        }

        void Validate()
        {
            int count = _computer.SplineCount;
            bool rebuild = _dirty || count != _lastSplineCount || _lastMatrix != _computer.transform.localToWorldMatrix;
            if (!rebuild)
            {
                for (int i = 0; i < count; i++)
                {
                    if (_computer[i].Version != _lastVersions[i] || _computer.GetCache(i).SampleCount != _lastSampleCounts[i])
                    {
                        rebuild = true;
                        break;
                    }
                }
            }
            if (rebuild) Rebuild();
        }

        void Rebuild()
        {
            Matrix4x4 matrix = _computer.transform.localToWorldMatrix;
            int count = _computer.SplineCount;
            bool structural = _dirty || count != _lastSplineCount || matrix != _lastMatrix;
            EnsureArrays(count);
            for (int i = 0; i < count; i++)
            {
                TbsSplineCache cache = _computer.GetCache(i);
                int samples = cache.SampleCount;
                int version = _computer[i].Version;
                Vector3[] polyline = _worldPolylines[i];
                bool changed = structural || polyline == null || polyline.Length != samples || _lastVersions[i] != version || _lastSampleCounts[i] != samples;
                if (changed)
                {
                    if (polyline == null || polyline.Length != samples) polyline = new Vector3[samples];
                    Bounds bounds = default;
                    for (int s = 0; s < samples; s++)
                    {
                        Vector3 world = matrix.MultiplyPoint3x4(cache.GetSamplePosition(s));
                        polyline[s] = world;
                        if (s == 0) bounds = new Bounds(world, Vector3.zero);
                        else bounds.Encapsulate(world);
                    }
                    _worldPolylines[i] = polyline;
                    _worldBounds[i] = bounds;
                    _lastVersions[i] = version;
                    _lastSampleCounts[i] = samples;
                }
            }
            _lastMatrix = matrix;
            _lastSplineCount = count;
            _dirty = false;
        }

        void EnsureArrays(int count)
        {
            if (_worldPolylines != null && _worldPolylines.Length == count) return;
            Vector3[][] polylines = new Vector3[count][];
            if (_worldPolylines != null)
            {
                int copy = Mathf.Min(_worldPolylines.Length, count);
                for (int i = 0; i < copy; i++) polylines[i] = _worldPolylines[i];
            }
            _worldPolylines = polylines;
            _worldBounds = new Bounds[count];
            _lastVersions = new int[count];
            _lastSampleCounts = new int[count];
        }


        static bool BoundsVisible(Bounds bounds, bool hasCamera, Vector3 cameraPosition, Vector3 cameraForward, Vector2 guiPosition, float thresholdPixels)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            bool anyInFront = false;
            bool anyBehind = false;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 world = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                if (hasCamera && Vector3.Dot(world - cameraPosition, cameraForward) <= 0f)
                {
                    anyBehind = true;
                    continue;
                }
                anyInFront = true;
                Vector2 gui = HandleUtility.WorldToGUIPoint(world);
                if (gui.x < minX) minX = gui.x;
                if (gui.y < minY) minY = gui.y;
                if (gui.x > maxX) maxX = gui.x;
                if (gui.y > maxY) maxY = gui.y;
            }
            if (!anyInFront) return false;
            if (anyBehind) return true;
            Rect rect = Rect.MinMaxRect(minX - thresholdPixels, minY - thresholdPixels, maxX + thresholdPixels, maxY + thresholdPixels);
            return rect.Contains(guiPosition);
        }

        static float GuiSqrDistance(Transform trs, in TbsCurve curve, float t, Vector2 guiPosition)
        {
            Vector3 world = trs.TransformPoint(curve.EvaluatePosition(t));
            return (HandleUtility.WorldToGUIPoint(world) - guiPosition).sqrMagnitude;
        }

        static void DisposeAll()
        {
            Registry.Clear();
        }
    }
}
