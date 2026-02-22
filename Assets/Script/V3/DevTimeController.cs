using UnityEngine;

public class DevTimeController : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetTimeScale(1f);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetTimeScale(5f);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetTimeScale(20f);
        if (Input.GetKeyDown(KeyCode.Alpha0)) SetTimeScale(0f);
    }

    private void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        Debug.Log($"Time Scale set to: {scale}x");
    }
}