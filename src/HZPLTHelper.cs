using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using static HZPLaserTripmineS2.HLTGlobals;

namespace HZPLaserTripmineS2;
public class HLTHelper
{
    private readonly ILogger<HLTHelper> _logger;
    private readonly ISwiftlyCore _core;

    public HLTHelper(ISwiftlyCore core, ILogger<HLTHelper> logger)
    {
        _core = core;
        _logger = logger;
    }

    public void EmitSoundFromEntity(CHandle<CBaseModelEntity> mineHandle, string SoundPath)
    {
        if (!mineHandle.IsValid)
            return;

        var mine = mineHandle.Value;
        if (mine == null || !mine.IsValid)
            return;

        if (!string.IsNullOrEmpty(SoundPath))
        {
            var sound = new SwiftlyS2.Shared.Sounds.SoundEvent(SoundPath, 1.0f, 1.0f);
            sound.SourceEntityIndex = (int)mine.Index;
            sound.Recipients.AddAllPlayers();
            _core.Scheduler.NextTick(() => { sound.Emit(); });
        }
    }

    public void ApplyKnockBack(CHandle<CBaseModelEntity> mineHandle, IPlayer owner, IPlayer target, float force)
    {
        if (!mineHandle.IsValid)
            return;

        var mine = mineHandle.Value;
        if (mine == null || !mine.IsValid)
            return;

        if (target == null || !target.IsValid || force <= 0)
            return;

        var targetPawn = target.PlayerPawn;
        if (targetPawn == null || !targetPawn.IsValid)
            return;

        var ownerPawn = owner.PlayerPawn;
        if (ownerPawn == null || !ownerPawn.IsValid)
            return;

        if (owner == target || ownerPawn.TeamNum == targetPawn.TeamNum)
            return;

        var mineRotation = mine.AbsRotation;
        if (mineRotation == null)
            return;

        QAngle mineAngle = mineRotation.Value;
        mineAngle.ToDirectionVectors(out Vector vecKnockback, out _, out _);
        var pushVelocity = vecKnockback * force;
        var vel = targetPawn.AbsVelocity;
        targetPawn.Teleport(null, null, vel + pushVelocity);
    }

    public SwiftlyS2.Shared.Natives.Vector CalculateKnockbackDirection(CHandle<CBaseModelEntity> mineHandle, IPlayer target, float force)
    {
        if (!mineHandle.IsValid)
            return new SwiftlyS2.Shared.Natives.Vector(0, 0, 0);

        var mine = mineHandle.Value;
        if (mine == null || !mine.IsValid)
            return new SwiftlyS2.Shared.Natives.Vector(0, 0, 0);

        var pawn = target.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return new SwiftlyS2.Shared.Natives.Vector(0, 0, 0);

        var minePos = mine.AbsOrigin;
        if (minePos == null)
            return new SwiftlyS2.Shared.Natives.Vector(0, 0, 0);

        var targetPos = pawn.AbsOrigin;
        if (targetPos == null)
            return new SwiftlyS2.Shared.Natives.Vector(0, 0, 0);

        var dir = new SwiftlyS2.Shared.Natives.Vector(
            targetPos.Value.X - minePos.Value.X,
            targetPos.Value.Y - minePos.Value.Y,
            targetPos.Value.Z - minePos.Value.Z
        );

        float length = MathF.Sqrt(dir.X * dir.X + dir.Y * dir.Y + dir.Z * dir.Z);
        if (length <= 0.01f) return new SwiftlyS2.Shared.Natives.Vector(0, 0, 0);

        return new SwiftlyS2.Shared.Natives.Vector(
            dir.X / length * force,
            dir.Y / length * force,
            50f
        );
    }

    public void ApplyDamage(IPlayer attacker, IPlayer target, CHandle<CBaseModelEntity> mineHandle, float damageAmount, string hurtSound, DamageTypes_t damageType = DamageTypes_t.DMG_BULLET)
    {
        if (!mineHandle.IsValid)
            return;

        var ent = mineHandle.Value;
        if (ent == null || !ent.IsValid)
            return;

        var AttackerPawn = attacker.PlayerPawn;
        if (AttackerPawn == null || !AttackerPawn.IsValid)
            return;

        var TargetPawn = target.PlayerPawn;
        if (TargetPawn == null || !TargetPawn.IsValid)
            return;


        if (attacker == target || AttackerPawn.TeamNum == TargetPawn.TeamNum)
            return;

        CBaseEntity inflictorEntity = ent;
        CBaseEntity attackerEntity = AttackerPawn;
        CBaseEntity abilityEntity = ent;


        var damageInfo = new CTakeDamageInfo(inflictorEntity, attackerEntity, abilityEntity, damageAmount, damageType);

        damageInfo.DamageForce = new SwiftlyS2.Shared.Natives.Vector(0, 0, 10f);

        var targetPos = TargetPawn.AbsOrigin;
        if (targetPos != null)
        {
            damageInfo.DamagePosition = targetPos.Value;
        }
        target.TakeDamage(damageInfo);
        EmitSoundFromEntity(mineHandle, hurtSound);

    }

