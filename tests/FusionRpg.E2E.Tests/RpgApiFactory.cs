using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FusionRpg.E2E.Tests;

public sealed class RpgApiFactory : WebApplicationFactory<Program>
{
    public string DataDir { get; } = Path.Combine(Path.GetTempPath(), "fusionrpg-e2e-" + Guid.NewGuid().ToString("N"));

    public RpgApiFactory()
    {
        Directory.CreateDirectory(DataDir);
        Environment.SetEnvironmentVariable("FUSIONRPG_SIM", "1");
        Environment.SetEnvironmentVariable("FUSIONRPG_NO_BROWSER", "1");
        Environment.SetEnvironmentVariable("FUSIONRPG_DATA", DataDir);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(DataDir);
        Environment.SetEnvironmentVariable("FUSIONRPG_SIM", "1");
        Environment.SetEnvironmentVariable("FUSIONRPG_NO_BROWSER", "1");
        Environment.SetEnvironmentVariable("FUSIONRPG_DATA", DataDir);
        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(DataDir, true); } catch { /* temp */ }
    }
}
