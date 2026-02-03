using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using RobotAI.Animation;

[RequireComponent(typeof(Rigidbody), typeof(CombatUnitHealth))]
public class CombatAgentUnit : Agent
{
    [Header("References")]
    [SerializeField] private RobotEyeController eyeController;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private LaserProjectileV3 projectilePrefab;
    [SerializeField] private ArenaSettings settings;

    private Collider[] _perceptionBuffer = new Collider[20];
    private Rigidbody _rb;
    private CombatUnitHealth _health;
    private AerialArenaController _arena;
    private Transform _target;
    private float _nextFireTime;
    private int _teamId;

    public int TeamId => _teamId;
    public bool IsAlive => _health != null && !_health.IsDead;
    public CombatUnitHealth Health => _health;
    public AerialArenaController Arena => _arena;
    public event System.Action OnFired;

    public void InitializeAgent(AerialArenaController arena, int teamId)
    {
        _arena = arena;
        _teamId = teamId;
        _rb = GetComponent<Rigidbody>();
        _health = GetComponent<CombatUnitHealth>();

        _rb.useGravity = true;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (settings != null)
        {
            _rb.mass = settings.AgentMass;
            _rb.linearDamping = settings.AgentDrag;
            _rb.angularDamping = settings.AgentAngularDrag;
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
        transform.SetPositionAndRotation(position, rotation);
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true; // Freeze for PreRound

        // Fix Trail Renderer dragging
        var trails = GetComponentsInChildren<TrailRenderer>();
        foreach (var t in trails) t.Clear();

        if (_health) _health.ResetHealth();
        _nextFireTime = 0f;
    }

    public void BeginRound()
    {
        _rb.isKinematic = false;
        _rb.WakeUp();
    }

    public void EndRound()
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true; // Freeze for PostRound
    }

    public void RegisterHit()
    {
        if (settings) AddReward(settings.AccuracyReward);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (_target == null)
        {
            for (int i = 0; i < 9; i++) sensor.AddObservation(0f);
            return;
        }

        Vector3 localTargetPos = transform.InverseTransformPoint(_target.position);
        sensor.AddObservation(localTargetPos.normalized);
        sensor.AddObservation(localTargetPos.magnitude / 200f);

        sensor.AddObservation(transform.InverseTransformDirection(_rb.linearVelocity) / 20f);
        sensor.AddObservation(transform.InverseTransformDirection(_rb.angularVelocity) / 10f);

        Vector3 targetWorldVel = Vector3.zero;
        if (_target.TryGetComponent<Rigidbody>(out var trb)) targetWorldVel = trb.linearVelocity;

        Vector3 relativeVel = targetWorldVel - _rb.linearVelocity;
        sensor.AddObservation(transform.InverseTransformDirection(relativeVel) / 20f);

        sensor.AddObservation(_health.HealthPercentage);
        sensor.AddObservation(Time.time >= _nextFireTime);

        bool isLockedOn = localTargetPos.normalized.z > 0.996f;
        sensor.AddObservation(isLockedOn);

        // --- NEW: Incoming Threat Detection (6 Floats) ---
        Vector3 threatRelPos = Vector3.zero;
        Vector3 threatRelVel = Vector3.zero;

        // Scan for projectiles within 30m
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, 30f, _perceptionBuffer);
        float closestDistSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _perceptionBuffer[i];
            // Skip self and non-projectiles. Using GetComponent is slightly expensive but safest without tags.
            // Optimization: Assume Projectiles are on a specific Layer if configured, but here we search broadly.
            if (col == null || col.attachedRigidbody == null) continue;

