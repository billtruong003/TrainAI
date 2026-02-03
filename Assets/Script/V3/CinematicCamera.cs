using UnityEngine;

public class CinematicCamera : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform targetA;
    [SerializeField] private Transform targetB;

    [Header("Orbit Settings")]
    [SerializeField] private float smoothSpeed = 3f;
    [SerializeField] private Vector3 offset = new Vector3(0, 8, -15);
    [SerializeField] private float minZoom = 12f;
    [SerializeField] private float maxZoom = 30f;
    [SerializeField] private bool invertSide = false;

    private CameraState _state = CameraState.MidpointOrbit;
    private float _currentZoom;

    public enum CameraState
    {
        MidpointOrbit, // Overview of both
        FocusA,
        FocusB,
        FPV_A,
        FPV_B
    }

    private void Awake()
    {
        _currentZoom = minZoom;
    }

    private void Update()
    {
        HandleInput();
        
        // Safety check
        if (targetA == null || targetB == null) return;

        switch (_state)
        {
            case CameraState.MidpointOrbit:
                UpdateOrbit();
                break;
            case CameraState.FocusA:
                UpdateFocus(targetA);
                break;
            case CameraState.FocusB:
                UpdateFocus(targetB);
                break;
            case CameraState.FPV_A:
                UpdateFPV(targetA);
                break;
            case CameraState.FPV_B:
                UpdateFPV(targetB);
                break;
        }
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) { _state = CameraState.FocusA; Debug.Log("Cam: Focus A"); }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { _state = CameraState.FocusB; Debug.Log("Cam: Focus B"); }
        if (Input.GetKeyDown(KeyCode.Alpha3)) { _state = CameraState.MidpointOrbit; Debug.Log("Cam: Orbit"); }
        if (Input.GetKeyDown(KeyCode.Alpha4)) { _state = CameraState.FPV_A; Debug.Log("Cam: FPV A"); }
        if (Input.GetKeyDown(KeyCode.Alpha5)) { _state = CameraState.FPV_B; Debug.Log("Cam: FPV B"); }
        if (Input.GetKeyDown(KeyCode.Space)) _state = CameraState.MidpointOrbit;
        if (Input.GetKeyDown(KeyCode.X)) { invertSide = !invertSide; Debug.Log($"Cam: Invert Side: {invertSide}"); }
    }

    private void UpdateOrbit()
    {
        // Find midpoint
        Vector3 centerPoint = (targetA.position + targetB.position) * 0.5f;
        float distance = Vector3.Distance(targetA.position, targetB.position);

        // Zoom logic based on distance
        float requiredZoom = Mathf.Clamp(distance * 1.0f, minZoom, maxZoom);
        _currentZoom = Mathf.Lerp(_currentZoom, requiredZoom, Time.deltaTime * 2f);

        // Calculate desired position
        // Try to stay perpendicular to the line connecting agents for best view
        Vector3 dirAB = (targetB.position - targetA.position).normalized;
        Vector3 perp = Vector3.Cross(dirAB, Vector3.up).normalized;
        if (perp == Vector3.zero) perp = Vector3.right;

        // Handle Side Inversion
        if (invertSide) perp = -perp;

        // We want to be roughly above and to the side
        Vector3 viewDir = (perp + Vector3.up * 0.5f).normalized;
        Vector3 desiredPos = centerPoint + viewDir * _currentZoom;

        transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * smoothSpeed);
        
        // Look at center, but slightly dampened
        Quaternion targetRot = Quaternion.LookRotation(centerPoint - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * smoothSpeed * 2f);
    }

    private void UpdateFocus(Transform target)
    {
        // Third person chase cam
        Vector3 desiredPos = target.position + target.TransformDirection(new Vector3(0, 3, -8));
        transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * smoothSpeed * 2f);
        
        Quaternion targetRot = Quaternion.LookRotation(target.position + target.forward * 10f - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
    }

    private void UpdateFPV(Transform target)
    {
        // Hard lock for FPV feel
        transform.position = target.position + target.forward * 0.5f + target.up * 0.2f;
        transform.rotation = target.rotation;
    }
}