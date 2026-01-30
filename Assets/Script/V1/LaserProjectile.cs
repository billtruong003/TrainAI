using UnityEngine;

public class LaserProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 100f;
    [SerializeField] private float lifeTime = 2f;
    [SerializeField] private float damage = 10f;

    private int ownerId;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        rb.linearVelocity = transform.forward * speed;
        Invoke(nameof(Deactivate), lifeTime);
    }

    private void OnDisable()
    {
        CancelInvoke();
        rb.linearVelocity = Vector3.zero;
    }

    public void Initialize(int id)
    {
        ownerId = id;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Agent"))
        {
            var agentHealth = other.GetComponent<CombatUnitHealth>();
            var agent = other.GetComponent<AerialCombatAgent>();

            if (agent != null && agent.GetInstanceID() != ownerId && agentHealth != null)
            {
                agentHealth.TakeDamage(damage);
                Deactivate();
            }
        }
        else if (other.CompareTag("Wall") || other.CompareTag("Ground"))
        {
            Deactivate();
        }
    }

    private void Deactivate()
    {
        gameObject.SetActive(false);
    }
}