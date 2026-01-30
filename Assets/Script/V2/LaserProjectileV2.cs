using UnityEngine;

[RequireComponent(typeof(PoolMember))]
public class LaserProjectileV2 : MonoBehaviour, IPoolable
{
    [SerializeField] private float speed = 100f;
    [SerializeField] private float lifeTime = 2f;
    [SerializeField] private float damage = 10f;

    private int ownerId;
    private AerialCombatAgentV2 ownerAgentV2;
    private CombatAgentUnit ownerAgentV3;
    private Rigidbody rb;
    private PoolMember poolMember;
    private float disableTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        poolMember = GetComponent<PoolMember>();
    }

    public void Initialize(AerialCombatAgentV2 agent)
    {
        ownerAgentV2 = agent;
        ownerId = agent.GetInstanceID();
        ownerAgentV3 = null;
    }

    public void Initialize(CombatAgentUnit agent)
    {
        ownerAgentV3 = agent;
        ownerId = agent.GetInstanceID();
        ownerAgentV2 = null;
    }

    public void Initialize(int id) { ownerId = id; }

    public void OnSpawn()
    {
        rb.linearVelocity = transform.forward * speed;
        disableTime = Time.time + lifeTime;
    }

    public void OnDespawn()
    {
        rb.linearVelocity = Vector3.zero;
        ownerId = -1;
        ownerAgentV2 = null;
        ownerAgentV3 = null;
    }

    private void Update()
    {
        if (Time.time >= disableTime) poolMember.ReturnToPool();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<CombatAgentUnit>(out var agentV3))
        {
            if (agentV3.GetInstanceID() == ownerId) return;
        }
        if (other.TryGetComponent<AerialCombatAgentV2>(out var agentV2))
        {
            if (agentV2.GetInstanceID() == ownerId) return;
        }

        if (other.CompareTag("Agent"))
        {
            var health = other.GetComponent<CombatUnitHealth>();
            if (health != null)
            {
                health.TakeDamage(damage);

                if (ownerAgentV2 != null) ownerAgentV2.AddReward(0.5f);
                if (ownerAgentV3 != null) ownerAgentV3.RegisterHit();

                poolMember.ReturnToPool();
            }
        }
        else if (other.CompareTag("Wall") || other.CompareTag("Ground"))
        {
            poolMember.ReturnToPool();
        }
    }
}