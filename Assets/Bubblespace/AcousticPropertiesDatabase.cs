using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


// This scriptable object contains acoustic properties (currently just absorption coefficient) for different voxel materials
// It is used by the Bubblespace Analyser to control WWise RTPCs based on the surrounding materials
[CreateAssetMenu(menuName = "Bubblespace/Acoustic Properties Database")]
public class AcousticPropertiesDatabase : ScriptableObject
{
    public List<AcousticProperties> AcousticProperties;

    public AcousticProperties GetProperties(Voxel.Material material)
    {
        return AcousticProperties.Find(x => x.Material == material);
    }
}

[System.Serializable]
public struct AcousticProperties
{
    public Voxel.Material Material;
    public float Absorption;
}