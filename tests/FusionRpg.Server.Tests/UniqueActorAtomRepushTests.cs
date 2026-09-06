using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Effects;
using FusionRpg.Data;
using FusionRpg.Data.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>
/// T6.1 (`mods-absorption`, 2026-09-06) — the real remaining gap the audit found: the Hello-time
/// atom-push union (<see cref="AtomPushService.OwnersForPlayer"/>) never re-fired mid-session, so a
/// unique actor deployed or recovered after Hello never actually reached the injector's runner. Proves
/// the WHOLE chain against a real in-process host — a real ack event through the real
/// <c>/api/events</c> endpoint, through the real <see cref="EventIngest"/> background drain, into the
/// real <see cref="UniqueActorService.ObserveEvents"/> — lands a real, correctly-shaped
/// <c>effects.grants.apply</c> command in the real <see cref="InjectorCommandInbox"/>, not just that
/// <c>ObserveUniqueActorEvents</c> returns the right player id in isolation (proven separately in
/// <c>UniqueActorStoreTests</c>).
/// </summary>
public class UniqueActorAtomRepushTests : IAsyncLifetime
{
    string _dir = "";
    RpgStore _store = null!;
    WebApplication _app = null!;
    HttpClient _http = null!;

    public async Task InitializeAsync()
    {
        var dir = Path.Combine(Path.GetTempPath(), "fusionrpg-unique-repush-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var store = new RpgStore(dir);
        store.Init();

        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton<InjectorCommandInbox>();
        builder.Services.AddSingleton<EffectGrantSession>();
        builder.Services.AddSingleton<IHotCompactor>(sp => new HotCompactor(sp.GetRequiredService<RpgStore>()));
        builder.Services.AddSingleton<CompactionWorker>();
        builder.Services.AddSingleton<UniqueActorService>();
        builder.Services.AddSingleton<EventIngest>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<EventIngest>());
        builder.WebHost.UseUrls(baseUrl);
        var app = builder.Build();
        app.UseDeveloperExceptionPage();
        app.MapHub<RpgHub>("/hub/rpg");
        app.MapUniqueActors();

        // Program.cs:842's own inline mapping — replicated exactly (never a hand-simplified stand-in)
        // so this test exercises the real request shape a real injector POST would send, not an
        // invented one.
        app.MapPost("/api/events", (JsonElement body, EventIngest ingest) =>
        {
            var accepted = 0;
            var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("events", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var env = item.Deserialize<EventEnvelope>(json);
                    if (env is not null && !string.IsNullOrWhiteSpace(env.Kind))
                        accepted += ingest.Enqueue(env);
                }
            }
            else
            {
                var env = body.Deserialize<EventEnvelope>(json);
                if (env is not null && !string.IsNullOrWhiteSpace(env.Kind))
                    accepted += ingest.Enqueue(env);
            }
            return Results.Ok(new { accepted });
        });

        await app.StartAsync();

        _dir = dir;
        _store = store;
        _app = app;
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    static int GetFreeTcpPort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    async Task PostEventAsync(string kind, string? matchKey, object payload)
    {
        var resp = await _http.PostAsJsonAsync("/api/events", new { kind, matchKey, payload });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        await _app.Services.GetRequiredService<EventIngest>().FlushPendingAsync();
    }

    List<CommandDto> DrainInbox() => _app.Services.GetRequiredService<InjectorCommandInbox>().Drain(64);

    static bool IsAtomPush(CommandDto cmd) =>
        cmd.Name == EffectGrantRehydrate.ApplyCommandName
        && cmd.Payload is JsonElement p
        && p.TryGetProperty("runnerBindings", out _)
        && p.TryGetProperty("defs", out _);

    [Fact]
    public async Task An_ack_event_pushes_a_real_atom_command_into_the_inbox()
    {
        var player = _store.CreatePlayer("Owner");
        var create = await _http.PostAsJsonAsync("/api/unique/actors", new { playerId = player.Id, side = "plant", typeId = 2 });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var actor = await create.Content.ReadFromJsonAsync<UniqueActorDto>();
        Assert.NotNull(actor);

        var deploy = await _http.PostAsJsonAsync(
            $"/api/unique/actors/{actor!.InstanceId}/deploy",
            new { correlationId = "repush-test-corr" });
        Assert.Equal(HttpStatusCode.OK, deploy.StatusCode);

        DrainInbox(); // clear whatever the create/deploy calls themselves enqueued (pvz.spawn.extra)

        await PostEventAsync("pvz.spawn.extra.ack", "m-repush",
            new { correlationId = "repush-test-corr", ptr = "0xREPUSH" });

        Assert.Equal(UniqueActorPhases.ActiveBound, _store.GetUniqueActor(actor.InstanceId)!.Phase);

        var commands = DrainInbox();
        Assert.Contains(commands, IsAtomPush);
    }

    [Fact]
    public async Task A_recover_event_after_ack_also_pushes_a_fresh_atom_command()
    {
        var player = _store.CreatePlayer("Owner");
        var create = await _http.PostAsJsonAsync("/api/unique/actors", new { playerId = player.Id, side = "zombie", typeId = 3 });
        var actor = await create.Content.ReadFromJsonAsync<UniqueActorDto>();
        await _http.PostAsJsonAsync($"/api/unique/actors/{actor!.InstanceId}/deploy", new { correlationId = "repush-recover-corr" });
        await PostEventAsync("pvz.spawn.extra.ack", "m-recover", new { correlationId = "repush-recover-corr", ptr = "0xRECOVER" });
        DrainInbox(); // clear the ack's own push (proven above) so this test isolates the recover push

        await PostEventAsync("zombie.die", "m-recover", new { ptr = "0xRECOVER" });

        Assert.Equal(UniqueActorPhases.Roster, _store.GetUniqueActor(actor.InstanceId)!.Phase);
        var commands = DrainInbox();
        Assert.Contains(commands, IsAtomPush);
    }

    [Fact]
    public async Task An_atom_repush_always_carries_a_grants_array_so_the_injector_does_not_refuse_it()
    {
        // Regression for a real bug caught before shipping: the injector's own RunEffectsGrantsApply
        // refuses the WHOLE command (never reaching InstallAtomPush) when "grants" is absent — an
        // atoms-only payload with no "grants" key at all would have silently done nothing.
        var player = _store.CreatePlayer("Owner");
        var create = await _http.PostAsJsonAsync("/api/unique/actors", new { playerId = player.Id, side = "plant", typeId = 4 });
        var actor = await create.Content.ReadFromJsonAsync<UniqueActorDto>();
        await _http.PostAsJsonAsync($"/api/unique/actors/{actor!.InstanceId}/deploy", new { correlationId = "repush-grants-corr" });
        DrainInbox();

        await PostEventAsync("pvz.spawn.extra.ack", "m-grants", new { correlationId = "repush-grants-corr", ptr = "0xGRANTS" });

        var push = DrainInbox().Single(IsAtomPush);
        var payload = (JsonElement)push.Payload!;
        Assert.True(payload.TryGetProperty("grants", out var grants));
        Assert.Equal(JsonValueKind.Array, grants.ValueKind);
    }
}
