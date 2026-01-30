using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using RobotAI.Animation;

[RequireComponent(typeof(Rigidbody), typeof(CombatUnitHealth))]
[RequireComponent(typeof(ThrusterController))]
public class CombatAgentUnit : Agent
{
    [Header("References")]
    [SerializeField] private RobotEyeController eyeController;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private LaserProjectileV3 projectilePrefab;
    [SerializeField] private ArenaSettings settings;

    private Rigidbody _rb;
    private CombatUnitHealth _health;
    private ThrusterController _thrusters;
    private AerialArenaController _arena;
    private Transform _target;
    private float _nextFireTime;
    private int _teamId;

    public int TeamId => _teamId;
    public bool IsAlive => _health != null && !_health.IsDead;

    public void InitializeAgent(AerialArenaController arena, int teamId)
    {
        _arena = arena;
        _teamId = teamId;
        _rb = GetComponent<Rigidbody>();
        _health = GetComponent<CombatUnitHealth>();
        _thrusters = GetComponent<ThrusterController>();

        _rb.useGravity = true;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (settings != null)
        {
            _rb.mass = settings.AgentMass;
            _rb.linearDamping = settings.AgentDrag;
            _rb.angularDamping = settings.AgentAngularDrag;
            _thrusters.Initialize(_rb, settings.MovementThrust);
        }

        if (_health != null)
        {
            _health.OnDeath += HandleDeath;
            _health.OnDamageTaken += HandleDamage;
        }
    }

    public void SetTarget(Transform target) => _target = target;

    public void PrepareForRound(Vector3 position, Quaternion rotation)
    {
        _thrusters.ResetPhysics(position, rotation);
        if (_health) _health.ResetHealth();
        _nextFireTime = 0f;
    }

    public void BeginRound() { }
    public void EndRound() { }

    public void RegisterHit()
    {
        if (settings) AddReward(settings.AccuracyReward);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (_target == null)
        {
            sensor.AddObservation(new float[23]); // Tang len 23
            return;
        }

        Vector3 toTarget = _target.position - transform.position;

        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(transform.localRotation);
        sensor.AddObservation(_rb.linearVelocity);
        sensor.AddObservation(_rb.angularVelocity);

        sensor.AddObservation(toTarget.normalized);
        sensor.AddObservation(toTarget.magnitude);

        Vector3 targetVel = Vector3.zero;
        if (_target.TryGetComponent<Rigidbody>(out var trb)) targetVel = trb.linearVelocity;
        sensor.AddObservation(targetVel);

        sensor.AddObservation(_health.HealthPercentage);
        sensor.AddObservation(Time.time >= _nextFireTime);

        // --- New Observation: Laser Sight (Is Looking At Enemy?) ---
        bool isLockedOn = false;
        Vector3 dirToTarget = (_target.position - transform.position).normalized;
        float dot = Vector3.Dot(transform.forward, dirToTarget);
        // Neu goc nhin < 5 do (dot > 0.996) coi nhu la Locked
        if (dot > 0.996f) isLockedOn = true;

        sensor.AddObservation(isLockedOn); // +1 Obs (Total 23)
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var continuous = actions.ContinuousActions;
        var discrete = actions.DiscreteActions;

        if (_thrusters.ThrusterCount == 4 && continuous.Length >= 4)
        {
            float[] thrustInputs = new float[] { continuous[0], continuous[1], continuous[2], continuous[3] };
            _thrusters.ApplyThrust(thrustInputs);
        }

        // --- 1. Flight Stability Reward (Day AI giu thang bang) ---
        // Neu truc Y cua may bay huong len troi -> Thuong. Giup AI khong bi lon nhao.
        float uprightDot = Vector3.Dot(transform.up, Vector3.up);
        if (uprightDot > 0)
        {
            AddReward(uprightDot * 0.002f);
        }

        // --- 2. Alive Bonus (Khuyen khich song sot) ---
        // Bù lại TimePenalty, giup AI hieu rang "Song la tot"
        AddReward(0.001f);

        // --- 3. Aiming Reward (Khuyen khich ngam) ---
        if (_target != null)
        {
            Vector3 toTarget = (_target.position - transform.position).normalized;
            float alignment = Vector3.Dot(transform.forward, toTarget);

            // Thuong nhe cho viec chi can nhin ve phia dich (alignment > 0)
            if (alignment > 0) AddReward(alignment * 0.001f);

            // Thuong dam neu ngam trung > 0.9 (tuc la gan thang hang)
            if (alignment > 0.9f)
            {
                AddReward(0.005f); // Tang thuong len 0.005
            }
        }

        if (discrete[0] > 0 && Time.time >= _nextFireTime)
        {
            // --- Spam Penalty (TAM TAT) ---
            // De AI hoc ban trung da, sau nay phat sau.
            // AddReward(-0.025f);
            FireWeapon();
        }

        if (settings) AddReward(settings.TimePenalty);
    }

    private void FireWeapon()
    {
        if (_arena == null || _arena.Pool == null || settings == null) return;

        _nextFireTime = Time.time + settings.FireCooldown;
        LaserProjectileV3 laser = _arena.Pool.Spawn(projectilePrefab, muzzlePoint.position, transform.rotation);

        if (laser != null)
        {
            Debug.Log($"[COMBAT] {name} Fired! Passing Owner ID: {GetInstanceID()}");
            laser.Initialize(this);
            _thrusters.ApplyRecoil(-transform.forward * 5f);
        }
    }

    private void HandleDeath()
    {
        if (settings) AddReward(settings.LossPenalty);
        if (_arena) _arena.NotifyAgentDied(this);
        gameObject.SetActive(false);
    }

    private void HandleDamage(float amount)
    {
        if (settings) AddReward(-amount * settings.DamagePenalty);
    }

    // --- Crash Logic ---

    private void OnCollisionEnter(Collision collision)
    {
        CheckCrash(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckCrash(other.gameObject);
    }

    private void CheckCrash(GameObject obj)
    {
        // Neu va cham voi Tuong hoac Dat -> Crash
        if (obj.CompareTag("Wall") || obj.CompareTag("Ground"))
        {
            // Chi phat 1 lan (theo settings.LossPenalty) de AI hieu la chet la te, nhung khong qua te.
            // if (settings) AddReward(settings.LossPenalty); // (Da co trong HandleDeath)

            // Phat them mot chut de no biet la Crash te hon bi ban chet
            AddReward(-0.2f);
            HandleDeath();
        }
    }
}