using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


public class InteractionManager : MonoBehaviour
{
    GameObject target;
    Camera cam;
    Inventory inventory;
    World world;
    UIManager ui;

    bool targetVisible;
    Vector3 targetPosition;
    Quaternion targetRotation;
    float targetSize = 1f;

    public float minDistance = 2;
    public float maxDistance = 100;
    public float placeSpeed = 0.05f;
    public float placeRadius = 2;

    public bool isTerraforming = false;

    public Material targetMaterial;
    public Mesh defaultTargetMesh;

    /*
    [Header("Sound Effects")]
    private EventInstance digSound;
    public FMODUnity.EventReference digEvent;
    */

    Dictionary<Voxel.Material, int> materialIndexDictionary = new Dictionary<Voxel.Material, int>()
        {
            { Voxel.Material.Grass, 0 },
            { Voxel.Material.Dirt, 1 },
            { Voxel.Material.Stone, 2 },
            { Voxel.Material.Sand, 3 },
            { Voxel.Material.Snow, 1 },
        };

    void Start()
    {
        target = GameObject.FindGameObjectWithTag("Target");
        cam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        inventory = GetComponent<FirstPersonController>().inventory;
        world = GameManager.gm.world;
        ui = GameManager.gm.ui;

        /*
        digSound = FMODUnity.RuntimeManager.CreateInstance(digEvent);
        digSound.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(transform.position));
        */

        StartCoroutine(Interaction());
        UpdateTargetShape(inventory.ActiveItem());
    }

    void Update()
    {
        //Update target transform and visibility
        target.transform.position = Vector3.Lerp(target.transform.position, targetPosition, Time.deltaTime * 25f);
        if(targetRotation != Quaternion.identity && target.transform.rotation != Quaternion.identity) target.transform.rotation = Quaternion.Lerp(target.transform.rotation, targetRotation, Time.deltaTime * 25f);
        target.transform.localScale = new Vector3(targetSize, targetSize, targetSize);
        
        //digSound.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(targetPosition));

        MeshRenderer targetRenderer = target.GetComponent<MeshRenderer>();
        targetRenderer.enabled = targetVisible;
    }

    public void OnInventorySelection(InputValue value)
    {
        inventory.IncrementActiveSlot((int)value.Get<float>());
        ui.UpdateInventory();
        UpdateTargetShape(inventory.ActiveItem());
    }

    public void OnNumberKeys(InputValue value)
    {
        inventory.SetActiveSlot((int)value.Get<float>());
        ui.UpdateInventory();
        UpdateTargetShape(inventory.ActiveItem());
    }

    void UpdateTargetShape(Item item)
    {
        MeshRenderer targetRenderer = target.GetComponent<MeshRenderer>();

        switch (item)
        {
            case SubstanceItem s:
                target.GetComponent<MeshFilter>().mesh = defaultTargetMesh;
                targetRenderer.material = targetMaterial;
                break;
            case ToolItem t:
                target.GetComponent<MeshFilter>().mesh = defaultTargetMesh;
                targetRenderer.material = targetMaterial;
                break;
            case ObjectItem o:
                Mesh objectMesh = o.objectPrefab.GetComponent<MeshFilter>().sharedMesh;
                target.GetComponent<MeshFilter>().mesh = objectMesh;
                var materials = new Material[o.objectPrefab.GetComponent<MeshRenderer>().sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = targetMaterial;
                targetRenderer.materials = materials;
                break;
            default:
                break;
        }
    }

    private IEnumerator Interaction()
    {
        while (true)
        {
            MeshRenderer targetRenderer = target.GetComponent<MeshRenderer>();
            RaycastHit hit;
            Ray ray = cam.GetComponent<Camera>().ScreenPointToRay(new Vector2(Screen.width / 2, Screen.height / 2));

            int layerMask = 1 << 6;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask) && target != null && hit.distance > minDistance && hit.distance < maxDistance)
            {
                targetVisible = true;
                targetPosition = hit.point;
                targetSize = placeRadius * 2f;
            }
            else targetVisible = false;

            placeRadius += (Mouse.current.scroll.ReadValue().y / 960);
            placeRadius = Mathf.Clamp(placeRadius, 1, 10);

            //Place
            if (Mouse.current.leftButton.isPressed == true &&
                Physics.Raycast(ray, out hit, maxDistance, layerMask) &&
                hit.distance > minDistance && hit.distance < maxDistance)
            {
                if (!isTerraforming)
                {
                    //digSound.start();
                    isTerraforming = true;
                }

                //digSound.setParameterByName("Material", materialIndexDictionary[s.material]);
                world.PlaceTerrain(hit.point, placeSpeed, placeRadius, Voxel.Material.Dirt);
            }
            //Dig
            else if (Mouse.current.rightButton.isPressed == true &&
                Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask) &&
                hit.distance < maxDistance)
            {
                if (!isTerraforming)
                {
                    //digSound.start();
                    isTerraforming = true;
                }

                Voxel.Material targetSubstance = world.GetVoxel(hit.point).material;
                materialIndexDictionary.TryGetValue(targetSubstance, out int soundIndex);
                //digSound.setParameterByName("Material", soundIndex);
                world.ModifyTerrain(hit.point, -placeSpeed,placeRadius);
            }

            //No action
            else if (isTerraforming)
            {
                //digSound.stop(STOP_MODE.ALLOWFADEOUT);
                isTerraforming = false;
            }

            yield return new WaitForSeconds(0.01f);
        }
    }
}
