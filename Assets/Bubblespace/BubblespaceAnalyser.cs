using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AK.Wwise;
using System.ComponentModel;

// The Bubblespace Analyser determines various properties about the environment surrounding the player using raycasts.
// These properties are used to control WWise RTPCs to apply appropriate reverb and ambience depending on the player's environment.
public class BubblespaceAnalyser : MonoBehaviour
{
    [Header("Raycasting")]
    [Tooltip("How many rays should be fired each frame")]
    [SerializeField] private int _rayCount = 3;
    [Tooltip("How many distance samples should be held in the buffers")]
    [SerializeField] private int _bufferSize = 4;
    [Tooltip("How many frames should the bubble width be smoothed over")]
    [SerializeField] private int _horizontalSmoothingFrames = 16;
    [Tooltip("How many frames should the bubble height be smoothed over")]
    [SerializeField] private int _verticalSmoothingFrames = 4;
    [Tooltip("How many different phases should the ray directions cycle through")]
    [SerializeField] private int _rotationPhases = 12;
    [Tooltip("The maximum distance that the rays will query")]
    [SerializeField] private float _maxRaycastDistance = 30.0f;
    [Tooltip("The horizontal offset from the player's position for the vertical rays")]
    [SerializeField] private float _verticalRaySpacing = 1.0f;
    [Tooltip("Which collision layers should be detected by the raycasts")]
    [SerializeField] private LayerMask _layerMask;

    private int _rotationPhase;
    private float _rotationAngle;

    [Header("Data")]
    [SerializeField] private AcousticPropertiesDatabase _acousticProperties;
    [SerializeField] private SpatialDefinitionDatabase _spatialDefinitions;

    [Header("Debug")]
    [SerializeField] private bool _drawDebug;

    [Header("Wwise Parameters")]
    [SerializeField] private AK.Wwise.RTPC _bubbleWidthRTPC;
    [SerializeField] private AK.Wwise.RTPC _bubbleHeightRTPC;
    [SerializeField] private AK.Wwise.RTPC _bubbleAverageRTPC;
    [SerializeField] private AK.Wwise.RTPC _bubbleAbsorptionRTPC;
    [SerializeField] private AK.Wwise.RTPC _outdoorExposureRTPC;

    [HideInInspector] public float SmoothedBubbleWidth;
    [HideInInspector] public float SmoothedBubbleHeight;
    [HideInInspector] public float SmoothedBubbleAbsorption;
    [HideInInspector] public float SmoothedOutdoorExposure;
    [HideInInspector] public Vector3 SmoothedOutdoorDirection;
    [HideInInspector] public SpaceDefinition CurrentSpace;

    private float _bubbleWidth;
    private float _bubbleHeight;
    private float _bubbleAbsorption;
    private float _outdoorExposure;
    private Vector3 _outdoorDirection;

    private Buffer<float> _bubbleWidthBuffer;
    private Buffer<float> _bubbleHeightBuffer;
    private Buffer<float> _bubbleAbsorptionBuffer;
    private Buffer<float> _outdoorExposureBuffer;
    private Buffer<Vector3> _outdoorDirectionBuffer;

    

    private Vector3 _previousPosition;

    private World _world;

    void Start()
    {
        _world = GameObject.FindGameObjectWithTag("World").GetComponent<World>();
        _world.TerrainChanged += UpdateBubble; // Update bubble if the terrain has changed

        _bubbleWidthBuffer = new Buffer<float>(_bufferSize);
        _bubbleHeightBuffer = new Buffer<float>(_bufferSize);
        _bubbleAbsorptionBuffer = new Buffer<float>(_bufferSize);
        _outdoorExposureBuffer = new Buffer<float>(_bufferSize);
        _outdoorDirectionBuffer = new Buffer<Vector3>(_bufferSize);

        // Calculate how many degrees the rays should rotate each frame
        _rotationAngle = 360f / _rayCount;
    }

    private void Update()
    {
        // If the player has moved since the last frame, recalculate the size of the bubble
        if (_previousPosition != transform.position) UpdateBubble();
        _previousPosition = transform.position;

        SmoothData();
        UpdateRTPCs();
    }

    private void UpdateRTPCs()
    {
        // Phase 1: Update bubble WWise parameters
        _bubbleWidthRTPC.SetGlobalValue(SmoothedBubbleWidth);
        _bubbleHeightRTPC.SetGlobalValue(SmoothedBubbleHeight);
        _bubbleAverageRTPC.SetGlobalValue((SmoothedBubbleWidth + SmoothedBubbleHeight) / 2f);
        _bubbleAbsorptionRTPC.SetGlobalValue(SmoothedBubbleAbsorption);
        _outdoorExposureRTPC.SetGlobalValue(SmoothedOutdoorExposure);

        // Phase 2: Using the bubble width and height, determine which environment(s) the player is most likely to be in and update WWise RTPCs
        Vector2 bubbleDimensions = new Vector2(SmoothedBubbleWidth, SmoothedBubbleHeight);
        SpaceDefinition likeliestSpace = new SpaceDefinition();
        var weights = _spatialDefinitions.GetWeights(bubbleDimensions);

        float highestWeight = 0f;

        foreach (SpaceDefinition d in _spatialDefinitions.SpaceDefinitions)
        {
            if (d.RTPC != null) d.RTPC.SetGlobalValue(weights[d]);

            if (weights[d] > highestWeight)
            {
                highestWeight = weights[d];
                likeliestSpace = d;
            }
        }

        CurrentSpace = likeliestSpace;
    }

