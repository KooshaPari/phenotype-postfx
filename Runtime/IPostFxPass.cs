using UnityEngine;

namespace Phenotype.PostFx
{
    /// <summary>
    /// Minimal post-processing pass interface. Every pass (built-in or custom)
    /// implements this so the <see cref="PostStack"/> driver can initialise,
    /// render, and clean up passes without knowing their concrete type.
    /// </summary>
    public interface IPostFxPass
    {
        /// <summary>Bind the owning PostStack. Called once after construction.</summary>
        void Init(PostStack owner);

        /// <summary>Render the pass from <paramref name="src"/> into <paramref name="dst"/>.</summary>
        /// <returns>True if the pass wrote to <paramref name="dst"/>.</returns>
        bool Render(RenderTexture src, RenderTexture dst);

        /// <summary>Release pass-specific resources. Called when the stack is destroyed.</summary>
        void Dispose();
    }
}
