using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.VisualScripting;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Bubblespace/Spatial Definitions")]
public class SpatialDefinitions : ScriptableObject
{
    public List<SpaceDefinition> SpaceDefinitions;
    public float Ambiguity = 15f;

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
    public Vector2 Position;
    public float Range;
    public AK.Wwise.RTPC RTPC;
    public Color Color;
}

[CustomEditor(typeof(SpatialDefinitions))]
public class SpatialDefinitionEditor : Editor
{
    Dictionary<SpaceDefinition, float> weights;

    public override void OnInspectorGUI()
    {
        SpatialDefinitions spatialDefinitions = (SpatialDefinitions)target;

        DrawDefaultInspector();

        //Rect layoutRect = GUILayoutUtility.GetRect(64, 64);
        //Rect graphRect = new Rect(layoutRect.x, layoutRect.y, layoutRect.width, layoutRect.width);
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
        //GUILayout.Box(graphTexture, style, options);
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