using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using RobotAI.Animation;

[RequireComponent(typeof(Rigidbody), typeof(CombatUnitHealth))]
public class AerialCombatAgent : Agent
{
    [Header("Components")]
    [SerializeField] private RobotEyeController eyeController;
    [SerializeField] private CombatUnitHealth health;
    [SerializeField] private Transform eyePosition;
    [SerializeField] private GameObject laserPrefab;

    [Header("Movement Settings")]
    [SerializeField] private float thrustForce = 200f;
    [SerializeField] private float torqueForce = 50f;
    [SerializeField] private float maxSpeed = 30f;

    // Thêm biến này để cho phép lộn mèo thoải mái hơn
    [SerializeField] private bool allowAcrobatics = true;

    [Header("Combat Settings")]
    [SerializeField] private float recoilForce = 15f;
    [SerializeField] private float fireCooldown = 0.2f;

    [Header("Boundary Settings")]
    [SerializeField] private float boundaryZ = 0f;
    [SerializeField] private bool allowedPositiveZ; // Dùng cho chế độ chia sân
    [SerializeField] private bool ignoreZBoundary = false; // Dùng cho chế độ tập bay toàn map

    private Rigidbody rb;
    private float nextFireTime;
    private Transform target;
    private DualCombatArena arena; // Tham chiếu ngược lại Arena để báo tin

    public void SetArena(DualCombatArena a) => arena = a;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        health.OnDeath += HandleDeath;
        health.OnDamageTaken += HandleDamage;
    }

    public override void OnEpisodeBegin()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        health.ResetHealth();
        nextFireTime = 0f;
        // Reset vị trí được gọi từ Arena, không gọi ở đây để tránh conflict
    }

    public void SetTarget(Transform t) => target = t;
    public void SetBoundaryIgnore(bool ignore) => ignoreZBoundary = ignore;

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. Bản thân (9 floats)
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(transform.localRotation); // Quaternion (4)
        sensor.AddObservation(rb.linearVelocity);

        // 2. Mục tiêu (3 floats)
        // Nếu không có target (vd mới vào game), trả về zero để không lỗi
        Vector3 targetOffset = target != null ? target.position - transform.position : Vector3.zero;
        sensor.AddObservation(targetOffset);

        // Thêm vận tốc tương đối nếu target là Rigidbody (để bắn đón đầu)
        Vector3 targetVel = Vector3.zero;
        if (target != null && target.GetComponent<Rigidbody>() != null)
            targetVel = target.GetComponent<Rigidbody>().linearVelocity;
        sensor.AddObservation(targetVel); // (3 floats)

        // 3. Trạng thái (3 floats)
        sensor.AddObservation(health.HealthPercentage);
        sensor.AddObservation(eyeController != null ? eyeController.IsVisionClear : true);
        sensor.AddObservation(IsWeaponReady());
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        ApplyMovement(actions.ContinuousActions);
        CheckBoundaries();

        // Chỉ bắn khi có target là Agent khác (không bắn waypoint)
        if (actions.DiscreteActions[0] == 1)
        {
            // Kiểm tra xem target có phải là địch không
            if (target != null && target.GetComponent<AerialCombatAgent>() != null)
            {
                AttemptToShoot();
            }
        }

        // Reward cho việc hướng đầu về phía mục tiêu (giúp học bay nhanh hơn)
        if (target != null)
        {
            Vector3 toTarget = target.position - transform.position;
            float alignment = Vector3.Dot(transform.forward, toTarget.normalized);
            // Khuyến khích luôn nhìn về mục tiêu (0.002 mỗi frame nếu nhìn thẳng)
            if (alignment > 0) AddReward(alignment * 0.002f);
        }

        // Alive bonus nhỏ
        AddReward(0.0005f);
    }

    private void ApplyMovement(ActionSegment<float> moves)
    {
        Vector3 thrust = new Vector3(moves[0], moves[1], moves[2]) * thrustForce;
        Vector3 torque = new Vector3(moves[3], moves[4], moves[5]) * torqueForce;

        rb.AddRelativeForce(thrust * Time.fixedDeltaTime, ForceMode.Acceleration);
        rb.AddRelativeTorque(torque * Time.fixedDeltaTime, ForceMode.Acceleration);

        // Giới hạn tốc độ
        if (rb.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;

        // Nếu KHÔNG cho phép lộn mèo (muốn bay ổn định kiểu drone quay phim)
        // ta sẽ phạt nếu trục Up chúi xuống đất.
        // Nhưng bạn muốn combat acrobatics nên ta bỏ qua hoặc phạt rất nhẹ thôi.
        if (!allowAcrobatics)
        {
            if (transform.up.y < 0) AddReward(-0.01f); // Phạt nhẹ nếu lộn ngược
        }
    }

    private void CheckBoundaries()
    {
        // Nếu đang tập bay toàn map (ignoreZBoundary = true) thì bỏ qua check chia sân
        if (ignoreZBoundary) return;

        float currentZ = transform.localPosition.z;
        bool outOfBounds = allowedPositiveZ ? currentZ < boundaryZ : currentZ > boundaryZ;

        if (outOfBounds)
        {
            AddReward(-0.01f); // Phạt nhẹ cảnh cáo
        }
    }

    private bool IsWeaponReady()
    {
        return Time.time >= nextFireTime;
    }

    private void AttemptToShoot()
    {
        if (!IsWeaponReady()) return;

        if (eyeController != null && !eyeController.IsVisionClear)
        {
            // Phạt bắn mù ít thôi, để nó dám bắn
            AddReward(-0.005f);
            return;
        }

        Shoot();
        nextFireTime = Time.time + fireCooldown;
    }

    private void Shoot()
    {
        if (laserPrefab == null) return;

        GameObject laser = Instantiate(laserPrefab, eyePosition.position, transform.rotation);
        var proj = laser.GetComponent<LaserProjectile>();
        if (proj != null) proj.Initialize(GetInstanceID());

        rb.AddForce(-transform.forward * recoilForce, ForceMode.Impulse);
    }

    private void HandleDamage(float damage)
    {
        AddReward(-damage * 0.02f); // Bị bắn đau hơn chút
    }

    private void HandleDeath()
    {
        AddReward(-1.0f); // Chết là hết
        EndEpisode();

        // Báo cho Arena biết để reset trận đấu
        if (arena != null) arena.OnAgentDied(this);
    }

    // Hàm xử lý va chạm waypoint (gọi từ trigger)
    public void TouchedWaypoint()
    {
        AddReward(1.0f); // Thưởng lớn khi ăn waypoint
        // Không EndEpisode, chỉ cộng điểm
    }
}