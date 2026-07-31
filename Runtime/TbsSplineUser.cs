using System;
using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// Which Unity callback a spline user rebuilds in.
    /// </summary>
    public enum TbsUpdateMethod
    {
        /// <summary>Rebuilds in Update, the usual choice.</summary>
        Update,

        /// <summary>Rebuilds in LateUpdate, after animation and other scripts have moved things.</summary>
        LateUpdate,

        /// <summary>Rebuilds in FixedUpdate, in step with physics.</summary>
        FixedUpdate,

        /// <summary>Never rebuilds on its own. Call <see cref="TbsSplineUser.RebuildImmediate"/> yourself.</summary>
        None
    }

    /// <summary>
    /// Base class for everything that reads a spline: generators, followers and your own components. It binds
    /// to a spline, tracks when that spline changes, applies the offset, rotation, color and size modifiers,
    /// and calls <see cref="Build"/> and <see cref="PostBuild"/> when a rebuild is due.
    /// </summary>
    public abstract class TbsSplineUser : MonoBehaviour
    {
        [SerializeField] protected TbsSplineComputer _computer;
        [SerializeField] protected int _splineId = 1;
        [SerializeField, Range(0f, 1f)] float _clipFrom;
        [SerializeField, Range(0f, 1f)] float _clipTo = 1f;
        [SerializeField] TbsUpdateMethod _updateMethod = TbsUpdateMethod.Update;
        [SerializeField] bool _autoUpdate = true;
        [SerializeField] bool _buildOnAwake = true;
        [SerializeField] bool _buildOnEnable = true;
        [SerializeField] int _sampleResolution;
        [SerializeField] TbsOffsetModifier _offsetModifier = new TbsOffsetModifier();
        [SerializeField] TbsRotationModifier _rotationModifier = new TbsRotationModifier();
        [SerializeField] TbsColorModifier _colorModifier = new TbsColorModifier();
        [SerializeField] TbsSizeModifier _sizeModifier = new TbsSizeModifier();

        [NonSerialized] int _cachedIndex = -1;
        [NonSerialized] int _lastVersion = int.MinValue;
        [NonSerialized] bool _dirty = true;

        /// <summary>Raised after every rebuild completes, once the generated result is ready to read.</summary>
        public event Action<TbsSplineUser> PostBuilt;

        /// <summary>Spline computer this user reads from. Setting it re-resolves the spline and rebuilds.</summary>
        public TbsSplineComputer Computer
        {
            get => _computer;
            set
            {
                _computer = value;
                _cachedIndex = -1;
                if (_computer != null && _computer.IndexOfSplineId(_splineId) < 0 && _computer.SplineCount > 0)
                    _splineId = _computer[0].Id;
                SetDirty();
            }
        }

        /// <summary>
        /// Identifier of the spline to read, stable across insertions and deletions. Falls back to the first
        /// spline when no spline carries it.
        /// </summary>
        public int SplineId
        {
            get => _splineId;
            set { _splineId = value; _cachedIndex = -1; SetDirty(); }
        }

        /// <summary>Which Unity callback this user rebuilds in.</summary>
        public TbsUpdateMethod UpdateMethod
        {
            get => _updateMethod;
            set => _updateMethod = value;
        }

        /// <summary>Rebuilds automatically whenever the spline changes. Turn off to rebuild on your own schedule.</summary>
        public bool AutoUpdate
        {
            get => _autoUpdate;
            set => _autoUpdate = value;
        }

        /// <summary>
        /// Start of the visible span, from 0 to 1. On a closed spline a value above <see cref="ClipTo"/> wraps
        /// the span around the seam.
        /// </summary>
        public float ClipFrom
        {
            get => _clipFrom;
            set { _clipFrom = Mathf.Clamp01(value); SetDirty(); }
        }

        /// <summary>End of the visible span, from 0 to 1.</summary>
        public float ClipTo
        {
            get => _clipTo;
            set { _clipTo = Mathf.Clamp01(value); SetDirty(); }
        }

        /// <summary>
        /// Fixed number of samples to take across the span. Zero follows the spline's own resolution, which is
        /// usually what you want; raise it only where geometry needs more detail than the spline carries.
        /// </summary>
        public int SampleResolution
        {
            get => _sampleResolution;
            set { _sampleResolution = value < 0 ? 0 : value; SetDirty(); }
        }

        /// <summary>
        /// Sets both ends of the visible span at once and rebuilds.
        /// </summary>
        /// <param name="from">Start of the span, from 0 to 1.</param>
        /// <param name="to">End of the span, from 0 to 1.</param>
        public void SetClipRange(float from, float to)
        {
            _clipFrom = Mathf.Clamp01(from);
            _clipTo = Mathf.Clamp01(to);
            SetDirty();
        }

        /// <summary>Whether the spline being read forms a closed loop.</summary>
        public bool IsClosed
        {
            get { TbsSplineCache cache = ResolveCache(); return cache != null && cache.Spline.Closed; }
        }

        bool Wrapped => _clipFrom > _clipTo && IsClosed;

        /// <summary>Fraction of the whole spline covered by the visible span.</summary>
        public float ClipSpan => Wrapped ? (1f - _clipFrom) + _clipTo : Mathf.Max(0f, _clipTo - _clipFrom);

        /// <summary>
        /// Converts a position within the visible span into a position on the whole spline.
        /// </summary>
        /// <param name="localT">Position within the span, from 0 to 1.</param>
        public float UnclipPercent(float localT)
        {
            float g = _clipFrom + Mathf.Clamp01(localT) * ClipSpan;
            return Wrapped ? Mathf.Repeat(g, 1f) : Mathf.Clamp01(g);
        }

        /// <summary>
        /// Converts a position on the whole spline into a position within the visible span. The inverse of
        /// <see cref="UnclipPercent"/>.
        /// </summary>
        /// <param name="globalT">Position on the whole spline, from 0 to 1.</param>
        public float ClipPercent(float globalT)
        {
            float span = ClipSpan;
            if (span <= TbsSplineMath.Epsilon) return 0f;
            if (Wrapped)
            {
                float d = globalT >= _clipFrom ? globalT - _clipFrom : (1f - _clipFrom) + globalT;
                return Mathf.Clamp01(d / span);
            }
            return Mathf.Clamp01((globalT - _clipFrom) / span);
        }

        /// <summary>Length of the visible span in world units, or 0 when no spline is bound.</summary>
        public float Length
        {
            get { TbsSplineCache cache = ResolveCache(); return cache != null ? cache.TotalLength * ClipSpan : 0f; }
        }

        /// <summary>Number of samples taken across the visible span, at least 2 whenever a spline is bound.</summary>
        public int SampleCount
        {
            get
            {
                TbsSplineCache cache = ResolveCache();
                if (cache == null) return 0;
                if (_sampleResolution >= 2) return _sampleResolution;
                return Mathf.Max(2, Mathf.RoundToInt(cache.SampleCount * ClipSpan));
            }
        }

        /// <summary>Keys that shift this user's samples sideways and vertically.</summary>
        public TbsOffsetModifier OffsetModifier => _offsetModifier;

        /// <summary>Keys that rotate this user's samples, used for banking and camber.</summary>
        public TbsRotationModifier RotationModifier => _rotationModifier;

        /// <summary>Keys that tint this user's samples.</summary>
        public TbsColorModifier ColorModifier => _colorModifier;

        /// <summary>Keys that widen or narrow this user's samples.</summary>
        public TbsSizeModifier SizeModifier => _sizeModifier;

        /// <summary>
        /// Samples the spline at a position within the visible span, with every modifier applied. This is the
        /// method to call from your own components.
        /// </summary>
        /// <param name="localT">Position within the visible span, from 0 to 1.</param>
        /// <param name="sample">Receives the result. Left untouched when no spline is bound.</param>
        public void Evaluate(float localT, ref TbsSample sample)
        {
            if (_computer == null) return;
            if (ResolveCache() == null) return;
            _computer.Evaluate(_cachedIndex, UnclipPercent(localT), ref sample);
            _offsetModifier.Apply(ref sample);
            _rotationModifier.Apply(ref sample);
            _colorModifier.Apply(ref sample);
            _sizeModifier.Apply(ref sample);
        }

        /// <summary>
        /// Samples the spline at one of the evenly spaced steps counted by <see cref="SampleCount"/>.
        /// </summary>
        /// <param name="index">Step to sample, from 0 to <see cref="SampleCount"/> minus 1.</param>
        /// <param name="sample">Receives the result.</param>
        public void GetSample(int index, ref TbsSample sample)
        {
            int n = SampleCount;
            float localT = n > 1 ? (float)index / (n - 1) : 0f;
            Evaluate(localT, ref sample);
        }

        /// <summary>
        /// Marks this user for a rebuild on its next update. Cheap to call repeatedly within a frame.
        /// </summary>
        public void Rebuild() => _dirty = true;

        /// <summary>
        /// Rebuilds right now instead of waiting for the next update, and raises <see cref="PostBuilt"/>. Use it
        /// when the result has to be ready within the current frame, or when the update method is
        /// <see cref="TbsUpdateMethod.None"/>.
        /// </summary>
        public void RebuildImmediate()
        {
            TbsSplineCache cache = ResolveCache();
            if (cache == null) return;
            _lastVersion = cache.Spline.Version;
            _dirty = false;
            Build();
            PostBuild();
            PostBuilt?.Invoke(this);
        }

        /// <summary>
        /// Returns the cache of the bound spline, re-resolving it when the spline list has shifted underneath.
        /// </summary>
        /// <returns>The cache, or null when no computer is assigned or it holds no splines.</returns>
        protected TbsSplineCache ResolveCache()
        {
            if (_computer == null) return null;
            if (_cachedIndex < 0 || _cachedIndex >= _computer.SplineCount || _computer[_cachedIndex].Id != _splineId)
            {
                _cachedIndex = _computer.IndexOfSplineId(_splineId);
                if (_cachedIndex < 0 && _computer.SplineCount > 0) _cachedIndex = 0;
            }
            return _cachedIndex >= 0 ? _computer.GetCache(_cachedIndex) : null;
        }

        /// <summary>
        /// Slot of the bound spline inside the computer. Valid only after <see cref="ResolveCache"/> has run.
        /// </summary>
        protected int ResolvedSplineIndex => _cachedIndex;

        /// <summary>
        /// Override to compute your data for a rebuild. Runs before <see cref="PostBuild"/>, and is where mesh
        /// generators fill their vertex lists.
        /// </summary>
        protected virtual void Build()
        {
        }

        /// <summary>
        /// Override to push the result of a rebuild into the scene. Runs after <see cref="Build"/>, and is where
        /// generators write meshes, colliders and transforms.
        /// </summary>
        protected virtual void PostBuild()
        {
        }

        /// <summary>Marks this user for a rebuild on its next update.</summary>
        protected void SetDirty() => _dirty = true;

        protected virtual void Awake()
        {
            if (_buildOnAwake) _dirty = true;
        }

        protected virtual void OnEnable()
        {
            if (_buildOnEnable) _dirty = true;
        }

        protected virtual void OnValidate()
        {
            _cachedIndex = -1;
            _dirty = true;
        }

        void Update()
        {
            if (_updateMethod == TbsUpdateMethod.Update) RunUpdate();
        }

        void LateUpdate()
        {
            if (_updateMethod == TbsUpdateMethod.LateUpdate) RunUpdate();
        }

        void FixedUpdate()
        {
            if (_updateMethod == TbsUpdateMethod.FixedUpdate) RunUpdate();
        }

        void RunUpdate()
        {
            TbsSplineCache cache = ResolveCache();
            if (cache == null) return;
            if (_autoUpdate && cache.Spline.Version != _lastVersion) _dirty = true;
            if (_dirty) RebuildImmediate();
        }
    }
}
