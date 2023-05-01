using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AK.Wwise;

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
    [SerializeField] private float _verticalRayHorizontalOffset = 1.0f;
    [Tooltip("Which collision layers should be detected by the raycasts")]
    [SerializeField] private LayerMask _layerMask;

    [SerializeField] private AcousticProperties[] _acousticProperties;
    private Dictionary<Voxel.Material, AcousticProperties> _acousticPropertiesDictionary;

    [Header("Debug")]
    [SerializeField] private bool _drawDebug;
    [SerializeField] private Mesh _debugHitMesh;
    [SerializeField] private float _debugHitSize;
    [SerializeField] private Material _debugHitMaterial;
    private Vector3[] _hitPoints;

    [Header("Wwise Parameters")]
    [SerializeField] private AK.Wwise.RTPC _bubbleWidthRTPC;
    [SerializeField] private AK.Wwise.RTPC _bubbleHeightRTPC;
    [SerializeField] private AK.Wwise.RTPC _bubbleAverageRTPC;
    [SerializeField] private AK.Wwise.RTPC _bubbleAbsorptionRTPC;

    [HideInInspector] public float SmoothedBubbleWidth;
    [HideInInspector] public float SmoothedBubbleHeight;
    [HideInInspector] public float SmoothedBubbleAbsorption;

    private float _bubbleWidth;
    private float _bubbleHeight;
    private float _bubbleAverage;
    private float _bubbleAbsorption;
    private int _rotationPhase;
    private float _rotationAngle;
    
    private Vector3 _previousPosition;

    private Buffer _bubbleWidthBuffer;
    private Buffer _bubbleHeightBuffer;
    private Buffer _bubbleAbsorptionBuffer;

    private World _world;

    void Start()
    {
        _world = GameObject.FindGameObjectWithTag("World").GetComponent<World>();

        _bubbleWidthBuffer = new Buffer(_bufferSize);
        _bubbleHeightBuffer = new Buffer(_bufferSize);
        _bubbleAbsorptionBuffer = new Buffer(_bufferSize);

        _rotationAngle = 360f / _rayCount;
        _hitPoints = new Vector3[_rotationPhases * 2];

        // Create a dictionary for easy access to different materials' acoustic properties
        _acousticPropertiesDictionary = new Dictionary<Voxel.Material, AcousticProperties>();
        foreach (AcousticProperties p in _acousticProperties)
        {
            _acousticPropertiesDictionary.Add(p.Material, p);
        }
    }

    private void Update()
    {
        // If the player has moved since the last frame, recalculate the size of the bubble
        if (_previousPosition != transform.position) UpdateBubble();
        _previousPosition = transform.position;

        SmoothData();
        if (_drawDebug) DrawDebug();
    }

    private void DrawDebug()
    {
        for (int i = 0; i < _hitPoints.Length; i++)
        {
            Matrix4x4 transformationMatrix = Matrix4x4.Translate(_hitPoints[i]);
            transformationMatrix *= Matrix4x4.Scale(new Vector3(_debugHitSize, _debugHitSize, _debugHitSize));
            Graphics.DrawMesh(_debugHitMesh, transformationMatrix, _debugHitMaterial, 0);
        }
    }

    private void SmoothData()
    {
        if (Mathf.Abs(_bubbleWidth - SmoothedBubbleWidth) > 1.0f)
        {
            SmoothedBubbleWidth = Mathf.Lerp(SmoothedBubbleWidth, _bubbleWidth, 1f / _horizontalSmoothingFrames);
            _bubbleWidthRTPC.SetGlobalValue(SmoothedBubbleWidth);

            _bubbleAverage = (SmoothedBubbleWidth + SmoothedBubbleHeight) / 2;
            _bubbleAverageRTPC.SetGlobalValue(_bubbleAverage);
        }

        if (Mathf.Abs(_bubbleHeight - SmoothedBubbleHeight) > 1.0f)
        {
            SmoothedBubbleHeight = Mathf.Lerp(SmoothedBubbleHeight, _bubbleHeight, 1f / _verticalSmoothingFrames);
            _bubbleHeightRTPC.SetGlobalValue(SmoothedBubbleHeight);

            _bubbleAverage = (SmoothedBubbleWidth + SmoothedBubbleHeight) / 2;
            _bubbleAverageRTPC.SetGlobalValue(_bubbleAverage);
        }

        if (Mathf.Abs(_bubbleAbsorption - SmoothedBubbleAbsorption) > 0.01f)
        {
            SmoothedBubbleAbsorption = Mathf.Lerp(SmoothedBubbleAbsorption, _bubbleAbsorption, 1f / 6f);
            _bubbleAbsorptionRTPC.SetGlobalValue(SmoothedBubbleAbsorption);
        }
    }

    public void UpdateBubble()
    {
        Bubble bubble = GetBubble();

        _bubbleWidthBuffer.Enqueue(bubble.Width);
        _bubbleWidth = _bubbleWidthBuffer.GetAverageOfSmallest(3);

        _bubbleHeightBuffer.Enqueue(bubble.Height);
        _bubbleHeight = _bubbleHeightBuffer.GetAverageOfSmallest(3);

        _bubbleAbsorptionBuffer.Enqueue(bubble.Absorption);
        _bubbleAbsorption = _bubbleAbsorptionBuffer.GetAverageOfSmallest(4);

        _rotationPhase++;
        if (_rotationPhase >= _rotationPhases) _rotationPhase = 0;
    }

    private void OnDrawGizmos()
    {
        if (!_drawDebug) return;

        Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.5f);
        Gizmos.DrawSphere(transform.position, SmoothedBubbleWidth);
        Gizmos.color = new Color(0.0f, 0.0f, 1.0f, 0.5f);
        Gizmos.DrawSphere(transform.position, SmoothedBubbleHeight);
    }

    private Bubble GetBubble()
    {
        float rayAngle = _rotationPhase * (360 / _rotationPhases);
        float averageHorizontalDistance;
        float averageVerticalDistance;
        float averageAbsorption;

        float[] horizontalDistances = new float[_rayCount];
        float[] verticalDistances = new float[_rayCount];
        List<float> absorptionValues = new List<float>();

        for (int i = 0; i < _rayCount; i++)
        {
            Vector3 horizontalRayDirection = Quaternion.AngleAxis(rayAngle, Vector3.up) * Vector3.forward;

            // Find horizontal distance
            Ray ray = new Ray(transform.position, horizontalRayDirection);
            RaycastHit hitInfo;
            if (Physics.Raycast(ray, out hitInfo, _maxRaycastDistance, _layerMask))
            {
                horizontalDistances[i] = hitInfo.distance;
                Voxel.Material hitMaterial = _world.GetVoxel(hitInfo.point).material;
                absorptionValues.Add(_acousticPropertiesDictionary[hitMaterial].Absorption);
            }
            else
            {
                horizontalDistances[i] = _maxRaycastDistance;
            }

            if (i == 0) _hitPoints[(_rotationPhase) + i] = hitInfo.point;

            // Find vertical distance

            // If a wall is closer than the ray's origin, consider the distance to be zero
            if (horizontalDistances[i] < _verticalRayHorizontalOffset) verticalDistances[i] = 0; 
            else { 
                Vector3 verticalRayOrigin = transform.position + (horizontalRayDirection * _verticalRayHorizontalOffset);
                ray = new Ray(verticalRayOrigin, Vector3.up);
                if (Physics.Raycast(ray, out hitInfo, _maxRaycastDistance, _layerMask))
                {
                    verticalDistances[i] = hitInfo.distance;
                    Voxel.Material hitMaterial = _world.GetVoxel(hitInfo.point).material;
                    absorptionValues.Add(_acousticPropertiesDictionary[hitMaterial].Absorption);
                }
                else
                {
                    verticalDistances[i] = _maxRaycastDistance;
                }

                if (i == 0) _hitPoints[_rotationPhases + _rotationPhase + i] = hitInfo.point;
            }

            rayAngle += _rotationAngle;
        }

        // Get the average of the two smallest values
        averageHorizontalDistance = horizontalDistances.Average();
        
        // For the vertical distances, remove any values which are significantly different than the average
        RemoveOutliers(verticalDistances, 1.0f);
        averageVerticalDistance = verticalDistances.Average();

        if (absorptionValues.Count > 0.0f) averageAbsorption = absorptionValues.Average();
        else averageAbsorption = 0.0f;

        return new Bubble(averageHorizontalDistance, averageVerticalDistance, averageAbsorption);
    }

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
}

// Custom container inheriting from queue which automatically dequeues the oldest element if it is over capacity
public class Buffer : Queue<float>
{
    public int Limit { get; set; }

    public Buffer(int limit) : base(limit)
    {
        Limit = limit;
    }

    public new void Enqueue(float item)
    {
        while (Count >= Limit)
        {
            Dequeue();
        }
        base.Enqueue(item);
    }

    public float GetAverageOfSmallest(int sampleSize)
    {
        var smallest = this.OrderBy(x => x).Take(sampleSize);
        return smallest.Average();
    }
}

[System.Serializable]
public struct AcousticProperties
{
    public Voxel.Material Material;
    public float Absorption;
}

public struct Bubble
{
    public float Width;
    public float Height;
    public float Absorption;

    public Bubble (float width, float height, float absorption)
    {
        Width = width;
        Height = height;
        Absorption = absorption;
    }
}