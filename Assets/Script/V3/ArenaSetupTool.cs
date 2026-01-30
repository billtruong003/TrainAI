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
    [SerializeField] private ArenaSettings arenaSettings;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private string teamAName = "Agent_TeamA";
    [SerializeField] private string teamBName = "Agent_TeamB";

#if UNITY_EDITOR
    [ContextMenu("Auto Configure Arena")]
    public void AutoConfigure()
    {
        if (arenaSettings == null || projectilePrefab == null) return;

        var controller = GetOrAddComponent<AerialArenaController>(gameObject);
        GetOrAddComponent<SmartPoolManager>(gameObject);

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("settings").objectReferenceValue = arenaSettings;
        
        var agentA = EnsureChildObject(teamAName, typeof(CombatAgentUnit));
        var agentB = EnsureChildObject(teamBName, typeof(CombatAgentUnit));
        
        so.FindProperty("agentA").objectReferenceValue = agentA.GetComponent<CombatAgentUnit>();
        so.FindProperty("agentB").objectReferenceValue = agentB.GetComponent<CombatAgentUnit>();
        so.ApplyModifiedProperties();

        ConfigureSingleAgent(agentA, 0);
        ConfigureSingleAgent(agentB, 1);
        
        agentA.GetComponent<CombatAgentUnit>().SetTarget(agentB.transform);
        agentB.GetComponent<CombatAgentUnit>().SetTarget(agentA.transform);
    }

    private void ConfigureSingleAgent(GameObject agentObj, int teamID)
    {
        agentObj.layer = LayerMask.NameToLayer("Agent");
        if (agentObj.CompareTag("Untagged")) agentObj.tag = "Agent";

        var rb = GetOrAddComponent<Rigidbody>(agentObj);
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        GetOrAddComponent<CombatUnitHealth>(agentObj);
        
        // Thruster Setup
        var thrusters = GetOrAddComponent<ThrusterController>(agentObj);
        thrusters.AutoSetupPoints(1.5f, 1.5f);

        var unit = GetOrAddComponent<CombatAgentUnit>(agentObj);
        SerializedObject unitSO = new SerializedObject(unit);
        if (projectilePrefab != null)
            unitSO.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab.GetComponent<LaserProjectileV3>();
        unitSO.FindProperty("settings").objectReferenceValue = arenaSettings;
        
        Transform muzzle = agentObj.transform.Find("Muzzle");
        if (muzzle == null)
        {
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(agentObj.transform);
            muzzle.localPosition = Vector3.forward * 1.0f;
        }
        unitSO.FindProperty("muzzlePoint").objectReferenceValue = muzzle;
        unitSO.ApplyModifiedProperties();

        var bp = GetOrAddComponent<BehaviorParameters>(agentObj);
        bp.BehaviorName = "AerialCombat";
        bp.TeamId = teamID;
        bp.BrainParameters.VectorObservationSize = 23;
        bp.BrainParameters.ActionSpec = new ActionSpec(4, new int[] { 2 });

        GetOrAddComponent<DecisionRequester>(agentObj).DecisionPeriod = 5;

        var sensor = GetOrAddComponent<RayPerceptionSensorComponent3D>(agentObj);
        sensor.SensorName = "AerialEye";
        sensor.RaysPerDirection = 4;
        sensor.MaxRayDegrees = 70;
        sensor.SphereCastRadius = 0.5f;
        sensor.RayLength = 50f;
        sensor.DetectableTags = new System.Collections.Generic.List<string> { "Wall", "Agent" };
    }

    private T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T comp = target.GetComponent<T>();
        if (comp == null) comp = target.AddComponent<T>();
        return comp;
    }

    private Component GetOrAddComponent(GameObject target, System.Type type)
    {
        Component comp = target.GetComponent(type);
        if (comp == null) comp = target.AddComponent(type);
        return comp;
    }

    private GameObject EnsureChildObject(string name, System.Type requiredComponent)
    {
        Transform child = transform.Find(name);
        if (child == null)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform);
            child = obj.transform;
        }
        if (requiredComponent != null) GetOrAddComponent(child.gameObject, requiredComponent);
        return child.gameObject;
    }
#endif
}