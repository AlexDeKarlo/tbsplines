using System;
using System.Collections.Generic;
using UnityEngine;

namespace TBSplineS
{
    /// <summary>
    /// A region of a spline over which a modifier acts, with a trapezoidal falloff: the effect ramps up from
    /// the start, holds at full strength across the centre, then ramps back down to the end. When the start
    /// lies after the end the region wraps around a closed spline.
    /// </summary>
    [Serializable]
    public class TbsModifierKey
    {
        /// <summary>Where the region begins, from 0 at the spline start to 1 at its end.</summary>
        [Range(0f, 1f)] public float Start;

        /// <summary>Where the region ends. A value below <see cref="Start"/> wraps around a closed spline.</summary>
        [Range(0f, 1f)] public float End = 1f;

        /// <summary>Where the ramp-up finishes, as a fraction of the region.</summary>
        [Range(0f, 1f)] public float CenterStart = 0.25f;

        /// <summary>Where the ramp-down begins, as a fraction of the region.</summary>
        [Range(0f, 1f)] public float CenterEnd = 0.75f;

        /// <summary>Overall strength of this key.</summary>
        [Range(0f, 1f)] public float Blend = 1f;

        /// <summary>
        /// Returns how strongly this key acts at a position, from 0 outside the region to <see cref="Blend"/>
        /// across its centre.
        /// </summary>
        /// <param name="t">Position along the spline, from 0 to 1.</param>
        public float Influence(float t)
        {
            bool wrapped = Start > End;
            float span = wrapped ? (1f - Start) + End : End - Start;
            if (span <= 1e-6f) return 0f;
            bool inside = wrapped ? (t >= Start || t <= End) : (t >= Start && t <= End);
            if (!inside) return 0f;
            float local = (wrapped && t <= End ? (1f - Start) + t : t - Start) / span;
            float cs = Mathf.Min(CenterStart, CenterEnd);
            float ce = Mathf.Max(CenterStart, CenterEnd);
            float ramp;
            if (local < cs) ramp = cs > 1e-6f ? local / cs : 1f;
            else if (local > ce) ramp = 1f - ce > 1e-6f ? (1f - local) / (1f - ce) : 1f;
            else ramp = 1f;
            return Mathf.Clamp01(ramp) * Mathf.Clamp01(Blend);
        }
    }

    /// <summary>
    /// Shifts the spline sideways and vertically over a region.
    /// </summary>
    [Serializable]
    public sealed class TbsOffsetKey : TbsModifierKey
    {
        /// <summary>Shift to apply at full strength: X across the spline, Y along its up axis.</summary>
        public Vector2 Offset;
    }

    /// <summary>
    /// Rotates the spline frame over a region, which is how banking and camber are authored.
    /// </summary>
    [Serializable]
    public sealed class TbsRotationKey : TbsModifierKey
    {
        /// <summary>Rotation applied at full strength, in degrees: X pitches, Y yaws, Z rolls around the tangent.</summary>
        public Vector3 Rotation;
    }

    /// <summary>
    /// How a color key combines with the color already on the sample.
    /// </summary>
    public enum TbsColorBlend
    {
        /// <summary>Fades towards the key's color.</summary>
        Lerp,

        /// <summary>Multiplies by the key's color, which darkens and tints.</summary>
        Multiply,

        /// <summary>Adds the key's color, which brightens.</summary>
        Add
    }

    /// <summary>
    /// Tints the spline over a region.
    /// </summary>
    [Serializable]
    public sealed class TbsColorKey : TbsModifierKey
    {
        /// <summary>Color applied at full strength.</summary>
        public Color Color = Color.white;

        /// <summary>How the color combines with what is already on the sample.</summary>
        public TbsColorBlend Mode = TbsColorBlend.Lerp;
    }

    /// <summary>
    /// Widens or narrows the spline over a region.
    /// </summary>
    [Serializable]
    public sealed class TbsSizeKey : TbsModifierKey
    {
        /// <summary>Amount added to the sample's size at full strength. Negative values narrow it.</summary>
        public float Size;
    }

    /// <summary>
    /// Collection of offset keys applied to every sample of a spline user.
    /// </summary>
    [Serializable]
    public sealed class TbsOffsetModifier
    {
        /// <summary>Strength of the whole modifier, on top of each key's own blend.</summary>
        [Range(0f, 1f)] public float Blend = 1f;

        /// <summary>Keys making up this modifier. Mutating the list changes the modifier directly.</summary>
        public List<TbsOffsetKey> Keys = new List<TbsOffsetKey>();

        /// <summary>
        /// Adds a key covering the given region and returns it, so the falloff can be tuned afterwards.
        /// </summary>
        /// <param name="offset">Shift applied at full strength.</param>
        /// <param name="from">Start of the region, from 0 to 1.</param>
        /// <param name="to">End of the region, from 0 to 1.</param>
        public TbsOffsetKey AddKey(Vector2 offset, float from, float to)
        {
            var key = new TbsOffsetKey { Offset = offset, Start = from, End = to };
            Keys.Add(key);
            return key;
        }

