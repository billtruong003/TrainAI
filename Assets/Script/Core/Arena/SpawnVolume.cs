using UnityEngine;

public class SpawnVolume : MonoBehaviour
{
    [SerializeField] private Color debugColor = new Color(0, 1, 0, 0.3f);
    [SerializeField] private Vector3 size = new Vector3(10, 10, 10);

    public Vector3 GetSafeSpawnPosition(float radius, LayerMask obstacleMask, int maxAttempts)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 randomPos = transform.position + new Vector3(
                Random.Range(-size.x * 0.5f, size.x * 0.5f),
                Random.Range(-size.y * 0.5f, size.y * 0.5f),
                Random.Range(-size.z * 0.5f, size.z * 0.5f)
            );

            if (!Physics.CheckSphere(randomPos, radius, obstacleMask))
            {
                return randomPos;
            }
        }
        return transform.position;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = debugColor;
        Gizmos.DrawCube(transform.position, size);
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, size);
    }
}