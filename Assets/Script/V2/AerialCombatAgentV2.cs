using UnityEngine;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using RobotAI.Animation;

[RequireComponent(typeof(Rigidbody), typeof(CombatUnitHealth))]
public class AerialCombatAgentV2 : SmartAgent
{
    [Header("V2 Config")]
    [SerializeField] private RobotEyeController eyeController;
    [SerializeField] private CombatUnitHealth health;
    [SerializeField] private Transform eyePosition;
    [SerializeField] private LaserProjectileV2 laserPrefab; // Reference truc tiep Prefab

    [Header("Flight Physics")]
    [SerializeField] private float thrustForce = 200f;
    [SerializeField] private float torqueForce = 50f;
    [SerializeField] private float maxSpeed = 30f;
    [SerializeField] private bool allowAcrobatics = true;

    private Rigidbody rb;
    private Transform currentTarget;
    private float nextFireTime;
    private const float FIRE_COOLDOWN = 0.2f;

    // Cache Env reference
    private AerialCombatEnvironment aerialEnv;

    public override void Initialize()
    {
        base.Initialize();
        rb = GetComponent<Rigidbody>();
        health.OnDeath += OnDeathHandler;
        health.OnDamageTaken += OnDamageHandler;
        aerialEnv = envHub as AerialCombatEnvironment;
        ConfigureSensors();
    }

    // -- Override SmartAgent Flow --
    public override void OnEnvironmentReady()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        health.ResetHealth();
        nextFireTime = 0f;
    }

    public void SetTarget(Transform t) => currentTarget = t;

    private void ConfigureSensors()
    {
        // Add Ray Perception Sensor 3D neu chua co
        var sensor = GetComponent<RayPerceptionSensorComponent3D>();
        if (sensor == null)
        {
            sensor = gameObject.AddComponent<RayPerceptionSensorComponent3D>();
            sensor.SensorName = "AerialEye";
            sensor.RaysPerDirection = 3;
            sensor.MaxRayDegrees = 60;
            sensor.SphereCastRadius = 0.5f;
            sensor.RayLength = 100f;
            sensor.DetectableTags = new System.Collections.Generic.List<string>() { "Agent", "Wall", "Ground" };
        }
    }

    protected override int CalculateObservationSize()
    {
        // Obs cu = 19. Them logic moi co the tang len, nhung hien tai RayPerception tu dong them obs cua no.
        // Vector Obs van giu 19.
        return 19;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(transform.localRotation);
        sensor.AddObservation(rb.linearVelocity);

        Vector3 targetOffset = currentTarget != null ? currentTarget.position - transform.position : Vector3.zero;
        sensor.AddObservation(targetOffset);

        Vector3 targetVel = Vector3.zero;
        if (currentTarget != null && currentTarget.TryGetComponent<Rigidbody>(out var targetRb))
            targetVel = targetRb.linearVelocity;
        sensor.AddObservation(targetVel);

        sensor.AddObservation(health.HealthPercentage);
        sensor.AddObservation(eyeController != null ? eyeController.IsVisionClear : true);
        sensor.AddObservation(Time.time >= nextFireTime);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        ApplyFlightForces(actions.ContinuousActions);

        float phase = aerialEnv != null ? aerialEnv.CurrentPhase : 0;

        // Shoot Logic: Chi cho phep ban tu Phase 2 tro di
        if (phase >= 2.0f && actions.DiscreteActions[0] == 1 && CanShoot())
        {
            FireLaser();
        }

        // --- STACKED REWARD SYSTEM ---
        AddReward(0.0005f); // Alive bonus small

        if (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            Vector3 toTarget = (currentTarget.position - transform.position).normalized;
            float align = Vector3.Dot(transform.forward, toTarget);

            // Skill 1: Flight Mastery (Phase 0+)
            // Thuong khi bay lai gan muc tieu
            if (phase < 3.0f) // Trong Combat thi khong can reward nay nua, de tranh kamikaze
            {
                // Reward: cang gan cang tot (1.0 tai khoang cach 0, 0.0 tai khoang cach 50)
                AddReward(Mathf.Clamp01(1.0f - dist / 50f) * 0.001f);
            }

            // Skill 2: Tracking (Phase 1+)
            // Thuong khi giu muc tieu trong tam mat (Alignment > 0)
            if (phase >= 1.0f)
            {
                if (align > 0.5f) AddReward(align * 0.002f);
            }

            // Logic Game: Chamm Waypoint (Phase 0, 1)
            if (phase < 2.0f && currentTarget.name.Contains("Waypoint"))
            {
                if (dist < 3f)
                {
                    AddReward(1.0f);
                    aerialEnv.SpawnNextWaypoint();
                }
            }
        }
    }

    private void ApplyFlightForces(ActionSegment<float> moves)
    {
        Vector3 thrust = new Vector3(moves[0], moves[1], moves[2]) * thrustForce;
        Vector3 torque = new Vector3(moves[3], moves[4], moves[5]) * torqueForce;

        rb.AddRelativeForce(thrust * Time.fixedDeltaTime, ForceMode.Acceleration);
        rb.AddRelativeTorque(torque * Time.fixedDeltaTime, ForceMode.Acceleration);

        if (rb.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;

        if (!allowAcrobatics && transform.up.y < 0)
            AddReward(-0.01f);
    }

    private bool CanShoot()
    {
        if (Time.time < nextFireTime) return false;
        if (eyeController != null && !eyeController.IsVisionClear)
        {
            AddReward(-0.005f); // Penalty blind fire
            return false;
        }
        return true;
    }

    private void FireLaser()
    {
        if (laserPrefab == null) return;
        nextFireTime = Time.time + FIRE_COOLDOWN;

        // Spawn using Smart Pool (Prefab as key)
        LaserProjectileV2 laser = envHub.Pool.Spawn(laserPrefab, eyePosition.position, transform.rotation);

        if (laser != null)
        {
            // Pass THIS agent de ghi diem neu ban trung
            laser.Initialize(this);
            rb.AddForce(-transform.forward * 15f, ForceMode.Impulse); // Recoil
        }
    }

    private void OnDeathHandler()
    {
        // Trong Dogfight (Phase 3), chet la thiet hai lon
        // Trong Training (Phase 0-2), chet la do dam tuong -> phat nang
        AddReward(-1.0f);
        envHub.NotifyAgentDone(this, false);
    }

    private void OnDamageHandler(float dmg)
    {
        // Phase 2+: Bi ban trung thi bi phat
        if (aerialEnv != null && aerialEnv.CurrentPhase >= 2.0f)
        {
            AddReward(-dmg * 0.01f);
        }
    }
}