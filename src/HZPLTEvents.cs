using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace HZPLaserTripmineS2;

public class HLTEvents
{
    private readonly ILogger<HLTEvents> _logger;
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<HLTConfigs> _config;
    private readonly HLTGlobals _globals;
    private readonly HLTService _service;

    public HLTEvents(ISwiftlyCore core, ILogger<HLTEvents> logger,
        HLTGlobals globals, IOptionsMonitor<HLTConfigs> config, HLTService service)
    {
        _core = core;
        _logger = logger;
        _globals = globals;
        _config = config;
        _service = service;
    }

    public void HookEvents()
    {
        _core.Event.OnPrecacheResource += Event_OnPrecacheResource;
        _core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
        _core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
        _core.Event.OnMapUnload += Event_OnMapUnload;

        _core.GameEvent.HookPre<EventPlayerDeath>(OnPlayerDeath);
        _core.Event.OnClientConnected += Event_OnClientConnected;
        _core.Event.OnClientDisconnected += Event_OnClientDisconnected;
    }

    private void Event_OnClientConnected(SwiftlyS2.Shared.Events.IOnClientConnectedEvent @event)
    {
        var playerId = @event.PlayerId;
        var player = _core.PlayerManager.GetPlayer(playerId);
        if (player == null || !player.IsValid)
            return;

        _globals.PlayerSteamCache[player.PlayerID] = player.SteamID;
    }

    private void Event_OnClientDisconnected(SwiftlyS2.Shared.Events.IOnClientDisconnectedEvent @event)
    {
        var playerId = @event.PlayerId;

        if (_globals.PlayerSteamCache.TryGetValue(playerId, out var steamID))
        {
            _service.RemoveAllPlayerMines(playerId, steamID);
            _globals.PlayerSteamCache.Remove(playerId);
        }
        else
        {
            _service.RemoveAllPlayerMines(playerId, 0);
        }
    }

    private void Event_OnMapUnload(SwiftlyS2.Shared.Events.IOnMapUnloadEvent @event)
    {
        _globals.MineData.Clear();
        _globals.PlayerMineCounts.Clear();
    }

    private HookResult OnRoundStart(EventRoundStart @event)
    {
        _globals.MineData.Clear();
        _globals.PlayerMineCounts.Clear();

        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event)
    {
        _globals.MineData.Clear();
        _globals.PlayerMineCounts.Clear();

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        int id = player.PlayerID;

        if (!_globals.MineOwner.TryGetValue(id, out var mineSet))
            return HookResult.Continue;

        foreach (var mineRaw in mineSet.ToList())
        {
            var mineHandle = new CHandle<CBaseModelEntity>(mineRaw);

            if (!_globals.MineBeamMap.TryGetValue(mineRaw, out var beamRaw))
                continue;

            var beamHandle = new CHandle<CBeam>(beamRaw);

            _service.KillMine(mineHandle, beamHandle);

            _globals.MineBeamMap.Remove(mineRaw);
        }

        mineSet.Clear();
        _globals.MineOwner.Remove(id);

        return HookResult.Continue;
    }

    private void Event_OnPrecacheResource(SwiftlyS2.Shared.Events.IOnPrecacheResourceEvent @event)
    {
        var mineList = _config.CurrentValue.MineList;
        if (mineList != null && mineList.Count > 0)
        {
            foreach (var minePrecache in mineList)
            {
                if (!string.IsNullOrEmpty(minePrecache.Model))
                {
                    @event.AddItem(minePrecache.Model);
                }
                if (!string.IsNullOrEmpty(minePrecache.PrecacheSoundEvent))
                {
                    @event.AddItem(minePrecache.PrecacheSoundEvent);
                }
            }

        }
    }
}