    // Updates the smoothed bubble parameters
    private void SmoothData()
    {
        // Bubble width
        if (Mathf.Abs(_bubbleWidth - SmoothedBubbleWidth) > 1.0f)
            SmoothedBubbleWidth = Mathf.Lerp(SmoothedBubbleWidth, _bubbleWidth, 1f / _horizontalSmoothingFrames);

        // Bubble height
        if (Mathf.Abs(_bubbleHeight - SmoothedBubbleHeight) > 1.0f)
        {
            SmoothedBubbleHeight = Mathf.Lerp(SmoothedBubbleHeight, _bubbleHeight, 1f / _verticalSmoothingFrames);
        }

        // Absorption coefficient
        if (Mathf.Abs(_bubbleAbsorption - SmoothedBubbleAbsorption) > 0.0001f)
        {
            SmoothedBubbleAbsorption = Mathf.Lerp(SmoothedBubbleAbsorption, _bubbleAbsorption, 1f / 6f);
        }

        // Outdoor exposure
        if (Mathf.Abs(_outdoorExposure - SmoothedOutdoorExposure) > 0.0001f)
        {
            SmoothedOutdoorExposure = Mathf.Lerp(SmoothedOutdoorExposure, _outdoorExposure, 1f / 15f);
        }

        // Outdoor direction
        if (Vector3.Distance(SmoothedOutdoorDirection, _outdoorDirection) > 0.1f)
        {
            SmoothedOutdoorDirection = Vector3.Lerp(SmoothedOutdoorDirection, _outdoorDirection, 1f / 30f);
        }
    }

    // Performs a single scan of the environment and adds data to the relevant buffers
    // Called when the player moves or the environment changes
    private void UpdateBubble()
    {
        Bubble bubble = GetBubble();

        // Get the average of the largest 3 samples in the bubble width buffer
        _bubbleWidthBuffer.Enqueue(bubble.Width);
        _bubbleWidth = _bubbleWidthBuffer.OrderByDescending(x => x).Take(3).Average();

        // Get the average of the smallest 3 samples in the bubble width buffer
        _bubbleHeightBuffer.Enqueue(bubble.Height);
        _bubbleHeight = _bubbleHeightBuffer.OrderBy(x => x).Take(3).Average();

        // Get the average absorption coefficient in the buffer
        _bubbleAbsorptionBuffer.Enqueue(bubble.Absorption);
        _bubbleAbsorption = _bubbleAbsorptionBuffer.Average();

        // Get the average outdoor exposure ratio in the buffer
        _outdoorExposureBuffer.Enqueue(bubble.OutdoorExposure);
        _outdoorExposure = _outdoorExposureBuffer.Average();

        // Get the average outdoor direction in the buffer
        _outdoorDirectionBuffer.Enqueue(bubble.OutdoorDirection);
        _outdoorDirection = Vector3.zero;
        foreach (Vector3 direction in _outdoorDirectionBuffer) _outdoorDirection += direction;
        _outdoorDirection = _outdoorDirection / _outdoorDirectionBuffer.Count();

        // Increment the rotation phase 
        _rotationPhase++;
        if (_rotationPhase >= _rotationPhases) _rotationPhase = 0;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_drawDebug) return;

        // Draw spheres representing the bubble's width and height in the editor
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, SmoothedBubbleWidth);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, SmoothedBubbleHeight);
    }
