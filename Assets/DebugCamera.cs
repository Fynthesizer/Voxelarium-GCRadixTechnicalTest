using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugCamera : MonoBehaviour
{
    // Attach this script to a camera, this will make it render in wireframe
    void OnPreRender()
    {
        GL.wireframe = true;
    }
}
