#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

public class CombatAgentAutoSetupV3 : EditorWindow
{
    private ArenaSettings settings;
    private LaserProjectileV3 projectilePrefab;

    [MenuItem("Tools/TrainAI/Combat Agent V3 Setup")]
    public static void ShowWindow()
    {
        GetWindow<CombatAgentAutoSetupV3>("Agent Setup V3");
    }

    private void OnGUI()
    {
        GUILayout.Label("V3 Simplified Setup (Hover Mode)", EditorStyles.boldLabel);
        
        settings = (ArenaSettings)EditorGUILayout.ObjectField("Arena Settings", settings, typeof(ArenaSettings), false);
        projectilePrefab = (LaserProjectileV3)EditorGUILayout.ObjectField("Laser Prefab", projectilePrefab, typeof(LaserProjectileV3), false);

        GUILayout.Space(10);

        if (GUILayout.Button("Setup Selected Object as Agent V3"))
        {
            if (Selection.activeGameObject != null)
            {
                SetupAgent(Selection.activeGameObject);
                Debug.Log($"[Setup] Completed for {Selection.activeGameObject.name}");
            }
            else
            {
                Debug.LogError("Please select a GameObject first!");
            }
        }
    }

    private void SetupAgent(GameObject obj)
    {
        // 1. Rigidbody
        Rigidbody rb = GetOrAdd<Rigidbody>(obj);
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.None; // AI controls balance

        if (settings != null)
        {
            rb.mass = settings.AgentMass;
            rb.linearDamping = settings.AgentDrag;
            rb.angularDamping = settings.AgentAngularDrag;
        }

        // 2. Health
        GetOrAdd<CombatUnitHealth>(obj);

        // 3. Thruster Controller (Still required to avoid null refs, even if simplified)
        ThrusterController thrusters = GetOrAdd<ThrusterController>(obj);
        // Force re-validate points
        SerializedObject soThrusters = new SerializedObject(thrusters);
        soThrusters.Update();
        // Trigger validation via method reflection or just let it init at runtime
        soThrusters.ApplyModifiedProperties();

        // 4. CombatAgentUnit
        CombatAgentUnit agent = GetOrAdd<CombatAgentUnit>(obj);
        SerializedObject soAgent = new SerializedObject(agent);
        
        soAgent.FindProperty("settings").objectReferenceValue = settings;
        if (projectilePrefab != null)
        {
            soAgent.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
        }

        Transform muzzle = obj.transform.Find("Muzzle");
        if (muzzle == null)
        {
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(obj.transform);
            muzzle.localPosition = Vector3.forward * 1.0f;
        }
        soAgent.FindProperty("muzzlePoint").objectReferenceValue = muzzle;
        soAgent.ApplyModifiedProperties();

        // 5. ML-Agents Components
        BehaviorParameters bp = GetOrAdd<BehaviorParameters>(obj);
        bp.BehaviorName = "AerialCombat"; // Must match YAML
        bp.BrainParameters.VectorObservationSize = 23; // Updated size
        
        // Action Spec: 2 Continuous (Move, Turn), 1 Discrete (Fire)
        // Note: Discrete branch size 2 means (0=NoFire, 1=Fire)
        bp.BrainParameters.ActionSpec = new ActionSpec(2, new int[] { 2 }); 

        GetOrAdd<DecisionRequester>(obj).DecisionPeriod = 5;

        // 6. Remove Ray Perception (To keep Observation Size at exactly 23)
        // Since we provide direct Target Position in CollectObservations, Rays are redundant for now
        // and would require increasing Obs Size significantly (e.g. +35 floats).
        var rays = obj.GetComponent<RayPerceptionSensorComponent3D>();
        if (rays != null)
        {
            DestroyImmediate(rays, true);
            Debug.Log("[Setup] Removed RayPerceptionSensor to match Observation Size 23.");
        }
        
        // Ensure Tag
        if (obj.tag == "Untagged") obj.tag = "Agent";
        
        EditorUtility.SetDirty(obj);
    }

    private T GetOrAdd<T>(GameObject obj) where T : Component
    {
        T c = obj.GetComponent<T>();
        return c != null ? c : obj.AddComponent<T>();
    }
}
#endif