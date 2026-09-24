using UnityEditor;
using UnityEngine;

/// <summary>Default inspector plus an embedded profile inspector and one-click Acrylic V3 preset buttons.</summary>
[CustomEditor(typeof(DungeonRunWorldStyleSettings))]
public sealed class DungeonRunWorldStyleSettingsEditor : Editor
{
    private Editor profileEditor;

    private void OnDisable()
    {
        if (profileEditor != null) DestroyImmediate(profileEditor);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var settings = (DungeonRunWorldStyleSettings)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Acrylic V3 Presets", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply CurrentPixel")) DungeonRunAcrylicLab.ApplyCurrentPixel();
            if (GUILayout.Button("Apply AcrylicPixel Target")) DungeonRunAcrylicLab.ApplyAcrylicPixelTarget();
            if (GUILayout.Button("Apply AcrylicSoft")) DungeonRunAcrylicLab.ApplyAcrylicSoft();
        }

        if (settings.profile == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Profile", EditorStyles.boldLabel);
        Editor.CreateCachedEditor(settings.profile, null, ref profileEditor);
        profileEditor.OnInspectorGUI();
    }
}
