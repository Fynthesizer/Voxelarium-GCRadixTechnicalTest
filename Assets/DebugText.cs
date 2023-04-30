using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DebugText : MonoBehaviour
{
    [SerializeField] private BubblespaceAnalyser _bubblespaceAnalyser;

    private TextMeshProUGUI _text;

    void Start()
    {
        _text = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        float bubbleWidth = _bubblespaceAnalyser.SmoothedBubbleWidth;
        float bubbleHeight = _bubblespaceAnalyser.SmoothedBubbleHeight;

        _text.text = $"Bubble Width: {bubbleWidth}\nBubble Height: {bubbleHeight}";
    }
}
