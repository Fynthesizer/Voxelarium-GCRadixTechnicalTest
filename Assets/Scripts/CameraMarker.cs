using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMarker : MonoBehaviour
{
    public bool visible;

    private void OnBecameVisible()
    {
        visible = true;
    }

    private void OnBecameInVisible()
    {
        visible = false;
    }
}
