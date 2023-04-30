using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AK.Wwise;

public class SplashDetection : MonoBehaviour
{
    public float splashVelocity = 3f;
    [SerializeField] private AK.Wwise.Event _splashEvent;
    public GameObject splashObject;

    private void OnTriggerEnter(Collider other)
    {
        float velocity;
        if (other.gameObject.tag == "Player") velocity = other.GetComponent<FirstPersonController>()._verticalVelocity;
        else if (other.attachedRigidbody != null) velocity = other.attachedRigidbody.velocity.y;
        else return;

        if(velocity < -splashVelocity) { 
            Vector3 contactPoint = other.gameObject.GetComponent<Collider>().ClosestPointOnBounds(transform.position);
            StartCoroutine(CreateSplash(contactPoint));
        }
    }

    public IEnumerator CreateSplash(Vector3 position)
    {
        splashObject.SetActive(true);
        splashObject.transform.position = new Vector3(position.x, 10f, position.z);
        splashObject.GetComponent<ParticleSystem>().Play();

        _splashEvent.Post(splashObject);

    yield return new WaitForSeconds(1f);

        splashObject.GetComponent<ParticleSystem>().Stop();
        splashObject.SetActive(false);
    }
}
