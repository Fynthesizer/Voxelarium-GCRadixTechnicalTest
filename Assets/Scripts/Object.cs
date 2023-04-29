using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Object : MonoBehaviour
{
    Rigidbody rb;
    public bool dislodged = false;
    public Transform rustleOrigin;

    /*
    public EventReference dislodgeEvent;
    public EventReference collisionEvent;
    */

    // Start is called before the first frame update
    void Start()
    {
        rb = gameObject.GetComponent<Rigidbody>();
    }
    public void Dislodge()
    {
        dislodged = true;
        rb.isKinematic = false;
        rb.velocity = Vector3.zero; //This stops the object from going flying when it is dislodged

        /*
        EventInstance dislodgeSound = RuntimeManager.CreateInstance(dislodgeEvent);
        dislodgeSound.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
        dislodgeSound.start();
        dislodgeSound.release();
        */
    }
}
