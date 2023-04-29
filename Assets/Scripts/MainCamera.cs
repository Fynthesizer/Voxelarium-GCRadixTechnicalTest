using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainCamera : MonoBehaviour
{
    public float grassCullDistance = 100;
    public float genericCullDistance = 800;

    void Start()
    {
        Camera camera = GetComponent<Camera>();
        float[] distances = new float[32];
        //distances[7] = genericCullDistance; //Terrain
        //distances[0] = genericCullDistance; //Default
        distances[16] = grassCullDistance; //Grass and small plants
        //distances[17] = 10000; //Sky
        //distances[11] = genericCullDistance; //Clouds
        camera.layerCullDistances = distances;
    }
}
