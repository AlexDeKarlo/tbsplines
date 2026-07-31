using System;
using System.Collections.Generic;
using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// Blends two values of the same type. Implement this to store your own value type along a spline.
    /// </summary>
    /// <typeparam name="T">Type of the value being blended.</typeparam>
    public interface ITbsInterpolator<T>
    {
        /// <summary>
        /// Returns the value at <paramref name="t"/> between <paramref name="a"/> and <paramref name="b"/>.
        /// </summary>
        /// <param name="t">Blend factor, 0 returns <paramref name="a"/> and 1 returns <paramref name="b"/>.</param>
        T Interpolate(T a, T b, float t);
    }

    /// <summary>
    /// Linear interpolator for <see cref="float"/> values.
    /// </summary>
    public struct TbsFloatInterpolator : ITbsInterpolator<float>
    {
        /// <inheritdoc/>
        public float Interpolate(float a, float b, float t) => Mathf.LerpUnclamped(a, b, t);
    }

    /// <summary>
    /// Linear interpolator for <see cref="Vector3"/> values.
    /// </summary>
    public struct TbsVector3Interpolator : ITbsInterpolator<Vector3>
    {
        /// <inheritdoc/>
        public Vector3 Interpolate(Vector3 a, Vector3 b, float t) => Vector3.LerpUnclamped(a, b, t);
    }

    /// <summary>
    /// Linear interpolator for <see cref="Color"/> values.
    /// </summary>
    public struct TbsColorInterpolator : ITbsInterpolator<Color>
    {
        /// <inheritdoc/>
        public Color Interpolate(Color a, Color b, float t) => Color.LerpUnclamped(a, b, t);
    }

    /// <summary>
    /// A single keyed value placed at a position along a spline.
    /// </summary>
    /// <typeparam name="T">Type of the stored value.</typeparam>
    [Serializable]
    public struct TbsDataPoint<T>
    {
        /// <summary>Position along the spline, expressed in the owning collection's unit.</summary>
        public float Index;

        /// <summary>Value at that position.</summary>
        public T Value;

        /// <summary>
        /// Creates a data point holding <paramref name="value"/> at <paramref name="index"/>.
        /// </summary>
        public TbsDataPoint(float index, T value)
        {
            Index = index;
            Value = value;
        }
    }

    /// <summary>
    /// A sorted set of keyed values along a spline that can be sampled at any position, the way the built-in
    /// size, offset, rotation and color modifiers store their keys.
    /// </summary>
    /// <typeparam name="T">Type of the stored value.</typeparam>
    [Serializable]
    public class TbsSplineData<T>
    {
        static readonly Comparison<TbsDataPoint<T>> ByIndex = (a, b) => a.Index.CompareTo(b.Index);

        [SerializeField] TbsPathUnit _unit = TbsPathUnit.Distance;
        [SerializeField] List<TbsDataPoint<T>> _points = new List<TbsDataPoint<T>>();

        /// <summary>Raised whenever the keys or the unit change.</summary>
        public event Action Changed;

        /// <summary>Number of keys currently stored.</summary>
        public int Count => _points.Count;

        /// <summary>Returns the key at the given slot, in ascending order of position.</summary>
        public TbsDataPoint<T> this[int index] => _points[index];

        /// <summary>Unit the keys' positions are expressed in.</summary>
        public TbsPathUnit Unit
        {
            get => _unit;
            set
            {
                if (_unit == value) return;
                _unit = value;
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Adds a key and keeps the collection sorted by position.
        /// </summary>
        /// <param name="index">Position along the spline, in <see cref="Unit"/>.</param>
        /// <param name="value">Value at that position.</param>
        public void Add(float index, T value)
        {
            _points.Add(new TbsDataPoint<T>(index, value));
            _points.Sort(ByIndex);
            Changed?.Invoke();
        }

        /// <summary>
        /// Replaces an existing key. The collection is re-sorted, so slots may shift afterwards.
        /// </summary>
        /// <param name="pointIndex">Slot of the key to replace.</param>
        /// <param name="index">New position along the spline, in <see cref="Unit"/>.</param>
        /// <param name="value">New value.</param>
        public void SetPoint(int pointIndex, float index, T value)
        {
            _points[pointIndex] = new TbsDataPoint<T>(index, value);
            _points.Sort(ByIndex);
            Changed?.Invoke();
        }

        /// <summary>
        /// Removes the key at the given slot.
        /// </summary>
        public void RemoveAt(int pointIndex)
        {
            _points.RemoveAt(pointIndex);
            Changed?.Invoke();
        }

        /// <summary>
        /// Removes every key.
        /// </summary>
        public void Clear()
        {
            _points.Clear();
            Changed?.Invoke();
        }

        /// <summary>
        /// Samples the keys at the given position. Values are held constant beyond the first and last key,
        /// and <paramref name="fallback"/> is returned when no keys are stored.
        /// </summary>
        /// <param name="cache">Cache of the spline being sampled, used to convert between units.</param>
        /// <param name="position">Position to sample at.</param>
        /// <param name="positionUnit">Unit <paramref name="position"/> is expressed in.</param>
        /// <param name="interpolator">Blends the two surrounding keys.</param>
        /// <param name="fallback">Value returned when the collection is empty.</param>
        /// <typeparam name="TInterpolator">Interpolator type, taken as a generic parameter to avoid boxing.</typeparam>
        public T Evaluate<TInterpolator>(TbsSplineCache cache, float position, TbsPathUnit positionUnit, TInterpolator interpolator, T fallback)
            where TInterpolator : ITbsInterpolator<T>
        {
            if (_points.Count == 0) return fallback;
            float converted = cache.ConvertUnit(position, positionUnit, _unit);
            TbsDataPoint<T> first = _points[0];
            if (converted <= first.Index) return first.Value;
            TbsDataPoint<T> last = _points[_points.Count - 1];
            if (converted >= last.Index) return last.Value;
            int lo = 0;
            int hi = _points.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_points[mid].Index <= converted) lo = mid;
                else hi = mid;
            }
            TbsDataPoint<T> a = _points[lo];
            TbsDataPoint<T> b = _points[hi];
            float span = b.Index - a.Index;
            float fraction = span > TbsSplineMath.Epsilon ? (converted - a.Index) / span : 0f;
            return interpolator.Interpolate(a.Value, b.Value, fraction);
        }
    }
}
