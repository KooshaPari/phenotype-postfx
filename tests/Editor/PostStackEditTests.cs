// T35: phenotype-postfx Unity Test Framework (UTF) EditMode tests.
// 12 test cases cover the BRP postfx chain.
using NUnit.Framework;
using UnityEngine;
using Phenotype.PostFx;

public class PostStackEditTests
{
    [Test] public void Pipeline_HasFiveStages() => Assert.AreEqual(5, PostStack.Stages.Length);
    [Test] public void SSAO_RunsFirst() => Assert.AreEqual("SSAO", PostStack.Stages[0]);
    [Test] public void SSGI_RunsSecond() => Assert.AreEqual("SSGI", PostStack.Stages[1]);
    [Test] public void Bloom_RunsThird() => Assert.AreEqual("Bloom", PostStack.Stages[2]);
    [Test] public void ACES_RunsFourth() => Assert.AreEqual("ACES", PostStack.Stages[3]);
    [Test] public void LUT_RunsLast() => Assert.AreEqual("LUT", PostStack.Stages[4]);
    [Test] public void Identity_LUT_PassesThrough() => Assert.IsTrue(PostStack.ApplyLut(Color.white).Equals(Color.white));
    [Test] public void Gamma_22_ToLinear() => Assert.AreEqual(0.5f, PostStack.GammaToLinear(0.5f * 0.5f), 1e-4f);
    [Test] public void Gamma_22_ToSRGB() => Assert.AreEqual(0.5f, PostStack.LinearToGamma(0.5f * 0.5f), 1e-4f);
    [Test] public void Bloom_Threshold_Default() => Assert.AreEqual(1.0f, PostStack.BloomThreshold);
    [Test] public void Shaders_Compile() => Assert.IsTrue(PostStack.ValidateShaders());
    [Test] public void PassRegistry_ContainsPasses() => Assert.Greater(PostStack.PassRegistry.Count, 0);
}
