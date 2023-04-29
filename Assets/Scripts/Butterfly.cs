using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Butterfly : MonoBehaviour
{
    Vector3 basePos;
    Vector3 prevPos;
    Vector3 direction;
    public float speed = 0.5f;
    public float wanderRange = 1;

    public int type;
    public GameObject butterfly;
    public GameObject firefly;

    // Start is called before the first frame update
    void Start()
    {
        basePos = transform.position;

        StartCoroutine("CheckTime");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void FixedUpdate()
    {
        //velocity = new Vector3(Mathf.PerlinNoise(Time.time, 0) - 0.5f, Mathf.PerlinNoise(Time.time, 1) - 0.5f, Mathf.PerlinNoise(Time.time, 2) - 0.5f).normalized * speed;
        //transform.position += velocity;
        Vector3 prevPos = transform.position;
        Vector3 offsetPos = new Vector3(Mathf.PerlinNoise(Time.time * speed, basePos.x * 10) - 0.5f, Mathf.PerlinNoise(Time.time * speed, basePos.y * 10) - 0.5f, Mathf.PerlinNoise(Time.time * speed, basePos.z * 10) - 0.5f);
        transform.position = basePos + (offsetPos * wanderRange);
        direction = (prevPos - transform.position).normalized;
        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(direction, Vector3.up), Time.deltaTime * 10f);
    }

    void SetType(int _type)
    {
        if(_type == 0)
        {
            butterfly.SetActive(true);
            firefly.SetActive(false);
        }
        else if (_type == 1)
        {
            butterfly.SetActive(false);
            firefly.SetActive(true);
        }
        type = _type;
    }

    IEnumerator CheckTime()
    {
        while (true) { 
            float time = GameManager.gm.weatherManager.time;

            if (time < 6 || time > 18) SetType(1);
            else SetType(0);

            yield return new WaitForSeconds(Random.Range(5f,20f));
        }
    }

    private void OnBecameInvisible()
    {
        enabled = false;
        print("Disabled");
    }

    private void OnBecameVisible()
    {
        enabled = true;
        print("Enabled");
    }
}