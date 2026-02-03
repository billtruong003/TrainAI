using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Renderer))]
public class EyeController : MonoBehaviour
{
    [Tooltip("Chỉ số của Material chứa Eye Shader trên Renderer này.")]
    [SerializeField] private int materialIndex = 0;

    [Tooltip("Layer của mắt cần được điều khiển (từ 1 đến 6).")]
    [Range(1, 6)]
    [SerializeField] private int targetLayerIndex = 1;

    [Header("Rotation Control")]
    [Tooltip("Phạm vi góc xoay ngẫu nhiên (Min, Max). Giá trị từ 0 đến 360.")]
    [SerializeField] private Vector2 rotationRange = new Vector2(0f, 360f);

    [Header("Scale Control")]
    [Tooltip("Phạm vi co giãn ngẫu nhiên (Min, Max). 1 là kích thước gốc.")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.9f, 1.1f);

    [Header("Timing Control")]
    [Tooltip("Thời gian chuyển đổi giữa các trạng thái ngẫu nhiên (Min, Max).")]
    [SerializeField] private Vector2 transitionDurationRange = new Vector2(0.5f, 1.5f);

    [Tooltip("Thời gian dừng lại ở mỗi trạng thái (Min, Max).")]
    [SerializeField] private Vector2 holdDurationRange = new Vector2(1f, 3f);

    private Material materialInstance;
    private int rotationPropertyID;
    private int scalePropertyID;

    private void Awake()
    {
        InitializeMaterial();
        CachePropertyIDs();
    }

    private void Start()
    {
        StartCoroutine(AnimateEyeLayer());
    }

    private void InitializeMaterial()
    {
        Renderer meshRenderer = GetComponent<Renderer>();
        if (meshRenderer.materials.Length > materialIndex)
        {
            materialInstance = meshRenderer.materials[materialIndex];
        }
    }

    private void CachePropertyIDs()
    {
        rotationPropertyID = Shader.PropertyToID($"_Layer{targetLayerIndex}_Rotation");
        scalePropertyID = Shader.PropertyToID($"_Layer{targetLayerIndex}_Scale");
    }

    private IEnumerator AnimateEyeLayer()
    {
        if (materialInstance == null)
        {
            yield break;
        }

        while (true)
        {
            float targetRotation = Random.Range(rotationRange.x, rotationRange.y);
            float targetScaleValue = Random.Range(scaleRange.x, scaleRange.y);
            Vector2 targetScale = new Vector2(targetScaleValue, targetScaleValue);

            float transitionDuration = Random.Range(transitionDurationRange.x, transitionDurationRange.y);

            float startRotation = materialInstance.GetFloat(rotationPropertyID);
            Vector2 startScale = materialInstance.GetVector(scalePropertyID);

            yield return AnimateToTarget(startRotation, targetRotation, startScale, targetScale, transitionDuration);

            float holdDuration = Random.Range(holdDurationRange.x, holdDurationRange.y);
            yield return new WaitForSeconds(holdDuration);
        }
    }

    private IEnumerator AnimateToTarget(float startRot, float targetRot, Vector2 startScale, Vector2 targetScale, float duration)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            float progress = elapsedTime / duration;
            float smoothedProgress = Mathf.SmoothStep(0f, 1f, progress);

            float currentRotation = Mathf.Lerp(startRot, targetRot, smoothedProgress);
            Vector2 currentScale = Vector2.Lerp(startScale, targetScale, smoothedProgress);

            materialInstance.SetFloat(rotationPropertyID, currentRotation);
            materialInstance.SetVector(scalePropertyID, currentScale);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        materialInstance.SetFloat(rotationPropertyID, targetRot);
        materialInstance.SetVector(scalePropertyID, targetScale);
    }
}