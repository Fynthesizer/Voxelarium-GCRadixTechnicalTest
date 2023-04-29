using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainCamera : MonoBehaviour
{
    public float grassCullDistance = 100;
    public float genericCullDistance = 800;

    /*
    public FMODUnity.EventReference reverbSnapshot;
    public FMODUnity.ParamRef reverbParameter;
    FMOD.Studio.EventInstance reverb;
    */

    public float reverbUpdateRate = 0.5f;
    public float rayThreshold = 0.9f; //What percentage of rays must be obstructed before reverb blending starts
    public int reverbRays = 26;
    public LayerMask reverbLayerMask;

    // Start is called before the first frame update
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

        /*
        reverb = FMODUnity.RuntimeManager.CreateInstance(reverbSnapshot);
        reverb.start();
        */
        InvokeRepeating("UpdateReverb", reverbUpdateRate, reverbUpdateRate);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void UpdateReverb()
    {
        RaycastHit[] hits = new RaycastHit[reverbRays];
        Vector3[] directions = PointsOnSphere(reverbRays);
        bool[] didHit = new bool[reverbRays];
        int hitCount = 0;

        //Cast rays in a sphere to find how many directions are obstructed
        for (int i = 0; i < reverbRays; i++)
        {
            if (Physics.Raycast(transform.position, directions[i], out hits[i], 50, reverbLayerMask))
            {
                hitCount += 1;
            }
        }
        //Adjust the reverb amount depending on how many rays hit a surface
        float reverbAmount = (float)hitCount / (float)reverbRays;
        //FMODUnity.RuntimeManager.StudioSystem.setParameterByName("InCave", reverbAmount);

        /*
        if (reverbAmount > 0.9f)
        {
            GameManager.gm.weatherManager.SetIndoors(true);
        }
        else GameManager.gm.weatherManager.SetIndoors(false);
        */
    }

    Vector3[] PointsOnSphere(int n)
    {
        List<Vector3> upts = new List<Vector3>();
        float inc = Mathf.PI * (3 - Mathf.Sqrt(5));
        float off = 2.0f / n;
        float x = 0;
        float y = 0;
        float z = 0;
        float r = 0;
        float phi = 0;

        for (var k = 0; k < n; k++)
        {
            y = k * off - 1 + (off / 2);
            r = Mathf.Sqrt(1 - y * y);
            phi = k * inc;
            x = Mathf.Cos(phi) * r;
            z = Mathf.Sin(phi) * r;

            upts.Add(new Vector3(x, y, z));
        }
        Vector3[] pts = upts.ToArray();
        return pts;
    }
}
