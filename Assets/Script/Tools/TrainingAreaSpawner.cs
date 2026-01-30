using UnityEngine;

using System.Collections.Generic;

public class TrainingAreaSpawner : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private GameObject arenaPrefab; // Kéo Prefab TrainingArena vào đây
    [SerializeField] private int gridX = 5; // Số lượng hàng ngang
    [SerializeField] private int gridZ = 5; // Số lượng hàng dọc (Tổng = X * Z)
    [SerializeField] private float spacing = 500f; // Tang len 500 cho an toan tuyet doi

    [ContextMenu("Spawn Training Grid")]
    public void SpawnGrid()
    {
        if (arenaPrefab == null)
        {
            Debug.LogError("Chua gan Prefab Arena!");
            return;
        }

        // Xoa cac Arena cu
        var children = new List<GameObject>();
        foreach (Transform child in transform) children.Add(child.gameObject);
        children.ForEach(child => DestroyImmediate(child));

        // Spawn Loop
        for (int x = 0; x < gridX; x++)
        {
            for (int z = 0; z < gridZ; z++)
            {
                Vector3 pos = transform.position + new Vector3(x * spacing, 0, z * spacing);
                GameObject instance = Instantiate(arenaPrefab, pos, Quaternion.identity, transform);
                instance.name = $"Arena_{x}_{z}";
            }
        }

        Debug.Log($"Da spawn {gridX * gridZ} Arenas!");
    }
}