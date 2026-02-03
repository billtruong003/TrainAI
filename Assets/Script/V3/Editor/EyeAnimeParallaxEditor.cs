using UnityEngine;
using UnityEditor;

public class EyeAnimeParallaxEditor : ShaderGUI
{
    private Material targetMaterial;
    private MaterialEditor materialEditor;
    private MaterialProperty[] properties;

    private enum BlendMode
    {
        AlphaBlend,
        Additive,
        Multiply
    }

    private readonly string[] layerKeywords = {
        "_LAYER1_ON", "_LAYER2_ON", "_LAYER3_ON",
        "_LAYER4_ON", "_LAYER5_ON", "_LAYER6_ON"
    };

    private readonly string[] layerToggleProperties = {
        "_Layer1_On", "_Layer2_On", "_Layer3_On",
        "_Layer4_On", "_Layer5_On", "_Layer6_On"
    };

    private readonly string[] layerTitles = {
        "Highlight / Catchlight",
        "Gradient / Iris Detail",
        "Emissive Sparkle / Magic Ring",
        "Shadow / Depth Layer",
        "Blood Veins",
        "Extra Overlay / Custom"
    };

    public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
    {
        targetMaterial = editor.target as Material;
        materialEditor = editor;
        properties = props;

        DrawHeader("Anime Eye - Dynamic Parallax (Optimized)");

        DrawGroup("Base Properties", () =>
        {
            DrawMaterialProperty("_MainTex");
            DrawMaterialProperty("_Color");
        });

        DrawGroup("Global Parallax", () =>
        {
            DrawMaterialProperty("_ParallaxStrength");
            DrawMaterialProperty("_ParallaxCenter");
        });

        DrawEyeSocketProperties();

        DrawHeader("Dynamic Layers (Max 6)");

        for (int i = 0; i < layerKeywords.Length; i++)
        {
            DrawLayerBlock(i + 1, layerKeywords[i], layerToggleProperties[i], layerTitles[i]);
        }

        DrawGroup("Rim Light", () =>
        {
            DrawMaterialProperty("_RimPower");
            DrawMaterialProperty("_RimColor");
        });

        materialEditor.RenderQueueField();
        materialEditor.EnableInstancingField();
    }

    private void DrawEyeSocketProperties()
    {
        MaterialProperty useSocketProp = FindProperty("_UseEyeSocket", properties);
        materialEditor.ShaderProperty(useSocketProp, new GUIContent("Enable Eye Socket Depth"));

        if (useSocketProp.floatValue > 0.5f)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawMaterialProperty("_EyeSocketMask");
            DrawMaterialProperty("_EyeSocketDepth");
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }
    }

    private void DrawLayerBlock(int layerIndex, string keyword, string toggleProperty, string title)
    {
        MaterialProperty toggleProp = FindProperty(toggleProperty, properties);
        bool isEnabled = toggleProp.floatValue > 0.5f;

        bool newEnabled = EditorGUILayout.ToggleLeft($"[{layerIndex}] {title}", isEnabled, EditorStyles.boldLabel);

        if (newEnabled != isEnabled)
        {
            toggleProp.floatValue = newEnabled ? 1.0f : 0.0f;
            if (newEnabled)
                targetMaterial.EnableKeyword(keyword);
            else
                targetMaterial.DisableKeyword(keyword);
        }

        if (newEnabled)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawMaterialProperty($"_Layer{layerIndex}_Tex");
            DrawMaterialProperty($"_Layer{layerIndex}_Tint");

            MaterialProperty blendModeProp = FindProperty($"_Layer{layerIndex}_BlendMode", properties);
            var currentMode = (BlendMode)blendModeProp.floatValue;
            var newMode = (BlendMode)EditorGUILayout.EnumPopup("Blend Mode", currentMode);
            if (newMode != currentMode)
            {
                blendModeProp.floatValue = (float)newMode;
            }

            DrawMaterialProperty($"_Layer{layerIndex}_Depth");
            DrawMaterialProperty($"_Layer{layerIndex}_Emissive");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Animation & Transform", EditorStyles.boldLabel);
            DrawMaterialProperty($"_Layer{layerIndex}_Offset");
            DrawMaterialProperty($"_Layer{layerIndex}_ScrollSpeed");
            DrawMaterialProperty($"_Layer{layerIndex}_Scale");
            DrawMaterialProperty($"_Layer{layerIndex}_Rotation");

            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space(2);
    }

    private void DrawHeader(string text)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
    }

    private void DrawGroup(string title, System.Action content)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        content();
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();
    }

    private void DrawMaterialProperty(string propertyName)
    {
        MaterialProperty prop = FindProperty(propertyName, properties, false);
        if (prop != null)
        {
            materialEditor.ShaderProperty(prop, prop.displayName);
        }
    }
}