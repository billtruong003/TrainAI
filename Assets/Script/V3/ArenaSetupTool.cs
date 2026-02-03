using UnityEngine;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class ArenaSetupTool : MonoBehaviour
{
    [Header("Arena Configuration")]
    [SerializeField] private ArenaSettings arenaSettings;
    [SerializeField] private string teamAName = "Agent_TeamA";
    [SerializeField] private string teamBName = "Agent_TeamB";

    [Header("Projectile Config")]
    [Tooltip("Prefab đạn cho Team A (VD: Màu Xanh)")]
    [SerializeField] private GameObject projectilePrefabA;
    [Tooltip("Prefab đạn cho Team B (VD: Màu Đỏ)")]
    [SerializeField] private GameObject projectilePrefabB;

    [Header("Debug Visualization")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color teamAColor = Color.cyan;
    [SerializeField] private Color teamBColor = Color.red;
    [SerializeField] private bool showSpawnVolumes = true;
    [SerializeField] private bool showConnections = true;

#if UNITY_EDITOR
    [ContextMenu("Auto Configure Arena")]
    public void AutoConfigure()
    {
        if (arenaSettings == null)
        {
            Debug.LogError("[ArenaSetup] Arena Settings is missing!");
            return;
        }

        // 1. Setup Controller & Pool
        var controller = GetOrAddComponent<AerialArenaController>(gameObject);
        GetOrAddComponent<SmartPoolManager>(gameObject);

        SerializedObject soController = new SerializedObject(controller);
        soController.FindProperty("settings").objectReferenceValue = arenaSettings;

        // 2. Setup Agents
        var agentA = EnsureChildObject(teamAName);
        var agentB = EnsureChildObject(teamBName);

        // Assign to Controller
        soController.FindProperty("agentA").objectReferenceValue = agentA.GetComponent<CombatAgentUnit>();
        soController.FindProperty("agentB").objectReferenceValue = agentB.GetComponent<CombatAgentUnit>();
        soController.ApplyModifiedProperties();

        // Configure Each Agent with specific bullets
        ConfigureSingleAgent(agentA, 0, projectilePrefabA, agentB.transform);
        ConfigureSingleAgent(agentB, 1, projectilePrefabB, agentA.transform);

        Debug.Log($"<color=green>[ArenaSetup] Configuration Complete!</color> Obs Size: 23. Proj A: {(projectilePrefabA ? projectilePrefabA.name : "null")}, Proj B: {(projectilePrefabB ? projectilePrefabB.name : "null")}");
    }

    private void ConfigureSingleAgent(GameObject agentObj, int teamID, GameObject projectilePrefab, Transform target)
    {
        // 1. Layers & Tags
        agentObj.layer = LayerMask.NameToLayer("Agent");
        if (agentObj.CompareTag("Untagged")) agentObj.tag = "Agent";

        // 2. Physics
        var rb = GetOrAddComponent<Rigidbody>(agentObj);
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        // Mass handled by Agent code from Settings

        // 3. Components
        GetOrAddComponent<CombatUnitHealth>(agentObj);
        var unit = GetOrAddComponent<CombatAgentUnit>(agentObj);

        // --- AUTO SETUP AUDIO & VISUALS ---
        var audioSource = GetOrAddComponent<AudioSource>(agentObj);
        audioSource.spatialBlend = 1.0f; // 3D Sound
        audioSource.dopplerLevel = 0.5f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 2.0f;
        audioSource.maxDistance = 40.0f;
        audioSource.loop = true;
        audioSource.playOnAwake = false;

        var visuals = GetOrAddComponent<AgentAudioVisuals>(agentObj);
        SerializedObject visualsSO = new SerializedObject(visuals);
        visualsSO.FindProperty("settings").objectReferenceValue = arenaSettings;
        visualsSO.FindProperty("engineSource").objectReferenceValue = audioSource;
        visualsSO.ApplyModifiedProperties();
        // ----------------------------------

        // 4. Inject Dependencies via SerializedObject
        SerializedObject unitSO = new SerializedObject(unit);
        if (projectilePrefab != null)
        {
            var projScript = projectilePrefab.GetComponent<LaserProjectileV3>();
            if (projScript != null)
                unitSO.FindProperty("projectilePrefab").objectReferenceValue = projScript;
            else
                Debug.LogError($"[ArenaSetup] Prefab {projectilePrefab.name} does not have LaserProjectileV3 component!");
        }
        unitSO.FindProperty("settings").objectReferenceValue = arenaSettings;

        // Muzzle Setup
        Transform muzzle = agentObj.transform.Find("Muzzle");
        if (muzzle == null)
        {
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(agentObj.transform);
            muzzle.localPosition = Vector3.forward * 1.5f; // Push out a bit
        }
        unitSO.FindProperty("muzzlePoint").objectReferenceValue = muzzle;
        unitSO.ApplyModifiedProperties();

        // Set Runtime Target
        unit.SetTarget(target);

        // 5. ML-Agents Config
        var bp = GetOrAddComponent<BehaviorParameters>(agentObj);
        bp.BehaviorName = "AerialCombat";
        bp.TeamId = teamID;
        bp.BrainParameters.VectorObservationSize = 34; // Base(16) + Threat(6) + Stun(2) + Rays(10)
        bp.BrainParameters.ActionSpec = new ActionSpec(4, new int[] { 2 });
        bp.UseChildSensors = false;

        GetOrAddComponent<DecisionRequester>(agentObj).DecisionPeriod = 5;

        // Cleanup old sensors
        var sensor = agentObj.GetComponent<RayPerceptionSensorComponent3D>();
        if (sensor != null) DestroyImmediate(sensor, true);
    }

    private T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T comp = target.GetComponent<T>();
        if (comp == null) comp = target.AddComponent<T>();
        return comp;
    }

    private GameObject EnsureChildObject(string name)
    {
        Transform child = transform.Find(name);
        if (child == null)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform);
            child = obj.transform;
        }
        GetOrAddComponent<CombatAgentUnit>(child.gameObject);
        return child.gameObject;
    }
