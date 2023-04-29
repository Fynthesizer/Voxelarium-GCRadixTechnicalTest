using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Structure : MonoBehaviour
{
    Chunk chunk;

    public StructureVoxel[,,] structureGrid;

    // Start is called before the first frame update
    void Start()
    {
        chunk = transform.parent.GetComponent<Chunk>();
        structureGrid = new StructureVoxel[chunk.width, chunk.height, chunk.length];
    }
}

