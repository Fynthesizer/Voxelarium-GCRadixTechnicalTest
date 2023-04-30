using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AK.Wwise;

public class AudioInput : MonoBehaviour
{
    [SerializeField] AK.Wwise.Event _audioInputEvent;

    private AkAudioFormat _audioFormat;
    private AkChannelConfig _channelConfig;

    void Start()
    {
        _channelConfig = new AkChannelConfig();
        _audioFormat = new AkAudioFormat();
        _channelConfig.SetStandard(0);
        _audioFormat.SetAll(48000, _channelConfig, 16, 2, AkSoundEngine.AK_INT, AkSoundEngine.AK_INTERLEAVED);

        _audioInputEvent.Post(gameObject);
    }
}
