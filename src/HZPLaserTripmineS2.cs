using HanZombiePlagueS2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;


namespace HZPLaserTripmineS2;

[PluginMetadata(
    Id = "HZPLaserTripmineS2",
    Version = "1.0.0",
    Name = "ZP激光绊雷/HZPLaserTripmine",
    Author = "H-AN",
    Description = "H-AN ZP激光绊雷 for Sw2/H-AN HZPLaserTripmine for Sw2")]

public partial class HanLaserTripmineS2(ISwiftlyCore core) : BasePlugin(core)
{
    private ServiceProvider? ServiceProvider { get; set; }
    private IOptionsMonitor<HLTConfigs> _mineCFGMonitor = null!;
    private IOptionsMonitor<HLTMainConfigs> _mainCfg = null!;
    private HLTCommand _Commands = null!;
    private HLTEvents _Events = null!;
    private HLTService _Service = null!;
    private HLTHelper _Helpers = null!;
    private HLTGlobals _Globals = null!;
    private HLTMenu _Menu = null!;
    private HLTMenuHelper _MenuHelper = null!;
    public static IHanZombiePlagueAPI? _zpApi { get; private set; }
    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        if (interfaceManager.HasSharedInterface("HanZombiePlague")) //获取api  Get API
        {
            _zpApi = interfaceManager.GetSharedInterface<IHanZombiePlagueAPI>("HanZombiePlague");  //获取api  Get API
            Core.Logger.LogInformation($"[HZPLaserTripmineS2] 成功获取 HZP API/Successfully obtained HZP API，Hash: {_zpApi.GetHashCode()}");
        }
    }


    public override void Load(bool hotReload)
    {
        Core.Configuration.InitializeJsonWithModel<HLTMainConfigs>("HZPLTMainConfigs.jsonc", "HZPLaserTripmineS2MainCFG").Configure(builder =>
        {
            builder.AddJsonFile("HZPLTMainConfigs.jsonc", false, true);
        });

        Core.Configuration.InitializeJsonWithModel<HLTConfigs>("HZPMineS2.jsonc", "HZPMineS2CFG").Configure(builder =>
        {
            builder.AddJsonFile("HZPMineS2.jsonc", false, true);
        });

        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);

        collection
            .AddOptionsWithValidateOnStart<HLTMainConfigs>()
            .BindConfiguration("HZPLaserTripmineS2MainCFG");

        collection
            .AddOptionsWithValidateOnStart<HLTConfigs>()
            .BindConfiguration("HZPMineS2CFG");

        collection.AddSingleton<HLTGlobals>();
        collection.AddSingleton<HLTHelper>();
        collection.AddSingleton<HLTMenu>();
        collection.AddSingleton<HLTMenuHelper>();
        collection.AddSingleton<HLTService>();
        collection.AddSingleton<HLTEvents>();
        collection.AddSingleton<HLTCommand>();

        

        ServiceProvider = collection.BuildServiceProvider();

        _Commands = ServiceProvider.GetRequiredService<HLTCommand>();
        _Events = ServiceProvider.GetRequiredService<HLTEvents>();
        _Service = ServiceProvider.GetRequiredService<HLTService>();
        _Helpers = ServiceProvider.GetRequiredService<HLTHelper>();
        _Globals = ServiceProvider.GetRequiredService<HLTGlobals>();
        _Menu = ServiceProvider.GetRequiredService<HLTMenu>();
        _MenuHelper = ServiceProvider.GetRequiredService<HLTMenuHelper>();

        _mineCFGMonitor = ServiceProvider.GetRequiredService<IOptionsMonitor<HLTConfigs>>();

        _mineCFGMonitor.OnChange(newConfig =>
        {
            Core.Logger.LogInformation($"{Core.Localizer["ServerCfgChange"]}");
        });

        _mainCfg = ServiceProvider.GetRequiredService<IOptionsMonitor<HLTMainConfigs>>();
        _mainCfg.OnChange(newConfig =>
        {
            Core.Logger.LogInformation($"{Core.Localizer["ServerCfgChange"]}");
        });

        _Events.HookEvents();
        _Commands.Commands();

    }


    public override void Unload()
    {
        ServiceProvider!.Dispose();
    }

    
}