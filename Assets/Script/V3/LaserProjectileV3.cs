using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(PoolMember))]
public class LaserProjectileV3 : MonoBehaviour, IPoolable
{
    [SerializeField] private float speed = 150f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float maxLifetime = 3f;

    private Rigidbody _rb;
    private PoolMember _poolMember;
    private CombatAgentUnit _owner;
    private float _timer;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _poolMember = GetComponent<PoolMember>();
    }

    public void Initialize(CombatAgentUnit owner)
    {
        _owner = owner;
    }

    public void OnSpawn()
    {
        _timer = 0f;
        _rb.useGravity = false; // Dam bao khong roi tu do
        _rb.linearVelocity = transform.forward * speed;
    }

    public void OnDespawn()
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _owner = null;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= maxLifetime)
        {
            _poolMember.ReturnToPool();
        }
    }

    // Xu ly va cham Trigger (xuyen qua hoac cham hitbox)
    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other.gameObject);
    }

    // Xu ly va cham Vat ly (nay hoac dung lai)
    private void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.gameObject);
    }

    private void HandleHit(GameObject hitObj)
    {
        if (_owner == null) return;
        if (hitObj.CompareTag("Ignore Raycast")) return; // Bo qua sensor vung

        // 1. Ban trung Agent dich
        if (hitObj.TryGetComponent<CombatAgentUnit>(out var hitAgent))
        {
            if (hitAgent.TeamId == _owner.TeamId) return; // Khong ban dong doi

            var health = hitAgent.GetComponent<CombatUnitHealth>();
            if (health != null)
            {
                Debug.Log($"[HIT] Owner {_owner.name} hit {hitAgent.name} for {damage} dmg!");
                health.TakeDamage(damage);
                _owner.RegisterHit();
                _poolMember.ReturnToPool();
            }
            return;
        }

        // 2. Ban trung bat cu thu gi khac (Tuong, Dat, Chuong ngai vat)
        // Tru Owner (luc moi ban ra)
        if (hitObj.GetInstanceID() == _owner.gameObject.GetInstanceID()) return;

        _poolMember.ReturnToPool();
    }
}