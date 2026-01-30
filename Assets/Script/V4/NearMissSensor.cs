using UnityEngine;

public class NearMissSensor : MonoBehaviour
{
    private DogfightAgent parentAgent;
    private int parentId;

    public void Initialize(DogfightAgent agent)
    {
        parentAgent = agent;
        parentId = agent.GetInstanceID();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<SmartProjectile>(out var projectile))
        {
            if (projectile.OwnerId != parentId)
            {
                parentAgent.RegisterNearMiss();
            }
        }
    }
}