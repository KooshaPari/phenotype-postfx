using System.Collections.Generic;
using UnityEngine;

namespace Phenotype.PostFx
{
    /// <summary>
    /// Composio-style pass provider contract. Each adapter (built-in pass,
    /// third-party effect, mod extension) implements this to register itself
    /// with the <see cref="PostFxPassRegistry"/>.
    /// </summary>
    public interface IPostFxPassProvider
    {
        /// <summary>Unique identifier for this pass provider.</summary>
        PostFxEffect Effect { get; }

        /// <summary>Human-readable display name.</summary>
        string DisplayName { get; }

        /// <summary>Material used for this pass. Null if the shader is unavailable.</summary>
        Material? Material { get; }

        /// <summary>Whether the pass is currently enabled by the user.</summary>
        bool IsEnabled(PostStack owner);

        /// <summary>Whether the shader is supported in this build.</summary>
        bool IsSupported(PostStack owner);

        /// <summary>Apply per-frame parameters before blit.</summary>
        void ApplyParams(PostStack owner);

        /// <summary>
        /// Execute custom render logic (e.g. Bloom multi-pass). Return true
        /// if the pass wrote to <paramref name="dst"/>.
        /// </summary>
        bool Render(PostStack owner, RenderTexture src, RenderTexture dst);
    }

    /// <summary>
    /// Built-in single-blit pass provider. Most effects (SSAO, SSGI, ACES,
    /// Vignette, ChromaticAberration, LUT) use this default implementation.
    /// </summary>
    internal sealed class BlitPassProvider : IPostFxPassProvider
    {
        readonly string _shaderName;
        readonly System.Func<PostStack, Material?> _materialAccessor;
        readonly System.Func<PostStack, bool> _enabledAccessor;
        readonly System.Func<PostStack, bool> _supportedAccessor;
        readonly System.Action<PostStack> _applyParams;

        public PostFxEffect Effect { get; }
        public string DisplayName { get; }
        public Material? Material => _owner != null ? _materialAccessor(_owner) : null;

        PostStack _owner;

        public BlitPassProvider(
            PostFxEffect effect,
            string displayName,
            string shaderName,
            System.Func<PostStack, Material?> materialAccessor,
            System.Func<PostStack, bool> enabledAccessor,
            System.Func<PostStack, bool> supportedAccessor,
            System.Action<PostStack> applyParams)
        {
            Effect = effect;
            DisplayName = displayName;
            _shaderName = shaderName;
            _materialAccessor = materialAccessor;
            _enabledAccessor = enabledAccessor;
            _supportedAccessor = supportedAccessor;
            _applyParams = applyParams;
        }

        public void BindOwner(PostStack owner) => _owner = owner;

        public bool IsEnabled(PostStack owner) => _enabledAccessor(owner);
        public bool IsSupported(PostStack owner) => _supportedAccessor(owner);

        public void ApplyParams(PostStack owner) => _applyParams(owner);

        public bool Render(PostStack owner, RenderTexture src, RenderTexture dst)
        {
            var mat = Material;
            if (mat == null) return false;
            Graphics.Blit(src, dst, mat);
            return true;
        }
    }

    /// <summary>
    /// Bloom-specific multi-pass provider. Encapsulates the 4-pass threshold →
    /// blur H → blur V → composite chain that cannot be expressed as a single blit.
    /// </summary>
    internal sealed class BloomPassProvider : IPostFxPassProvider
    {
        public PostFxEffect Effect => PostFxEffect.Bloom;
        public string DisplayName => "Bloom";

        public Material? Material => _owner?._bloomMat;

        PostStack _owner;
        public void BindOwner(PostStack owner) => _owner = owner;

        public bool IsEnabled(PostStack owner) => owner.EnableBloom;
        public bool IsSupported(PostStack owner) => owner._bloomSupported;

        public void ApplyParams(PostStack owner) { }

        public bool Render(PostStack owner, RenderTexture src, RenderTexture dst)
        {
            var bloomMat = owner._bloomMat;
            if (bloomMat == null) return false;

            int w = Mathf.Max(1, src.width / 4);
            int h = Mathf.Max(1, src.height / 4);
            RenderTexture bA = RenderTexture.GetTemporary(w, h, 0, src.format);
            RenderTexture bB = RenderTexture.GetTemporary(w, h, 0, src.format);
            try
            {
                Graphics.Blit(src, bA, bloomMat, 0);
                Graphics.Blit(bA, bB, bloomMat, 1);
                Graphics.Blit(bB, bA, bloomMat, 2);
                bloomMat.SetTexture(PostStack.BloomTexId, bA);
                Graphics.Blit(src, dst, bloomMat, 3);
                return true;
            }
            finally
            {
                bloomMat.SetTexture(PostStack.BloomTexId, null);
                RenderTexture.ReleaseTemporary(bA);
                RenderTexture.ReleaseTemporary(bB);
            }
        }
    }

