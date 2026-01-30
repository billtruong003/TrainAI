using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

public abstract class SmartAgent : Agent
{
    [Header("Hub Connection")]
    [SerializeField] protected EnvironmentHub envHub;

    protected override void OnEnable()
    {
        base.OnEnable();
        if (envHub == null)
            envHub = GetComponentInParent<EnvironmentHub>();
        
        if (envHub != null)
            envHub.RegisterAgent(this);
    }

    // -- Interface for Hub to control Agent flow --
    public virtual void OnEnvironmentReady() 
    {
        // Cho phep Agent hoat dong, reset force, etc.
        // Logic nay thay the OnEpisodeBegin truyen thong de dong bo voi Hub
    }

    public virtual void OnEnvironmentFinished()
    {
        EndEpisode(); // Bao hieu cho ML-Agents biet episode ket thuc
    }

    // -- Auto Config Tools --
    [ContextMenu("Auto Configure Agent")]
#if ODIN_INSPECTOR
    [Button("Auto Configure Agent", ButtonSizes.Large), GUIColor(0, 1, 0)]
#endif
    public void AutoConfigure()
    {
        // 1. Behavior Parameters
        var bp = GetComponent<BehaviorParameters>();
        if (bp == null) bp = gameObject.AddComponent<BehaviorParameters>();
        
        bp.BehaviorName = this.GetType().Name;
        bp.BrainParameters.VectorObservationSize = CalculateObservationSize();
        bp.Model = null; // Reset model de tranh conflict ID
        
        // 2. Decision Requester
        var dr = GetComponent<DecisionRequester>();
        if (dr == null) dr = gameObject.AddComponent<DecisionRequester>();
        dr.DecisionPeriod = 5;
        dr.TakeActionsBetweenDecisions = true;

        Debug.Log($"[SmartAgent] Configured {name}: ObsSize={bp.BrainParameters.VectorObservationSize}");
    }

    // Class con phai khai bao size obs de auto config hoat dong
    protected abstract int CalculateObservationSize();
}