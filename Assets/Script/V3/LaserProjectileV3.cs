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
    private int _ownerId;
    private int _ownerTeamId;
    private ArenaSettings _settings;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _poolMember = GetComponent<PoolMember>();
    }

    public void Initialize(CombatAgentUnit owner, ArenaSettings settings)
    {
        _owner = owner;
        _ownerId = owner.GetInstanceID();
        _ownerTeamId = owner.TeamId;
        _settings = settings;
    }

    public void OnSpawn()
    {
        _timer = 0f;
        _rb.useGravity = false;
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

    private void OnTriggerEnter(Collider other)
    {
        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 normal = (transform.position - hitPoint).normalized;
        HandleHit(other.gameObject, hitPoint, normal);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ContactPoint contact = collision.contacts[0];
        HandleHit(collision.gameObject, contact.point, contact.normal);
    }

    private void HandleHit(GameObject hitObj, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (_owner == null) return;
        if (hitObj.layer == 2) return;
        if (hitObj.GetInstanceID() == _ownerId) return;

        // Defensive: Training Mode Check
        bool isTraining = _settings != null && _settings.IsTrainingMode;

        // --- Bullet vs Bullet Interaction ---
        // If we hit another LaserProjectile, explode immediately
        if (hitObj.GetComponent<LaserProjectileV3>())
        {
            if (!isTraining)
            {
                // Play a small pop sound
                if (_settings && _settings.ImpactWallClip)
                    SoundManager.Instance.PlaySound(_settings.ImpactWallClip, transform.position, 0.4f, 0.2f);

                // Show Impact VFX
                var myVisuals = (_ownerTeamId == 0) ? _settings.TeamA_Visuals : _settings.TeamB_Visuals;
                if (myVisuals.ImpactVFX)
                    SpawnVFX(myVisuals.ImpactVFX, transform.position, Quaternion.identity);
            }
            _poolMember.ReturnToPool();
            return;
        }

        var visuals = (_ownerTeamId == 0 && _settings != null) ? _settings.TeamA_Visuals : (_settings != null ? _settings.TeamB_Visuals : default);

        if (hitObj.TryGetComponent<CombatAgentUnit>(out var hitAgent))
        {
            if (hitAgent.TeamId == _ownerTeamId) return;

            var health = hitAgent.GetComponent<CombatUnitHealth>();
            if (health != null)
            {
                health.TakeDamage(damage, 15f);

                if (hitAgent.TryGetComponent<Rigidbody>(out var targetRb))
                {
                    targetRb.AddForce(transform.forward * 50f, ForceMode.Impulse);
                    targetRb.AddRelativeTorque(Random.insideUnitSphere * 50f, ForceMode.Impulse);
                }

                // SFX & VFX (Only in Play Mode)
                if (!isTraining)
                {
                    if (_settings && _settings.ImpactFleshClip)
                        SoundManager.Instance.PlaySound(_settings.ImpactFleshClip, transform.position, 0.8f, 0.1f);

                    if (visuals.ImpactVFX)
                        SpawnVFX(visuals.ImpactVFX, hitPoint + hitNormal * 0.2f, Quaternion.LookRotation(hitNormal));
                }

                _owner.RegisterHit();
                _poolMember.ReturnToPool();
            }
            return;
        }

        // Hit Wall/Obstacle
        if (!isTraining)
        {
            if (_settings && _settings.ImpactWallClip)
                SoundManager.Instance.PlaySound(_settings.ImpactWallClip, transform.position, 0.6f, 0.1f);

            if (visuals.ImpactVFX)
                SpawnVFX(visuals.ImpactVFX, hitPoint + hitNormal * 0.2f, Quaternion.LookRotation(hitNormal));
        }

        _poolMember.ReturnToPool();
    }

    private void SpawnVFX(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        // Use Owner's Arena Pool to ensure we use the main pool system
        if (_owner != null && _owner.Arena != null && _owner.Arena.Pool != null)
        {
            var obj = _owner.Arena.Pool.Spawn(prefab, pos, rot);
            if (obj != null && obj.GetComponent<VFXAutoReturn>() == null)
            {
                obj.AddComponent<VFXAutoReturn>();
            }
        }
    }
}