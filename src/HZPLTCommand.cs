

using HanZombiePlagueS2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;

namespace HZPLaserTripmineS2;
public class HLTCommand
{
    private readonly ILogger<HLTCommand> _logger;
    private readonly ISwiftlyCore _core;
    private readonly HLTMenu _menus;
    private readonly IOptionsMonitor<HLTMainConfigs> _mainconfig;
    private IHanZombiePlagueAPI _zpApi;
    public HLTCommand(ISwiftlyCore core, ILogger<HLTCommand> logger,
        HLTMenu menus, IOptionsMonitor<HLTMainConfigs> mainconfig
        , IHanZombiePlagueAPI API)
    {
        _core = core;
        _logger = logger;
        _menus = menus;
        _mainconfig = mainconfig;
        _zpApi = API;
    }

    public void Commands()
    {
        string MenuCommand = string.IsNullOrEmpty(_mainconfig.CurrentValue.MenuCommand) ? "sw_mine" : _mainconfig.CurrentValue.MenuCommand;
        _core.Command.RegisterCommand($"{MenuCommand}", OpenMineMenu, true);
    }

    public void OpenMineMenu(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || !player.IsValid)
            return;

        var Controller = player.Controller;
        if (Controller == null || !Controller.IsValid)
            return;

        bool isZombie = _zpApi?.HZP_IsZombie(player.PlayerID) ?? false;
        if (isZombie)
            return;

        _menus.OpenMinesMenu(player);
    }

}