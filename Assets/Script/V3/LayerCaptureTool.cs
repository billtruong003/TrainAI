using System;
using System.IO;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tools.Graphics
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class UltimateCaptureTool : MonoBehaviour
    {
        [Title("Camera Setup")]
        [SerializeField, Required]
        private Camera targetCamera;

        [SerializeField]
        private LayerMask captureLayers = -1;

        [Title("Resolution & Format")]
        [SerializeField]
        private Vector2Int resolution = new Vector2Int(2048, 2048);

        [SerializeField, Range(1, 4)]
        private int superSampling = 1;

        [Title("Output")]
        [SerializeField, FolderPath]
        private string savePath = "Assets/Captures";

        [SerializeField]
        private string fileName = "TransparentCapture";

        private void Reset()
        {
            targetCamera = GetComponent<Camera>();
        }

        [Button(ButtonSizes.Gigantic), GUIColor(0.2f, 0.8f, 1f)]
        private void CaptureTransparent()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
                if (targetCamera == null) return;
            }

            PrepareDirectory();

            RenderTexture rt = CreateRenderTexture();
            Texture2D finalTexture = RenderToTexture(rt);

            SaveTextureToPng(finalTexture);

            Cleanup(rt, finalTexture);
        }

        [Button(ButtonSizes.Medium)]
        private void OpenFolder()
        {
            PrepareDirectory();
#if UNITY_EDITOR
            EditorUtility.RevealInFinder(savePath);
#endif
        }

        private RenderTexture CreateRenderTexture()
        {
            int finalWidth = resolution.x * superSampling;
            int finalHeight = resolution.y * superSampling;

            RenderTextureDescriptor desc = new RenderTextureDescriptor(finalWidth, finalHeight, RenderTextureFormat.ARGB32, 24)
            {
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
                msaaSamples = Mathf.Max(1, QualitySettings.antiAliasing),
                useMipMap = false,
                autoGenerateMips = false
            };

            return RenderTexture.GetTemporary(desc);
        }

        private Texture2D RenderToTexture(RenderTexture rt)
        {
            CameraClearFlags oldFlags = targetCamera.clearFlags;
            Color oldColor = targetCamera.backgroundColor;
            RenderTexture oldTarget = targetCamera.targetTexture;
            int oldMask = targetCamera.cullingMask;
            bool oldHdr = targetCamera.allowHDR;

            try
            {
                targetCamera.targetTexture = rt;
                targetCamera.cullingMask = captureLayers;
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                targetCamera.allowHDR = false;

                targetCamera.Render();

                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                return tex;
            }
            finally
            {
                targetCamera.clearFlags = oldFlags;
                targetCamera.backgroundColor = oldColor;
                targetCamera.targetTexture = oldTarget;
                targetCamera.cullingMask = oldMask;
                targetCamera.allowHDR = oldHdr;
            }
        }

        private void SaveTextureToPng(Texture2D texture)
        {
            byte[] bytes = texture.EncodeToPNG();
            string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fullPath = Path.Combine(savePath, $"{fileName}_{timeStamp}.png");

            File.WriteAllBytes(fullPath, bytes);

#if UNITY_EDITOR
            AssetDatabase.Refresh();
            Debug.Log($"<b>[Capture]</b> Saved: <color=cyan>{fullPath}</color>");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fullPath));
#endif
        }

        private void PrepareDirectory()
        {
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }
        }

        private void Cleanup(RenderTexture rt, Texture2D tex)
        {
            if (rt != null) RenderTexture.ReleaseTemporary(rt);

            if (tex != null)
            {
                if (Application.isEditor) DestroyImmediate(tex);
                else Destroy(tex);
            }
        }
    }
}