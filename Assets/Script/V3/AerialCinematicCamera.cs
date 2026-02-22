using UnityEngine;

public class AerialCinematicCamera : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField] private AerialArenaController arena;
    [SerializeField] private float smoothness = 5f;
    [SerializeField] private float rotationSmoothness = 2f;
    
    [Header("Cinematic Settings")]
    [SerializeField] private Vector3 followOffset = new Vector3(0, 5, -15);
    [SerializeField] private float minFieldOfView = 60f;
    [SerializeField] private float maxFieldOfView = 90f;
    [SerializeField] private float closeDistanceThreshold = 20f; // Khoảng cách để chuyển sang Group Mode

    [Header("Shake Settings")]
    [SerializeField] private float shakeIntensity = 0.5f;
    [SerializeField] private float shakeDuration = 0.5f;

    private Camera _cam;
    private Transform _targetA;
    private Transform _targetB;
    private Transform _currentTarget;
    
    private float _shakeTimer;
    private Vector3 _originalPos;
    private float _currentFOV;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (arena == null) arena = FindObjectOfType<AerialArenaController>();
    }

    private void Start()
    {
        if (arena != null)
        {
            _targetA = arena.AgentA.transform;
            _targetB = arena.AgentB.transform;
            _currentTarget = _targetA;
        }
        _currentFOV = minFieldOfView;
    }

    private void LateUpdate()
    {
        if (_targetA == null || _targetB == null || !arena.gameObject.activeInHierarchy) return;

        HandleCameraMovement();
        HandleShake();
    }

    private void HandleCameraMovement()
    {
        float distance = Vector3.Distance(_targetA.position, _targetB.position);
        Vector3 finalPosition;
        Quaternion finalRotation;

        // Logic: Nếu 2 máy bay gần nhau -> Quay góc trung tâm (Group Shot)
        // Nếu xa nhau -> Bám theo máy bay đang active hoặc ngẫu nhiên
        if (distance < closeDistanceThreshold)
        {
            // --- GROUP SHOT MODE ---
            Vector3 centerPoint = (_targetA.position + _targetB.position) / 2f;
            
            // Camera lùi ra xa dựa trên khoảng cách 2 máy bay để giữ cả 2 trong khung hình
            float zoomFactor = Mathf.Clamp(distance, 10f, 50f);
            Vector3 offset = Vector3.up * (zoomFactor * 0.5f) - Vector3.forward * zoomFactor;
            
            finalPosition = centerPoint + offset;
            
            // Luôn nhìn vào điểm giữa
            Vector3 direction = (centerPoint - transform.position).normalized;
            if (direction != Vector3.zero)
                finalRotation = Quaternion.LookRotation(direction);
            else
                finalRotation = transform.rotation;

            _currentFOV = Mathf.Lerp(_currentFOV, maxFieldOfView, Time.deltaTime * 2f);
        }
        else
        {
            // --- CHASE MODE ---
            // Chọn target dựa trên ai đang sống hoặc switch ngẫu nhiên (có thể mở rộng logic này)
            Transform activeTarget = DetermineBestTarget();
            
            // Vị trí mong muốn: Phía sau và trên cao một chút so với đuôi máy bay
            Vector3 localOffset = activeTarget.TransformDirection(followOffset);
            finalPosition = activeTarget.position + localOffset;

            // Nhìn về phía mục tiêu
            Vector3 lookDir = activeTarget.position - transform.position + activeTarget.forward * 10f; // Nhìn xa hơn mũi máy bay 1 chút
            finalRotation = Quaternion.LookRotation(lookDir.normalized);

            _currentFOV = Mathf.Lerp(_currentFOV, minFieldOfView, Time.deltaTime * 2f);
        }

        // Apply Movement & Rotation (Smoothing)
        transform.position = Vector3.Lerp(transform.position, finalPosition, Time.deltaTime * smoothness);
        transform.rotation = Quaternion.Slerp(transform.rotation, finalRotation, Time.deltaTime * rotationSmoothness);
        _cam.fieldOfView = _currentFOV;
    }

    private Transform DetermineBestTarget()
    {
        // Logic đơn giản: Nếu 1 con chết, camera follow con còn sống
        if (!arena.AgentA.IsAlive) return _targetB;
        if (!arena.AgentB.IsAlive) return _targetA;
        
        // Nếu cả 2 sống, giữ nguyên target hiện tại hoặc switch mỗi 5s (có thể code thêm)
        // Hiện tại ta sẽ ưu tiên Agent A (Main)
        return _targetA; 
    }

    public void TriggerImpactEffect()
    {
        _shakeTimer = shakeDuration;
    }

    private void HandleShake()
    {
        if (_shakeTimer > 0)
        {
            transform.position += Random.insideUnitSphere * shakeIntensity;
            _shakeTimer -= Time.deltaTime;
        }
    }
}