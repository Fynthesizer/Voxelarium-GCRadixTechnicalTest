using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System;
using System.Linq;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
    public WorldSettings worldSettings;

    public static GameManager gm;
    public WeatherManager weatherManager;
    public World world;
    public UIManager ui;

    GameObject player;

    public ItemDatabase itemDatabase;

    public int seed = 10;
    public int size = 5;

    public float maxSteepness = 5f;

    private void Awake()
    {
        if (gm == null) { 
            DontDestroyOnLoad(gameObject);
            gm = this;
        }
        else if (gm != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (SceneManager.GetActiveScene().name == "Game")
        {
            //If starting from the game scene, randomize the world seed
            worldSettings.seed = Random.Range(0, 99999999);
            SetupGame();
        }
    }

    Vector3 FindSpawnLocation(Vector3 origin, float range)
    {
        int attempts = 0;
        while(attempts < 500)
        {
            attempts += 1;
            Vector3 attemptPos = new Vector3(origin.x + Random.Range(-range, range), world.chunkHeight, origin.z + Random.Range(-range, range));
            RaycastHit hit;

            int layerMask = (1 << 6) | (1 << 4); //Cast against terrain and water
            Ray ray = new Ray(attemptPos, Vector3.down);
            if (Physics.Raycast(ray, out hit, world.chunkHeight, layerMask))
            {
                if (hit.collider.gameObject.layer == 4) continue;
                else return hit.point;
            }
            else continue;
        }
        
        return new Vector3(0, 10f, 0); //If location couldn't be found, return this
    }

    public void OnWorldLoad()
    {
        Vector3 startPos = FindSpawnLocation(Vector3.zero, world.preloadSize * world.preloadSize);
        player.transform.position = startPos + Vector3.up;

        GameObject.FindGameObjectWithTag("MinimapCamera").GetComponent<CreateMinimap>().OnWorldLoad();
        weatherManager.OnWorldLoad();
        ui.OnWorldLoad();
        player.GetComponent<FirstPersonController>().enabled = true;
    }

    IEnumerator LoadWorld()
    {
        AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(1);
        Slider loadBar = GameObject.FindGameObjectWithTag("LoadBar").GetComponent<Slider>();

        while (!sceneLoad.isDone)
        {
            loadBar.value = Mathf.Clamp01(sceneLoad.progress / 0.9f);
            yield return null;
        }

        SetupGame();
    }

    public void SetupGame()
    {
        world = GameObject.FindGameObjectWithTag("World").GetComponent<World>();
        world.GenerateWorld(worldSettings);

        player = GameObject.FindGameObjectWithTag("Player");
        ui = GameObject.FindGameObjectWithTag("Canvas").GetComponent<UIManager>();
        weatherManager = GameObject.FindGameObjectWithTag("WeatherManager").GetComponent<WeatherManager>();
        weatherManager.Setup();
        ui.Setup();
    }

    public void NewGame(WorldSettings settings)
    {
        worldSettings = settings;
        size = 25;
        StartCoroutine(LoadWorld());
    }

    public static Item GetItemByID(string ID)
    {
        Item item = gm.itemDatabase.items.FirstOrDefault(i => i.itemID == ID);
        if (item != null) return item;
        else
        {
            Debug.LogError("Couldn't find item with specified ID");
            return null;
        }
    }
    public static Item GetItemByName(string name)
    {
        return gm.itemDatabase.items.FirstOrDefault(i => i.itemName == name);
    }
}

[Serializable]
public class WorldSettings
{
    public int seed;
}