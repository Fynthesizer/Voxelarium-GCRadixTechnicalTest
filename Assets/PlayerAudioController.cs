using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AK.Wwise;

public class PlayerAudioController : MonoBehaviour
{
    [SerializeField] private AK.Wwise.Event _footstepEvent;
    [SerializeField] private float _footstepRate;
    private float _lastStepDist = 0f;
    private Vector3 _prevPos;

    [SerializeField] private GroundMaterialSwitch[] _groundMaterialSwitches = { };

    private Dictionary<Voxel.Material, AK.Wwise.Switch> _groundMaterialDictionary;

    void Start()
    {
        // Initialise ground material dictionary
        _groundMaterialDictionary = new Dictionary<Voxel.Material, Switch>();
        foreach (GroundMaterialSwitch g in _groundMaterialSwitches)
        {
            _groundMaterialDictionary.Add(g.material, g.wwiseSwitch);
        }
    }

    public void Movement()
    {
        float distance = Vector3.Distance(transform.position, _prevPos);
        _lastStepDist += distance;
        _prevPos = transform.position;

        if (_lastStepDist > _footstepRate)
        {
            PlayFootstep();
            _lastStepDist = 0f;
        }
    }


    public void PlayFootstep()
    {
        _footstepEvent.Post(gameObject);
    }

    public void SetGroundMaterial(Voxel.Material material)
    {
        if (material == Voxel.Material.Air) return;
        _groundMaterialDictionary[material].SetValue(gameObject);
    }
}

[System.Serializable]
class GroundMaterialSwitch
{
    public AK.Wwise.Switch wwiseSwitch;
    public Voxel.Material material;
}