            // Check if it's a projectile moving towards us
            // Note: We check if it has the component. 
            // To optimize, you should assign a Tag "Projectile" to the prefab.
            if (col.GetComponent<LaserProjectileV3>() != null)
            {
                Vector3 toMe = transform.position - col.transform.position;
                // Dot > 0 means the bullet velocity is roughly in our direction
                if (Vector3.Dot(col.attachedRigidbody.linearVelocity, toMe) > 0)
                {
                    float dSqr = toMe.sqrMagnitude;
                    if (dSqr < closestDistSqr)
                    {
                        closestDistSqr = dSqr;
                        threatRelPos = transform.InverseTransformPoint(col.transform.position);
                        threatRelVel = transform.InverseTransformDirection(col.attachedRigidbody.linearVelocity);
                    }
                }
            }
        }

        sensor.AddObservation(threatRelPos / 30f); // Normalized Pos
        sensor.AddObservation(threatRelVel / 150f); // Normalized Vel (Speed ~150)

        // --- NEW: Stun Status (2 Floats) ---
        sensor.AddObservation(_health.IsStunned);
        sensor.AddObservation(_health.StunPercentage);

        // --- NEW: Wall Detection Rays (10 Floats) ---
        // Helps agent avoid walls blindly
        float rayDist = 30f;
        int layerMask = settings != null ? settings.ObstacleLayer : 1;

        // 8 Horizontal Rays (Relative to Agent)
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * transform.forward;
            bool hit = Physics.Raycast(transform.position, dir, out RaycastHit hitInfo, rayDist, layerMask);
            sensor.AddObservation(hit ? hitInfo.distance / rayDist : 1f);
            // Debug.DrawRay(transform.position, dir * (hit ? hitInfo.distance : rayDist), Color.gray);
        }

        // 2 Vertical Rays (Up/Down Relative)
        bool hitUp = Physics.Raycast(transform.position, transform.up, out RaycastHit hitInfoUp, rayDist, layerMask);
        sensor.AddObservation(hitUp ? hitInfoUp.distance / rayDist : 1f);

        bool hitDown = Physics.Raycast(transform.position, -transform.up, out RaycastHit hitInfoDown, rayDist, layerMask);
        sensor.AddObservation(hitDown ? hitInfoDown.distance / rayDist : 1f);

        // Total Obs = 16 (Base) + 6 (Threat) + 2 (Stun) + 10 (Rays) = 34.
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // If Frozen (Pre-Round) or Stunned, lose control
        if (_rb.isKinematic || _health.IsStunned)
        {
            AddReward(-0.005f); // Penalty while stunned
            return; // Skip control logic -> Free fall / Tumble
        }

        var continuous = actions.ContinuousActions;
        var discrete = actions.DiscreteActions;

        _rb.AddForce(-Physics.gravity * _rb.mass, ForceMode.Force);

        float moveZ = Mathf.Clamp(continuous[0], -1f, 1f);
        float yaw = Mathf.Clamp(continuous[1], -1f, 1f);
        float pitch = (continuous.Length > 2) ? Mathf.Clamp(continuous[2], -1f, 1f) : 0f;
        float moveY = (continuous.Length > 3) ? Mathf.Clamp(continuous[3], -1f, 1f) : 0f;

        if (settings)
        {
            Vector3 force = (Vector3.forward * moveZ) + (Vector3.up * moveY);
            _rb.AddRelativeForce(force * settings.MovementThrust, ForceMode.Force);

            Vector3 torque = new Vector3(pitch, yaw, 0) * settings.RotationTorque;
            _rb.AddRelativeTorque(torque, ForceMode.Force);

            // --- NEW: Movement Incentive ---
            // Penalty for standing still (camping)
            float speed = _rb.linearVelocity.magnitude;
            if (speed < 5f)
            {
                AddReward(-0.0005f);
            }
            else
            {
                // Small reward for maintaining combat speed
                AddReward(0.0002f);
            }
        }

        float roll = transform.eulerAngles.z;
        if (roll > 180) roll -= 360;
        _rb.AddRelativeTorque(Vector3.forward * -roll * 2.0f, ForceMode.Acceleration);

        if (_target != null)
        {
            Vector3 toTarget = _target.position - transform.position;
            float alignment = Vector3.Dot(transform.forward, toTarget.normalized);

            if (alignment > 0.5f)
            {
                AddReward(alignment * 0.01f);
            }
        }

        if (discrete[0] > 0 && Time.time >= _nextFireTime)
        {
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
            laser.Initialize(this, settings);
            _rb.AddForce(-transform.forward * 2f, ForceMode.Impulse);
            _rb.AddRelativeTorque(Vector3.up * Random.Range(-0.5f, 0.5f), ForceMode.Impulse);
            AddReward(-0.002f);
            OnFired?.Invoke();
        }
    }

    private void HandleDeath()
    {
        // Play Mode Visuals
        if (settings && !settings.IsTrainingMode)
        {
            var visuals = (_teamId == 0) ? settings.TeamA_Visuals : settings.TeamB_Visuals;
            if (visuals.ExplosionVFX && _arena && _arena.Pool)
            {
                var vfx = _arena.Pool.Spawn(visuals.ExplosionVFX, transform.position, transform.rotation);
                if (vfx != null && vfx.GetComponent<VFXAutoReturn>() == null)
                    vfx.AddComponent<VFXAutoReturn>();
            }

            if (settings.ExplosionClip)
                AudioSource.PlayClipAtPoint(settings.ExplosionClip, transform.position);
        }

        if (settings) AddReward(settings.LossPenalty);
        if (_arena) _arena.NotifyAgentDied(this);
    }

    private void HandleDamage(float amount)
    {
        if (settings) AddReward(-amount * settings.DamagePenalty);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Agent"))
        {
            Vector3 pushDir = (transform.position - collision.transform.position).normalized;
            _rb.AddForce(pushDir * 20f, ForceMode.Impulse);
            AddReward(-0.5f);
        }
        else if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Ground"))
        {
            CrashPenalty();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Wall") || other.CompareTag("Ground"))
        {
            CrashPenalty();
        }
    }

    private void CrashPenalty()
    {
        if (!IsAlive) return;
        AddReward(-1.0f);
        HandleDeath();
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 5f);

        if (_target != null)
        {
            Vector3 toTarget = _target.position - transform.position;
            float dot = Vector3.Dot(transform.forward, toTarget.normalized);
            Gizmos.color = dot > 0.996f ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, _target.position);
        }
    }


}