using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SmartProjectile : MonoBehaviour, IPoolable
{
    private Rigidbody rb;
    private float damage;
    private int ownerId;
    private float despawnTime;
    private DogfightAgent ownerRef;

    public int OwnerId => ownerId;

    // IPoolable Interface
    public void OnSpawn()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public void OnDespawn()
    {
        ownerRef = null;
        ownerId = -1;
    }

    public void Launch(DogfightAgent owner, Vector3 startPos, Quaternion direction, float speed, float dmg, float lifeTime)
    {
        ownerRef = owner;
        ownerId = owner.GetInstanceID();
        damage = dmg;

        transform.position = startPos;
        transform.rotation = direction;

        rb.AddForce(transform.forward * speed, ForceMode.VelocityChange);
        despawnTime = Time.time + lifeTime;
    }

    private void Update()
    {
        if (Time.time >= despawnTime) ReturnToPool();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;

        if (other.TryGetComponent<DogfightAgent>(out var agent))
        {
            if (agent.GetInstanceID() == ownerId) return;

            agent.TakeDamage(damage);
            if (ownerRef != null) ownerRef.RegisterHit();

            ReturnToPool();
        }
        else
        {
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        if (TryGetComponent<PoolMember>(out var member))
            member.ReturnToPool();
        else
            gameObject.SetActive(false);
    }
}