    public bool TryParseColor(string colorStr, out SwiftlyS2.Shared.Natives.Color color)
    {
        color = new SwiftlyS2.Shared.Natives.Color(255, 100, 0, 255); // 默认值
        var parts = colorStr.Split(',');
        if (parts.Length < 3 || parts.Length > 4)
            return false;

        if (byte.TryParse(parts[0].Trim(), out byte r) &&
            byte.TryParse(parts[1].Trim(), out byte g) &&
            byte.TryParse(parts[2].Trim(), out byte b))
        {
            byte a = (parts.Length == 4 && byte.TryParse(parts[3].Trim(), out byte parsedA)) ? parsedA : (byte)255;
            color = new SwiftlyS2.Shared.Natives.Color(r, g, b, a);
            return true;
        }
        return false;
    }

    public SwiftlyS2.Shared.Natives.QAngle NormalToAngles(Vector normal, Vector playerForward, Mines mineData, out bool isVerticalSurface)
    {
        normal.Normalize();
        playerForward.Normalize();

        float yaw = 0.0f;
        float pitch = 0.0f;
        float roll = 0.0f;

        isVerticalSurface = MathF.Abs(MathF.Abs(normal.Z) - 1.0f) < 0.01f;

        float AngleFix = mineData.ModelAngleFix;

        if (isVerticalSurface)
        {
            float originalPitch = MathF.Asin(-normal.Z) * 180f / MathF.PI;
            if (AngleFix != 0)
            {
                if (normal.Z > 0.5f)
                {
                    pitch = originalPitch - AngleFix;
                    roll = AngleFix;
                }
                else
                {
                    pitch = originalPitch + AngleFix;
                    roll = -AngleFix;
                }
            }
            else
            {
                if (normal.Z > 0.5f)
                {
                    pitch = originalPitch;
                }
                else
                {
                    pitch = originalPitch;
                }
            }
        }
        else
        {
            pitch = MathF.Asin(-normal.Z) * 180f / MathF.PI;
            yaw = MathF.Atan2(normal.Y, normal.X) * 180f / MathF.PI;
            if (AngleFix != 0)
            {
                yaw += AngleFix;
            }
            Vector right = normal.Cross(playerForward).Normalized();
            Vector forward = right.Cross(normal).Normalized();
            roll = MathF.Atan2(forward.Z, forward.X) * 180f / MathF.PI;
        }

        return new SwiftlyS2.Shared.Natives.QAngle(pitch, yaw, roll);
    }

    public bool CreateTraceByEyePosition(IPlayer player, out CGameTrace trace, out Vector forward)
    {
        trace = new CGameTrace();
        forward = new Vector(0, 0, 0); // 初始化

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid)
            return false;


        var eyePos = pawn.EyePosition;
        if (eyePos == null)
            return false;

        pawn.EyeAngles.ToDirectionVectors(out forward, out _, out _);

        var startPos = new Vector(eyePos.Value.X, eyePos.Value.Y, eyePos.Value.Z);
        var endPos = startPos + forward * 8192;

        trace = new CGameTrace();
        _core.Trace.SimpleTrace(
            startPos,
            endPos,
            RayType_t.RAY_TYPE_LINE,
            RnQueryObjectSet.Static | RnQueryObjectSet.Dynamic,
            MaskTrace.Solid | MaskTrace.Player,
            MaskTrace.Empty,
            MaskTrace.Empty,
            CollisionGroup.Player,
            ref trace,
            null
        );

        if (trace.Fraction < 1.0f)
        {
            return true;
        }

        return false;
    }

    public void SetGlow(CBaseEntity entity, string glowColorStr)
    {
        if (entity == null || !entity.IsValid)
            return;

        if (string.IsNullOrEmpty(glowColorStr))
            return;

        if (!TryParseColor(glowColorStr, out SwiftlyS2.Shared.Natives.Color parsedColor))
        {
            _core.Logger.LogError($"{_core.Localizer["GlowColorError", glowColorStr]}");
            return;
        }

        CBaseModelEntity modelGlow = _core.EntitySystem.CreateEntity<CBaseModelEntity>();
        CBaseModelEntity modelRelay = _core.EntitySystem.CreateEntity<CBaseModelEntity>();

        if (modelGlow == null || modelRelay == null)
            return;

        string modelName = entity.CBodyComponent!.SceneNode!.GetSkeletonInstance().ModelState.ModelName;

        modelRelay.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));
        modelRelay.SetModel(modelName);
        modelRelay.Spawnflags = 256u;
        modelRelay.RenderMode = RenderMode_t.kRenderNone;
        modelRelay.DispatchSpawn();

        modelGlow.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));
        modelGlow.SetModel(modelName);
        modelGlow.Spawnflags = 256u;
        modelGlow.DispatchSpawn();

        modelGlow.Glow.GlowColorOverride = parsedColor;
        modelGlow.Glow.GlowRange = 5000;
        modelGlow.Glow.GlowTeam = -1;
        modelGlow.Glow.GlowType = 3;
        modelGlow.Glow.GlowRangeMin = 100;

        modelRelay.AcceptInput("FollowEntity", "!activator", entity, modelRelay);
        modelGlow.AcceptInput("FollowEntity", "!activator", modelRelay, modelGlow);
    }
}