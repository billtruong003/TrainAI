using UnityEngine;
using System.Collections.Generic;

public class ThrusterController : MonoBehaviour
{
    [SerializeField] private List<Transform> thrusterPoints;

    private Rigidbody rb;
    private float maxForce;

    public void Initialize(Rigidbody targetRb, float forceLimit)
    {
        rb = targetRb;
        maxForce = forceLimit;
    }

    public void ApplyThrust(float[] inputs)
    {
        if (inputs.Length != thrusterPoints.Count) return;

        for (int i = 0; i < thrusterPoints.Count; i++)
        {
            float forceMagnitude = Mathf.Clamp01(inputs[i]) * maxForce;
            Vector3 force = thrusterPoints[i].up * forceMagnitude;
            rb.AddForceAtPosition(force, thrusterPoints[i].position, ForceMode.Force);
        }
    }

    public void ApplyRecoil(Vector3 recoilImpulse)
    {
        rb.AddForce(recoilImpulse, ForceMode.Impulse);
    }

    public void ResetPhysics(Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public int ThrusterCount => thrusterPoints.Count;

#if UNITY_EDITOR
    public void AutoSetupPoints(float spreadX, float spreadZ)
    {
        thrusterPoints = new List<Transform>();
        CreatePoint("Thruster_FL", -spreadX, spreadZ);
        CreatePoint("Thruster_FR", spreadX, spreadZ);
        CreatePoint("Thruster_RL", -spreadX, -spreadZ);
        CreatePoint("Thruster_RR", spreadX, -spreadZ);
    }

    private void CreatePoint(string name, float x, float z)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform);
        obj.transform.localPosition = new Vector3(x, 0, z);
        obj.transform.localRotation = Quaternion.identity;
        thrusterPoints.Add(obj.transform);
    }
#endif
}