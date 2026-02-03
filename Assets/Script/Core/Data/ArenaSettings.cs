using UnityEngine;

[CreateAssetMenu(fileName = "ArenaConfig", menuName = "AI/Arena Settings")]
public class ArenaSettings : ScriptableObject
{
    [Header("Match Settings")]
    public float PreRoundDelay = 0.0f;
    public float PostRoundDelay = 0.0f;
    public float MaxRoundTime = 60.0f;

    [Tooltip("If TRUE, disables all VFX and Sound for faster training performance.")]
    public bool IsTrainingMode = false;

    [Header("Agent Physics")]
    public float MovementThrust = 150f;
    public float RotationTorque = 50f;
    public float MaxLinearSpeed = 30f;
    public float AgentMass = 1.0f;
    public float AgentDrag = 0.5f;
    public float AgentAngularDrag = 2.0f;

    [Header("Weapon Config")]
    public float FireCooldown = 0.2f;
    public float BulletDamage = 10f;
    public float BulletSpeed = 150f;

    [Header("Spawning")]
    public float SpawnCheckRadius = 1.5f;
    public int MaxSpawnAttempts = 10;
    public LayerMask ObstacleLayer;

    [Header("Rewards")]
    public float WinReward = 1.0f;
    public float LossPenalty = -0.5f;
    public float TimePenalty = -0.0001f;
    public float AccuracyReward = 0.5f;
    public float DamagePenalty = 0.02f;

    [Header("Shared Audio")]
    public AudioClip EngineLoopClip;
    public AudioClip ShootClip;
    public AudioClip ImpactFleshClip;
    public AudioClip ImpactWallClip;
    public AudioClip ExplosionClip;
    public AudioClip CollisionClip;

    [System.Serializable]
    public struct TeamVisualProfile
    {
        public GameObject MuzzleFlash;
        public GameObject ImpactVFX;
        public GameObject ExplosionVFX;
        public GameObject DamageSmoke;
    }

    [Header("Team Visuals")]
    public TeamVisualProfile TeamA_Visuals; // 0
    public TeamVisualProfile TeamB_Visuals; // 1
}