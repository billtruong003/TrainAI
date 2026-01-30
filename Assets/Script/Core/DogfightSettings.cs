using UnityEngine;

[CreateAssetMenu(fileName = "DogfightSettings", menuName = "AI/Dogfight Settings")]
public class DogfightSettings : ScriptableObject
{
    [Header("Physics")]
    public float MaxThrusterForce = 40f;
    public float AgentMass = 1.0f;
    public float AgentDrag = 0.5f;
    public float AgentAngularDrag = 2.5f;

    [Header("Combat")]
    public float MaxHealth = 100f;
    public float FireCooldown = 0.15f;
    public float RecoilForce = 5f;
    public float BulletDamage = 10f;
    public float BulletSpeed = 150f;
    public float BulletLifetime = 3f;

    [Header("Scoring")]
    public float WinReward = 5.0f;
    public float LossPenalty = -1.0f;
    public float HitReward = 0.5f;
    public float DamagePenalty = -0.2f;
    public float NearMissReward = 0.1f;
    public float CrashPenalty = -1.0f;
    public float TimePenalty = -0.0001f;
}