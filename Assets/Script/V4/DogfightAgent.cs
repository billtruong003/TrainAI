using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

[RequireComponent(typeof(Rigidbody), typeof(ThrusterController))]
public class DogfightAgent : Agent
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

    public void PrepareRound(Vector3 pos, Quaternion rot)
    {
        currentHealth = settings.MaxHealth;
        thrusters.ResetPhysics(pos, rot);
        nextFireTime = 0f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (target == null)
        {
            sensor.AddObservation(new float[14]);
            return;
        }

        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(transform.localRotation);
        sensor.AddObservation(rb.linearVelocity);
        sensor.AddObservation(rb.angularVelocity);

        Vector3 toTarget = target.position - transform.position;
        sensor.AddObservation(toTarget.normalized);
        sensor.AddObservation(toTarget.magnitude);

        Vector3 targetVel = Vector3.zero;
        if (target.TryGetComponent<Rigidbody>(out var tRb)) targetVel = tRb.linearVelocity;
        sensor.AddObservation(targetVel);

        sensor.AddObservation(transform.InverseTransformDirection(toTarget));
        sensor.AddObservation(currentHealth / settings.MaxHealth);

        sensor.AddObservation(Vector3.Dot(transform.forward, toTarget.normalized));
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
        CheckSelfDestruct();
    }

    private void Fire()
    {
        nextFireTime = Time.time + settings.FireCooldown;

        var bullet = Instantiate(projectilePrefab); // Replace with Object Pool in production
        bullet.Launch(this, muzzlePoint.position, transform.rotation, settings.BulletSpeed, settings.BulletDamage, settings.BulletLifetime);

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
        EndEpisode();
    }

    public void RegisterDraw()
    {
        EndEpisode();
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