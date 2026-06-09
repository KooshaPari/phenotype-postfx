using System;
using UnityEngine;
using Phenotype.PostFx.Ports;

namespace Phenotype.PostFx
{
    /// <summary>
    /// Multi-pass bloom implementation using the <see cref="Ports.IPostFxPass"/> hexagonal port.
    /// Supports configurable threshold, intensity, and iteration count, and selects
    /// a shader quality variant (Low / Medium / High / Ultra) based on the current
    /// <see cref="PostFxContext.Quality"/>.
    /// </summary>
    public sealed class BloomPass : Ports.IPostFxPass
    {
        public string Name => "Bloom";
        public float Cost => 0.35f;
        public bool IsEnabled { get; set; } = true;

        public float Threshold { get; set; } = 0.8f;
        public float Intensity { get; set; } = 0.5f;
        public int Iterations { get; set; } = 2;

        public Material? Material => _material;

        Material? _material;

        static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");

        const string ShaderName = "Hidden/Phenotype/BloomPass";

        const string LowKeyword = "BLOOM_LOW";
        const string MediumKeyword = "BLOOM_MEDIUM";
        const string HighKeyword = "BLOOM_HIGH";
        const string UltraKeyword = "BLOOM_ULTRA";

        public void OnSetup(PostFxContext ctx)
        {
            if (_material != null) return;

            Shader? shader = Shader.Find(ShaderName);
            if (shader == null) return;

            _material = new Material(shader);
            ApplyQualityKeyword(ctx.Quality);
        }

        public void OnRender(PostFxContext ctx)
        {
            if (_material == null) return;

            _material.SetFloat(ThresholdId, Threshold);
            _material.SetFloat(IntensityId, Intensity);
            ApplyQualityKeyword(ctx.Quality);

            int w = Mathf.Max(1, ctx.Source.width / 4);
            int h = Mathf.Max(1, ctx.Source.height / 4);

            RenderTexture bloomA = RenderTexture.GetTemporary(w, h, 0, ctx.Source.format);
            RenderTexture bloomB = RenderTexture.GetTemporary(w, h, 0, ctx.Source.format);

            try
            {
                // Pass 0: Threshold
                Graphics.Blit(ctx.Source, bloomA, _material, 0);

                // Pass 1 & 2: Iterative blur
                for (int i = 0; i < Iterations; i++)
                {
                    Graphics.Blit(bloomA, bloomB, _material, 1); // Blur H
                    Graphics.Blit(bloomB, bloomA, _material, 2); // Blur V
                }

                // Pass 3: Composite
                _material.SetTexture(BloomTexId, bloomA);
                Graphics.Blit(ctx.Source, ctx.Destination, _material, 3);
            }
            finally
            {
                _material.SetTexture(BloomTexId, null);
                RenderTexture.ReleaseTemporary(bloomA);
                RenderTexture.ReleaseTemporary(bloomB);
            }
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
            string keyword = GetQualityKeyword(PostFxQuality.High);
            if (!shaderProvider.IsAvailable(ShaderName, keyword))
                throw new InvalidOperationException($"Shader variant unavailable: {ShaderName} ({keyword})");
        }

        void ApplyQualityKeyword(PostFxQuality quality)
        {
            if (_material == null) return;

            _material.DisableKeyword(LowKeyword);
            _material.DisableKeyword(MediumKeyword);
            _material.DisableKeyword(HighKeyword);
            _material.DisableKeyword(UltraKeyword);

            string kw = GetQualityKeyword(quality);
            _material.EnableKeyword(kw);
        }

        static string GetQualityKeyword(PostFxQuality quality) => quality switch
        {
            PostFxQuality.Low => LowKeyword,
            PostFxQuality.Medium => MediumKeyword,
            PostFxQuality.High => HighKeyword,
            PostFxQuality.Ultra => UltraKeyword,
            _ => MediumKeyword,
        };
    }
}
