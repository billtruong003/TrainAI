using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SmartProjectile : MonoBehaviour
{
    private Rigidbody rb;
    private float damage;
    private int ownerId;
    private float despawnTime;
    private DogfightAgent ownerRef;

    public int OwnerId => ownerId;

    public void Launch(DogfightAgent owner, Vector3 startPos, Quaternion direction, float speed, float dmg, float lifeTime)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        ownerRef = owner;
        ownerId = owner.GetInstanceID();
        damage = dmg;

        transform.position = startPos;
        transform.rotation = direction;
        gameObject.SetActive(true);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(transform.forward * speed, ForceMode.VelocityChange);

        despawnTime = Time.time + lifeTime;
    }

    private void Update()
    {
        if (Time.time >= despawnTime) gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;

        if (other.TryGetComponent<DogfightAgent>(out var agent))
        {
            if (agent.GetInstanceID() == ownerId) return;

            agent.TakeDamage(damage);
            if (ownerRef != null) ownerRef.RegisterHit();

            gameObject.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}