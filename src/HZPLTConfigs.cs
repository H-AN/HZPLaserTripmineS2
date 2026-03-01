namespace HZPLaserTripmineS2;

public class HLTConfigs
{
    public class LaserMine
    {
        public bool Enable { get; set; } = true;
        public string Name { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public bool CanExplorer { get; set; } = false;
        public bool CanOwnerTeamTrigger { get; set; } = false;
        public float LaserRate { get; set; } = 0f;
        public float LaserDamage { get; set; } = 0f;
        public float LaserKnockBack { get; set; } = 0f;
        public int ExplorerRadius { get; set; } = 0;
        public int ExplorerDamage { get; set; } = 0;
        public string Team { get; set; } = string.Empty;
        public int Limit { get; set; } = 0;
        public int Price { get; set; } = 0;
        public string Permissions { get; set; } = string.Empty;
        public string GlowColor { get; set; } = string.Empty;
        public string laserColor { get; set; } = string.Empty;
        public float laserSize { get; set; } = 0f;
        public string MineOpenSound { get; set; } = string.Empty;
        public string LaserOpenSound { get; set; } = string.Empty;
        public string LaserTouchSound { get; set; } = string.Empty;
        public string PrecacheSoundEvent { get; set; } = string.Empty;
        public float ModelAngleFix { get; set; } = 0f;
    }
    public List<LaserMine> MineList { get; set; } = new List<LaserMine>();

}