// SPDX-License-Identifier: MIT OR Apache-2.0
// SPDX-FileCopyrightText: 2026 KooshaPari <kooshapari@gmail.com>

//! T27 stub: URP 17 RecordRenderGraph adapter contract.
//!
//! URP 17 (Unity 6) replaces the OnRenderImage callback with the
//! ScriptableRenderPass.RecordRenderGraph API.  This file is the
//! specification of the adapter port trait that the 7 existing passes
//! must implement when migrating from BRP (OnRenderImage) to URP 17.
//!
//! Reference: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/manual/renderer-graph.html

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Phenotype.PostFx.Urp
{
    /// <summary>
    /// Hexagonal port: a post-fx pass that can record itself into a URP 17
    /// RenderGraph.  Each existing BRP pass will gain an adapter implementing
    /// this port without changing the BRP-side implementation.
    /// </summary>
    public interface IUrpPostFxPass
    {
        /// <summary>Stable name for profiling + ordering.</summary>
        string Name { get; }

        /// <summary>True if this pass should run in the current frame.</summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Record the pass into the supplied RenderGraph.  The pass is responsible
        /// for declaring its inputs (via UseTexture) and outputs (via SetRenderAttachment).
        /// </summary>
        void RecordRenderGraph(RenderGraph graph, ContextContainer frameData, UrpPostFxContext ctx);
    }

    /// <summary>Per-camera context for the URP 17 adapter.</summary>
    public sealed class UrpPostFxContext
    {
        public Camera Camera { get; }
        public UniversalResourceData ResourceData { get; }
        public UniversalCameraData CameraData { get; }

        public UrpPostFxContext(Camera camera, UniversalResourceData resourceData, UniversalCameraData cameraData)
        {
            Camera = camera;
            ResourceData = resourceData;
            CameraData = cameraData;
        }
    }

    /// <summary>
    /// Adapter that bridges an existing IPostFxPass (BRP-side) to the URP 17
    /// RecordRenderGraph API.  It allocates a temporary texture, runs the BRP
    /// blit into it, then writes the result back to the active color buffer.
    /// </summary>
    public sealed class BrpToUrpAdapter : IUrpPostFxPass
    {
        private readonly IPostFxPass _brpPass;
        public string Name => _brpPass.Name;
        public bool IsEnabled => _brpPass.IsEnabled;

        public BrpToUrpAdapter(IPostFxPass brpPass)
        {
            _brpPass = brpPass ?? throw new System.ArgumentNullException(nameof(brpPass));
        }

        public void RecordRenderGraph(RenderGraph graph, ContextContainer frameData, UrpPostFxContext ctx)
        {
            // The actual URP 17 implementation requires Unity 6 + URP 17 packages.
            // This stub documents the API; the migration work (T27) will fill it in
            // once the project upgrades to Unity 6 in a follow-up.
            // See: docs/migration/urp-17.md (created in T27 batch)
        }
    }
}
