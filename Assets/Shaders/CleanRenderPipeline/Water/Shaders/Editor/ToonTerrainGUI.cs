using UnityEditor;
using UnityEngine;

public class ToonTerrainGUI : ShaderGUI
{
    // ── Foldout states ──
    static bool _foldLayers = true;
    static bool _foldHeightBlend = true;
    static bool _foldTriplanar = false;
    static bool _foldTexScale = false;
    static bool _foldCelShading = true;

    // ── Styles ──
    static GUIStyle _headerStyle;
    static GUIStyle _sectionBox;
    static bool _stylesInit;

    static readonly Color AccentTerrain = new Color(0.45f, 0.75f, 0.35f, 1f);

    static void InitStyles()
    {
        if (_stylesInit) return;
        _stylesInit = true;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            richText = true
        };

        _sectionBox = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 6, 6),
            margin = new RectOffset(0, 0, 2, 4)
        };
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        InitStyles();
        Material mat = materialEditor.target as Material;

        EditorGUILayout.Space(4);
        DrawBanner("TOON TERRAIN", AccentTerrain);
        EditorGUILayout.Space(4);

        // ━━ Terrain Layers ━━
        _foldLayers = DrawSection("Terrain Layers", _foldLayers, () =>
        {
            DrawLayerRow(materialEditor, properties, "_Layer0", "_Layer0Color", "Layer 0 — Low Ground");
            EditorGUILayout.Space(4);
            DrawLayerRow(materialEditor, properties, "_Layer1", "_Layer1Color", "Layer 1 — Mid Ground");
            EditorGUILayout.Space(4);
            DrawLayerRow(materialEditor, properties, "_Layer2", "_Layer2Color", "Layer 2 — High / Snow");
            EditorGUILayout.Space(4);
            DrawLayerRow(materialEditor, properties, "_Layer3", "_Layer3Color", "Layer 3 — Cliff (Triplanar)");
        });

        // ━━ Height Blending ━━
        _foldHeightBlend = DrawSection("Height Blending", _foldHeightBlend, () =>
        {
            DrawProp(materialEditor, properties, "_HeightLow", "Low → Mid Height");
            DrawProp(materialEditor, properties, "_HeightMid", "Mid → High Height");
            DrawProp(materialEditor, properties, "_BlendSharpness", "Blend Sharpness");
            DrawProp(materialEditor, properties, "_HeightOffset", "Height Offset");
            DrawHelpBox("Adjusts the Y threshold for layer transitions");
        });

        // ━━ Triplanar Cliff ━━
        _foldTriplanar = DrawSection("Triplanar Cliff", _foldTriplanar, () =>
        {
            DrawProp(materialEditor, properties, "_TriplanarScale", "Triplanar Scale");
            DrawProp(materialEditor, properties, "_TriplanarSharpness", "Blend Sharpness");
            DrawProp(materialEditor, properties, "_CliffAngle", "Cliff Angle Threshold");
            DrawHelpBox("Lower = more cliff coverage  ·  Higher = steeper slopes only");
        });

        // ━━ Texture Scale ━━
        _foldTexScale = DrawSection("Texture Scale", _foldTexScale, () =>
        {
            DrawProp(materialEditor, properties, "_TexScale", "Global Texture Scale");
            DrawHelpBox("Scales UV for layers 0–2 (world XZ projection)");
        });

        // ━━ Cel Shading ━━
        _foldCelShading = DrawSection("Cel Shading", _foldCelShading, () =>
        {
            DrawProp(materialEditor, properties, "_ShadowColor", "Shadow Color");
            DrawProp(materialEditor, properties, "_Threshold", "Shadow Threshold");
            DrawProp(materialEditor, properties, "_Smoothness", "Shadow Smoothness");
        });

        EditorGUILayout.Space(6);
        materialEditor.RenderQueueField();
    }

    // ════════════════════════════════════════════════════════════════
    // Layer Row: texture + tint on same line
    // ════════════════════════════════════════════════════════════════

    static void DrawLayerRow(MaterialEditor editor, MaterialProperty[] props,
        string texName, string colorName, string label)
    {
        MaterialProperty tex = FindProperty(texName, props, false);
        MaterialProperty col = FindProperty(colorName, props, false);

        if (tex != null && col != null)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            editor.TexturePropertySingleLine(new GUIContent("Texture"), tex, col);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Drawing Helpers
    // ════════════════════════════════════════════════════════════════

    static bool DrawSection(string title, bool foldout, System.Action drawContent)
    {
        EditorGUILayout.Space(2);
        Rect headerRect = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));

        Color bgCol = foldout ? new Color(0.25f, 0.25f, 0.25f, 0.6f) : new Color(0.2f, 0.2f, 0.2f, 0.3f);
        EditorGUI.DrawRect(headerRect, bgCol);

        Rect accentRect = new Rect(headerRect.x, headerRect.y, 3f, headerRect.height);
        EditorGUI.DrawRect(accentRect, AccentTerrain);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && headerRect.Contains(e.mousePosition))
        {
            foldout = !foldout;
            e.Use();
        }

        Rect labelRect = new Rect(headerRect.x + 16f, headerRect.y + 2f, headerRect.width - 16f, headerRect.height);
        string arrow = foldout ? "▼ " : "► ";
        EditorGUI.LabelField(labelRect, arrow + title, _headerStyle);

        if (foldout)
        {
            EditorGUILayout.BeginVertical(_sectionBox);
            drawContent();
            EditorGUILayout.EndVertical();
        }

        return foldout;
    }

    static void DrawBanner(string text, Color color)
    {
        Rect r = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.8f));

        Rect accent = new Rect(r.x, r.y, r.width, 2f);
        EditorGUI.DrawRect(accent, color);

        GUIStyle bannerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = color }
        };
        EditorGUI.LabelField(r, text, bannerStyle);
    }

    static void DrawProp(MaterialEditor editor, MaterialProperty[] props, string name, string label)
    {
        MaterialProperty p = FindProperty(name, props, false);
        if (p != null)
            editor.ShaderProperty(p, label);
    }

    static void DrawHelpBox(string msg)
    {
        EditorGUILayout.LabelField(msg, EditorStyles.centeredGreyMiniLabel);
    }
}