        /// <summary>
        /// Applies every key to a sample in place.
        /// </summary>
        public void Apply(ref TbsSample sample)
        {
            for (int i = 0; i < Keys.Count; i++)
            {
                float inf = Keys[i].Influence(sample.T) * Blend;
                if (inf <= 0f) continue;
                sample.Position += (sample.Right * Keys[i].Offset.x + sample.Up * Keys[i].Offset.y) * inf;
            }
        }
    }

    /// <summary>
    /// Collection of rotation keys applied to every sample of a spline user.
    /// </summary>
    [Serializable]
    public sealed class TbsRotationModifier
    {
        /// <summary>Strength of the whole modifier, on top of each key's own blend.</summary>
        [Range(0f, 1f)] public float Blend = 1f;

        /// <summary>Keys making up this modifier. Mutating the list changes the modifier directly.</summary>
        public List<TbsRotationKey> Keys = new List<TbsRotationKey>();

        /// <summary>
        /// Adds a key covering the given region and returns it, so the falloff can be tuned afterwards.
        /// </summary>
        /// <param name="rotation">Rotation applied at full strength, in degrees.</param>
        /// <param name="from">Start of the region, from 0 to 1.</param>
        /// <param name="to">End of the region, from 0 to 1.</param>
        public TbsRotationKey AddKey(Vector3 rotation, float from, float to)
        {
            var key = new TbsRotationKey { Rotation = rotation, Start = from, End = to };
            Keys.Add(key);
            return key;
        }

        /// <summary>
        /// Applies every key to a sample in place, rotating its tangent and up axis.
        /// </summary>
        public void Apply(ref TbsSample sample)
        {
            for (int i = 0; i < Keys.Count; i++)
            {
                float inf = Keys[i].Influence(sample.T) * Blend;
                if (inf <= 0f) continue;
                Vector3 euler = Keys[i].Rotation * inf;
                Vector3 tangent = sample.Tangent.sqrMagnitude > TbsSplineMath.Epsilon ? sample.Tangent.normalized : Vector3.forward;
                Quaternion q = Quaternion.AngleAxis(euler.y, sample.Up)
                    * Quaternion.AngleAxis(euler.x, sample.Right)
                    * Quaternion.AngleAxis(euler.z, tangent);
                sample.Tangent = q * sample.Tangent;
                sample.Up = q * sample.Up;
            }
        }
    }

    /// <summary>
    /// Collection of color keys applied to every sample of a spline user.
    /// </summary>
    [Serializable]
    public sealed class TbsColorModifier
    {
        /// <summary>Strength of the whole modifier, on top of each key's own blend.</summary>
        [Range(0f, 1f)] public float Blend = 1f;

        /// <summary>Keys making up this modifier. Mutating the list changes the modifier directly.</summary>
        public List<TbsColorKey> Keys = new List<TbsColorKey>();

        /// <summary>
        /// Adds a key covering the given region and returns it, so the falloff and blend mode can be tuned
        /// afterwards.
        /// </summary>
        /// <param name="color">Color applied at full strength.</param>
        /// <param name="from">Start of the region, from 0 to 1.</param>
        /// <param name="to">End of the region, from 0 to 1.</param>
        public TbsColorKey AddKey(Color color, float from, float to)
        {
            var key = new TbsColorKey { Color = color, Start = from, End = to };
            Keys.Add(key);
            return key;
        }

        /// <summary>
        /// Applies every key to a sample in place, tinting its color.
        /// </summary>
        public void Apply(ref TbsSample sample)
        {
            for (int i = 0; i < Keys.Count; i++)
            {
                TbsColorKey key = Keys[i];
                float inf = key.Influence(sample.T) * Blend;
                if (inf <= 0f) continue;
                Color target;
                switch (key.Mode)
                {
                    case TbsColorBlend.Multiply: target = sample.Color * key.Color; break;
                    case TbsColorBlend.Add: target = sample.Color + key.Color; break;
                    default: target = key.Color; break;
                }
                sample.Color = Color.Lerp(sample.Color, target, inf);
            }
        }
    }

    /// <summary>
    /// Collection of size keys applied to every sample of a spline user.
    /// </summary>
    [Serializable]
    public sealed class TbsSizeModifier
    {
        /// <summary>Strength of the whole modifier, on top of each key's own blend.</summary>
        [Range(0f, 1f)] public float Blend = 1f;

        /// <summary>Keys making up this modifier. Mutating the list changes the modifier directly.</summary>
        public List<TbsSizeKey> Keys = new List<TbsSizeKey>();

        /// <summary>
        /// Adds a key covering the given region and returns it, so the falloff can be tuned afterwards.
        /// </summary>
        /// <param name="size">Amount added to the sample's size at full strength.</param>
        /// <param name="from">Start of the region, from 0 to 1.</param>
        /// <param name="to">End of the region, from 0 to 1.</param>
        public TbsSizeKey AddKey(float size, float from, float to)
        {
            var key = new TbsSizeKey { Size = size, Start = from, End = to };
            Keys.Add(key);
            return key;
        }

        /// <summary>
        /// Applies every key to a sample in place, adjusting its size.
        /// </summary>
        public void Apply(ref TbsSample sample)
        {
            for (int i = 0; i < Keys.Count; i++)
            {
                float inf = Keys[i].Influence(sample.T) * Blend;
                if (inf <= 0f) continue;
                sample.Size += Keys[i].Size * inf;
            }
        }
    }
}
