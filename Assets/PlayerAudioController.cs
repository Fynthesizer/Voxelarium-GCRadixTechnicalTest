using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using AK.Wwise;
using UnityEngine.InputSystem.LowLevel;

public class PlayerAudioController : MonoBehaviour
{
    [SerializeField] private BubblespaceAnalyser _bubblespaceAnalyser;
    [SerializeField] private FirstPersonController _firstPersonController;
    [SerializeField] private GameObject _target;
    [SerializeField] private InputActionAsset _inputActions;

    [Header("Footsteps")]
    [SerializeField] private AK.Wwise.Event _footstepEvent;
    [SerializeField] private AK.Wwise.RTPC _submersionRTPC;
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

    [Header("Acoustics Test")]
    [SerializeField] private AK.Wwise.Event _snapEvent;

    void Start()
    {
        // Initialise ground material dictionary
        _groundMaterialDictionary = new Dictionary<Voxel.Material, Switch>();
        foreach (GroundMaterialSwitch g in _groundMaterialSwitches)
        {
            _groundMaterialDictionary.Add(g.material, g.wwiseSwitch);
        }

        _inputActions.FindAction("Snap").performed += SnapButtonPressed;
    }

    private void SnapButtonPressed(InputAction.CallbackContext context) => _snapEvent.Post(gameObject);

    private void Update()
    {
        float distance = Vector3.Distance(transform.position, _prevPos);
        _lastStepDist += distance;
        _prevPos = transform.position;

        if (!_firstPersonController.enabled) return;

        if (_lastStepDist > _footstepRate && (_firstPersonController.Grounded || _firstPersonController.Submerged))
        {
            SetGroundMaterial(_firstPersonController.GetGroundMaterial());
            print(_firstPersonController.Submerged);
            _submersionRTPC.SetValue(gameObject, _firstPersonController.Submerged ? 1f : 0f);
            PlayFootstep();
            _lastStepDist = 0f;
        }
    }


    public void PlayFootstep() => _footstepEvent.Post(gameObject);

    public void SetGroundMaterial(Voxel.Material material)
    {
        if (material == Voxel.Material.Air || material == 0) return;
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