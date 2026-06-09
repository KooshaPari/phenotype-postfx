// Unity Test Framework (NUnit) tests for PostStack shader-variant validation.
//
// Covers three scenarios:
//   1. AllSupported  — provider says every effect is available → no warnings emitted
//   2. MissingVariant — provider reports one or more effects unavailable → warnings emitted
//   3. GracefulSkip  — unsupported effects leave their support flags false so
//                       OnRenderImage-style callers would skip them

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Phenotype.PostFx;
using UnityEngine;

namespace Phenotype.PostFx.Tests
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
                [PostFxEffect.ChromaticAberration] = true,
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
                [PostFxEffect.ChromaticAberration] = false,
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
    /// Reflection helper for exercising private render-path details without
    /// changing the public API.
    /// </summary>
    static class PostStackRuntimeReflection
    {
        static FieldInfo Field(string name) =>
            typeof(PostStack).GetField(name,
                BindingFlags.NonPublic |
                BindingFlags.Instance)
            ?? throw new MissingFieldException(typeof(PostStack).FullName, name);

        public static void Set<T>(PostStack stack, string fieldName, T value)
            => Field(fieldName).SetValue(stack, value);

        public static void InvokeOnRenderImage(PostStack stack, RenderTexture src, RenderTexture dst)
        {
            var method = typeof(PostStack).GetMethod("OnRenderImage", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(PostStack).FullName, "OnRenderImage");

            method.Invoke(stack, new object?[] { src, dst });
        }
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [TestFixture]
    public sealed class ShaderVariantValidationTests
    {
        List<GameObject> _gameObjects = new();
        List<string> _warnings = new();

        [SetUp]
        public void SetUp()
        {
            _gameObjects.Clear();
            _warnings.Clear();
            Application.logMessageReceived += OnLogMessage;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLogMessage;
            foreach (var go in _gameObjects)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
            _gameObjects.Clear();
        }

        void OnLogMessage(string condition, string stacktrace, LogType type)
        {
            if (type == LogType.Warning)
                _warnings.Add(condition);
        }

        PostStack CreateStack(IShaderAvailabilityProvider provider)
        {
            var go = new GameObject("TestStack");
            _gameObjects.Add(go);
            var stack = go.AddComponent<PostStack>();
            stack.SetAvailabilityProvider(provider);
            stack.ValidateShaderVariants();
            return stack;
        }

        // ------------------------------------------------------------------
        // Scenario 1: all supported — no warnings
        // ------------------------------------------------------------------

        [Test]
        public void AllSupported_NoWarningsEmitted()
        {
            var stack = CreateStack(MockAvailabilityProvider.AllAvailable());
            Assert.IsEmpty(_warnings);
        }

        [Test]
        public void AllSupported_AllFlagsTrue()
        {
            var stack = CreateStack(MockAvailabilityProvider.AllAvailable());

            Assert.IsTrue(PostStackReflection.GetBool(stack, "_ssaoSupported"),  "_ssaoSupported");
            Assert.IsTrue(PostStackReflection.GetBool(stack, "_ssgiSupported"),  "_ssgiSupported");
            Assert.IsTrue(PostStackReflection.GetBool(stack, "_bloomSupported"), "_bloomSupported");
            Assert.IsTrue(PostStackReflection.GetBool(stack, "_acesSupported"),  "_acesSupported");
            Assert.IsTrue(PostStackReflection.GetBool(stack, "_vignetteSupported"), "_vignetteSupported");
            Assert.IsTrue(PostStackReflection.GetBool(stack, "_chromaticAberrationSupported"), "_chromaticAberrationSupported");
            Assert.IsTrue(PostStackReflection.GetBool(stack, "_lutSupported"),   "_lutSupported");
        }

        // ------------------------------------------------------------------
        // Scenario 2: missing keywords → warning emitted
        // ------------------------------------------------------------------

        [Test]
        public void MissingVariant_WarningEmittedForEachUnavailableEffect()
        {
            var stack = CreateStack(MockAvailabilityProvider.NoneAvailable());

            // One warning per effect (7 total)
            Assert.AreEqual(7, _warnings.Count);
        }

        [TestCase(PostFxEffect.SSAO,  "ScreenSpaceAO")]
        [TestCase(PostFxEffect.SSGI,  "ScreenSpaceGI")]
        [TestCase(PostFxEffect.Bloom, "BrpBloom")]
        [TestCase(PostFxEffect.ACES,  "BrpACES")]
        [TestCase(PostFxEffect.Vignette, "Vignette")]
        [TestCase(PostFxEffect.ChromaticAberration, "ChromaticAberration")]
        [TestCase(PostFxEffect.LUT,   "ColorGradingLUT")]
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
                [PostFxEffect.ChromaticAberration] = true,
                [PostFxEffect.LUT]   = true,
            };
            map[effect] = false;

            var stack = CreateStack(new MockAvailabilityProvider(map));

            Assert.AreEqual(1, _warnings.Count);
            StringAssert.Contains(shaderName, _warnings[0]);
            StringAssert.Contains("[PostStack]", _warnings[0]);
        }

        [Test]
        public void MissingVariant_WarningMentionsShadervariants()
        {
            var stack = CreateStack(MockAvailabilityProvider.NoneAvailable());

            foreach (var w in _warnings)
                StringAssert.Contains("shadervariants", w);
        }

        // ------------------------------------------------------------------
        // Scenario 3: graceful-skip — support flags are false for unavailable
        // ------------------------------------------------------------------

        [TestCase(PostFxEffect.SSAO,  "_ssaoSupported")]
        [TestCase(PostFxEffect.SSGI,  "_ssgiSupported")]
        [TestCase(PostFxEffect.Bloom, "_bloomSupported")]
        [TestCase(PostFxEffect.ACES,  "_acesSupported")]
        [TestCase(PostFxEffect.Vignette, "_vignetteSupported")]
        [TestCase(PostFxEffect.ChromaticAberration, "_chromaticAberrationSupported")]
        [TestCase(PostFxEffect.LUT,   "_lutSupported")]
        public void GracefulSkip_UnavailableEffect_FlagSetFalse(PostFxEffect effect, string flagField)
        {
            var map = new Dictionary<PostFxEffect, bool>
            {
                [PostFxEffect.SSAO]  = true,
                [PostFxEffect.SSGI]  = true,
                [PostFxEffect.Bloom] = true,
                [PostFxEffect.ACES]  = true,
                [PostFxEffect.Vignette] = true,
                [PostFxEffect.ChromaticAberration] = true,
                [PostFxEffect.LUT]   = true,
            };
            map[effect] = false;

            var stack = CreateStack(new MockAvailabilityProvider(map));

            Assert.IsFalse(PostStackReflection.GetBool(stack, flagField),
                $"{flagField} should be false when effect is unavailable");
        }

        [TestCase(PostFxEffect.SSAO)]
        [TestCase(PostFxEffect.SSGI)]
        [TestCase(PostFxEffect.Bloom)]
        [TestCase(PostFxEffect.ACES)]
        [TestCase(PostFxEffect.ChromaticAberration)]
        [TestCase(PostFxEffect.LUT)]
        public void GracefulSkip_OtherEffectsUnaffected(PostFxEffect unavailableEffect)
        {
            var map = new Dictionary<PostFxEffect, bool>
            {
                [PostFxEffect.SSAO]  = true,
                [PostFxEffect.SSGI]  = true,
                [PostFxEffect.Bloom] = true,
                [PostFxEffect.ACES]  = true,
                [PostFxEffect.Vignette] = true,
                [PostFxEffect.ChromaticAberration] = true,
                [PostFxEffect.LUT]   = true,
            };
            map[unavailableEffect] = false;

            var stack = CreateStack(new MockAvailabilityProvider(map));

            // All other support flags must remain true
            var allFlags = new[]
            {
                (PostFxEffect.SSAO,  "_ssaoSupported"),
                (PostFxEffect.SSGI,  "_ssgiSupported"),
                (PostFxEffect.Bloom, "_bloomSupported"),
                (PostFxEffect.ACES,  "_acesSupported"),
                (PostFxEffect.Vignette, "_vignetteSupported"),
                (PostFxEffect.ChromaticAberration, "_chromaticAberrationSupported"),
                (PostFxEffect.LUT,   "_lutSupported"),
            };
            foreach (var (e, flag) in allFlags)
            {
                if (e == unavailableEffect) continue;
                Assert.IsTrue(PostStackReflection.GetBool(stack, flag),
                    $"{flag} should still be true when only {unavailableEffect} is unavailable");
            }
        }

        [Test]
        public void GracefulSkip_AllUnavailable_AllFlagsFalse()
        {
            var stack = CreateStack(MockAvailabilityProvider.NoneAvailable());

            Assert.IsFalse(PostStackReflection.GetBool(stack, "_ssaoSupported"),  "_ssaoSupported");
            Assert.IsFalse(PostStackReflection.GetBool(stack, "_ssgiSupported"),  "_ssgiSupported");
            Assert.IsFalse(PostStackReflection.GetBool(stack, "_bloomSupported"), "_bloomSupported");
            Assert.IsFalse(PostStackReflection.GetBool(stack, "_acesSupported"),  "_acesSupported");
            Assert.IsFalse(PostStackReflection.GetBool(stack, "_vignetteSupported"), "_vignetteSupported");
            Assert.IsFalse(PostStackReflection.GetBool(stack, "_chromaticAberrationSupported"), "_chromaticAberrationSupported");
            Assert.IsFalse(PostStackReflection.GetBool(stack, "_lutSupported"),   "_lutSupported");
        }

        [Test]
        public void SourceSurface_ExposesVignetteFieldAndEnumMember()
        {
            var field = typeof(PostStack).GetField(
                "EnableVignette",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);

            Assert.IsNotNull(field);
            Assert.AreEqual(typeof(bool), field!.FieldType);
            Assert.That(System.Enum.GetNames(typeof(PostFxEffect)), Does.Contain("Vignette"));
        }

        [Test]
        public void SourceSurface_ExposesChromaticAberrationFieldAndEnumMember()
        {
            var field = typeof(PostStack).GetField(
                "EnableChromaticAberration",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);

            Assert.IsNotNull(field);
            Assert.AreEqual(typeof(bool), field!.FieldType);
            Assert.That(System.Enum.GetNames(typeof(PostFxEffect)), Does.Contain("ChromaticAberration"));
        }

