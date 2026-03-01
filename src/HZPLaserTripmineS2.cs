using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;


namespace HanLaserTripmineS2;

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
    private HLTCommand _Commands = null!;
    private HLTEvents _Events = null!;

    public override void Load(bool hotReload)
    {
        Core.Configuration.InitializeJsonWithModel<HLTMainConfigs>("HLTMainConfigs.jsonc", "HanLaserTripmineS2MainCFG").Configure(builder =>
        {
            builder.AddJsonFile("HLTMainConfigs.jsonc", false, true);
        });

        Core.Configuration.InitializeJsonWithModel<HLTConfigs>("HanMineS2.jsonc", "HanMineS2CFG").Configure(builder =>
        {
            builder.AddJsonFile("HanMineS2.jsonc", false, true);
        });

        var collection = new ServiceCollection();
        collection.AddSwiftly(Core);

        collection
            .AddOptionsWithValidateOnStart<HLTMainConfigs>()
            .BindConfiguration("HanLaserTripmineS2MainCFG");

        collection
            .AddOptionsWithValidateOnStart<HLTConfigs>()
            .BindConfiguration("HanMineS2CFG");

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

        _mineCFGMonitor = ServiceProvider.GetRequiredService<IOptionsMonitor<HLTConfigs>>();

        _mineCFGMonitor.OnChange(newConfig =>
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