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
        string bubbleWidth = _bubblespaceAnalyser.SmoothedBubbleWidth.ToString("0.00");
        string bubbleHeight = _bubblespaceAnalyser.SmoothedBubbleHeight.ToString("0.00");
        string bubbleAbsorption = _bubblespaceAnalyser.SmoothedBubbleAbsorption.ToString("0.00");

        _text.text = $"<b>Bubble Data</b>\nWidth: {bubbleWidth}\nHeight: {bubbleHeight}\nAbsorption: {bubbleAbsorption}";
    }
}
