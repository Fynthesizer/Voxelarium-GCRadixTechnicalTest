using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AK.Wwise;

// In charge of playing ambience tracks and controlling RTPCs relating to ambience
public class AmbienceAudioController : MonoBehaviour
{
    [SerializeField] private WeatherManager _weatherManager;
    [SerializeField] private FirstPersonController _player;
    [SerializeField] private BubblespaceAnalyser _bubblespaceAnalyser;

    [SerializeField] private AK.Wwise.Event _outdoorAmbienceEvent;
    [SerializeField] private AK.Wwise.Event _caveAmbienceEvent;
    [SerializeField] private AK.Wwise.Event _rainEvent;
    [SerializeField] private AK.Wwise.Event _windEvent;
    [SerializeField] private AK.Wwise.RTPC _timeOfDayRTPC;
    [SerializeField] private AK.Wwise.RTPC _rainRTPC;
    [SerializeField] private AK.Wwise.RTPC _windRTPC;

    void Start()
    {
        GameManager.Instance.WorldLoaded += WorldLoaded;
    }

    private void WorldLoaded()
    {
        // Once the world is loaded, start playing ambience
        _outdoorAmbienceEvent.Post(gameObject);
        _caveAmbienceEvent.Post(gameObject);
        _rainEvent.Post(gameObject);
        _windEvent.Post(gameObject);
    }

    void Update()
    {
        UpdateRTPCs();
        UpdatePosition();
    }

    private void UpdateRTPCs()
    {
        _rainRTPC.SetGlobalValue(_weatherManager.rainValue);
        _timeOfDayRTPC.SetGlobalValue(_weatherManager.timeOfDay);
        _windRTPC.SetGlobalValue(_weatherManager.windSpeed + (_player.transform.position.y / 96f));
    }

    void UpdatePosition()
    {
        // If the player is partially indoors, the emitter's position will be offset to sound as though ambience is coming from the outside world
        float panningInfluence = (1 - _bubblespaceAnalyser.SmoothedOutdoorExposure) * 50f;
        Vector3 positionOffset = _bubblespaceAnalyser.SmoothedOutdoorDirection * panningInfluence;
        transform.position = _bubblespaceAnalyser.transform.position + positionOffset;
    }
}
