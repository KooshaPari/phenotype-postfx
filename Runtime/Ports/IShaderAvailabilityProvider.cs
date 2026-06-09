// SPDX-License-Identifier: MIT OR Apache-2.0
// SPDX-FileCopyrightText: 2026 KooshaPari <kooshapari@gmail.com>

//! IShaderAvailabilityProvider — port for shader-variant detection.
//! Adapters query this to validate that all required shader variants are loaded
//! before the pass runs (otherwise the pass would no-op or assert at runtime).

namespace Phenotype.PostFx.Ports
{
    /// <summary>
    /// Hexagonal port: asks the platform "is this shader available right now?"
    /// Adapters include DefaultShaderAvailabilityProvider (built-in),
    /// AddressablesShaderProvider, and AssetBundleShaderProvider.
    /// </summary>
    public interface IShaderAvailabilityProvider
    {
        /// <summary>True if the named shader is loaded and the keyword <paramref name="keyword"/> is supported.</summary>
        bool IsAvailable(string shaderName, string keyword);
    }

    /// <summary>Default implementation that always returns true (built-in render pipeline).</summary>
    public sealed class DefaultShaderAvailabilityProvider : IShaderAvailabilityProvider
    {
        public bool IsAvailable(string shaderName, string keyword) => true;
    }
}
