using UnityEngine;

[RequireComponent(typeof(PoolMember))]
public class VFXAutoReturn : MonoBehaviour, IPoolable
{
    private ParticleSystem _ps;
    private PoolMember _poolMember;
    private float _timer;

    private void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
        _poolMember = GetComponent<PoolMember>();
    }

    public void OnSpawn()
    {
        _timer = 0f;
        if (_ps) _ps.Play();
    }

    public void OnDespawn() { }

    private void Update()
    {
        if (_poolMember == null) return;

        _timer += Time.deltaTime;

        // Return if particle finished or timeout (2s)
        if ((_ps != null && !_ps.IsAlive(true)) || _timer > 2.0f)
        {
            _poolMember.ReturnToPool();
        }
    }
}