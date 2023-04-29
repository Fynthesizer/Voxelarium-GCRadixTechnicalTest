using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;

public class CreateMinimap : MonoBehaviour
{
    RawImage minimap;

    public RenderTexture renderTexture;
    Camera cam;
    GameObject player;
    World world;

    public bool continuouslyUpdate = true;
    public float updateRate = 1f;

    // Start is called before the first frame update
    void Start()
    {
        cam = gameObject.GetComponent<Camera>();
        player = GameObject.FindGameObjectWithTag("Player");
        world = GameObject.FindGameObjectWithTag("World").GetComponent<World>();

        if (continuouslyUpdate) StartCoroutine(UpdateMinimap());
    }

    public void OnWorldLoad()
    {
        cam.Render();
    }

    IEnumerator UpdateMinimap()
    {
        while (true)
        {
            transform.position = new Vector3(player.transform.position.x, world.chunkHeight + 10, player.transform.position.z);
            cam.Render();
            yield return new WaitForSeconds(updateRate);
        }
    }
}