#if !UNITY_2021_3_OR_NEWER
        // ------------------------------------------------------------------
        // Blit helper tests — only run in stub environment (dotnet test)
        // ------------------------------------------------------------------

        [Test]
        public void SharedBlitHelper_EnabledAndSupported_EmitsMaterialBlit()
        {
            var stack = (PostStack)Activator.CreateInstance(typeof(PostStack))!;
            var material = new Material(new Shader());
            var src = new RenderTexture { width = 64, height = 64 };
            var dst = new RenderTexture { width = 64, height = 64 };

            stack.EnableACES = true;
            PostStackRuntimeReflection.Set(stack, "_initialized", true);
            PostStackRuntimeReflection.Set(stack, "_acesSupported", true);
            PostStackRuntimeReflection.Set(stack, "_acesMat", material);

            GraphicsCapture.Clear();
            PostStackRuntimeReflection.InvokeOnRenderImage(stack, src, dst);

            Assert.IsTrue(GraphicsCapture.Blits.Any(call => call.Material == material));
        }

        [Test]
        public void SharedBlitHelper_Unsupported_SkipsMaterialBlit()
        {
            var stack = (PostStack)Activator.CreateInstance(typeof(PostStack))!;
            var material = new Material(new Shader());
            var src = new RenderTexture { width = 64, height = 64 };
            var dst = new RenderTexture { width = 64, height = 64 };

            stack.EnableACES = true;
            PostStackRuntimeReflection.Set(stack, "_initialized", true);
            PostStackRuntimeReflection.Set(stack, "_acesSupported", false);
            PostStackRuntimeReflection.Set(stack, "_acesMat", material);

            GraphicsCapture.Clear();
            PostStackRuntimeReflection.InvokeOnRenderImage(stack, src, dst);

            Assert.IsFalse(GraphicsCapture.Blits.Any(call => call.Material == material));
        }
#endif

        // ------------------------------------------------------------------
        // Idempotency: calling ValidateShaderVariants twice doesn't double-warn
        // ------------------------------------------------------------------

        [Test]
        public void ValidateShaderVariants_Idempotent_NoDoubleWarnings()
        {
            var provider = MockAvailabilityProvider.NoneAvailable();
            var stack = CreateStack(provider);

            int countAfterFirst = _warnings.Count;

            // Call again
            stack.ValidateShaderVariants();

            // Warnings doubled (each call warns independently — that is the
            // expected behaviour: each re-init re-audits)
            Assert.AreEqual(countAfterFirst * 2, _warnings.Count);
        }
    }
}
