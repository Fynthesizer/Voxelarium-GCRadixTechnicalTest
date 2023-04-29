using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AK.Wwise;

public class PlayerAudioController : MonoBehaviour
{
    [SerializeField] private BubblespaceAnalyser _bubblespaceAnalyser;
    [SerializeField] private GameObject _target;

    [Header("Footsteps")]
    [SerializeField] private AK.Wwise.Event _footstepEvent;
    [SerializeField] private float _footstepRate;
    private float _lastStepDist = 0f;
    private Vector3 _prevPos;

    [SerializeField] private GroundMaterialSwitch[] _groundMaterialSwitches = { };

    private Dictionary<Voxel.Material, AK.Wwise.Switch> _groundMaterialDictionary;

    [Header("Sculpting")]
    [SerializeField] private AK.Wwise.Event _sculptPlayEvent;
    [SerializeField] private AK.Wwise.Event _sculptStopEvent;
    [SerializeField] private AK.Wwise.State _dirtSculptState;
    [SerializeField] private AK.Wwise.State _stoneSculptState;
    [SerializeField] private Voxel.Material _dirtSculptMaterials;
    [SerializeField] private Voxel.Material _stoneSculptMaterials;

    void Start()
    {
        // Initialise ground material dictionary
        _groundMaterialDictionary = new Dictionary<Voxel.Material, Switch>();
        foreach (GroundMaterialSwitch g in _groundMaterialSwitches)
        {
            _groundMaterialDictionary.Add(g.material, g.wwiseSwitch);
        }
    }

    public void Movement(bool grounded)
    {
        float distance = Vector3.Distance(transform.position, _prevPos);
        _lastStepDist += distance;
        _prevPos = transform.position;

        if (_lastStepDist > _footstepRate && grounded)
        {
            PlayFootstep();
            _lastStepDist = 0f;
        }

        _bubblespaceAnalyser.UpdateBubble();
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

    public void SetSculptMaterial(Voxel.Material material)
    {
        if ((material & _dirtSculptMaterials) > 0) _dirtSculptState.SetValue();
        else if ((material & _stoneSculptMaterials) > 0) _stoneSculptState.SetValue();
    }

    public void ToggleSculptSound(bool enabled)
    {
        if (enabled) _sculptPlayEvent.Post(_target);
        else _sculptStopEvent.Post(_target);
    }
}

[System.Serializable]
class GroundMaterialSwitch
{
    public AK.Wwise.Switch wwiseSwitch;
    public Voxel.Material material;
}