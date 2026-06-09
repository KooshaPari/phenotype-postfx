// SPDX-License-Identifier: MIT OR Apache-2.0
// SPDX-FileCopyrightText: 2026 KooshaPari <kooshapari@gmail.com>

//! IPostFxPass — hexagonal port trait for post-processing passes.
//!
//! Every post-fx effect (Bloom, ACES, LUT, SSAO, SSGI, etc.) is an adapter
//! that implements this trait.  The PostStack driver calls them in order
//! without knowing the concrete type.
//!
//! Reference: phenotype-infra/REUSE.toml (T20), phenotype-voxel/src/ports/* (T2 SSOT pattern).

using System;
using UnityEngine;

namespace Phenotype.PostFx.Ports
{
    /// <summary>
    /// The hexagonal port for a single post-fx pass.
    /// Adapters (BrpBloomPass, UrpAcesPass, etc.) implement this trait.
    /// </summary>
    public interface IPostFxPass
    {
        /// <summary>Stable name used for ordering, logging, and profiling.</summary>
        string Name { get; }

        /// <summary>Relative cost hint (0.0 = free, 1.0 = full-frame).  Used by the scheduler.</summary>
        float Cost { get; }

        /// <summary>True if this pass should run on the current frame (e.g., toggle, quality setting).</summary>
        bool IsEnabled { get; }

        /// <summary>Build pass-specific material + camera target allocations.  Called once per camera.</summary>
        void OnSetup(PostFxContext ctx);

        /// <summary>Render the pass into the supplied source texture.  Output goes to <c>ctx.Destination</c>.</summary>
        void OnRender(PostFxContext ctx);

        /// <summary>Free materials + temporary targets.  Called when the camera is destroyed or the pass is disabled.</summary>
        void OnDispose();

        /// <summary>Validate that all required shader variants are present.  Throws if any are missing.</summary>
        void ValidateVariants(IShaderAvailabilityProvider shaderProvider);
    }

    /// <summary>
    /// Per-camera context passed to each pass.  Immutable from the pass's perspective
    /// (the PostStack driver swaps the source/destination pair between passes).
    /// </summary>
    public sealed class PostFxContext
    {
        public RenderTexture Source { get; }
        public RenderTexture Destination { get; }
        public Camera Camera { get; }
        public PostFxQuality Quality { get; }
        public MaterialPropertyBlock PropertyBlock { get; }

        public PostFxContext(
            RenderTexture source,
            RenderTexture destination,
            Camera camera,
            PostFxQuality quality,
            MaterialPropertyBlock propertyBlock)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Destination = destination ?? throw new ArgumentNullException(nameof(destination));
            Camera = camera ?? throw new ArgumentNullException(nameof(camera));
            Quality = quality;
            PropertyBlock = propertyBlock ?? new MaterialPropertyBlock();
        }
    }

    /// <summary>Quality settings — the driver passes the current value to each pass.</summary>
    [Flags]
    public enum PostFxQuality : byte
    {
        Off        = 0,
        Low        = 1 << 0,
        Medium     = 1 << 1,
        High       = 1 << 2,
        Ultra      = 1 << 3,
        All        = Low | Medium | High | Ultra
    }

    /// <summary>
    /// Adapter that wraps the legacy <c>IPostFxPassProvider</c> (in PostFxPassRegistry.cs)
    /// so it can also participate in the new hexagonal <c>IPostFxPass</c> registry.
    /// This is the bridge for the T21 migration — existing providers don't need
    /// to be rewritten, just wrapped.
    /// </summary>
    public sealed class PostFxPassProviderAdapter : IPostFxPass
    {
        private readonly IPostFxPassProvider _provider;
        private readonly PostStack _owner;

        public string Name => _provider.DisplayName;
        public float Cost => 0.1f; // Conservative default; can be tuned per-effect.
        public bool IsEnabled => _provider.IsEnabled(_owner);

        public PostFxPassProviderAdapter(IPostFxPassProvider provider, PostStack owner)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _provider.BindOwner(owner);
        }

        public void OnSetup(PostFxContext ctx) { /* providers handle their own state */ }
        public void OnRender(PostFxContext ctx) => _provider.Render(_owner, ctx.Source, ctx.Destination);
        public void OnDispose() { /* providers own their materials */ }
        public void ValidateVariants(IShaderAvailabilityProvider p)
        {
            if (!_provider.IsSupported(_owner))
                throw new InvalidOperationException($"Pass not supported: {Name}");
        }
    }
}
