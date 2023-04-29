using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class UIManager : MonoBehaviour
{
    public float guidelineLength = 150f;

    private GameManager gameManager;
    private WeatherManager weather;
    private World world;

    public Text worldSeed;

    public GameObject inventoryUI;
    public GameObject playerStatsUI;

    private GameObject player;
    public GameObject loadScreen;
    private Slider loadBar;
    private Camera cam;

    private Camera minimapCam;
    private Camera iconCam;
    public Image playerIcon;
    private RawImage minimap;

    public void Setup()
    {
        //windIndicator = GameObject.FindGameObjectWithTag("WindIndicator");
        player = GameObject.FindGameObjectWithTag("Player");
        cam = Camera.main;

        minimapCam = GameObject.FindGameObjectWithTag("MinimapCamera").GetComponent<Camera>();
        iconCam = GameObject.FindGameObjectWithTag("IconCamera").GetComponent<Camera>();
        minimap = GameObject.FindGameObjectWithTag("Minimap").GetComponent<RawImage>();

        gameManager = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameManager>();
        world = GameObject.FindGameObjectWithTag("World").GetComponent<World>();

        minimapCam.orthographicSize = ((world.chunkWidth - 1) * world.renderDistance * world.voxelSize) - (world.chunkWidth * 2);
        iconCam.orthographicSize = ((world.chunkWidth - 1) * world.renderDistance * world.voxelSize) - (world.chunkWidth * 2);

        weather = gameManager.weatherManager;
        //float windAngle = Vector2.Angle(new Vector2(1,0), weather.windDirection);
        //float windSpeed = Mathf.Round(weather.windSpeed * 10000) / 100;
        //windIndicator.transform.GetChild(0).GetComponent<Image>().rectTransform.eulerAngles = new Vector3(0, 0, windAngle * - 1);
        //windIndicator.transform.GetChild(1).GetComponent<Text>().text = windSpeed + " km/h";

        UpdateUI();
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

    public void UpdateMarker(GameObject target, GameObject marker)
    {

        //marker.SetActive(true);

        float minX = marker.GetComponent<Image>().GetPixelAdjustedRect().width / 2;
        float maxX = Screen.width - minX;

        float minY = marker.GetComponent<Image>().GetPixelAdjustedRect().height / 2;
        float maxY = Screen.height - minY;

        Vector2 pos = cam.WorldToScreenPoint(target.transform.position);

        if (Vector3.Dot((target.transform.position - player.transform.position), player.transform.forward) < 0)
        {
            if (pos.x < Screen.width / 2)
            {
                pos.x = maxX;
            }
            else
            {
                pos.x = minX;
            }
        }

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        marker.GetComponent<RectTransform>().position = pos;
        int dist = Mathf.RoundToInt(Vector3.Distance(player.transform.position, target.transform.position));
        marker.transform.GetChild(0).GetComponent<Text>().text = dist + "m";
    }

    public void UpdateInventory()
    {
        Inventory inventory = player.GetComponent<FirstPersonController>().inventory;
        TextMeshProUGUI itemName = inventoryUI.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
        Transform hotbar = inventoryUI.transform.GetChild(0);

        for (int i = 0; i < hotbar.childCount; i++)
        {
            Image inventorySlot = hotbar.GetChild(i).GetComponent<Image>();
            if (inventory.activeSlot == i) inventorySlot.color = new Color(1f, 1f, 1f, 0.66f);
            else inventorySlot.color = new Color(0f, 0f, 0f, 0.33f);

            Image inventorySprite = inventorySlot.transform.GetChild(0).GetComponent<Image>();
            TextMeshProUGUI inventoryQuantity = inventorySlot.transform.GetChild(1).GetComponent<TextMeshProUGUI>();

            if (inventory.slots[i].item != null)
            {
                inventorySprite.sprite = inventory.slots[i].item.itemIcon;
                inventorySprite.enabled = true;
                if (inventory.slots[i].item.stackable) inventoryQuantity.text = inventory.slots[i].quantity.ToString();
                else inventoryQuantity.text = ""; //Don't show quantity if item isn't stackable
            }
            else
            {
                inventorySprite.sprite = null;
                inventorySprite.enabled = false;
                inventoryQuantity.text = "";
            }
        }

        if (inventory.ActiveItem() != null) itemName.text = inventory.ActiveItem().itemName;
        else itemName.text = "Nothin'";
    }
}
