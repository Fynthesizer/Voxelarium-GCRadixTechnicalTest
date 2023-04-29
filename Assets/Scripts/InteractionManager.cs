using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


public class InteractionManager : MonoBehaviour
{
    GameObject target;
    Camera cam;
    World world;
    UIManager ui;

    [SerializeField] PlayerAudioController _audioController;

    bool targetVisible;
    Vector3 targetPosition;
    Quaternion targetRotation;
    float targetSize = 1f;

    public float minDistance = 2;
    public float maxDistance = 100;
    public float sculptSpeed = 0.05f;
    public float sculptRadius = 2;

    public bool isSculpting = false;

    public Material targetMaterial;
    public Mesh defaultTargetMesh;

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
        world = GameManager.gm.world;
        ui = GameManager.gm.ui;

        /*
        digSound = FMODUnity.RuntimeManager.CreateInstance(digEvent);
        digSound.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(transform.position));
        */

        StartCoroutine(Interaction());
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
                targetSize = sculptRadius * 2f;
            }
            else targetVisible = false;

            sculptRadius += (Mouse.current.scroll.ReadValue().y / 960);
            sculptRadius = Mathf.Clamp(sculptRadius, 1, 10);

            //Place
            if (Mouse.current.leftButton.isPressed == true &&
                Physics.Raycast(ray, out hit, maxDistance, layerMask) &&
                hit.distance > minDistance && hit.distance < maxDistance)
            {
                if (!isSculpting)
                {
                    _audioController.ToggleSculptSound(true);
                    _audioController.SetSculptMaterial(Voxel.Material.Dirt);
                    isSculpting = true;
                }

                //digSound.setParameterByName("Material", materialIndexDictionary[s.material]);
                world.PlaceTerrain(hit.point, sculptSpeed, sculptRadius, Voxel.Material.Dirt);
            }
            //Dig
            else if (Mouse.current.rightButton.isPressed == true &&
                Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask) &&
                hit.distance < maxDistance)
            {
                if (!isSculpting)
                {
                    _audioController.ToggleSculptSound(true);
                    isSculpting = true;
                }

                Voxel.Material targetMaterial = world.GetVoxel(hit.point).material;
                _audioController.SetSculptMaterial(targetMaterial);
                world.ModifyTerrain(hit.point, -sculptSpeed,sculptRadius);
            }

            //No action
            else if (isSculpting)
            {
                _audioController.ToggleSculptSound(false);
                isSculpting = false;
            }

            yield return new WaitForSeconds(0.01f);
        }
    }
}
