using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.VisualScripting;
using UnityEngine.Rendering;

// This scriptable object contains a list of definitions for different types of environments
// Each environment covers a specific width and height range
// The Bubblespace Analyser can use this to determine which type of environment(s) the bubble most resembles
[CreateAssetMenu(menuName = "Bubblespace/Spatial Definition Database")]
public class SpatialDefinitionDatabase : ScriptableObject
{
    public List<SpaceDefinition> SpaceDefinitions;
    public float Ambiguity = 15f;

    // Given a bubble width and height, returns a dictionary which contains the resemblance of the bubble to each possible environment
    public Dictionary<SpaceDefinition, float> GetWeights(Vector2 position)
    {
        var weights = new Dictionary<SpaceDefinition, float>();
        float magnitude = 0f;
        float spaceLength = 30f * Ambiguity;

        foreach (SpaceDefinition d in SpaceDefinitions)
        {
            float distance = Vector2.Distance(d.Position, position);
            weights[d] = Mathf.Pow((1f - (distance / spaceLength)), (30f / d.Range));
            magnitude += weights[d];
        }

        // Normalise the weights so that they add up to 1
        Dictionary<SpaceDefinition, float> normalizedWeights = new Dictionary<SpaceDefinition, float>();
        foreach (SpaceDefinition d in SpaceDefinitions)
        {
            normalizedWeights[d] = weights[d] / magnitude;
        }

        return normalizedWeights;
    }
}

[System.Serializable]
public class SpaceDefinition
{
    public string Name;
    public Vector2 Position;    // The average width and height of this space
    public float Range;         // 
    public AK.Wwise.RTPC RTPC;  // Which RTPC should this control
    public Color Color;         // For visualisation purposes
}

#if UNITY_EDITOR
// Custom editor for visualising space definitions on a 2D texture
[CustomEditor(typeof(SpatialDefinitionDatabase))]
public class SpatialDefinitionEditor : Editor
{
    Dictionary<SpaceDefinition, float> weights;

    public override void OnInspectorGUI()
    {
        SpatialDefinitionDatabase spatialDefinitions = (SpatialDefinitionDatabase)target;

        DrawDefaultInspector();

        Texture2D graphTexture = new Texture2D(30, 30);
        Color32[] colours = new Color32[graphTexture.width * graphTexture.height];
        
        int i = 0;
        for (int y = 0; y < graphTexture.height; y++)
        {
            for (int x = 0; x < graphTexture.width; x++)
            {
                Color32 pixelColour = Color.black;

                Vector2 spacePos = new Vector2(Remap(x, 0, graphTexture.width, 0, 30f), Remap(y, 0, graphTexture.height, 0, 30f));

                var weights = spatialDefinitions.GetWeights(spacePos);
                foreach (SpaceDefinition d in spatialDefinitions.SpaceDefinitions)
                {
                    pixelColour += d.Color * weights[d];
                }

                colours[i] = pixelColour;
                i++;
            }
        }
        
        graphTexture.SetPixels32(colours);

        graphTexture.Apply();

        graphTexture.Size();

        GUILayoutOption[] options = { GUILayout.ExpandWidth(true), GUILayout.Width(1000) };
        GUIStyle style = new GUIStyle();
        style.stretchWidth = true;
        style.fixedWidth = 1000;

        Rect rect = GUILayoutUtility.GetRect(500f, 500f);

        graphTexture.filterMode = FilterMode.Point;

        GUILayout.BeginHorizontal();
        GUILayout.ExpandWidth(true);
        GUI.DrawTexture(rect, graphTexture);
        GUILayout.ExpandWidth(false);
        GUILayout.EndHorizontal();
        EditorGUI.indentLevel++;
        EditorGUI.indentLevel--;

        // Mark the object as dirty so that changes are saved
        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
        }
    }

    public static float Remap(float value, float inputMin, float inputMax, float outputMin, float outputMax)
    {
        return outputMin + (value - inputMin) * (outputMax - outputMin) / (inputMax - inputMin);
    }

}
#endif