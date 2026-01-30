#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

public class DogfightAutoSetup : EditorWindow
{
    private DogfightSettings settings;
    private GameObject projectilePrefab;
    private GameObject agentModelPrefab;

    [MenuItem("Tools/TrainAI/Dogfight Setup")]
    public static void ShowWindow()
    {
        GetWindow<DogfightAutoSetup>("Dogfight Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("Configuration Assets", EditorStyles.boldLabel);
        settings = (DogfightSettings)EditorGUILayout.ObjectField("Settings Data", settings, typeof(DogfightSettings), false);
        projectilePrefab = (GameObject)EditorGUILayout.ObjectField("Projectile Prefab", projectilePrefab, typeof(GameObject), false);
        agentModelPrefab = (GameObject)EditorGUILayout.ObjectField("Agent Model", agentModelPrefab, typeof(GameObject), false);

        GUILayout.Space(10);
        GUILayout.Label("Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Create Full Arena"))
        {
            CreateArena();
        }

        if (GUILayout.Button("Setup Selected Object as Agent"))
        {
            if (Selection.activeGameObject != null)
                SetupAgent(Selection.activeGameObject, 0);
        }
    }

    private void CreateArena()
    {
        GameObject root = new GameObject("Dogfight_Arena_V2");
        
        GameObject spawnA = CreateObject("Spawn_A", root, new Vector3(0, 10, -20));
        GameObject spawnB = CreateObject("Spawn_B", root, new Vector3(0, 10, 20));
        spawnB.transform.rotation = Quaternion.Euler(0, 180, 0);

        GameObject agentA = agentModelPrefab ? Instantiate(agentModelPrefab) : CreateCube("Agent_Team0");
        GameObject agentB = agentModelPrefab ? Instantiate(agentModelPrefab) : CreateCube("Agent_Team1");
        
        agentA.name = "Agent_Team0";
        agentB.name = "Agent_Team1";
        agentA.transform.SetParent(root.transform);
        agentB.transform.SetParent(root.transform);
        agentA.transform.position = spawnA.transform.position;
        agentB.transform.position = spawnB.transform.position;

        SetupAgent(agentA, 0);
        SetupAgent(agentB, 1);

        DogfightArena arena = root.AddComponent<DogfightArena>();
        SerializedObject so = new SerializedObject(arena);
        so.FindProperty("agentA").objectReferenceValue = agentA.GetComponent<DogfightAgent>();
        so.FindProperty("agentB").objectReferenceValue = agentB.GetComponent<DogfightAgent>();
        so.FindProperty("spawnA").objectReferenceValue = spawnA.transform;
        so.FindProperty("spawnB").objectReferenceValue = spawnB.transform;
        so.ApplyModifiedProperties();

        DogfightAgent agA = agentA.GetComponent<DogfightAgent>();
        DogfightAgent agB = agentB.GetComponent<DogfightAgent>();
        
        SerializedObject soA = new SerializedObject(agA);
        soA.FindProperty("arena").objectReferenceValue = arena;
        soA.ApplyModifiedProperties();

        SerializedObject soB = new SerializedObject(agB);
        soB.FindProperty("arena").objectReferenceValue = arena;
        soB.ApplyModifiedProperties();
    }

    private void SetupAgent(GameObject obj, int teamId)
    {
        obj.layer = LayerMask.NameToLayer("Default");
        
        Rigidbody rb = GetOrAdd<Rigidbody>(obj);
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (settings != null)
        {
            rb.mass = settings.AgentMass;
            rb.linearDamping = settings.AgentDrag;
            rb.angularDamping = settings.AgentAngularDrag;
        }

        ThrusterController thrusters = GetOrAdd<ThrusterController>(obj);
        SetupThrusters(obj, thrusters);

        DogfightAgent agent = GetOrAdd<DogfightAgent>(obj);
        SerializedObject soAgent = new SerializedObject(agent);
        soAgent.FindProperty("settings").objectReferenceValue = settings;
        
        if (projectilePrefab != null)
        {
            SmartProjectile sp = projectilePrefab.GetComponent<SmartProjectile>();
            soAgent.FindProperty("projectilePrefab").objectReferenceValue = sp;
        }

        Transform muzzle = obj.transform.Find("Muzzle");
        if (muzzle == null)
        {
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(obj.transform);
            muzzle.localPosition = Vector3.forward * 1.5f;
        }
        soAgent.FindProperty("muzzlePoint").objectReferenceValue = muzzle;

        GameObject sensorObj = new GameObject("NearMissSensor");
        sensorObj.transform.SetParent(obj.transform);
        sensorObj.transform.localPosition = Vector3.zero;
        sensorObj.layer = LayerMask.NameToLayer("Ignore Raycast");
        SphereCollider sc = sensorObj.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 3.0f;
        NearMissSensor nms = sensorObj.AddComponent<NearMissSensor>();
        soAgent.FindProperty("dodgeSensor").objectReferenceValue = nms;
        soAgent.ApplyModifiedProperties();

        BehaviorParameters bp = GetOrAdd<BehaviorParameters>(obj);
        bp.BehaviorName = "DogfightV2";
        bp.TeamId = teamId;
        bp.BrainParameters.VectorObservationSize = 27;
        bp.BrainParameters.ActionSpec = new ActionSpec(4, new int[] { 2 });

        RayPerceptionSensorComponent3D rays = GetOrAdd<RayPerceptionSensorComponent3D>(obj);
        rays.SensorName = "FrontEye";
        rays.RaysPerDirection = 4;
        rays.MaxRayDegrees = 70;
        rays.SphereCastRadius = 0.5f;
        rays.RayLength = 50f;
        rays.DetectableTags = new List<string> { "Agent", "Wall", "Ground" };

        GetOrAdd<DecisionRequester>(obj).DecisionPeriod = 5;
    }

    private void SetupThrusters(GameObject obj, ThrusterController controller)
    {
        SerializedObject so = new SerializedObject(controller);
        SerializedProperty prop = so.FindProperty("thrusterPoints");
        
        if (prop.arraySize == 0)
        {
            prop.arraySize = 4;
            float xOffset = 1.5f;
            float zOffset = 1.5f;
            
            prop.GetArrayElementAtIndex(0).objectReferenceValue = CreateThrusterPoint(obj, "FL", -xOffset, zOffset);
            prop.GetArrayElementAtIndex(1).objectReferenceValue = CreateThrusterPoint(obj, "FR", xOffset, zOffset);
            prop.GetArrayElementAtIndex(2).objectReferenceValue = CreateThrusterPoint(obj, "RL", -xOffset, -zOffset);
            prop.GetArrayElementAtIndex(3).objectReferenceValue = CreateThrusterPoint(obj, "RR", xOffset, -zOffset);
        }
        so.ApplyModifiedProperties();
    }

    private Transform CreateThrusterPoint(GameObject parent, string name, float x, float z)
    {
        Transform t = new GameObject($"Thruster_{name}").transform;
        t.SetParent(parent.transform);
        t.localPosition = new Vector3(x, 0, z);
        t.localRotation = Quaternion.identity; 
        return t;
    }

    private T GetOrAdd<T>(GameObject obj) where T : Component
    {
        T c = obj.GetComponent<T>();
        return c != null ? c : obj.AddComponent<T>();
    }

    private GameObject CreateObject(string name, GameObject parent, Vector3 pos)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent.transform);
        obj.transform.position = pos;
        return obj;
    }

    private GameObject CreateCube(string name)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        return obj;
    }
}
#endif