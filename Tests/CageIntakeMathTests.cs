using NUnit.Framework;

namespace BetterRimworlds.ChickenBatteryCage.Tests;

public class CageIntakeMathTests
{
    const int TicksPerYear = 3600000;

    [TestCase(0L, -1f, true)]
    [TestCase(46800000L, -1f, true)]
    [TestCase(3600000L, 2f, true)]
    [TestCase(7200000L, 2f, true)]
    [TestCase(7200001L, 2f, false)]
    [TestCase(5400000L, 1.5f, true)]
    [TestCase(5400001L, 1.5f, false)]
    [TestCase(0L, 0f, true)]
    [TestCase(1L, 0f, false)]
    public void AdmissionUsesExactBiologicalAge(long age, float maximum, bool expected)
    {
        Assert.That(CageIntakeMath.AcceptsAge(age, maximum, TicksPerYear), Is.EqualTo(expected));
    }
}