#endif

    // Gets information about the current environment 
    private Bubble GetBubble()
    {
        float rayAngle = _rotationPhase * (360 / _rotationPhases);
        float averageHorizontalDistance;
        float averageVerticalDistance;
        float averageAbsorption;
        Vector3 outdoorDirection = Vector3.zero;

        float[] horizontalDistances = new float[_rayCount];
        float[] verticalDistances = new float[_rayCount];
        List<float> absorptionSamples = new();
        float outdoorExposure = 0;

        Ray ray;
        RaycastHit hitInfo;

        for (int i = 0; i < _rayCount; i++)
        {
            Vector3 horizontalRayDirection = Quaternion.AngleAxis(rayAngle, Vector3.up) * Vector3.forward;

            // Find horizontal distance
            ray = new Ray(transform.position, horizontalRayDirection);
            if (Physics.Raycast(ray, out hitInfo, _maxRaycastDistance, _layerMask))
            {
                horizontalDistances[i] = hitInfo.distance;

                // Find the absorption coefficient of the hit material
                Voxel.Material hitMaterial = _world.GetVoxel(hitInfo.point).material;
                absorptionSamples.Add(_acousticProperties.GetProperties(hitMaterial).Absorption);

                DrawDebugRay(ray, hitInfo, Color.red);

                // If a surface is hit, check to see if the surface is outdoors by doing another raycast straight upwards from the hit point
                ray = new Ray(hitInfo.point, Vector3.up);
                if (hitInfo.normal.y > 0 && !Physics.Raycast(ray, out hitInfo, _maxRaycastDistance, _layerMask))
                {
                    outdoorExposure++;
                    outdoorDirection += horizontalRayDirection;
                }

                DrawDebugRay(ray, hitInfo, Color.green);
            }
            else
            {
                horizontalDistances[i] = _maxRaycastDistance;
                outdoorExposure++; // If nothing is hit, this ray is considered to be outside
                outdoorDirection += horizontalRayDirection;

                DrawDebugRay(ray, hitInfo, Color.red);
            }
            
            // Find vertical distance
            // If a wall is closer than the ray's origin, consider the distance to be zero
            if (horizontalDistances[i] < _verticalRaySpacing) verticalDistances[i] = 0; 
            else { 
                Vector3 verticalRayOrigin = transform.position + (horizontalRayDirection * _verticalRaySpacing);
                ray = new Ray(verticalRayOrigin, Vector3.up);
                if (Physics.Raycast(ray, out hitInfo, _maxRaycastDistance, _layerMask))
                {
                    verticalDistances[i] = hitInfo.distance;

                    // Find the absorption coefficient of the hit material
                    Voxel.Material hitMaterial = _world.GetVoxel(hitInfo.point).material;
                    absorptionSamples.Add(_acousticProperties.GetProperties(hitMaterial).Absorption);

                    DrawDebugRay(ray, hitInfo, Color.blue);
                }
                else
                {
                    verticalDistances[i] = _maxRaycastDistance;
                    outdoorExposure++; // If nothing is hit, this ray is considered to be outside

                    DrawDebugRay(ray, hitInfo, Color.blue);
                }
            }

            rayAngle += _rotationAngle;
        }

        // Get the average horizontal distance
        averageHorizontalDistance = horizontalDistances.Average();
        
        // For the vertical distances, remove any values which are significantly different than the average
        RemoveOutliers(verticalDistances, 1.0f);
        averageVerticalDistance = verticalDistances.Average();

        // Get the average absorption value
        if (absorptionSamples.Count > 0) averageAbsorption = absorptionSamples.Average();
        else averageAbsorption = 0.0f;

        // Get the average outdoor exposure and outdoor direction
        outdoorExposure /= (_rayCount * 2f);
        outdoorDirection /= (_rayCount);

        return new Bubble(averageHorizontalDistance, averageVerticalDistance, averageAbsorption, outdoorExposure, outdoorDirection);
    }

    // Removes any values from an array which are significantly different from the average
    void RemoveOutliers(float[] array, float deviationThreshold)
    {
        float average = array.Average();
        float variance = array.Select(x => (x - average) * (x - average)).Average();
        float standardDeviation = (float)Mathf.Sqrt(variance);
        float threshold = deviationThreshold * standardDeviation;

        for (int i = array.Length - 1; i >= 0; i--)
        {
            if (Mathf.Abs(array[i] - average) > threshold)
            {
                List<float> list = array.ToList();
                list.RemoveAt(i);
                array = list.ToArray();
            }
        }
    }

    void DrawDebugRay(Ray ray, RaycastHit hitInfo, Color color)
    {
#if UNITY_EDITOR
        if (!_drawDebug) return;

        float length = hitInfo.collider == null ? _maxRaycastDistance : hitInfo.distance;
        Debug.DrawRay(ray.origin, ray.direction * length, color);
#endif
    }
}

// Custom queue which automatically dequeues the oldest element if it is over capacity
public class Buffer<T> : Queue<T>
{
    public int Limit { get; set; }

    public Buffer(int limit) : base(limit)
    {
        Limit = limit;
    }

    public new void Enqueue(T item)
    {
        while (Count >= Limit)
        {
            Dequeue();
        }
        base.Enqueue(item);
    }
}

// This struct contains data captured from a single environmental scan
public struct Bubble
{
    public float Width;                 // Width of the bubble
    public float Height;                // Height of the bubble
    public float Absorption;            // Average absorption coefficient of hit voxels
    public float OutdoorExposure;       // Ratio of outdoors area to indoors area of the bubble
    public Vector3 OutdoorDirection;    // Approximate direction towards the outdoors. If all or none of the rays are outside, this will be zero.

    public Bubble (float width, float height, float absorption, float outdoorExposure, Vector3 outdoorDirection)
    {
        Width = width;
        Height = height;
        Absorption = absorption;
        OutdoorExposure = outdoorExposure;
        OutdoorDirection = outdoorDirection;
    }
}