using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

using UnityEditor;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Burst;

using ProceduralNoiseProject;
using System;
using Random = UnityEngine.Random;

public class Chunk : MonoBehaviour
{
    public int seed = 0;

    public int width = 32;
    public int height = 32;
    public int length = 32;

    public int snowHeight = 98;

    //public PhysicMaterial physicMat;

    public Vector2Int chunkPos;
    public float voxelSize = 2;

    public Voxel[] voxels;
    public StructureVoxel[,,] structureVoxels;

    //List<GameObject> meshes = new List<GameObject>();

    World world;
    public GameObject terrain;
    public GameObject objects;
    public GameObject structures;

    //Transform water;

    public float treeThreshold = 0.5f;
    public float grassThreshold = 0.3f;
    public float treeChance = 0.75f;
    public float flowerChance = 0.05f;
    public int treeSpacing = 4;
    public float rockChance = 0.01f;
    public float mushroomChance = 0.05f;
    public float mushroomThreshold = 0.5f;
    public float butterflyChance = 0.005f;

    [Header("Object Prefabs")]
    public GameObject[] trees;
    public GameObject[] rocks;
    public GameObject grass;
    public GameObject reeds;
    public GameObject[] flowers;
    public GameObject mushroom;
    public GameObject lilypad;
    public GameObject butterfly;

    public Bounds chunkBounds;

    public float fertilityNoiseFrequency = 1f;
    float[,] fertilityMap;

    public float averageStep = 0.75f;

    public bool playLoadAnimation = true;

    public bool voxelDebug;

    public GameObject floor;

    public void SetupChunk(Voxel[] voxels)
    {
        world = transform.parent.gameObject.GetComponent<World>();
        this.voxels = voxels;
        structureVoxels = new StructureVoxel[width, height, length];
        chunkBounds = new Bounds(new Vector3(transform.position.x + ((width * voxelSize) / 2), transform.position.y + ((height * voxelSize) / 2), transform.position.z + ((length * voxelSize) / 2)),
            new Vector3(width * voxelSize, height * voxelSize, length * voxelSize));

        GenerateFertilityMap();
        //SetupMaterials();

        //Create mesh
        Mesh mesh = new Mesh();

        terrain.transform.parent = transform;
        terrain.transform.localPosition = Vector3.zero;
        terrain.isStatic = true;
        terrain.GetComponent<MeshFilter>().mesh = mesh;
        terrain.GetComponent<MeshCollider>().sharedMesh = mesh;

        GenerateObjects();
        if (playLoadAnimation) StartCoroutine(LoadAnimation());
    }

