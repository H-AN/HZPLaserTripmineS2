using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mono.Cecil.Cil;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using static HanLaserTripmineS2.HLTConfigs;
using static HanLaserTripmineS2.HLTGlobals;

namespace HanLaserTripmineS2;


public class HLTService
{
    private readonly ILogger<HLTService> _logger;
    private readonly ISwiftlyCore _core;
    private readonly HLTHelper _helpers;
    private readonly HLTGlobals _globals;
    private readonly IOptionsMonitor<HLTConfigs> _config;
    public HLTService(ISwiftlyCore core, ILogger<HLTService> logger,
        HLTHelper helpers, HLTGlobals globals,
        IOptionsMonitor<HLTConfigs> config)
    {
        _core = core;
        _logger = logger;
        _helpers = helpers;
        _globals = globals;
        _config = config;
    }

    public CBaseModelEntity CreateMineEnt(IPlayer player, string mineName)
    {
        if (player == null || !player.IsValid)
            return null;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return null;

        var Controller = player.Controller;
        if (Controller == null || !Controller.IsValid)
            return null;

        if (string.IsNullOrEmpty(mineName))
            return null;

        var mineConfig = GetMineConfigByName(mineName, _logger);
        if (mineConfig == null)
            return null;

        var SteamId = player.SteamID;
        if (SteamId == 0)
            return null;

        if (!_globals.PlayerMineCounts.TryGetValue(SteamId, out var playerMines))
        {
            playerMines = new Dictionary<string, HashSet<uint>>();
            _globals.PlayerMineCounts[SteamId] = playerMines;
        }

        if (!playerMines.TryGetValue(mineName, out var mineSet))
        {
            mineSet = new HashSet<uint>();
            playerMines[mineName] = mineSet;
        }

        if (mineConfig.Limit > 0 && mineSet.Count >= mineConfig.Limit)
        {
            player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["MineLimit", mineConfig.Limit]}"); 
            return null;
        }

        if (!_helpers.CreateTraceByEyePosition(player, out CGameTrace trace, out Vector playerForward))
        {
            return null;
        }

        int Price = mineConfig.Price;
        var moneyServices = Controller.InGameMoneyServices;
        if (moneyServices != null && moneyServices.IsValid && Price > 0)
        {
            int current = moneyServices.Account;
            if (current < Price)
            {
                player.SendMessage(MessageType.Chat, $"{_core.Translation.GetPlayerLocalizer(player)["NoMoney"]}");
                return null;
            }
            moneyServices.Account = current - Price;
            Controller.InGameMoneyServicesUpdated();
        }

        CBaseModelEntity mineEntity = _core.EntitySystem.CreateEntityByDesignerName<CBaseModelEntity>("prop_dynamic_override");
        if (mineEntity == null)
            return null;

