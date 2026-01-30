using UnityEngine;
using System.Collections.Generic;
using Unity.MLAgents;

public enum ArenaMode
{
    SoloWaypoints = 0, // Chỉ 1 agent, bay theo điểm
    SoloTargetPractice = 1, // 1 agent, bắn bia tĩnh/động
    Dogfight = 2 // 2 agents, bắn nhau
}

public class DualCombatArena : MonoBehaviour
{
    [SerializeField] private AerialCombatAgent agentA; // Team 0
    [SerializeField] private AerialCombatAgent agentB; // Team 1
    
    [Header("Waypoint System")]
    [SerializeField] private List<Transform> waypoints; // Kéo các EmptyGameObject vào đây
    [SerializeField] private GameObject waypointVisualPrefab; // Một quả cầu xanh đỏ để agent nhìn thấy
    private int currentWaypointIndex;
    private GameObject currentWaypointObj;

    [Header("Arena Settings")]
    [SerializeField] private Vector3 arenaSize = new Vector3(40, 20, 40);
    [SerializeField] private float centerBuffer = 2f;

    // Biến môi trường đọc từ Config
    private float currentLessonPhase;

    private void Start()
    {
        agentA.SetArena(this);
        agentB.SetArena(this);
        
        // Đăng ký nhận thông số từ Curriculum
        Academy.Instance.OnEnvironmentReset += ApplyLessonPhase;
        ApplyLessonPhase(); // Gọi lần đầu
    }

    private void ApplyLessonPhase()
    {
        // Đọc giá trị từ file yaml (mặc định là 0 nếu không tìm thấy)
        currentLessonPhase = Academy.Instance.EnvironmentParameters.GetWithDefault("lesson_phase", 0.0f);
        ResetMatch();
    }

    public void ResetMatch()
    {
        int mode = Mathf.FloorToInt(currentLessonPhase);

        if (mode == 0 || mode == 1) // Phase 0 & 1: Solo Training
        {
            agentB.gameObject.SetActive(false); // Tắt con B đi
            agentA.gameObject.SetActive(true);
            agentA.SetBoundaryIgnore(true); // Cho phép bay toàn map

            SpawnAgent(agentA, true, true); // Spawn ngẫu nhiên toàn map

            if (mode == 0) SetupWaypoints();
            // if (mode == 1) SetupTargetPractice(); // Bạn có thể tự triển khai thêm
        }
        else // Phase 2+: Dogfight
        {
            if (currentWaypointObj != null) currentWaypointObj.SetActive(false);
            
            agentA.gameObject.SetActive(true);
            agentB.gameObject.SetActive(true);
            agentA.SetBoundaryIgnore(false); // Chia sân
            agentB.SetBoundaryIgnore(false);

            SpawnAgent(agentA, true, false);
            SpawnAgent(agentB, false, false);
            
            // Set mục tiêu là lẫn nhau
            agentA.SetTarget(agentB.transform);
            agentB.SetTarget(agentA.transform);
        }
    }

    private void SetupWaypoints()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        // Chọn ngẫu nhiên 1 điểm bắt đầu
        currentWaypointIndex = Random.Range(0, waypoints.Count);
        MoveWaypointVisual(waypoints[currentWaypointIndex].position);
        agentA.SetTarget(currentWaypointObj.transform);
    }

    private void MoveWaypointVisual(Vector3 pos)
    {
        if (currentWaypointObj == null && waypointVisualPrefab != null)
        {
            currentWaypointObj = Instantiate(waypointVisualPrefab, transform);
        }
        
        if (currentWaypointObj != null)
        {
            currentWaypointObj.SetActive(true);
            currentWaypointObj.transform.position = pos;
        }
    }

    // Được gọi khi Agent A chạm vào Waypoint (Bạn cần gắn Collider Trigger cho WaypointVisual)
    public void OnWaypointReached()
    {
        agentA.TouchedWaypoint(); // Thưởng cho agent
        
        if (waypoints == null || waypoints.Count == 0) return;

        // Chuyển sang điểm tiếp theo
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
        
        // Random nhẹ vị trí điểm tiếp theo để không bị học vẹt
        Vector3 basePos = waypoints[currentWaypointIndex].position;
        Vector3 randomOffset = Random.insideUnitSphere * 2f;
        
        MoveWaypointVisual(basePos + randomOffset);
    }

    public void OnAgentDied(AerialCombatAgent agent)
    {
        // Nếu là Dogfight, 1 con chết thì reset cả trận
        if (currentLessonPhase >= 2.0f)
        {
            agentA.EndEpisode();
            agentB.EndEpisode();
            ResetMatch();
        }
        else
        {
            // Nếu Solo, chỉ reset con đó
            agent.EndEpisode();
            SpawnAgent(agent, true, true); 
        }
    }

    private void SpawnAgent(AerialCombatAgent agent, bool isTeamA, bool fullMap)
    {
        Vector3 spawnPos;
        Quaternion rot;

        if (fullMap)
        {
            spawnPos = transform.position + new Vector3(
                Random.Range(-arenaSize.x / 2, arenaSize.x / 2),
                Random.Range(2f, arenaSize.y),
                Random.Range(-arenaSize.z / 2, arenaSize.z / 2)
            );
            rot = Quaternion.Euler(0, Random.Range(0, 360), 0);
        }
        else
        {
            float zMin = isTeamA ? -arenaSize.z / 2 : centerBuffer;
            float zMax = isTeamA ? -centerBuffer : arenaSize.z / 2;
            spawnPos = transform.position + new Vector3(
                Random.Range(-arenaSize.x / 2, arenaSize.x / 2),
                Random.Range(2f, arenaSize.y),
                Random.Range(zMin, zMax)
            );
            rot = Quaternion.Euler(0, isTeamA ? 0 : 180, 0);
        }

        agent.transform.position = spawnPos;
        agent.transform.rotation = rot;
        agent.OnEpisodeBegin(); // Reset velocity...
    }

    private void FixedUpdate()
    {
        if (agentA.gameObject.activeSelf) CheckAgentBounds(agentA);
        if (agentB.gameObject.activeSelf) CheckAgentBounds(agentB);
        
        // Kiểm tra khoảng cách Waypoint (Thay vì dùng Trigger collider có thể check distance cho nhanh)
        if (currentLessonPhase < 2.0f && currentWaypointObj != null && agentA.gameObject.activeSelf)
        {
            float dist = Vector3.Distance(agentA.transform.position, currentWaypointObj.transform.position);
            if (dist < 3.0f) // Bán kính 3m coi như chạm
            {
                OnWaypointReached();
            }
        }
    }

    private void CheckAgentBounds(AerialCombatAgent agent)
    {
        if (agent.transform.localPosition.y < 0 || 
            Mathf.Abs(agent.transform.localPosition.x) > arenaSize.x / 2 + 5 ||
            Mathf.Abs(agent.transform.localPosition.z) > arenaSize.z / 2 + 5)
        {
            agent.AddReward(-1.0f);
            agent.EndEpisode();
            if (currentLessonPhase >= 2.0f) ResetMatch();
            else SpawnAgent(agent, true, true);
        }
    }
}