// SPDX-License-Identifier: MIT OR Apache-2.0
// SPDX-FileCopyrightText: 2026 KooshaPari <kooshapari@gmail.com>

//! T40 spec: HDR LUT pipeline (Gen-5 RNP) hexagonal port.
//!
//! Supports 4 LUT formats: .cube, .3dl, .csp, and PNG-encoded Hald images.
//! Each format is an adapter; the pipeline can be swapped at runtime via the
//! config (e.g., "use .cube in editor, .csp in shipping").

using UnityEngine;

namespace Phenotype.PostFx.Lut
{
    public enum LutFormat { Cube, ThreeDl, Csp, HaldPng }

    public sealed class LutData
    {
        public LutFormat Format { get; }
        public int Size { get; }              // 17/33/65 for cube, 8-256 for others
        public bool IsHdr { get; }
        public Color[] Colors { get; }        // Flattened (Size^3 or Size*Size for 1D)
        public string Source { get; }

        public LutData(LutFormat format, int size, bool isHdr, Color[] colors, string source)
        {
            Format = format; Size = size; IsHdr = isHdr; Colors = colors; Source = source;
        }

        /// <summary>The identity LUT (no color change).</summary>
        public static LutData Identity(LutFormat format, int size = 17)
        {
            var colors = new Color[size * size * size];
            for (int b = 0; b < size; b++)
            for (int g = 0; g < size; g++)
            for (int r = 0; r < size; r++)
                colors[(b * size + g) * size + r] = new Color((float)r / (size-1), (float)g / (size-1), (float)b / (size-1), 1f);
            return new LutData(format, size, false, colors, "<identity>");
        }
    }

    /// <summary>Hexagonal port: parse + serialize a LUT in the specific format.</summary>
    public interface ILutAdapter
    {
        LutFormat Format { get; }
        LutData Parse(string path);
        bool TryParse(string path, out LutData data);
        void Serialize(string path, LutData data);
    }

    /// <summary>Hexagonal port: the LUT pipeline (parse → validate → upload to GPU).</summary>
    public interface ILutPipeline
    {
        LutData Current { get; }
        void Load(string path);
        void UseIdentity();
        bool Validate(LutData data, out string error);
        Texture2D ToTexture2D(LutData data);
    }

    /// <summary>Detects if a LUT is the identity transform (no color change).</summary>
    public static class LutPipelineHelpers
    {
        public static bool IsIdentity(LutData data, float tolerance = 0.001f) { /* ... */ return false; }
        public static bool IsSrgb(LutData data) { /* ... */ return true; }
    }
}