#endif

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Draw Spawn Volumes if Controller exists
        var controller = GetComponent<AerialArenaController>();
        if (controller != null)
        {
            var volumes = GetComponentsInChildren<SpawnVolume>();
            foreach (var vol in volumes)
            {
                if (!showSpawnVolumes) continue;
                Gizmos.color = vol.gameObject.name.Contains("A") ? new Color(0, 1, 1, 0.2f) : new Color(1, 0, 0, 0.2f);
                Gizmos.matrix = vol.transform.localToWorldMatrix;
                // Use built-in collider if possible, else rough cube
                var collider = vol.GetComponent<Collider>();
                if (collider is BoxCollider box)
                {
                    Gizmos.DrawCube(box.center, box.size);
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireCube(box.center, box.size);
                }
                else
                {
                    Gizmos.DrawCube(Vector3.zero, Vector3.one);
                }
                Gizmos.matrix = Matrix4x4.identity;

#if UNITY_EDITOR
                Handles.Label(vol.transform.position, vol.gameObject.name);
#endif
            }
        }

        // Draw Agent Relations
        DrawAgentGizmos(teamAName, teamAColor, teamBName);
        DrawAgentGizmos(teamBName, teamBColor, teamAName);
    }

    private void DrawAgentGizmos(string agentName, Color color, string targetName)
    {
        Transform agent = transform.Find(agentName);
        if (agent == null) return;

        Gizmos.color = color;
        // Draw Sphere
        Gizmos.DrawWireSphere(agent.position, 1.0f);

        // Draw Forward Arrow
        Vector3 fwdEnd = agent.position + agent.forward * 3.0f;
        Gizmos.DrawLine(agent.position, fwdEnd);
        // Draw simple arrow head
        Vector3 right = agent.right * 0.5f;
        Gizmos.DrawLine(fwdEnd, fwdEnd - agent.forward * 0.5f + right);
        Gizmos.DrawLine(fwdEnd, fwdEnd - agent.forward * 0.5f - right);

        // Draw Muzzle
        Transform muzzle = agent.Find("Muzzle");
        if (muzzle)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(muzzle.position, 0.2f);
        }

        // Draw Line to Target
        if (showConnections)
        {
            Transform target = transform.Find(targetName);
            if (target != null)
            {
                Gizmos.color = new Color(color.r, color.g, color.b, 0.5f);
                Gizmos.DrawLine(agent.position, target.position);

                // Show Distance
                float dist = Vector3.Distance(agent.position, target.position);
                Vector3 mid = (agent.position + target.position) * 0.5f;
#if UNITY_EDITOR
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.white;
                style.alignment = TextAnchor.MiddleCenter;
                Handles.Label(mid, $"{dist:F1}m", style);
#endif
            }
        }

        // Draw Detection Radius (30m as per CombatAgentUnit)
        Gizmos.color = new Color(color.r, color.g, color.b, 0.1f);
        Gizmos.DrawWireSphere(agent.position, 30.0f);
    }
}