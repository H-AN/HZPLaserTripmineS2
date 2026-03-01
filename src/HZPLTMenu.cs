using System.Drawing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace HanLaserTripmineS2;

public class HLTMenu
{
    private readonly ILogger<HLTMenu> _logger;
    private readonly ISwiftlyCore _core;
    private readonly IOptionsMonitor<HLTConfigs> _config;
    private readonly HLTMenuHelper _menuhelper;
    private readonly HLTService _service;
    public HLTMenu(ISwiftlyCore core, ILogger<HLTMenu> logger,
        HLTMenuHelper menuhelper, IOptionsMonitor<HLTConfigs> config,
        HLTService service)
    {
        _core = core;
        _logger = logger;
        _menuhelper = menuhelper;
        _config = config;
        _service = service;
    }
    
    
    public IMenuAPI OpenMinesMenu(IPlayer player)
    {
        var main = _core.MenusAPI.CreateBuilder();
        IMenuAPI menu = _menuhelper.CreateMenu($"{_core.Translation.GetPlayerLocalizer(player)["MenuTitle"]}"); 

        // 顶部滚动文字
        menu.AddOption(new TextMenuOption(HtmlGradient.GenerateGradientText(
            $"{_core.Translation.GetPlayerLocalizer(player)["MenuSelelctLaseMine"]}", 
            Color.Red, Color.LightBlue, Color.Red),
            updateIntervalMs: 500, pauseIntervalMs: 100)
        {
            TextStyle = MenuOptionTextStyle.ScrollLeftLoop
        });

        var mineList = _config.CurrentValue.MineList;
        if (mineList != null && mineList.Count > 0)
        {
            foreach (var mineCfg in mineList)
            {

                if (!mineCfg.Enable) 
                    continue;

                var pawn = player.PlayerPawn;
                if(pawn == null || !pawn.IsValid)
                    continue;


                string teamStr = string.IsNullOrEmpty(mineCfg.Team) ? "all" : mineCfg.Team.ToLower();

                int playerTeam = pawn.TeamNum;
                if (teamStr != "all")
                {
                    if (teamStr == "t" && playerTeam != 2) 
                        continue;
                    if (teamStr == "ct" && playerTeam != 3) 
                        continue;
                }

                var steamId = player.SteamID;
                if (steamId == 0)
                    continue;

                if (!string.IsNullOrEmpty(mineCfg.Permissions) && !_core.Permission.PlayerHasPermission(steamId, mineCfg.Permissions))
                    continue;

                string priceText;
                if (mineCfg.Price > 0)
                {
                    priceText = $"${mineCfg.Price}";
                }
                else
                {
                    priceText = _core.Translation.GetPlayerLocalizer(player)["FreeText"] ?? "免费";
                }

                string limitText;
                if (mineCfg.Limit > 0)
                {
                    limitText = $"{mineCfg.Limit}";
                }
                else
                {
                    limitText = _core.Translation.GetPlayerLocalizer(player)["LimitText"] ?? "∞";
                }

                string buttonText = $"{mineCfg.Name}[{priceText}丨{_core.Translation.GetPlayerLocalizer(player)["MenuLimitText"] ?? "限制"}: {limitText}]";

                var turretButton = new ButtonMenuOption(buttonText)
                {
                    TextStyle = MenuOptionTextStyle.ScrollLeftLoop,
                    CloseAfterClick = false
                };
                turretButton.Tag = "extend";

                turretButton.Click += async (_, args) =>
                {
                    var clicker = args.Player;
                    _core.Scheduler.NextTick(() =>
                    {
                        if (!clicker.IsValid)
                            return;

                        _service.CreateMineEnt(clicker, mineCfg.Name);
                    });
                };

                menu.AddOption(turretButton);
            }
        }

        _core.MenusAPI.OpenMenuForPlayer(player, menu);
        return menu;
    }
    

}