    /// <summary>
    /// Composio-style provider registry for post-processing passes.
    /// Replaces the hard-coded switch statements in <see cref="PostStack"/>
    /// with a discoverable, extensible registration model.
    /// </summary>
    public sealed class PostFxPassRegistry
    {
        readonly Dictionary<PostFxEffect, IPostFxPassProvider> _providers = new();
        readonly List<PostFxEffect> _renderOrder = new();

        /// <summary>All registered providers in current render order.</summary>
        public IEnumerable<IPostFxPassProvider> Providers => _renderOrder.ConvertAll(e => _providers[e]);

        /// <summary>Register a provider. Overwrites any existing provider for the same effect.</summary>
        public void Register(IPostFxPassProvider provider)
        {
            _providers[provider.Effect] = provider;
            if (!_renderOrder.Contains(provider.Effect))
                _renderOrder.Add(provider.Effect);
        }

        /// <summary>Remove a provider from the registry.</summary>
        public void Unregister(PostFxEffect effect)
        {
            _providers.Remove(effect);
            _renderOrder.Remove(effect);
        }

        /// <summary>Change the render order of effects. Any omitted effects keep their relative order.</summary>
        public void SetRenderOrder(params PostFxEffect[] order)
        {
            var newOrder = new List<PostFxEffect>(order);
            foreach (var effect in _renderOrder)
            {
                if (!newOrder.Contains(effect))
                    newOrder.Add(effect);
            }
            _renderOrder.Clear();
            _renderOrder.AddRange(newOrder);
        }

        /// <summary>Get a provider by effect, or null if not registered.</summary>
        public IPostFxPassProvider? GetProvider(PostFxEffect effect)
        {
            _providers.TryGetValue(effect, out var provider);
            return provider;
        }

        /// <summary>
        /// Bind all providers that require an owner reference to the given stack.
        /// Call after construction or after replacing the registry.
        /// </summary>
        public void BindOwner(PostStack owner)
        {
            foreach (var p in _providers.Values)
            {
                if (p is BlitPassProvider bp) bp.BindOwner(owner);
                else if (p is BloomPassProvider bloom) bloom.BindOwner(owner);
            }
        }

        /// <summary>True if any registered pass is active for the given owner.</summary>
        public bool HasAnyActivePass(PostStack owner)
        {
            foreach (var effect in _renderOrder)
            {
                if (_providers.TryGetValue(effect, out var p))
                {
                    if (p.IsEnabled(owner) && p.IsSupported(owner) && p.Material != null)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Build the default registry with all built-in passes.
        /// </summary>
        public static PostFxPassRegistry CreateDefault()
        {
            var registry = new PostFxPassRegistry();

            registry.Register(new BlitPassProvider(
                PostFxEffect.SSAO, "SSAO", "ScreenSpaceAO",
                o => o._ssaoMat,
                o => o.EnableSSAO,
                o => o._ssaoSupported,
                o => o.ApplySSAOParams()));

            registry.Register(new BlitPassProvider(
                PostFxEffect.SSGI, "SSGI", "ScreenSpaceGI",
                o => o._ssgiMat,
                o => o.EnableSSGI,
                o => o._ssgiSupported,
                o => o.ApplySSGIParams()));

            registry.Register(new BloomPassProvider());

            registry.Register(new BlitPassProvider(
                PostFxEffect.ACES, "ACES", "BrpACES",
                o => o._acesMat,
                o => o.EnableACES,
                o => o._acesSupported,
                o => o.ApplyACESParams()));

            registry.Register(new BlitPassProvider(
                PostFxEffect.Vignette, "Vignette", "Vignette",
                o => o._vignetteMat,
                o => o.EnableVignette,
                o => o._vignetteSupported,
                o => o.ApplyVignetteParams()));

            registry.Register(new BlitPassProvider(
                PostFxEffect.ChromaticAberration, "ChromaticAberration", "ChromaticAberration",
                o => o._chromaticAberrationMat,
                o => o.EnableChromaticAberration,
                o => o._chromaticAberrationSupported,
                o => o.ApplyChromaticAberrationParams()));

            // LUT is handled as a special final pass in PostStack, but we register it
            // so the registry can report its availability.
            registry.Register(new BlitPassProvider(
                PostFxEffect.LUT, "LUT", "ColorGradingLUT",
                o => o._lutMat,
                o => o.EnableLUT,
                o => o._lutSupported,
                o => { }));

            return registry;
        }
    }
}