        try
        {
            mineEntity.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= ~(uint)(1 << 2);
            mineEntity.DispatchSpawn();

            var mineHandle = _core.EntitySystem.GetRefEHandle(mineEntity);
            if (!mineHandle.IsValid)
                return null;

            var mineData = new Mines
            {
                Name = mineConfig.Name,
                Model = mineConfig.Model,
                CanExplorer = mineConfig.CanExplorer,
                CanOwnerTeamTrigger = mineConfig.CanOwnerTeamTrigger,
                LaserRate = mineConfig.LaserRate,
                LaserDamage = mineConfig.LaserDamage,
                LaserKnockBack = mineConfig.LaserKnockBack,
                ExplorerRadius = mineConfig.ExplorerRadius,
                ExplorerDamage = mineConfig.ExplorerDamage,
                Team = mineConfig.Team,
                Price = mineConfig.Price,
                Limit = mineConfig.Limit,
                Permissions = mineConfig.Permissions,
                GlowColor = mineConfig.GlowColor,
                laserColor = mineConfig.laserColor,
                laserSize = mineConfig.laserSize,
                MineOpenSound = mineConfig.MineOpenSound,
                LaserOpenSound = mineConfig.LaserOpenSound,
                LaserTouchSound = mineConfig.LaserTouchSound,
                ModelAngleFix = mineConfig.ModelAngleFix,
            };

            _globals.MineData[mineHandle.Raw] = mineData;

            mineSet.Add(mineHandle.Raw);

            var ent = mineHandle.Value;
            if (ent == null)
                return null;

            _core.Scheduler.NextTick(() =>
            {
                if (ent == null)
                    return;

                ent.SetModel(mineData.Model);
                ent.OwnerEntity.Raw = pawn.Index;
                ent.OwnerEntityUpdated();
                ent.MaxHealth = 3000;
                ent.Health = 3000;
                ent.MoveType = MoveType_t.MOVETYPE_NONE;
                ent.MoveTypeUpdated();

                _helpers.SetGlow(ent, mineData.GlowColor);
            });

            var endPos = trace.EndPos;
            var normal = trace.HitNormal;
            SwiftlyS2.Shared.Natives.QAngle angle = _helpers.NormalToAngles(normal, normal, mineData, out bool isVerticalSurface);
            ent.Teleport(endPos, angle, null);

            _helpers.EmitSoundFromEntity(mineHandle, mineData.MineOpenSound);

            SwiftlyS2.Shared.Natives.Color laserColor;
            if (!_helpers.TryParseColor(mineData.laserColor, out laserColor))
            {
                laserColor = new SwiftlyS2.Shared.Natives.Color(255, 0, 0, 255);
            }
            _core.Scheduler.DelayBySeconds(1.0f, () =>
            {
                if (ent == null)
                    return;

                CreateBeam(player, mineHandle, laserColor, mineData, isVerticalSurface);
            });

            return mineEntity;
        }
        catch (Exception ex)
        {
            _core.Logger.LogError($"{_core.Localizer["CreateMineError"]}: {ex.Message}");
            if (mineEntity.IsValid)
            {
                mineEntity.AcceptInput("Kill", 0);
            }
            return null;
        }
    }

    public void CreateBeam(IPlayer player, CHandle<CBaseModelEntity> mineHandle, SwiftlyS2.Shared.Natives.Color color, Mines mineData, bool isVerticalSurface)
    {
        if (!mineHandle.IsValid)
            return;

        var ent = mineHandle.Value;
        if (ent == null || !ent.IsValid)
            return;

        if (!CreateTraceByEntity(mineHandle, out CGameTrace trace, out Vector Forward, mineData, isVerticalSurface))
        {
            return;
        }

        CBeam beam = _core.EntitySystem.CreateEntity<CBeam>();
        if (beam == null)
            return;

        beam.DispatchSpawn();

        _helpers.EmitSoundFromEntity(mineHandle, mineData.LaserOpenSound);

        var beamHandle = _core.EntitySystem.GetRefEHandle(beam);
        if (!beamHandle.IsValid)
            return;

        var beament = beamHandle.Value;
        if (beament == null)
            return;


        var startPos = trace.StartPos;
        var endPos = trace.EndPos;

        float size = mineData.laserSize;
        beament.Render = color;
        beament.Width = size;
        beament.EndWidth = size;

        beament.Teleport(startPos, null, null);
        beament.EndPos = endPos;

        var beamStart = startPos;
        var beamDir = Forward;
        var beamOwner = ent;

        if (_globals.MineThink.TryGetValue(mineHandle.Raw, out var task))
        {
            task?.Cancel();
            task = null;
            _globals.MineThink.Remove(mineHandle.Raw);
        }
        float Rate = mineData.CanExplorer ? 0.1f : mineData.LaserRate;

        _globals.MineThink[mineHandle.Raw] = _core.Scheduler.RepeatBySeconds(Rate, () =>
        {
            if (!ent.IsValid)
            {
                if (_globals.MineThink.TryGetValue(mineHandle.Raw, out var task))
                {
                    task?.Cancel();
                    task = null;
                    _globals.MineThink.Remove(mineHandle.Raw);
                }
            }
            PenetratingTrace(player, mineHandle, mineData, beamHandle, beamStart, beamDir, 8192, 10);
        });
        _core.Scheduler.StopOnMapChange(_globals.MineThink[mineHandle.Raw]);
    }


    public List<IPlayer> PenetratingTrace(IPlayer player, CHandle<CBaseModelEntity> mineHandle, Mines mineData, CHandle<CBeam> beamHandle, Vector start, Vector direction, float maxDistance, int maxTargets = 8)
    {
        if (!mineHandle.IsValid)
            return null;

        var ent = mineHandle.Value;
        if (ent == null || !ent.IsValid)
            return null;

        var OwnerPawn = player.PlayerPawn;
        if (OwnerPawn == null || !OwnerPawn.IsValid)
            return null;

        var beamOwner = ent;

        var hitPlayers = new List<IPlayer>();
        Vector currentStart = start;
        float remainingDistance = maxDistance;
        int penetrationCount = 0;

        while (remainingDistance > 0.1f && penetrationCount < maxTargets)
        {
            Vector end = currentStart + direction * remainingDistance;
            CGameTrace trace = new CGameTrace();

            _core.Trace.SimpleTrace(
                currentStart,
                end,
                RayType_t.RAY_TYPE_LINE,
                RnQueryObjectSet.Static | RnQueryObjectSet.Dynamic,
                MaskTrace.Hitbox | MaskTrace.Player,
                MaskTrace.Empty,
                MaskTrace.Empty,
                CollisionGroup.Always,
                ref trace,
                beamOwner
            );

            if (!trace.DidHit || trace.Fraction >= 0.99f)
                break;

            if (trace.HitPlayer(out IPlayer target))
            {

                if (!hitPlayers.Contains(target))
                {
                    hitPlayers.Add(target);
                }

                var targetPawn = target.PlayerPawn;
                if (targetPawn == null || !targetPawn.IsValid)
                    return null;

                bool isOwnerTeam = OwnerPawn.TeamNum == targetPawn.TeamNum;
                bool isEnemy = !isOwnerTeam;
                bool canTrigger = isEnemy || (isOwnerTeam && mineData.CanOwnerTeamTrigger);

                if (mineData.CanExplorer)
                {
                    if (canTrigger)
                    {
                        CreateGrenadeAndExplorer(player, mineHandle, beamHandle, mineData);
                        return hitPlayers;
                    }
                }
                else
                {
                    if (mineData.LaserDamage > 0)
                        _helpers.ApplyDamage(player, target, mineHandle, mineData.LaserDamage, mineData.LaserTouchSound);

                    if (mineData.LaserKnockBack != 0)
                        _helpers.ApplyKnockBack(mineHandle, player, target, mineData.LaserKnockBack);

                }

                Vector offsetPoint = trace.HitPoint + direction * 8.0f; 
                remainingDistance = remainingDistance * (1 - trace.Fraction) - 8.0f;
                currentStart = offsetPoint;


            }
            else
            {
                break;
            }

            penetrationCount++;
        }

        return hitPlayers;
    }

    public bool CreateTraceByEntity(CHandle<CBaseModelEntity> mineHandle, out CGameTrace trace, out Vector forward, Mines mineData, bool isVerticalSurface)
    {
        trace = new CGameTrace();
        forward = new Vector(0, 0, 0);

        if (!mineHandle.IsValid)
            return false;

        var ent = mineHandle.Value;
        if (ent == null || !ent.IsValid)
            return false;

        var MinePos = ent.AbsOrigin;
        if (MinePos == null)
            return false;

        var MineAngle = ent.AbsRotation;
        if (MineAngle == null)
            return false;

        QAngle Angle = new QAngle(MineAngle.Value.Pitch, MineAngle.Value.Yaw, MineAngle.Value.Roll);

        float AngleFix = mineData.ModelAngleFix;

        if (isVerticalSurface)
        {
            if (AngleFix != 0)
            {
                if (Angle.Pitch > 0)
                {
                    Angle.Pitch -= AngleFix;
                }
                else
                {
                    Angle.Pitch += AngleFix;
                }
            }
        }
        else
        {

            if (AngleFix != 0)
            {
                Angle.Yaw -= AngleFix;
            }
        }

        Angle.ToDirectionVectors(out forward, out _, out _);

        var startPos = new Vector(MinePos.Value.X, MinePos.Value.Y, MinePos.Value.Z);

        float maxDistance = 8192f;

        var endPos = startPos + forward * maxDistance;

        trace = new CGameTrace();
        _core.Trace.SimpleTrace(
            startPos,
            endPos,
            RayType_t.RAY_TYPE_LINE,
            RnQueryObjectSet.Static | RnQueryObjectSet.Dynamic,
            MaskTrace.Solid | MaskTrace.Player,
            MaskTrace.Empty,
            MaskTrace.Empty,
            CollisionGroup.NPC,
            ref trace,
            null
        );

        if (trace.Fraction < 1.0f)
        {
            endPos = trace.EndPos;
        }
        else
        {
            endPos = startPos + forward * maxDistance;
        }

        return true;
    }

    public void CreateGrenadeAndExplorer(IPlayer player, CHandle<CBaseModelEntity> mineHandle, CHandle<CBeam> beamHandle, Mines mineData)
    {
        if (!mineHandle.IsValid || !beamHandle.IsValid)
            return;

        var mine = mineHandle.Value;
        if (mine == null || !mine.IsValid)
            return;

        var beam = beamHandle.Value;
        if (beam == null || !beam.IsValid)
            return;

        var minePos = mine.AbsOrigin;
        if (minePos == null)
            return;

        var mineAngle = mine.AbsRotation;
        if (mineAngle == null)
            return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return;

        var explosions = CHEGrenadeProjectile.EmitGrenade(minePos.Value, mineAngle.Value, mine.AbsVelocity, pawn);
        if (explosions == null)
            return;

        explosions.DispatchSpawn();

        int expDamage = mineData.ExplorerDamage;
        int expRadius = mineData.ExplorerRadius;

        explosions.Damage = expDamage;
        explosions.DamageUpdated();

        explosions.DmgRadius = expRadius;
        explosions.DmgRadiusUpdated();

        explosions.Globalname = "激光绊雷";
        explosions.Teleport(minePos, null, null);
        explosions.AcceptInput("InitializeSpawnFromWorld", "", pawn, pawn);
        explosions.DetonateTime.Value = 0;
        explosions.DetonateTimeUpdated();

        if (_globals.MineThink.TryGetValue(mineHandle.Raw, out var repeatTask))
        {
            repeatTask?.Cancel();              
            _globals.MineThink.Remove(mineHandle.Raw);  
        }

        _globals.MineData.Remove(mineHandle.Raw);

        _core.Scheduler.NextTick(() =>
        {
            if (mine.IsValid)
                mine.AcceptInput("Kill", 0);
            if (beam.IsValid)
                beam.AcceptInput("Kill", 0);
        });


    }

    public LaserMine? GetMineConfigByName(string name, ILogger? logger = null)
    {
        var selectedTurret = _config.CurrentValue.MineList.FirstOrDefault(t =>
            t.Enable && t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (selectedTurret == null)
        {
            _logger.LogWarning($"{_core.Localizer["MineNameError", name]}");
        }
        return selectedTurret;
    }

}