    public void GenerateFertilityMap()
    {
        INoise fertilityNoise = new PerlinNoise(seed, fertilityNoiseFrequency);
        fertilityMap = new float[width, length];
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                fertilityMap[x, z] = fertilityNoise.Sample2D((x + transform.position.x) / 128f, (z + transform.position.z) / 128f);
            }
        }
    }

    public void SetupMaterials()
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                float depth = 0;

                for (int y = height - 1; y >= 0; y--)
                {
                    int index = (z * height * width) + (y * width) + x;

                    if (voxels[index].density > 0f) depth++;
                    if (y < 12f) depth++;
                    else if (depth > 0) depth -= 0.25f;

                    float fertility = fertilityMap[x,z] + (y * 0.04f);
                    if (y < 20 && depth < 6 && fertility < 0.2f)
                    {
                        /*
                        if (fertility < -0.2f) voxels[index].material = Voxel.Material.Stone;
                        
                        else voxels[index].material = Voxel.Material.Sand;
                        */
                        //if (fertility < 0.2f) voxels[index].material = Voxel.Material.Sand;
                        voxels[index].material = Voxel.Material.Sand;
                    }
                    else if (depth < 3)
                    {
                        if (y > snowHeight) voxels[index].material = Voxel.Material.Snow;
                        //else if (y > snowHeight - 16) voxels[index].material = Voxel.Material.Dirt;
                        else voxels[index].material = Voxel.Material.Grass;
                    }
                    else if (depth < 10) voxels[index].material = Voxel.Material.Dirt;
                    else voxels[index].material = Voxel.Material.Stone;
                }
            }
        }
    }

    public void PlacePart(Vector3 position, PartType type)
    {
        Vector3Int gridPos = new Vector3Int(Mathf.FloorToInt(position.x), Mathf.RoundToInt(position.y), Mathf.FloorToInt(position.z));
        gridPos.Clamp(Vector3Int.zero, new Vector3Int(width, height, length));
        switch(type)
        {
            case PartType.Floor:
                if(!structureVoxels[gridPos.x, gridPos.y, gridPos.z].floor) { 
                    structureVoxels[gridPos.x, gridPos.y, gridPos.z].floor = true;
                    Instantiate(floor, (transform.position + gridPos) + new Vector3(0.5f,0f,0.5f), Quaternion.identity, structures.transform);
                }
                break;
            case PartType.Wall:
                if (!structureVoxels[gridPos.x, gridPos.y, gridPos.z].westWall)
                {
                    structureVoxels[gridPos.x, gridPos.y, gridPos.z].westWall = true;
                    Instantiate(floor, (transform.position + gridPos) + new Vector3(0.5f, 0f, 0.5f), Quaternion.identity, structures.transform);
                }
                break;
            case PartType.Block:
                if (!structureVoxels[gridPos.x, gridPos.y, gridPos.z].center)
                {
                    structureVoxels[gridPos.x, gridPos.y, gridPos.z].center = true;
                    Instantiate(floor, (transform.position + gridPos) + new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity, structures.transform);
                }
                break;
            default:
                break;
        }
        
    }

    float GetOffset(float v1, float v2)
    {
        float delta = v2 - v1;
        return (delta == 0.0f) ? 0 : (0 - v1) / delta;
    }

    public void WeldDuplicateVerts(ref List<Vector3> verts, ref List<int> indices, ref List<Color> colors)
    {
        //Remove duplicate vertices
        var vertexDic = new Dictionary<Vector3, int>();
        var newVerts = new List<Vector3>();
        var newIndices = new List<int>();
        var newColors = new List<Color>();
        int index = 0;

        for (int i = verts.Count - 1; i >= 0; i--)
        {
            Vector3 vert = verts[i];
            int sharedVertIndex;

            if (vertexDic.TryGetValue(vert, out sharedVertIndex))
            {
                newIndices.Add(sharedVertIndex);
            }
            else
            {
                vertexDic.Add(vert, index);
                newVerts.Add(vert);
                newColors.Add(colors[i]);
                newIndices.Add(index);

                index++;
            }

        }

        verts = newVerts;
        indices = newIndices;
        colors = newColors;
        indices.Reverse();
    }

    public void RemoveSmallTriangles(ref List<Vector3> verts, ref List<int> indices, float minSize)
    {
        for (int i = 0; i < indices.Count; i += 3)
        {
            var tri = new Vector3[3];
            for (int j = 0; j < 3; j++) tri[j] = verts[indices[i + j]];
            var dist = new float[3];
            dist[0] = Vector3.Distance(tri[0], tri[1]);
            dist[1] = Vector3.Distance(tri[1], tri[2]);
            dist[2] = Vector3.Distance(tri[2], tri[0]);
            float area = 0.25f * Mathf.Sqrt((dist[0] + dist[1] + dist[2]) * (-dist[0] + dist[1] + dist[2]) * (dist[0] - dist[1] + dist[2]) * (dist[0] + dist[1] - dist[2]));
            if (area < minSize)
            {
                Vector3 avgPos = (tri[0] + tri[1] + tri[2]) / 3;
                for (int j = 0; j < 3; j++) verts[indices[i + j]] = avgPos;
            }
        }
    }

    public void AverageVerts(List<Vector3> verts, List<int> indices, float step, float offset)
    {
        var nearIndices = new List<int>[Mathf.CeilToInt(width / step), Mathf.CeilToInt(height / step), Mathf.CeilToInt(length / step)];

        for (int i = 0; i < indices.Count; i++)
        {
            Vector3 vert = verts[indices[i]];
            int x = Mathf.Clamp(Mathf.RoundToInt((((vert.x) / voxelSize) + offset) / step), 0, Mathf.CeilToInt(width / step));
            int y = Mathf.Clamp(Mathf.RoundToInt((((vert.y) / voxelSize) + offset) / step), 0, Mathf.CeilToInt(height / step));
            int z = Mathf.Clamp(Mathf.RoundToInt((((vert.z) / voxelSize) + offset) / step), 0, Mathf.CeilToInt(length / step));

            if (nearIndices[x, y, z] == null) nearIndices[x, y, z] = new List<int>();

            nearIndices[x, y, z].Add(indices[i]);
        }

        int edgeDist = Mathf.CeilToInt(1 / step);

        for (int x = edgeDist; x < nearIndices.GetLength(0) - edgeDist; x++)
        {
            for (int y = edgeDist; y < nearIndices.GetLength(1) - edgeDist; y++)
            {
                for (int z = edgeDist; z < nearIndices.GetLength(2) - edgeDist; z++)
                {
                    if (nearIndices[x, y, z] != null)
                    {

                        Vector3 avgPos = Vector3.zero;
                        Vector3 closest = new Vector3();
                        Vector3 voxPos = new Vector3(x * step, y * step, z * step);

                        for (int i = 0; i < nearIndices[x, y, z].Count; i++)
                        {

                            Vector3 vert = verts[nearIndices[x, y, z][i]];
                            avgPos += vert;
                            if (i == 0) closest = vert;
                            else if (Vector3.Distance(vert, voxPos) < Vector3.Distance(closest, voxPos)) closest = vert;
                        }

                        avgPos /= nearIndices[x, y, z].Count;

                        for (int i = 0; i < nearIndices[x, y, z].Count; i++)
                        {
                            verts[nearIndices[x, y, z][i]] = avgPos;
                        }
                    }
                }
            }
        }
    }

    public int GetVoxelIndex(Vector3 point)
    {
        int fx = Mathf.RoundToInt(point.x);
        int fy = Mathf.RoundToInt(point.y);
        int fz = Mathf.RoundToInt(point.z);
        int index = (fz * height * width) + (fy * width) + fx;
        return index;
    }

    public Vector3 GetVoxelPosition(int id)
    {
        Vector3 p = new Vector3();
        p.z = Mathf.Floor(id / (height * width));
        p.y = Mathf.Floor((id % (height * width)) / width);
        p.x = Mathf.Floor((id % (height * width)) % width);
        return p;
    }

    public Voxel GetVoxel(Vector3 point)
    {
        int index = GetVoxelIndex(point);
        return voxels[index];
    }

    //Get information about the surface at a given x and z position
    public void GetSurface(Vector2Int pos, out float surface, out float steepness)
    {
        int[,] VertexOffset = new int[,]
        {
            {0, 0, 0},{1, 0, 0},{1, 0, 1},{0, 0, 1},
            {0, 1, 0},{1, 1, 0},{1, 1, 1},{0, 1, 1}
        };

        int baseY = 0;

        for (int y = height - 3; y >= 0; y--)
        {
            int index = pos.x + y * width + pos.y * width * height;
            if (voxels[index].density > 0f)
            {
                baseY = y;
                break;
            }
        }

        float[] cubeLevel = new float[4];
        float[] difference = new float[4];

        //Get the surface level of each edge in the cube
        for (int i = 0; i < 4; i++)
        {
            int ix1 = pos.x + VertexOffset[i, 0];
            int iy1 = baseY + VertexOffset[i, 1];
            int iz1 = pos.y + VertexOffset[i, 2];
            int ix2 = pos.x + VertexOffset[i + 4, 0];
            int iy2 = baseY + VertexOffset[i + 4, 1];
            int iz2 = pos.y + VertexOffset[i + 4, 2];

            int id1 = ix1 + iy1 * width + iz1 * width * height;
            int id2 = ix2 + iy2 * width + iz2 * width * height;

            float offset = GetOffset(voxels[id1].density, voxels[id2].density);
            cubeLevel[i] = baseY + offset;
        }

        surface = (cubeLevel[0] + cubeLevel[1] + cubeLevel[2] + cubeLevel[3]) / 4f;
        for (int i = 0; i < 4; i++)
        {
            difference[i] = Mathf.Abs(cubeLevel[i] - surface);
        }
        steepness = Mathf.Max(difference);
    }

    void GenerateObjects()
    {
        for (int x = 0; x < (width * voxelSize) - 1; x++)
        {
            for (int z = 0; z < (length * voxelSize) - 1; z++)
            {
                //Get the surface height and steepness at this position
                float surface, steepness;
                GetSurface(new Vector2Int(x, z), out surface, out steepness);

                float fx = (x + transform.position.x) / (128);
                float fz = (z + transform.position.z) / (128);

                float fertility = fertilityMap[x, z];

                Vector3 position = new Vector3(x + 0.5f, surface, z + 0.5f);

                //Forest
                if (fertility > treeThreshold && x % treeSpacing == 0 && z % treeSpacing == 0 && surface > 10 && steepness < 0.1f)
                {
                    GameObject obj;
                    //Trees
                    if (Random.value > mushroomChance || fertility < mushroomThreshold)
                    {
                        if (Random.value < treeChance)
                        {
                            int treeType = Random.Range(0, trees.Length);
                            obj = trees[treeType];
                            PlaceObject(obj, position, 1, 2, false);
                        }
                    }
                    //Mushrooms
                    else
                    {
                        obj = mushroom;
                        PlaceObject(obj, position, 0.5f, 2f, false);
                    }
                }

                //Rocks
                else if (Random.value < rockChance && steepness < 0.2f)
                {
                    int rockType = Random.Range(0, rocks.Length);
                    GameObject rock = rocks[rockType];
                    PlaceObject(rock, position, 1f, 2f, true);
                }

                //Grass, Reeds, Lily Pads
                else if (fertility > grassThreshold && steepness < 0.3f && surface < snowHeight)
                {
                    if (Random.value < (fertility - grassThreshold) * 2f)
                    {
                        float grassScale = fertility * 1.25f;
                        PlaceGrass(x, z, surface, grassScale);
                    }
                    if (Random.value < butterflyChance && surface > 10)
                    {
                        PlaceButterfly(x, z, surface, 2f);
                    }
                }

            }
        }
    }

    void PlaceObject(GameObject obj, Vector3 pos, float minSize, float maxSize, bool allowUnderwater)
    {
        Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));
        float scale = Random.Range(minSize, maxSize);
        float rotation = Random.Range(0, 360);
        Vector3 position = pos + offset + transform.position;

        GameObject newObject = Instantiate(obj, position, Quaternion.identity, objects.transform) as GameObject;
        newObject.transform.localScale = new Vector3(scale, scale, scale);
        newObject.transform.localEulerAngles = new Vector3(0, rotation, 0);
        newObject.GetComponent<Rigidbody>().mass *= scale;
    }

    void PlaceGrass(int x, int z, float level, float scale)
    {
        float rotation = Random.Range(0, 360);
        Vector3 position = new Vector3(x, level, z) + transform.position;

        GameObject newObject;
        //Grass and Flowers
        if (level > 10f)
        {
            if (Random.value > flowerChance) newObject = Instantiate(grass, position, Quaternion.identity, objects.transform);
            else
            {
                int flowerType = Random.Range(0, flowers.Length);
                newObject = Instantiate(flowers[flowerType], position, Quaternion.identity, objects.transform);
            }
        }
        //Reeds
        else if (level < 10f && level > 9f && Random.value > 0.85f)
        {
            newObject = Instantiate(reeds, position, Quaternion.identity, objects.transform);
        }
        //Lily pad
        else if (level < 9f && level > 8f && Random.value > 0.85f)
        {
            Vector3 pos = new Vector3(position.x, 10f, position.z);
            newObject = Instantiate(lilypad, pos, Quaternion.identity, objects.transform);
        }
        else return;

        newObject.transform.localScale = new Vector3(scale, scale, scale);
        newObject.transform.localEulerAngles = new Vector3(0, rotation, 0);
    }

    void PlaceButterfly(int x, int z, float level, float yOffset)
    {
        Vector3 position = new Vector3(x, level + yOffset, z) + transform.position;
        GameObject newObject;
        newObject = Instantiate(butterfly, position, Quaternion.identity, objects.transform);

    }

    public void SetMesh(List<Vector3> verts, List<int> indices, List<Color> colors)
    {

        Mesh mesh = terrain.GetComponent<MeshFilter>().mesh;
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(indices, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        terrain.GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    public void SetMesh(NativeArray<float3> verts, List<int> indices, NativeArray<float4> colors)
    {

        Mesh mesh = terrain.GetComponent<MeshFilter>().mesh;
        mesh.Clear();
        mesh.SetVertices<float3>(verts);
        mesh.SetTriangles(indices, 0);
        mesh.SetColors<float4>(colors);
        mesh.RecalculateNormals();
        terrain.GetComponent<MeshCollider>().sharedMesh = mesh;

        verts.Dispose();
        colors.Dispose();
    }

    public List<Color> GenerateVertexColors(Mesh mesh)
    {
        var colors = new List<Color>();
        var verts = mesh.vertices;

        //Run for each vert
        for (int i = 0; i < verts.Length; i++)
        {
            Color c;
            Vector3 vert = verts[i];
            int voxI = GetVoxelIndex(vert);
            c = TerrainColors[voxels[voxI].material];
            colors.Add(c);
        }

        return colors;
    }

    private static readonly Dictionary<Voxel.Material, Color32> TerrainColors = new Dictionary<Voxel.Material, Color32>()
    {
        { Voxel.Material.Dirt, new Color32(79,56,33,255)},
        { Voxel.Material.Grass, new Color32(61,108,46,255)},
        { Voxel.Material.Sand, new Color32(192,195,108,255)},
        { Voxel.Material.Stone, new Color32(75,75,75,255)},
        { Voxel.Material.Snow, new Color32(240,240,240,255)},
    };

    IEnumerator LoadAnimation()
    {
        float t = 0f;
        float yOffset;
        while (t < 1f)
        {
            t += Time.deltaTime;
            float bezier = t * t * (3.0f - 2.0f * t);
            yOffset = Mathf.Lerp(-64f, 0f, bezier);
            transform.position = new Vector3(transform.position.x, yOffset, transform.position.z);
            yield return null;
        }
        yOffset = 0f;
        transform.position = new Vector3(transform.position.x, yOffset, transform.position.z);
    }

#if UNITY_EDITOR
    public void OnDrawGizmosSelected()
    {
        if (voxelDebug)
        {
            Gizmos.color = Color.white;
            GUIStyle style = GUI.skin.GetStyle("Label");
            style.alignment = TextAnchor.MiddleCenter;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int z = 0; z < length; z++)
                    {
                        Vector3 chunkPos = new Vector3(x, y, z);
                        Vector3 worldPos = transform.position + chunkPos;
                        if(Vector3.Distance(worldPos,Camera.current.transform.position) < 8f) {
                            float density = GetVoxel(chunkPos).density;
                            float size = Helpers.Map(density, -1f, 1f, 0.01f, 0.1f);
                            string text = density.ToString("0.00");
                            Gizmos.DrawSphere(worldPos, size);
                            Handles.Label(worldPos + Vector3.up * 0.2f, text, style);
                        }
                    }
                }
            }
        }
    }
#endif
}

public struct Voxel
{
    public float density;
    public Material material;

    [Flags]
    public enum Material : byte
    {
        Grass = 1 << 0,
        Dirt = 1 << 1,
        Stone = 1 << 2,
        Sand = 1 << 3,
        Snow = 1 << 4,
        Air = 1 << 5
    }

    public Voxel(float density, Material material)
    {
        this.density = density;
        this.material = material;
    }
}

public struct StructureVoxel
{
    public bool westWall; //Western edge
    public bool southWall; //Southern edge
    public bool floor;
    public bool center;
}

public enum PartType
{
    Wall,
    Floor,
    Block
}