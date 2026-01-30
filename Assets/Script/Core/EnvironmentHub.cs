using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;

public enum EnvState { Initializing, Active, Cooldown }

[RequireComponent(typeof(SmartPoolManager))]
public abstract class EnvironmentHub : MonoBehaviour
{
    [Header("Recording Mode Settings")]
    [SerializeField] protected bool isTrainingMode = true;
    [SerializeField] protected float preStartDelay = 2.0f;
    [SerializeField] protected float postRoundDelay = 1.5f;

    protected EnvState currentState = EnvState.Initializing;
    protected SmartPoolManager poolManager;
    protected List<SmartAgent> registeredAgents = new List<SmartAgent>();

    public SmartPoolManager Pool => poolManager;
    public bool IsActive => currentState == EnvState.Active;

    protected virtual void Awake()
    {
        poolManager = GetComponent<SmartPoolManager>();
    }

    protected virtual void Start()
    {
        // Nhan event reset toan cuc neu can (Training mode)
        if (isTrainingMode)
        {
            Academy.Instance.OnEnvironmentReset += OnGlobalReset;
        }
        StartCoroutine(SessionLoop());
    }

    public void RegisterAgent(SmartAgent agent)
    {
        if (!registeredAgents.Contains(agent))
        {
            registeredAgents.Add(agent);
            agent.transform.SetParent(transform);
        }
    }

    // Goi boi Agent khi chet hoac hoan thanh nv
    public virtual void NotifyAgentDone(SmartAgent agent, bool success)
    {
        if (currentState != EnvState.Active) return;

        // Logic reset tuy thuoc gameplay (VD: 1 chet la reset het, hay cho hoi sinh?)
        // Default: Reset all environment
        StartCoroutine(ResetEnvironmentSequence());
    }

    protected IEnumerator SessionLoop()
    {
        while (true)
        {
            // 1. Setup Phase
            currentState = EnvState.Initializing;
            // Clean up all bullets/vfx from previous round
            poolManager.DespawnAll();
            ResetSceneElements();

            if (!isTrainingMode && preStartDelay > 0)
                yield return new WaitForSeconds(preStartDelay);

            // 2. Action Phase
            currentState = EnvState.Active;
            foreach (var agent in registeredAgents) agent.OnEnvironmentReady();

            // Cho den khi reset duoc kich hoat (tu NotifyAgentDone)
            yield return new WaitUntil(() => currentState == EnvState.Cooldown);

            // 3. Cooldown Phase (Recording only)
            if (!isTrainingMode && postRoundDelay > 0)
                yield return new WaitForSeconds(postRoundDelay);
        }
    }

    protected IEnumerator ResetEnvironmentSequence()
    {
        currentState = EnvState.Cooldown;
        foreach (var agent in registeredAgents) agent.OnEnvironmentFinished();
        yield return null; // Wait for frame end
    }

    private void OnGlobalReset()
    {
        // Force reset ngay lap tuc cho training
        StopAllCoroutines();
        StartCoroutine(SessionLoop());
    }

    // Implement boi class con: Reset vi tri tuong, spawn point, etc.
    protected abstract void ResetSceneElements();
}