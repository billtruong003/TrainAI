using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatHUD : MonoBehaviour
{
    [Header("Agent A (Left)")]
    [SerializeField] private Slider hpSliderA;
    [SerializeField] private Slider stunSliderA;
    [SerializeField] private TextMeshPro scoreTextA;

    [Header("Agent B (Right)")]
    [SerializeField] private Slider hpSliderB;
    [SerializeField] private Slider stunSliderB;
    [SerializeField] private TextMeshPro scoreTextB;

    [Header("General")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private AerialArenaController arenaController;

    private void Start()
    {
        if (arenaController == null)
            arenaController = FindFirstObjectByType<AerialArenaController>();
    }

    private void Update()
    {
        if (arenaController == null) return;

        UpdateAgentUI(arenaController.AgentA, hpSliderA, stunSliderA, scoreTextA, arenaController.ScoreA);
        UpdateAgentUI(arenaController.AgentB, hpSliderB, stunSliderB, scoreTextB, arenaController.ScoreB);

        // Update Timer (Need to expose MatchTime from Controller or approximate)
        // Since matchTime is local in Coroutine, we might not get it easily without modification.
        // For now, let's just show Score.
    }

    private void UpdateAgentUI(CombatAgentUnit agent, Slider hpSlider, Slider stunSlider, TextMeshPro scoreText, int score)
    {
        if (agent != null && agent.Health != null)
        {
            if (hpSlider) 
                hpSlider.value = Mathf.Lerp(hpSlider.value, agent.Health.HealthPercentage, Time.deltaTime * 10f);
            
            if (stunSlider) 
                stunSlider.value = Mathf.Lerp(stunSlider.value, agent.Health.StunPercentage, Time.deltaTime * 10f);
        }

        if (scoreText)
            scoreText.text = score.ToString();
    }
}