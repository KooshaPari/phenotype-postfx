// SPDX-License-Identifier: MIT OR Apache-2.0
// SPDX-FileCopyrightText: 2026 KooshaPari <kooshapari@gmail.com>

//! SSAOPass — Screen Space Ambient Occlusion using the IPostFxPass hexagonal port.
//!
//! Generates a configurable sample kernel and applies an occlusion mask via
//! depth-buffer comparison.  Parameters are exposed as mutable properties
//! so the driver or test code can tune them without re-creating the pass.

using System;
using UnityEngine;
using Phenotype.PostFx.Ports;

namespace Phenotype.PostFx
{
    /// <summary>
    /// Screen-space ambient occlusion pass using the <see cref="IPostFxPass"/> hexagonal port.
    /// Supports configurable radius, intensity, bias, and kernel size, and generates
    /// a deterministic sample kernel for depth-buffer sampling.
    /// </summary>
    public sealed class SSAOPass : Phenotype.PostFx.Ports.IPostFxPass
    {
        public string Name => "SSAO";
        public float Cost => 0.25f;
        public bool IsEnabled { get; set; } = true;

        public float Radius { get; set; } = 0.5f;
        public float Intensity { get; set; } = 1.2f;
        public float Bias { get; set; } = 0.04f;
        public int KernelSize { get; set; } = 8;

        public Material? Material => _material;

        Material? _material;
        Vector4[] _kernel = Array.Empty<Vector4>();

        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int BiasId = Shader.PropertyToID("_Bias");
        static readonly int SampleCountId = Shader.PropertyToID("_SampleCount");
        static readonly int SamplesId = Shader.PropertyToID("_Samples");

        const string ShaderName = "Hidden/Phenotype/SSAOPass";
        const string PassKeyword = "SSAOPASS";

        public void OnSetup(PostFxContext ctx)
        {
            if (_material == null)
            {
                Shader? shader = Shader.Find(ShaderName);
                if (shader == null) return;
                _material = new Material(shader);
            }

            if (_kernel.Length == 0)
            {
                BuildKernel(KernelSize);
            }

            ApplyParams();
        }

        public void OnRender(PostFxContext ctx)
        {
            if (_material == null) return;

            ApplyParams();
            Graphics.Blit(ctx.Source, ctx.Destination, _material);
        }

        public void OnDispose()
        {
            if (_material != null)
            {
                UnityEngine.Object.Destroy(_material);
                _material = null;
            }
        }

        public void ValidateVariants(Phenotype.PostFx.Ports.IShaderAvailabilityProvider shaderProvider)
        {
            if (!shaderProvider.IsAvailable(ShaderName, PassKeyword))
                throw new InvalidOperationException($"Shader variant unavailable: {ShaderName} ({PassKeyword})");
        }

        /// <summary>
        /// Generates a deterministic sample kernel of the given size.
        /// Samples are distributed on a unit disk and scaled so that inner samples
        /// are closer to the origin and outer samples are farther away.
        /// </summary>
        public void BuildKernel(int size)
        {
            _kernel = new Vector4[size];
            var rng = new System.Random(1337);
            for (int i = 0; i < size; i++)
            {
                Vector2 s;
                s.x = Mathf.Lerp(-1f, 1f, (float)rng.NextDouble());
                s.y = Mathf.Lerp(-1f, 1f, (float)rng.NextDouble());
                if (s.sqrMagnitude < 0.0001f) s = Vector2.right;
                s.Normalize();
                float scale = (i + 1f) / size;
                _kernel[i] = new Vector4(s.x, s.y, 0f, scale);
            }
        }

        void ApplyParams()
        {
            if (_material == null) return;
            _material.SetFloat(RadiusId, Radius);
            _material.SetFloat(IntensityId, Intensity);
            _material.SetFloat(BiasId, Bias);
            _material.SetInt(SampleCountId, _kernel.Length);
            _material.SetVectorArray(SamplesId, _kernel);
        }
    }
}
