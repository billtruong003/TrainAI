using UnityEngine;

[RequireComponent(typeof(CombatAgentUnit), typeof(AudioSource))]
public class AgentAudioVisuals : MonoBehaviour
{
    [SerializeField] private ArenaSettings settings;
    [SerializeField] private AudioSource engineSource;
    [SerializeField] private ParticleSystem smokeVFX;

    private CombatAgentUnit _agent;
    private CombatUnitHealth _health;
    private Rigidbody _rb;
    private ArenaSettings.TeamVisualProfile _visuals;

    private void Awake()
    {
        _agent = GetComponent<CombatAgentUnit>();
        _health = GetComponent<CombatUnitHealth>();
        _rb = GetComponent<Rigidbody>();

        if (engineSource == null) engineSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        // --- DEFENSIVE: TRAINING MODE CHECK ---
        if (settings != null && settings.IsTrainingMode)
        {
            enabled = false; // Stop Update loop
            return; // Skip SFX/VFX setup
        }

        // Pick Profile
        if (settings != null && _agent != null)
        {
            _visuals = (_agent.TeamId == 0) ? settings.TeamA_Visuals : settings.TeamB_Visuals;
        }

        // Engine
        if (settings != null && settings.EngineLoopClip != null)
        {
            engineSource.clip = settings.EngineLoopClip;
            engineSource.loop = true;
            engineSource.spatialBlend = 1.0f;
            engineSource.Play();
        }

        // Smoke
        if (_visuals.DamageSmoke != null && smokeVFX == null)
        {
            var go = Instantiate(_visuals.DamageSmoke, transform);
            go.transform.localPosition = Vector3.zero;
            smokeVFX = go.GetComponent<ParticleSystem>();
            if (smokeVFX) smokeVFX.Stop();
        }

        if (_health != null)
        {
            _health.OnDeath += OnDeath;
        }

        if (_agent != null)
        {
            _agent.OnFired += PlayShoot;
        }
    }

    private void OnDestroy()
    {
        if (_health != null) _health.OnDeath -= OnDeath;
        if (_agent != null) _agent.OnFired -= PlayShoot;
    }

    private void Update()
    {
        if (_rb != null)
        {
            float speed = _rb.linearVelocity.magnitude;
            float targetPitch = 0.8f + (speed / 30f) * 0.4f;
            engineSource.pitch = Mathf.Lerp(engineSource.pitch, targetPitch, Time.deltaTime * 5f);
        }

        if (_health != null && smokeVFX != null)
        {
            bool lowHP = _health.HealthPercentage < 0.4f;
            if (lowHP && !smokeVFX.isPlaying) smokeVFX.Play();
            else if (!lowHP && smokeVFX.isPlaying) smokeVFX.Stop();
        }
    }

    private void PlayShoot()
    {
        if (settings && settings.ShootClip)
            SoundManager.Instance.PlaySound(settings.ShootClip, transform.position, 0.7f, 0.1f);

        if (_visuals.MuzzleFlash)
        {
            SpawnVFX(_visuals.MuzzleFlash, _agent.transform.position + _agent.transform.forward * 0.5f, _agent.transform.rotation);
        }
    }

    private void OnDeath()
    {
        if (settings && settings.ExplosionClip)
            SoundManager.Instance.PlaySound(settings.ExplosionClip, transform.position, 1.0f);

        if (_visuals.ExplosionVFX)
            SpawnVFX(_visuals.ExplosionVFX, transform.position, Quaternion.identity);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.impulse.magnitude > 2f)
        {
            if (settings == null) return;

            if (settings.CollisionClip)
                SoundManager.Instance.PlaySound(settings.CollisionClip, collision.contacts[0].point, 0.8f, 0.1f);

            // Use Team Colored sparks for collision
            if (_visuals.ImpactVFX)
            {
                ContactPoint contact = collision.contacts[0];
                SpawnVFX(_visuals.ImpactVFX, contact.point, Quaternion.LookRotation(contact.normal));
            }
        }
    }

    private void SpawnVFX(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (!_agent || !_agent.Arena || !_agent.Arena.Pool) return;

        GameObject obj = _agent.Arena.Pool.Spawn(prefab, pos, rot);
        if (obj != null && !obj.GetComponent<VFXAutoReturn>())
        {
            obj.AddComponent<VFXAutoReturn>();
        }
    }
}