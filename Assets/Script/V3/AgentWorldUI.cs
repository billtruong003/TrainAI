using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AgentWorldUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CombatUnitHealth health;
    [SerializeField] private TextMeshPro damageTextPrefab;
    [SerializeField] private Transform damageTextSpawnPoint;

    private Camera _cam;

    private void Start()
    {
        _cam = Camera.main;
        if (health == null) health = GetComponentInParent<CombatUnitHealth>();
        
        if (health)
        {
            health.OnDamageTaken += ShowDamage;
        }
    }

    private void Update()
    {
        // Billboard effect (Always face camera)
        if (_cam)
        {
            transform.LookAt(transform.position + _cam.transform.forward);
        }

        // Removed Slider Logic (Moved to CombatHUD)
    }

    private void ShowDamage(float amount)
    {
        if (damageTextPrefab && damageTextSpawnPoint)
        {
            var textObj = Instantiate(damageTextPrefab, damageTextSpawnPoint.position, Quaternion.identity, transform);
            textObj.text = Mathf.RoundToInt(amount).ToString();
            
            // Random offset
            textObj.transform.localPosition += new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.5f, 1.0f), 0);
            
            // Auto Destroy
            Destroy(textObj.gameObject, 1.5f);
            
            // Simple Animation (Move Up)
            StartCoroutine(AnimateDamageText(textObj.transform));
        }
    }
    
    private System.Collections.IEnumerator AnimateDamageText(Transform target)
    {
        float timer = 0f;
        Vector3 startPos = target.localPosition;
        while (timer < 1.5f)
        {
            if (target == null) yield break;
            timer += Time.deltaTime;
            target.localPosition = startPos + Vector3.up * (timer * 2f);
            yield return null;
        }
    }
}