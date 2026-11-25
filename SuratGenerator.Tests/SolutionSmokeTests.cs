using SuratGenerator.Core;

namespace SuratGenerator.Tests;

public class SolutionSmokeTests
{
    [Fact]
    public void CoreAssemblyTerakses()
    {
        var name = typeof(SuratGeneratorAssembly).Assembly.GetName().Name;

        Assert.Equal("SuratGenerator.Core", name);
    }
}
