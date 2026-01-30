using UnityEngine;

[CreateAssetMenu(fileName = "ArenaConfig", menuName = "AI/Arena Settings")]
public class ArenaSettings : ScriptableObject
{
    [Header("Match Settings")]
    public float PreRoundDelay = 0.0f; // Instant Start
    public float PostRoundDelay = 0.0f; // Instant Restart
    public float MaxRoundTime = 60.0f;

    [Header("Agent Physics")]
    public float MovementThrust = 150f;   // Lực đẩy tới
    public float RotationTorque = 50f;    // Lực xoay
    public float MaxLinearSpeed = 30f;    // Giới hạn tốc độ bay
    public float AgentMass = 1.0f;
    public float AgentDrag = 0.5f;
    public float AgentAngularDrag = 2.0f;

    [Header("Weapon Config")]
    public float FireCooldown = 0.2f;     // Tốc độ bắn
    public float BulletDamage = 10f;
    public float BulletSpeed = 150f;

    [Header("Spawning")]
    public float SpawnCheckRadius = 1.5f;
    public int MaxSpawnAttempts = 10;
    public LayerMask ObstacleLayer;

    [Header("Rewards")]
    public float WinReward = 1.0f;
    public float LossPenalty = -0.5f; // Giam phat de bot so chet
    public float TimePenalty = -0.0001f; // Phat rat nhe thoi gian
    public float AccuracyReward = 0.5f;  // Tang thuong ban trung len 0.5 (dong luc lon)
    public float DamagePenalty = 0.02f; // Tang phat khi bi ban trung
}