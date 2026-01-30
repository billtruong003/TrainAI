using UnityEngine;
using System.Collections;

public class DogfightArena : MonoBehaviour
{
    [SerializeField] private DogfightAgent agentA;
    [SerializeField] private DogfightAgent agentB;
    [SerializeField] private Transform spawnA;
    [SerializeField] private Transform spawnB;
    [SerializeField] private float roundLimit = 60f;

    private Coroutine gameLoop;

    private void Start()
    {
        agentA.SetMatchData(this, 0, agentB.transform);
        agentB.SetMatchData(this, 1, agentA.transform);
        StartRound();
    }

    public void ReportDeath(DogfightAgent victim)
    {
        DogfightAgent winner = victim == agentA ? agentB : agentA;
        winner.RegisterWin();
        victim.EndEpisode();
        StartRound();
    }

    private void StartRound()
    {
        if (gameLoop != null) StopCoroutine(gameLoop);
        gameLoop = StartCoroutine(RoundRoutine());
    }

    private IEnumerator RoundRoutine()
    {
        // Reset
        agentA.gameObject.SetActive(true);
        agentB.gameObject.SetActive(true);

        agentA.PrepareRound(spawnA.position, spawnA.rotation);
        agentB.PrepareRound(spawnB.position, spawnB.rotation);

        yield return null; // Wait for physics update

        float timer = 0f;
        while (timer < roundLimit)
        {
            if (!agentA.IsAlive || !agentB.IsAlive) yield break;
            timer += Time.deltaTime;
            yield return null;
        }

        // Draw
        agentA.RegisterDraw();
        agentB.RegisterDraw();
        StartRound();
    }
}