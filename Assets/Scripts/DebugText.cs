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
        string outdoorExposure = _bubblespaceAnalyser.SmoothedOutdoorExposure.ToString("0.00");
        string currentSpace = _bubblespaceAnalyser.CurrentSpace.Name;

        _text.text = $"<b>Bubble Data</b>\n" +
            $"Width: {bubbleWidth}\n" +
            $"Height: {bubbleHeight}\n" +
            $"Absorption: {bubbleAbsorption}\n" +
            $"Outdoor exposure: {outdoorExposure}\n" +
            $"Environment: {currentSpace}";
    }
}
