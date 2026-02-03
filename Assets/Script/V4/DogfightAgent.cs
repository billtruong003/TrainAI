using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

[RequireComponent(typeof(Rigidbody), typeof(ThrusterController))]
public class DogfightAgent : SmartAgent
{
    [Header("Config")]
    [SerializeField] private DogfightSettings settings;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private SmartProjectile projectilePrefab;
    [SerializeField] private NearMissSensor dodgeSensor;

    private ThrusterController thrusters;
    private Rigidbody rb;
    private DogfightArena arena;
    private Transform target;

    private float currentHealth;
    private float nextFireTime;
    private int teamId;

    public int TeamId => teamId;
    public bool IsAlive => currentHealth > 0;

    public override void Initialize()
    {
        base.Initialize();
        rb = GetComponent<Rigidbody>();
        thrusters = GetComponent<ThrusterController>();
        thrusters.Initialize(rb, settings.MaxThrusterForce);
        if (dodgeSensor) dodgeSensor.Initialize(this);
    }

    public void SetMatchData(DogfightArena arenaRef, int team, Transform enemy)
    {
        arena = arenaRef;
        teamId = team;
        target = enemy;
    }

    public override void OnEnvironmentReady()
    {
        base.OnEnvironmentReady();

        // Reset Health & Physics
        currentHealth = settings.MaxHealth;
        nextFireTime = 0f;

        // Reset Position from Arena
        if (arena != null)
        {
            Transform spawn = arena.GetSpawnPoint(teamId);
            thrusters.ResetPhysics(spawn.position, spawn.rotation);
        }

        gameObject.SetActive(true);
    }

    protected override int CalculateObservationSize()
    {
        return 18; // 6 vectors (x3) = 18 floats
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (target == null)
        {
            sensor.AddObservation(new float[14]);
            return;
        }

        // 1. Self Physics (Local Space) - 6 floats
        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));
        sensor.AddObservation(transform.InverseTransformDirection(rb.angularVelocity));

        // 2. Target Info (Relative) - 7 floats
        Vector3 toTarget = target.position - transform.position;
        sensor.AddObservation(transform.InverseTransformDirection(toTarget.normalized)); // Direction to target (Local)
        sensor.AddObservation(toTarget.magnitude / 100f); // Normalized Distance (Assuming max ~100)

        Vector3 targetVel = Vector3.zero;
        if (target.TryGetComponent<Rigidbody>(out var tRb)) targetVel = tRb.linearVelocity;

        // Relative Velocity in Local Space
        Vector3 relativeVel = targetVel - rb.linearVelocity;
        sensor.AddObservation(transform.InverseTransformDirection(relativeVel));

        // Target Facing (Is it looking at me?)
        sensor.AddObservation(transform.InverseTransformDirection(target.forward));

        // 3. Status - 2 floats
        sensor.AddObservation(currentHealth / settings.MaxHealth);
        sensor.AddObservation(Time.time >= nextFireTime);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var continuous = actions.ContinuousActions;
        var discrete = actions.DiscreteActions;

        // Map actions to thrusters
        float[] thrustInputs = new float[thrusters.ThrusterCount];
        for (int i = 0; i < thrusters.ThrusterCount; i++)
        {
            thrustInputs[i] = continuous[i];
        }
        thrusters.ApplyThrust(thrustInputs);

        // Fire Logic
        bool trigger = discrete[0] > 0;
        if (trigger && Time.time >= nextFireTime)
        {
            Fire();
        }

        AddReward(settings.TimePenalty);

        // Directional Reward (Facing Target)
        if (target != null)
        {
            Vector3 toTarget = (target.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, toTarget);
            if (dot > 0.7f)
            {
                AddReward(0.0005f * dot);
            }
        }

        CheckSelfDestruct();
    }

    private void Fire()
    {
        nextFireTime = Time.time + settings.FireCooldown;

        // Use Environment Pool
        if (envHub != null && envHub.Pool != null)
        {
            var bullet = envHub.Pool.Spawn(projectilePrefab, muzzlePoint.position, transform.rotation);
            bullet.Launch(this, muzzlePoint.position, transform.rotation, settings.BulletSpeed, settings.BulletDamage, settings.BulletLifetime);
        }

        thrusters.ApplyRecoil(-transform.forward * settings.RecoilForce);
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        AddReward(settings.DamagePenalty);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            AddReward(settings.LossPenalty);
            arena.ReportDeath(this);
            gameObject.SetActive(false);
        }
    }

    public void RegisterHit()
    {
        AddReward(settings.HitReward);
    }

    public void RegisterNearMiss()
    {
        AddReward(settings.NearMissReward);
    }

    public void RegisterWin()
    {
        AddReward(settings.WinReward);
        // EndEpisode handled by EnvHub
    }

    public void RegisterDraw()
    {
        AddReward(settings.TimePenalty * 10); // Penalty for wasting time
        // EndEpisode handled by EnvHub
    }

    private void CheckSelfDestruct()
    {
        if (transform.position.y < -10 || transform.position.y > 100 ||
            Mathf.Abs(transform.position.x) > 100 || Mathf.Abs(transform.position.z) > 100)
        {
            AddReward(settings.CrashPenalty);
            arena.ReportDeath(this);
        }
    }
}