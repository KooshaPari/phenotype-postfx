// SPDX-License-Identifier: MIT OR Apache-2.0
// SPDX-FileCopyrightText: 2026 KooshaPari <kooshapari@gmail.com>

//! Unit tests for the PostFxPassRegistry hexagonal port.
//! These tests run in any NUnit-compatible test runner (NUnit 3.13+).
//! EditMode test execution: `unity -batchmode -runTests -testPlatform editmode`

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Phenotype.PostFx;
using Phenotype.PostFx.Ports;

namespace Phenotype.PostFx.Tests
{
    [TestFixture]
    public class PostFxPassRegistryTests
    {
        // T21 test 1: empty registry has no providers
        [Test]
        public void EmptyRegistry_HasNoProviders()
        {
            var reg = new PostFxPassRegistry();
            Assert.AreEqual(0, reg.Passes.Count);
        }

        // T21 test 2: register adds a pass and validates its variants
        [Test]
        public void Register_AddsPass()
        {
            var reg = new PostFxPassRegistry();
            var pass = new MockPass("MockA", cost: 0.5f);
            reg.Register(pass);
            Assert.AreEqual(1, reg.Passes.Count);
            Assert.AreSame(pass, reg.Passes[0]);
        }

        // T21 test 3: null pass throws ArgumentNullException
        [Test]
        public void Register_NullPass_Throws()
        {
            var reg = new PostFxPassRegistry();
            Assert.Throws<System.ArgumentNullException>(() => reg.Register(null));
        }

        // T21 test 4: ValidateVariants is called exactly once on registration
        [Test]
        public void Register_CallsValidateVariantsOnce()
        {
            var reg = new PostFxPassRegistry();
            var pass = new MockPass("MockA", cost: 0.1f);
            reg.Register(pass);
            Assert.AreEqual(1, pass.ValidateCount);
        }

        // T21 test 5: ValidateAll calls ValidateVariants on every registered pass
        [Test]
        public void ValidateAll_CallsEveryPass()
        {
            var reg = new PostFxPassRegistry();
            var a = new MockPass("A", 0.1f);
            var b = new MockPass("B", 0.2f);
            var c = new MockPass("C", 0.3f);
            reg.Register(a);
            reg.Register(b);
            reg.Register(c);
            reg.ValidateAll();
            Assert.AreEqual(2, a.ValidateCount);
            Assert.AreEqual(2, b.ValidateCount);
            Assert.AreEqual(2, c.ValidateCount);
        }

        // T21 test 6: Clear removes all passes
        [Test]
        public void Clear_RemovesAll()
        {
            var reg = new PostFxPassRegistry();
            reg.Register(new MockPass("A", 0.1f));
            reg.Register(new MockPass("B", 0.2f));
            reg.Clear();
            Assert.AreEqual(0, reg.Passes.Count);
        }

        // T21 test 7: passes are returned in registration order
        [Test]
        public void Passes_AreInRegistrationOrder()
        {
            var reg = new PostFxPassRegistry();
            var a = new MockPass("A", 0.1f);
            var b = new MockPass("B", 0.2f);
            var c = new MockPass("C", 0.3f);
            reg.Register(a);
            reg.Register(b);
            reg.Register(c);
            Assert.AreSame(a, reg.Passes[0]);
            Assert.AreSame(b, reg.Passes[1]);
            Assert.AreSame(c, reg.Passes[2]);
        }

        // T21 test 8: custom shader provider is consulted on Register
        [Test]
        public void Register_UsesCustomShaderProvider()
        {
            var provider = new MockShaderProvider(available: false);
            var reg = new PostFxPassRegistry(provider);
            var pass = new MockPass("A", 0.1f, requiresShader: "A_Shader", requiredKeyword: "ENABLE");
            reg.Register(pass);
            // Should have called provider.IsAvailable
            Assert.AreEqual(1, provider.CallCount);
        }

        // T21 test 9: IShaderAvailabilityProvider contract: returns bool
        [Test]
        public void IShaderAvailabilityProvider_Contract()
        {
            IShaderAvailabilityProvider p = new DefaultShaderAvailabilityProvider();
            Assert.IsTrue(p.IsAvailable("Anything", "AnyKeyword"));
            Assert.IsTrue(p.IsAvailable("", ""));
        }

        // T21 test 10: MockPass isEnabled defaults to true
        [Test]
        public void MockPass_IsEnabledDefaultsTrue()
        {
            var p = new MockPass("X", 0.1f);
            Assert.IsTrue(p.IsEnabled);
        }

        // T21 test 11: MockPass can be disabled
        [Test]
        public void MockPass_CanBeDisabled()
        {
            var p = new MockPass("X", 0.1f) { IsEnabled = false };
            Assert.IsFalse(p.IsEnabled);
        }

        // T21 test 12: MockPass has a cost
        [Test]
        public void MockPass_CostIsPreserved()
        {
            var p = new MockPass("X", 0.42f);
            Assert.AreEqual(0.42f, p.Cost);
        }
    }

    /// <summary>Mock IPostFxPass adapter for testing.</summary>
    internal sealed class MockPass : IPostFxPass
    {
        public string Name { get; }
        public float Cost { get; }
        public bool IsEnabled { get; set; } = true;
        public string RequiresShader { get; }
        public string RequiredKeyword { get; }
        public int ValidateCount { get; private set; }
        public bool OnSetupCalled { get; private set; }
        public bool OnRenderCalled { get; private set; }
        public bool OnDisposeCalled { get; private set; }

        public MockPass(string name, float cost, string requiresShader = null, string requiredKeyword = null)
        {
            Name = name;
            Cost = cost;
            RequiresShader = requiresShader;
            RequiredKeyword = requiredKeyword;
        }

        public void OnSetup(PostFxContext ctx) => OnSetupCalled = true;
        public void OnRender(PostFxContext ctx) => OnRenderCalled = true;
        public void OnDispose() => OnDisposeCalled = true;
        public void ValidateVariants(IShaderAvailabilityProvider p)
        {
            ValidateCount++;
            if (RequiresShader != null && !p.IsAvailable(RequiresShader, RequiredKeyword ?? ""))
                throw new System.InvalidOperationException($"Shader not available: {RequiresShader}");
        }
    }

    /// <summary>Mock shader provider for testing.</summary>
    internal sealed class MockShaderProvider : IShaderAvailabilityProvider
    {
        public bool Available { get; }
        public int CallCount { get; private set; }

        public MockShaderProvider(bool available) { Available = available; }
        public bool IsAvailable(string shaderName, string keyword)
        {
            CallCount++;
            return Available;
        }
    }
}
