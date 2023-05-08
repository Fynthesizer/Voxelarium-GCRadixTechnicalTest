using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    public float guidelineLength = 150f;

    private GameManager gameManager;
    private WeatherManager weather;
    private World world;

    public Text worldSeed;

    [SerializeField] private GameObject player;
    public GameObject loadScreen;
    private Slider loadBar;
    private Camera cam;

    private Camera minimapCam;
    private Camera iconCam;
    public Image playerIcon;
    private RawImage minimap;

    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private GameObject helpScreen;

    public void Setup()
    {
        cam = Camera.main;

        minimapCam = GameObject.FindGameObjectWithTag("MinimapCamera").GetComponent<Camera>();
        iconCam = GameObject.FindGameObjectWithTag("IconCamera").GetComponent<Camera>();
        minimap = GameObject.FindGameObjectWithTag("Minimap").GetComponent<RawImage>();

        gameManager = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameManager>();
        world = GameObject.FindGameObjectWithTag("World").GetComponent<World>();

        minimapCam.orthographicSize = ((world.chunkWidth - 1) * world.renderDistance * world.voxelSize) - (world.chunkWidth * 2);
        iconCam.orthographicSize = ((world.chunkWidth - 1) * world.renderDistance * world.voxelSize) - (world.chunkWidth * 2);

        weather = gameManager.weatherManager;

        inputActions.FindAction("ToggleHelp").performed += ToggleHelpScreen;
        helpScreen.SetActive(false);

        UpdateUI();
    }

    public void ToggleHelpScreen(InputAction.CallbackContext context)
    {
        helpScreen.SetActive(!helpScreen.activeSelf);
    }

    private void OnGUI()
    {
        playerIcon.rectTransform.eulerAngles = new Vector3(0, 0, (-player.transform.eulerAngles.y));
    }

    public void OnWorldLoad()
    {
        loadScreen.SetActive(false);
    }

    public void UpdateUI()
    {
        FirstPersonController playerData = player.GetComponent<FirstPersonController>();
        worldSeed.text = "Seed: " + gameManager.world.worldSettings.seed;
    }
}
