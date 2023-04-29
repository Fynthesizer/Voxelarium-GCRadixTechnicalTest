using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public GameManager gameManager;
    public int seed;
    public int size;
    public int players;

    public WorldSettings worldSettings;

    // Start is called before the first frame update
    void Start()
    {
        worldSettings = new WorldSettings();
        gameManager = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameManager>();
        SetSeed(null);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public void SetSeed(string _seed)
    {
        if(_seed == "" || _seed == null) worldSettings.seed = Random.Range(0, 99999999); //If seed field is empty, generate random seed
        else { 
            bool success = int.TryParse(_seed, out worldSettings.seed); //Try parse string to int
            if(!success) worldSettings.seed = _seed.GetHashCode(); //If it fails, use the hash code of the string
        }
    }

    public void NewGame()
    {
        gameManager.NewGame(worldSettings);
    }
}
