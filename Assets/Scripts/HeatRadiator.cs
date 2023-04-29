using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeatRadiator : MonoBehaviour
{
    public float temperature;
    public float radius;

    public float GetTemperature(Vector3 position)
    {
        float distance = Vector3.Distance(transform.position, position);
        //Exponential attenuation
        float temp;
        if (distance < radius)
        {
            float attenuation = Mathf.Pow(((distance / radius) - 1), 2);
            attenuation = Mathf.Clamp(attenuation, 0f, 1f);
            temp = temperature * attenuation;
        }
        else temp = 0f;
        return temp;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
