using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace BetterRimworlds.ChickenBatteryCage.Tests;

// The test assembly does not load RimWorld's window classes.
public class CageHenIntakeWindowTests
{
    static string Source()
    {
        string path = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "Source", "Window_CageHenIntake.cs");
        return Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/|//[^\r\n]*", "", RegexOptions.Singleline);
    }

    [Test]
    public void NetworkWritesRequirePlayerChanges()
    {
        string source = Source();
        Assert.That(Regex.Matches(source, @"CageNetwork\.SetMaximumIntakeAgeYears\("), Has.Count.EqualTo(1));
        Assert.That(source, Does.Match(
            @"if \(changed \|\| \(limited && selectedAge != previousAge\)\)\s*\{\s*observedMaximum = limited \? selectedAge : -1f;\s*CageNetwork\.SetMaximumIntakeAgeYears\(cluster, observedMaximum\);"));
        Assert.That(source, Does.Contain("float previousAge = selectedAge;"));
        Assert.That(source, Does.Contain("!limited) && limited)"));
        Assert.That(source, Does.Contain("limited) && !limited)"));
    }

    [Test]
    public void ClusterAndPolicyChangesRefreshDisplayedAgeBeforeDrawingControls()
    {
        string source = Source();
        Assert.That(source, Does.Match(
            @"if \(membershipChanged \|\| observedMaximum != maximum\)\s*\{\s*observedMaximum = maximum;\s*selectedAge = maximum >= 0f \? maximum : 2f;\s*ageBuffer = selectedAge.ToString\(\);"));
        Assert.That(source, Does.Contain("new HashSet<Building_ChickenBatteryCage>(observedCluster).SetEquals(cluster)"));
        Assert.That(source, Does.Match(
            @"var cluster = CageNetwork.Cluster\(cage\);\s*RefreshSelection\(cluster\);\s*bool limited = observedMaximum >= 0f;"));
    }
}
