using System.Collections;
using UnityEngine;
using Unity.MLAgents;

[RequireComponent(typeof(SmartPoolManager))]
public class AerialArenaController : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private ArenaSettings settings;
    [SerializeField] private SpawnVolume teamASpawn;
    [SerializeField] private SpawnVolume teamBSpawn;

    [Header("Agents")]
    [SerializeField] private CombatAgentUnit agentA;
    [SerializeField] private CombatAgentUnit agentB;

    private SmartPoolManager _poolManager;
    private MatchState _currentState;

    public SmartPoolManager Pool => _poolManager;

    private void Awake()
    {
        _poolManager = GetComponent<SmartPoolManager>();
        InitializeAgents();
    }

    private void Start()
    {
        StartCoroutine(GameLoop());
    }

    private void InitializeAgents()
    {
        agentA.InitializeAgent(this, 0);
        agentB.InitializeAgent(this, 1);

        agentA.SetTarget(agentB.transform);
        agentB.SetTarget(agentA.transform);
    }

    private IEnumerator GameLoop()
    {
        while (true)
        {
            _currentState = MatchState.Initializing;
            _poolManager.DespawnAll();

            SpawnAgent(agentA, teamASpawn);
            SpawnAgent(agentB, teamBSpawn);

            agentA.gameObject.SetActive(true);
            agentB.gameObject.SetActive(true);

            if (settings.PreRoundDelay > 0)
                yield return new WaitForSeconds(settings.PreRoundDelay);

            _currentState = MatchState.Combat;
            agentA.BeginRound();
            agentB.BeginRound();

            float matchTime = 0f;
            while (matchTime < settings.MaxRoundTime && IsMatchActive())
            {
                matchTime += Time.deltaTime;
                yield return null;
            }

            _currentState = MatchState.Resolution;
            agentA.EndRound();
            agentB.EndRound();

            if (IsMatchActive())
            {
                agentA.AddReward(0f);
                agentB.AddReward(0f);
            }

            agentA.EndEpisode();
            agentB.EndEpisode();

            if (settings.PostRoundDelay > 0)
                yield return new WaitForSeconds(settings.PostRoundDelay);
            else
                yield return null; // Wait 1 frame to reset physics properly
        }
    }

    private void SpawnAgent(CombatAgentUnit agent, SpawnVolume volume)
    {
        Vector3 pos = volume.GetSafeSpawnPosition(
            settings.SpawnCheckRadius,
            settings.ObstacleLayer,
            settings.MaxSpawnAttempts
        );

        Quaternion rot = Quaternion.LookRotation(Vector3.zero - pos);
        agent.PrepareForRound(pos, rot);
    }

    private bool IsMatchActive()
    {
        return agentA.IsAlive && agentB.IsAlive;
    }

    public void NotifyAgentDied(CombatAgentUnit victim)
    {
        // Neu dang trong tran ma chet -> Ket thuc ngay lap tuc
        if (_currentState != MatchState.Combat) return;

        CombatAgentUnit winner = (victim == agentA) ? agentB : agentA;

        // 1. Thuong cho nguoi chien thang
        winner.AddReward(settings.WinReward);

        // 2. QUAN TRONG: Goi EndEpisode cho CA HAI de chot so Neural Network
        // Neu khong goi, AI se khong biet la tran dau da ket thuc
        victim.EndEpisode();
        winner.EndEpisode();

        // 3. Force End Round immediately & Reset Physics
        StopAllCoroutines();
        StartCoroutine(GameLoop());
    }
}
public enum MatchState
{
    Initializing,
    Warmup,
    Combat,
    Resolution,
    Cooldown
}