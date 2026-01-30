using UnityEngine;
using UnityEditor;

public class ToonShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // Material target
        Material targetMat = materialEditor.target as Material;

        // Header style
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 12;
        headerStyle.margin = new RectOffset(0, 0, 10, 5);

        // --- SECTION: BASE ---
        GUILayout.Label("Base Settings", headerStyle);
        MaterialProperty baseMap = FindProperty("_BaseMap", properties);
        MaterialProperty baseColor = FindProperty("_BaseColor", properties);

        materialEditor.TexturePropertySingleLine(new GUIContent("Base Map & Color"), baseMap, baseColor);
        materialEditor.TextureScaleOffsetProperty(baseMap);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // --- SECTION: TOON LIGHTING ---
        GUILayout.Label("Toon Lighting (Ramp)", headerStyle);
        MaterialProperty rampTex = FindProperty("_RampTex", properties);
        MaterialProperty rampThresh = FindProperty("_RampThreshold", properties);
        MaterialProperty rampSmooth = FindProperty("_RampSmooth", properties);

        materialEditor.TexturePropertySingleLine(new GUIContent("Ramp Texture"), rampTex);
        if (rampTex.textureValue == null)
        {
            EditorGUILayout.HelpBox("Please assign a Ramp Texture for the Toon effect.", MessageType.Warning);
        }

        // Chỉ hiển thị sliders nếu muốn fallback hoặc debug
        materialEditor.ShaderProperty(rampThresh, "Threshold (Debug)");
        materialEditor.ShaderProperty(rampSmooth, "Smoothness (Debug)");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // --- SECTION: SPECULAR ---
        GUILayout.Label("Specular", headerStyle);
        MaterialProperty useSpecular = FindProperty("_UseSpecular", properties);

        materialEditor.ShaderProperty(useSpecular, "Enable Specular");

        if (targetMat.IsKeywordEnabled("_SPECULAR_ON") || useSpecular.floatValue == 1)
        {
            EditorGUI.indentLevel++;
            MaterialProperty specColor = FindProperty("_SpecularColor", properties);
            MaterialProperty specSize = FindProperty("_SpecularSize", properties);
            MaterialProperty specFalloff = FindProperty("_SpecularFalloff", properties);

            materialEditor.ColorProperty(specColor, "Specular Color");
            materialEditor.RangeProperty(specSize, "Size");
            materialEditor.RangeProperty(specFalloff, "Falloff (Softness)");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // --- SECTION: RIM LIGHT ---
        GUILayout.Label("Rim Light", headerStyle);
        MaterialProperty useRim = FindProperty("_UseRim", properties);
        materialEditor.ShaderProperty(useRim, "Enable Rim Light");

        if (targetMat.IsKeywordEnabled("_RIM_ON") || useRim.floatValue == 1)
        {
            EditorGUI.indentLevel++;
            MaterialProperty rimColor = FindProperty("_RimColor", properties);
            MaterialProperty rimPower = FindProperty("_RimPower", properties);
            MaterialProperty rimThresh = FindProperty("_RimThreshold", properties);

            materialEditor.ColorProperty(rimColor, "Rim Color");
            materialEditor.RangeProperty(rimPower, "Rim Power (Thinness)");
            materialEditor.RangeProperty(rimThresh, "Rim Threshold");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // --- SECTION: OUTLINE ---
        GUILayout.Label("Outline", headerStyle);
        MaterialProperty outlineColor = FindProperty("_OutlineColor", properties);
        MaterialProperty outlineWidth = FindProperty("_OutlineWidth", properties);

        materialEditor.ColorProperty(outlineColor, "Color");
        materialEditor.RangeProperty(outlineWidth, "Width");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Default Rendering Options (Queue, Instancing, etc.)
        GUILayout.Label("Advanced Options", headerStyle);
        materialEditor.EnableInstancingField();
        materialEditor.RenderQueueField();
    }
}