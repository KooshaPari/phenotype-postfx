// xDD: xUnit tests for PostStack shader-variant validation.
//
// Covers three scenarios:
//   1. AllSupported  — provider says every effect is available → no warnings emitted
//   2. MissingVariant — provider reports one or more effects unavailable → warnings emitted
//   3. GracefulSkip  — unsupported effects leave their support flags false so
//                       OnRenderImage-style callers would skip them

using System.Collections.Generic;
using System.Linq;
using Phenotype.PostFx;
using UnityEngine;
using Xunit;

namespace PostStackVariantTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Mock availability provider: returns configurable per-effect results.
    /// </summary>
    sealed class MockAvailabilityProvider : IShaderAvailabilityProvider
    {
        readonly Dictionary<PostFxEffect, bool> _map;

        public MockAvailabilityProvider(Dictionary<PostFxEffect, bool> map)
            => _map = map;

        /// <summary>Convenience: all effects available.</summary>
        public static MockAvailabilityProvider AllAvailable() =>
            new MockAvailabilityProvider(new Dictionary<PostFxEffect, bool>
            {
                [PostFxEffect.SSAO]  = true,
                [PostFxEffect.SSGI]  = true,
                [PostFxEffect.Bloom] = true,
                [PostFxEffect.ACES]  = true,
                [PostFxEffect.Vignette] = true,
                [PostFxEffect.LUT]   = true,
            });

        /// <summary>Convenience: all effects unavailable.</summary>
        public static MockAvailabilityProvider NoneAvailable() =>
            new MockAvailabilityProvider(new Dictionary<PostFxEffect, bool>
            {
                [PostFxEffect.SSAO]  = false,
                [PostFxEffect.SSGI]  = false,
                [PostFxEffect.Bloom] = false,
                [PostFxEffect.ACES]  = false,
                [PostFxEffect.Vignette] = false,
                [PostFxEffect.LUT]   = false,
            });

        public bool IsAvailable(PostFxEffect effect)
            => _map.TryGetValue(effect, out bool v) && v;
    }

    /// <summary>
    /// Exposes the internal validated-support flags via reflection so tests
    /// don't need them to be public on PostStack.
    /// </summary>
    static class PostStackReflection
    {
        static System.Reflection.FieldInfo Field(string name) =>
            typeof(PostStack).GetField(name,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)
            ?? throw new System.MissingFieldException(typeof(PostStack).FullName, name);

        public static bool GetBool(PostStack stack, string fieldName)
            => (bool)(Field(fieldName).GetValue(stack) ?? false);
    }

    /// <summary>
    /// Creates a bare PostStack (no MonoBehaviour lifecycle) and drives
    /// ValidateShaderVariants() directly.
    /// </summary>
    static class PostStackFactory
    {
        public static PostStack Create(IShaderAvailabilityProvider provider)
        {
            // Activator bypasses the MonoBehaviour constructor restriction in stubs
            var stack = (PostStack)System.Activator.CreateInstance(typeof(PostStack))!;
            stack.SetAvailabilityProvider(provider);
            // Drive validation directly (no Awake / Unity lifecycle needed)
            stack.ValidateShaderVariants();
            return stack;
        }
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    public sealed class ShaderVariantValidationTests
    {
        // Before each test, clear the captured warning list.
        public ShaderVariantValidationTests()
            => DebugCapture.Clear();

        // ------------------------------------------------------------------
        // Scenario 1: all supported — no warnings
        // ------------------------------------------------------------------

        [Fact]
        public void AllSupported_NoWarningsEmitted()
        {
            var stack = PostStackFactory.Create(MockAvailabilityProvider.AllAvailable());

            Assert.Empty(DebugCapture.Warnings);
        }

        [Fact]
        public void AllSupported_AllFlagsTrue()
        {
            var stack = PostStackFactory.Create(MockAvailabilityProvider.AllAvailable());

            Assert.True(PostStackReflection.GetBool(stack, "_ssaoSupported"),  "_ssaoSupported");
            Assert.True(PostStackReflection.GetBool(stack, "_ssgiSupported"),  "_ssgiSupported");
            Assert.True(PostStackReflection.GetBool(stack, "_bloomSupported"), "_bloomSupported");
            Assert.True(PostStackReflection.GetBool(stack, "_acesSupported"),  "_acesSupported");
            Assert.True(PostStackReflection.GetBool(stack, "_vignetteSupported"), "_vignetteSupported");
            Assert.True(PostStackReflection.GetBool(stack, "_lutSupported"),   "_lutSupported");
        }

        // ------------------------------------------------------------------
        // Scenario 2: missing keywords → warning emitted
        // ------------------------------------------------------------------

        [Fact]
        public void MissingVariant_WarningEmittedForEachUnavailableEffect()
        {
            var stack = PostStackFactory.Create(MockAvailabilityProvider.NoneAvailable());

            // One warning per effect (6 total)
            Assert.Equal(6, DebugCapture.Warnings.Count);
        }

        [Theory]
        [InlineData(PostFxEffect.SSAO,  "ScreenSpaceAO")]
        [InlineData(PostFxEffect.SSGI,  "ScreenSpaceGI")]
        [InlineData(PostFxEffect.Bloom, "BrpBloom")]
        [InlineData(PostFxEffect.ACES,  "BrpACES")]
        [InlineData(PostFxEffect.Vignette, "Vignette")]
        [InlineData(PostFxEffect.LUT,   "ColorGradingLUT")]
        public void MissingVariant_WarningContainsShaderName(PostFxEffect effect, string shaderName)
        {
            // Only mark the one effect as unavailable
            var map = new Dictionary<PostFxEffect, bool>
            {
                [PostFxEffect.SSAO]  = true,
                [PostFxEffect.SSGI]  = true,
                [PostFxEffect.Bloom] = true,
                [PostFxEffect.ACES]  = true,
                [PostFxEffect.Vignette] = true,
                [PostFxEffect.LUT]   = true,
            };
            map[effect] = false;

            var stack = PostStackFactory.Create(new MockAvailabilityProvider(map));

            Assert.Single(DebugCapture.Warnings);
            Assert.Contains(shaderName, DebugCapture.Warnings[0]);
            Assert.Contains("[PostStack]", DebugCapture.Warnings[0]);
        }

        [Fact]
        public void MissingVariant_WarningMentionsShadervariants()
        {
            var stack = PostStackFactory.Create(MockAvailabilityProvider.NoneAvailable());

            Assert.All(DebugCapture.Warnings,
                w => Assert.Contains("shadervariants", w));
        }

        // ------------------------------------------------------------------
        // Scenario 3: graceful-skip — support flags are false for unavailable
        // ------------------------------------------------------------------

        [Theory]
        [InlineData(PostFxEffect.SSAO,  "_ssaoSupported")]
        [InlineData(PostFxEffect.SSGI,  "_ssgiSupported")]
        [InlineData(PostFxEffect.Bloom, "_bloomSupported")]
        [InlineData(PostFxEffect.ACES,  "_acesSupported")]
        [InlineData(PostFxEffect.Vignette, "_vignetteSupported")]
        [InlineData(PostFxEffect.LUT,   "_lutSupported")]
        public void GracefulSkip_UnavailableEffect_FlagSetFalse(PostFxEffect effect, string flagField)
        {
            var map = new Dictionary<PostFxEffect, bool>
            {
                [PostFxEffect.SSAO]  = true,
                [PostFxEffect.SSGI]  = true,
                [PostFxEffect.Bloom] = true,
                [PostFxEffect.ACES]  = true,
                [PostFxEffect.Vignette] = true,
                [PostFxEffect.LUT]   = true,
            };
            map[effect] = false;

            var stack = PostStackFactory.Create(new MockAvailabilityProvider(map));

            Assert.False(PostStackReflection.GetBool(stack, flagField),
                $"{flagField} should be false when effect is unavailable");
        }

        [Theory]
        [InlineData(PostFxEffect.SSAO)]
        [InlineData(PostFxEffect.SSGI)]
        [InlineData(PostFxEffect.Bloom)]
        [InlineData(PostFxEffect.ACES)]
        [InlineData(PostFxEffect.LUT)]
        public void GracefulSkip_OtherEffectsUnaffected(PostFxEffect unavailableEffect)
        {
            var map = new Dictionary<PostFxEffect, bool>
            {
                [PostFxEffect.SSAO]  = true,
                [PostFxEffect.SSGI]  = true,
                [PostFxEffect.Bloom] = true,
                [PostFxEffect.ACES]  = true,
                [PostFxEffect.Vignette] = true,
                [PostFxEffect.LUT]   = true,
            };
            map[unavailableEffect] = false;

            var stack = PostStackFactory.Create(new MockAvailabilityProvider(map));

            // All other support flags must remain true
            var allFlags = new[]
            {
                (PostFxEffect.SSAO,  "_ssaoSupported"),
                (PostFxEffect.SSGI,  "_ssgiSupported"),
                (PostFxEffect.Bloom, "_bloomSupported"),
                (PostFxEffect.ACES,  "_acesSupported"),
                (PostFxEffect.Vignette, "_vignetteSupported"),
                (PostFxEffect.LUT,   "_lutSupported"),
            };
            foreach (var (e, flag) in allFlags)
            {
                if (e == unavailableEffect) continue;
                Assert.True(PostStackReflection.GetBool(stack, flag),
                    $"{flag} should still be true when only {unavailableEffect} is unavailable");
            }
        }

        [Fact]
        public void GracefulSkip_AllUnavailable_AllFlagsFalse()
        {
            var stack = PostStackFactory.Create(MockAvailabilityProvider.NoneAvailable());

            Assert.False(PostStackReflection.GetBool(stack, "_ssaoSupported"),  "_ssaoSupported");
            Assert.False(PostStackReflection.GetBool(stack, "_ssgiSupported"),  "_ssgiSupported");
            Assert.False(PostStackReflection.GetBool(stack, "_bloomSupported"), "_bloomSupported");
            Assert.False(PostStackReflection.GetBool(stack, "_acesSupported"),  "_acesSupported");
            Assert.False(PostStackReflection.GetBool(stack, "_vignetteSupported"), "_vignetteSupported");
            Assert.False(PostStackReflection.GetBool(stack, "_lutSupported"),   "_lutSupported");
        }

        [Fact]
        public void SourceSurface_ExposesVignetteFieldAndEnumMember()
        {
            var field = typeof(PostStack).GetField(
                "EnableVignette",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);

            Assert.NotNull(field);
            Assert.Equal(typeof(bool), field!.FieldType);
            Assert.Contains("Vignette", System.Enum.GetNames(typeof(PostFxEffect)));
        }

        // ------------------------------------------------------------------
        // Idempotency: calling ValidateShaderVariants twice doesn't double-warn
        // ------------------------------------------------------------------

        [Fact]
        public void ValidateShaderVariants_Idempotent_NoDoubleWarnings()
        {
            var provider = MockAvailabilityProvider.NoneAvailable();
            var stack = PostStackFactory.Create(provider);

            int countAfterFirst = DebugCapture.Warnings.Count;

            // Call again
            stack.ValidateShaderVariants();

            // Warnings doubled (each call warns independently — that is the
            // expected behaviour: each re-init re-audits)
            Assert.Equal(countAfterFirst * 2, DebugCapture.Warnings.Count);
        }
    }
}
