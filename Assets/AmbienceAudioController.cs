using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AK.Wwise;

public class AmbienceAudioController : MonoBehaviour
{
    [SerializeField] private WeatherManager _weatherManager;
    [SerializeField] private FirstPersonController _player;

    [SerializeField] private AK.Wwise.Event _outdoorAmbienceEvent;
    [SerializeField] private AK.Wwise.Event _caveAmbienceEvent;
    [SerializeField] private AK.Wwise.Event _rainEvent;
    [SerializeField] private AK.Wwise.Event _windEvent;
    [SerializeField] private AK.Wwise.RTPC _timeOfDayRTPC;
    [SerializeField] private AK.Wwise.RTPC _rainRTPC;
    [SerializeField] private AK.Wwise.RTPC _windRTPC;

    void Start()
    {
        _outdoorAmbienceEvent.Post(gameObject);
        _caveAmbienceEvent.Post(gameObject);
        _rainEvent.Post(gameObject);
        _windEvent.Post(gameObject);
    }

    void Update()
    {
        _rainRTPC.SetGlobalValue(_weatherManager.rainValue);
        _timeOfDayRTPC.SetGlobalValue(_weatherManager.timeOfDay);
        _windRTPC.SetGlobalValue(_weatherManager.windSpeed + (_player.transform.position.y / 96f));
    }
}
