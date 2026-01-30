using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;

public class AerialCombatEnvironment : EnvironmentHub
{
    [Header("Spawn Config")]
    [SerializeField] private BoxCollider spawnArea;
    [SerializeField] private GameObject waypointPrefab;
    [SerializeField] private int maxWaypoints = 5;

    // Curriculum State
    private float currentLessonPhase;
    private GameObject currentWaypoint;

    public float CurrentPhase => currentLessonPhase;

    protected override void Awake()
    {
        base.Awake();
        if (spawnArea == null) spawnArea = GetComponent<BoxCollider>();
    }

    protected override void ResetSceneElements()
    {
        // Doc Curriculum
        currentLessonPhase = Academy.Instance.EnvironmentParameters.GetWithDefault("lesson_phase", 0.0f);
        int mode = Mathf.FloorToInt(currentLessonPhase);

        // Note: Pool.DespawnAll() is called by Hub before this, so currentWaypoint is already gone logic-wise
        currentWaypoint = null;

        // Setup Agents Positions
        // Phase 0, 1, 2: Solo training (voi Waypoint hoac Target)
        // Phase 3: Dogfight
        bool isDogfight = mode >= 3;

        // Reset tung agent
        for (int i = 0; i < registeredAgents.Count; i++)
        {
            var agent = registeredAgents[i];

            // Phase 0: Chi active agent 0
            if (!isDogfight && i > 0)
            {
                agent.gameObject.SetActive(false);
                continue;
            }

            agent.gameObject.SetActive(true);
            SpawnAgentRandomly(agent.transform, i);
        }

        // Setup Waypoint for Phase 0
        if (!isDogfight)
        {
            SpawnNextWaypoint();
        }
    }

    public void SpawnNextWaypoint()
    {
        if (currentWaypoint != null)
        {
            var member = currentWaypoint.GetComponent<PoolMember>();
            if (member) member.ReturnToPool();
        }

        Vector3 pos = GetRandomPointInBounds();
        currentWaypoint = Pool.Spawn(waypointPrefab, pos, Quaternion.identity);

        // Bao cho Agent biet target moi
        foreach (var agent in registeredAgents)
        {
            if (agent.gameObject.activeSelf && agent is AerialCombatAgentV2 aerialAgent)
            {
                aerialAgent.SetTarget(currentWaypoint.transform);
            }
        }
    }

    private void SpawnAgentRandomly(Transform t, int teamIndex)
    {
        // Chia san neu la Dogfight
        Bounds b = spawnArea.bounds;
        float zMin = b.min.z, zMax = b.max.z;

        if (registeredAgents.Count > 1)
        {
            float mid = b.center.z;
            if (teamIndex == 0) zMax = mid - 2f;
            else zMin = mid + 2f;
        }

        Vector3 pos = new Vector3(
            Random.Range(b.min.x, b.max.x),
            Random.Range(b.min.y, b.max.y),
            Random.Range(zMin, zMax)
        );

        t.position = pos;
        t.rotation = Quaternion.Euler(0, teamIndex == 0 ? 0 : 180, 0);

        // Reset physics is handled in Agent.OnEnvironmentReady
    }

    private Vector3 GetRandomPointInBounds()
    {
        Bounds b = spawnArea.bounds;
        return new Vector3(
            Random.Range(b.min.x, b.max.x),
            Random.Range(b.min.y, b.max.y),
            Random.Range(b.min.z, b.max.z)
        );
    }
}