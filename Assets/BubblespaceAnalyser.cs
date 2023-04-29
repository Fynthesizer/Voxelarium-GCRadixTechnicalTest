using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AK.Wwise;
using Unity.VisualScripting.FullSerializer;
using UnityEditor.Search;
using System.Collections.Concurrent;

public class BubblespaceAnalyser : MonoBehaviour
{
    [SerializeField] private int _rayCount = 3;
    [SerializeField] private int _bufferSize = 4;
    [SerializeField] private int _horizontalSmoothingFrames = 16;
    [SerializeField] private int _verticalSmoothingFrames = 4;
    [SerializeField] private int _rotationPhases = 12;
    [SerializeField] private float _maxRaycastDistance = 10.0f;
    [SerializeField] private float _verticalRayPlayerDistance = 1.0f;
    [SerializeField] private LayerMask _layerMask;

    [SerializeField] private bool _drawDebug;

    private float _bubbleWidth;
    private float _smoothedBubbleWidth;
    private float _bubbleHeight;
    private float _smoothedBubbleHeight;
    private int _rotationPhase;
    private float _rotationAngle;

    private DistanceBuffer _bubbleWidthBuffer;
    private DistanceBuffer _bubbleHeightBuffer;

    void Start()
    {
        _bubbleWidthBuffer = new DistanceBuffer(_bufferSize);
        _bubbleHeightBuffer = new DistanceBuffer(_bufferSize);
        _rotationAngle = 360f / _rayCount;
    }

    private void Update()
    {
        // Smooth bubble size
        if (Mathf.Abs(_bubbleWidth - _smoothedBubbleWidth) > 1.0f)
            _smoothedBubbleWidth = Mathf.Lerp(_smoothedBubbleWidth, _bubbleWidth, 1f / _horizontalSmoothingFrames);

        if (Mathf.Abs(_bubbleHeight - _smoothedBubbleHeight) > 1.0f)
            _smoothedBubbleHeight = Mathf.Lerp(_smoothedBubbleHeight, _bubbleHeight, 1f / _verticalSmoothingFrames);
    }

    //Called when the player has moved
    public void UpdateBubble()
    {
        Vector2 bubble = GetBubble();

        _bubbleWidthBuffer.Enqueue(bubble.x);
        _bubbleWidth = _bubbleWidthBuffer.GetAverageOfSmallest(3);
       

        _bubbleHeightBuffer.Enqueue(bubble.y);
        _bubbleHeight = _bubbleHeightBuffer.GetAverageOfSmallest(3);

        _rotationPhase++;
        if (_rotationPhase > _rotationPhases) _rotationPhase = 0;
    }

    private void OnDrawGizmos()
    {
        if (!_drawDebug) return;

        Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.5f);
        Gizmos.DrawSphere(transform.position, _smoothedBubbleWidth);
        Gizmos.color = new Color(0.0f, 0.0f, 1.0f, 0.5f);
        Gizmos.DrawSphere(transform.position, _smoothedBubbleHeight);
    }

    Vector2 GetBubble()
    {
        float rayAngle = _rotationPhase * (360 / _rotationPhases);
        float averageHorizontalDistance = 0f;
        float averageVerticalDistance = 0f;

        float[] horizontalDistances = new float[_rayCount];
        float[] verticalDistances = new float[_rayCount];

        for (int i = 0; i < _rayCount; i++)
        {
            Vector3 horizontalRayDirection = Quaternion.AngleAxis(rayAngle, Vector3.up) * Vector3.forward;

            // Find horizontal distance
            Ray ray = new Ray(transform.position, horizontalRayDirection);
            RaycastHit hitInfo;
            if (Physics.Raycast(ray, out hitInfo, _maxRaycastDistance, _layerMask))
            {
                //averageHorizontalDistance += hitInfo.distance;
                horizontalDistances[i] = hitInfo.distance;
            }
            else
            {
                //averageHorizontalDistance += _maxRaycastDistance;
                horizontalDistances[i] = _maxRaycastDistance;
            }

            // Find vertical distance
            Vector3 verticalRayOrigin = transform.position + (horizontalRayDirection * _verticalRayPlayerDistance);
            ray = new Ray(verticalRayOrigin, Vector3.up);
            if (Physics.Raycast(ray, out hitInfo, _maxRaycastDistance, _layerMask))
            {
                //averageVerticalDistance += hitInfo.distance;
                verticalDistances[i] = hitInfo.distance;
            }
            else
            {
                //averageVerticalDistance += _maxRaycastDistance;
                verticalDistances[i] = _maxRaycastDistance;
            }

            if (_drawDebug)
            {
                Debug.DrawRay(transform.position, horizontalRayDirection * horizontalDistances[i], Color.red);
                Debug.DrawRay(verticalRayOrigin, Vector3.up * verticalDistances[i], Color.blue);
            }

            rayAngle += _rotationAngle;
        }

        //averageHorizontalDistance /= _rayCount;
        //averageVerticalDistance /= _rayCount;

        averageHorizontalDistance = horizontalDistances.Average();
        
        // For the vertical distances, remove any values which are significantly different than the average
        RemoveOutliers(verticalDistances, 1.0f);
        averageVerticalDistance = verticalDistances.Average();

        return new Vector2(averageHorizontalDistance, averageVerticalDistance);
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

public class DistanceBuffer : Queue<float>
{
    public int Limit { get; set; }

    public DistanceBuffer(int limit) : base(limit)
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