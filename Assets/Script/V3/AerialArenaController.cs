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

    public CombatAgentUnit AgentA => agentA;
    public CombatAgentUnit AgentB => agentB;

    public int ScoreA { get; private set; }
    public int ScoreB { get; private set; }

    public SmartPoolManager Pool => _poolManager;

    private void Awake()
    {
        _poolManager = GetComponent<SmartPoolManager>();
        InitializeAgents();
    }

    private void Start()
    {
        // StartCoroutine(GameLoop()); // Old Loop
        StartCoroutine(SessionLoop());
    }

    private void InitializeAgents()
    {
        agentA.InitializeAgent(this, 0);
        agentB.InitializeAgent(this, 1);

        agentA.SetTarget(agentB.transform);
        agentB.SetTarget(agentA.transform);
    }

    private IEnumerator SessionLoop()
    {
        while (true)
        {
            _currentState = MatchState.Initializing;
            _poolManager.DespawnAll();

            agentA.gameObject.SetActive(false);
            agentB.gameObject.SetActive(false);

            SpawnAgent(agentA, teamASpawn, teamBSpawn.transform.position);
            SpawnAgent(agentB, teamBSpawn, teamASpawn.transform.position);

            agentA.gameObject.SetActive(true);
            agentB.gameObject.SetActive(true);

            // Enforce delay in Play Mode, Disable in Training
            float preDelay = settings.PreRoundDelay;
            if (settings.IsTrainingMode)
                preDelay = 0f;
            else if (preDelay < 2f)
                preDelay = 3f;

            if (preDelay > 0)
                yield return new WaitForSeconds(preDelay);

            _currentState = MatchState.Combat;
            agentA.BeginRound();
            agentB.BeginRound();

            float matchTime = 0f;
            while (_currentState == MatchState.Combat)
            {
                // If Training: Check Time Limit. If Play: Infinite Time.
                if (settings.IsTrainingMode && matchTime >= settings.MaxRoundTime) break;

                matchTime += Time.deltaTime;
                yield return null;
            }

            if (_currentState == MatchState.Combat)
            {
                agentA.AddReward(-0.1f);
                agentB.AddReward(-0.1f);
                agentA.EndEpisode();
                agentB.EndEpisode();
            }

            _currentState = MatchState.Resolution;
            agentA.EndRound();
            agentB.EndRound();

            float postDelay = settings.PostRoundDelay;
            if (settings.IsTrainingMode)
                postDelay = 0f;
            else if (postDelay < 2f)
                postDelay = 2f;

            if (postDelay > 0)
                yield return new WaitForSeconds(postDelay);
            else
                yield return null;
        }
    }

    private void SpawnAgent(CombatAgentUnit agent, SpawnVolume volume, Vector3 lookAtTarget)
    {
        Vector3 pos = volume.GetSafeSpawnPosition(
            settings.SpawnCheckRadius,
            settings.ObstacleLayer,
            settings.MaxSpawnAttempts
        );

        Vector3 dir = lookAtTarget - pos;
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;

        Quaternion rot = Quaternion.LookRotation(dir);
        agent.PrepareForRound(pos, rot);
    }

    private bool IsMatchActive()
    {
        return agentA.IsAlive && agentB.IsAlive;
    }

    public void NotifyAgentDied(CombatAgentUnit victim)
    {
        if (_currentState != MatchState.Combat) return;

        CombatAgentUnit winner = (victim == agentA) ? agentB : agentA;

        winner.AddReward(settings.WinReward);

        // Track Score (Only useful in Play Mode)
        if (winner == agentA) ScoreA++; else ScoreB++;
        if (!settings.IsTrainingMode) Debug.Log($"<color=yellow>SCORE UPDATE: Team A ({ScoreA}) - Team B ({ScoreB})</color>");

        victim.EndEpisode();
        winner.EndEpisode();

        _currentState = MatchState.Resolution;
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