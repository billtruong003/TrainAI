using UnityEngine;
using System.Collections;

public class DogfightArena : EnvironmentHub
{
    [SerializeField] private DogfightAgent agentA;
    [SerializeField] private DogfightAgent agentB;
    [SerializeField] private Transform spawnA;
    [SerializeField] private Transform spawnB;
    [SerializeField] private float roundLimit = 60f;

    private Unity.MLAgents.EnvironmentParameters envParams;
    private float currentRoundTimer;

    protected override void Awake()
    {
        base.Awake();
        envParams = Unity.MLAgents.Academy.Instance.EnvironmentParameters;
    }

    protected override void ResetSceneElements()
    {
        // Force Activate Agents specifically for Dogfight to ensure they are both present
        if (agentA != null) agentA.gameObject.SetActive(true);
        if (agentB != null) agentB.gameObject.SetActive(true);

        // Curriculum: Adjust spawn distance
        float distance = envParams.GetWithDefault("spawn_distance", 40.0f);
        float halfDist = distance * 0.5f;

        spawnA.localPosition = new Vector3(0, 0, -halfDist);
        spawnB.localPosition = new Vector3(0, 0, halfDist);

        spawnA.localRotation = Quaternion.Euler(0, 0, 0);
        spawnB.localRotation = Quaternion.Euler(0, 180, 0);

        // Setup Match Data
        agentA.SetMatchData(this, 0, agentB.transform);
        agentB.SetMatchData(this, 1, agentA.transform);

        currentRoundTimer = 0f;
    }

    private void Update()
    {
        if (currentState == EnvState.Active)
        {
            currentRoundTimer += Time.deltaTime;
            if (currentRoundTimer >= roundLimit)
            {
                // Time Over -> Draw
                agentA.RegisterDraw();
                agentB.RegisterDraw();
                NotifyAgentDone(null, false);
            }
        }
    }

    public void ReportDeath(DogfightAgent victim)
    {
        if (currentState != EnvState.Active) return;

        DogfightAgent winner = victim == agentA ? agentB : agentA;
        winner.RegisterWin();
        // Victim already handled loss in its TakeDamage

        NotifyAgentDone(victim, false);
    }

    public Transform GetSpawnPoint(int teamId)
    {
        return teamId == 0 ? spawnA : spawnB;
    }
}