using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;

namespace HZPLaserTripmineS2;

public class HLTEvents
{
    private readonly ILogger<HLTEvents> _logger;
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<HLTConfigs> _config;
    private readonly HLTGlobals _globals;
    
    public HLTEvents(ISwiftlyCore core, ILogger<HLTEvents> logger,
        HLTGlobals globals, IOptionsMonitor<HLTConfigs> config)
    {
        _core = core;
        _logger = logger;
        _globals = globals;
        _config = config;
    }

    public void HookEvents()
    {
        _core.Event.OnPrecacheResource += Event_OnPrecacheResource;
        _core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
        _core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
        _core.Event.OnMapUnload += Event_OnMapUnload;
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