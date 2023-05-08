using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

using ProceduralNoiseProject;
using Unity.Collections;

public class World : MonoBehaviour
{
    public enum GenerationMode { Finite, Infinite };

    public GenerationMode generationMode;

    public WorldSettings worldSettings;

    public Dictionary<Vector2Int, GameObject> chunkDic = new Dictionary<Vector2Int, GameObject>();
    public List<GameObject> activeChunks = new List<GameObject>();
    public HashSet<Vector2Int> chunksToGenerate = new HashSet<Vector2Int>();
    public HashSet<Chunk> chunksToUpdate = new HashSet<Chunk>();

    public GameObject chunkPrefab;
    public int totalChunks = 0;
    public int loadedChunks = 0;

    public int chunkWidth = 32;
    public int chunkHeight = 32;
    public int chunkLength = 32;

    public int preloadSize = 4;

    public Vector3 noiseScale = new Vector3(10f, 10f, 10f);

    public float voxelSize = 2;

    public int renderDistance = 2;

    GameObject player;
    public Vector2Int playerChunk;
    UIManager UIManager;
    GameManager gameManager;

    private bool playerSpawned = false;

    public delegate void TerrainChangedHandler();
    public event TerrainChangedHandler TerrainChanged;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        UIManager = GameObject.FindGameObjectWithTag("Canvas").GetComponent<UIManager>();
        gameManager = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameManager>();
    }

    public void GenerateWorld(WorldSettings settings)
    {
        worldSettings = settings;

        totalChunks = renderDistance * renderDistance * 4;

        //StartCoroutine(WaitForWorldToGenerate());
        if (generationMode == GenerationMode.Infinite)
        {
            StartCoroutine(RefreshActiveChunks());
        }
    }

    public Chunk GenerateChunk(Vector2Int pos, Voxel[] voxels)
    {
        GameObject newChunk = chunkPrefab;

        newChunk.GetComponent<Chunk>().seed = worldSettings.seed;
        newChunk.GetComponent<Chunk>().chunkPos = new Vector2Int(pos.x, pos.y);
        newChunk.GetComponent<Chunk>().width = chunkWidth;
        newChunk.GetComponent<Chunk>().height = chunkHeight;
        newChunk.GetComponent<Chunk>().length = chunkLength;
        newChunk.GetComponent<Chunk>().voxelSize = voxelSize;

        Vector3 chunkPos = new Vector3(pos.x * (chunkWidth - 1), 0, pos.y * (chunkLength - 1)) * voxelSize;

        chunkDic.Add(new Vector2Int(pos.x, pos.y), Instantiate(newChunk, chunkPos, Quaternion.identity, transform));
        chunkDic[pos].name = "Chunk " + "(" + pos.x + "," + pos.y + ")";
        chunkDic[pos].GetComponent<Chunk>().SetupChunk(voxels);
        activeChunks.Add(chunkDic[pos]);

        // Check for a valid location to spawn the player
        if (!playerSpawned)
        {
            if (chunkDic[pos].GetComponent<Chunk>().FindSpawnSurface(out Vector3 spawnPosition)){
                gameManager.SpawnPlayer(spawnPosition);
                playerSpawned = true;
            }
            else if (activeChunks.Count > preloadSize * preloadSize)
            {
                gameManager.SpawnPlayer(new Vector3(0, 15, 0));
                playerSpawned = true;
            }
        }

        return chunkDic[pos].GetComponent<Chunk>();
    }

    IEnumerator WaitForWorldToGenerate()
    {
        int chunksToLoad = preloadSize * preloadSize;
        while (activeChunks.Count < chunksToLoad)
        {
            yield return null;
        }
        gameManager.OnWorldLoad();
    }

    IEnumerator RefreshActiveChunks()
    {
        while (true)
        {

            //Check whether the player has moved into a new chunk
            var currentChunk = new Vector2Int(Mathf.FloorToInt(player.transform.position.x / chunkWidth), Mathf.FloorToInt(player.transform.position.z / chunkLength));
            if (playerChunk != currentChunk) playerChunk = currentChunk;
            //else goto LoopEnd;

            var nearChunks = new List<Vector2Int>();
            var chunksToUnload = new List<GameObject>();
            var chunksToLoad = new List<GameObject>();

            //Find which chunks are within the render distance
            for (int x = -renderDistance; x < renderDistance; x++)
            {
                for (int y = -renderDistance; y < renderDistance; y++)
                {
                    Vector2Int displacement = new Vector2Int(x, y);
                    Vector2Int pos = playerChunk + displacement;
                    nearChunks.Add(pos);
                }
            }

            //Unload any currently active chunks that are out of range
            foreach (GameObject c in activeChunks)
            {
                Vector2Int pos = c.GetComponent<Chunk>().chunkPos;
                Vector2Int dist = new Vector2Int(Mathf.Abs(playerChunk.x - pos.x), Mathf.Abs(playerChunk.y - pos.y));
                if (dist.x > renderDistance || dist.y > renderDistance)
                {
                    chunksToUnload.Add(c);
                }
            }

            foreach (Vector2Int c in nearChunks)
            {
                //Otherwise, mark the chunk to be generated
                if (!chunkDic.ContainsKey(c)) chunksToGenerate.Add(c);
            }

            //Unload chunks that are out of range
            foreach (GameObject c in chunksToUnload)
            {
                activeChunks.Remove(c);
                chunkDic.Remove(c.GetComponent<Chunk>().chunkPos);
                c.transform.GetChild(0).GetComponent<MeshFilter>().sharedMesh.Clear();
                Destroy(c.transform.GetChild(0).GetComponent<MeshFilter>().sharedMesh);
                Destroy(c);
            }

            //Load chunks that are in range
            foreach (GameObject c in chunksToLoad)
            {
                c.SetActive(true);
                Instantiate(c);
                activeChunks.Add(c);
            }

            yield return new WaitForSeconds(1f);
        }
    }

    public Chunk GetChunk(Vector2Int position)
    {
        GameObject chunk;
        chunkDic.TryGetValue(position, out chunk);
        if (chunk != null) return chunk.GetComponent<Chunk>();
        else return null;
    }

    public Chunk GetChunk(Vector3 position)
    {
        Vector2Int coords = new Vector2Int(Mathf.FloorToInt(position.x / (chunkWidth - 1)), Mathf.FloorToInt(position.z / (chunkLength - 1)));
        GameObject chunk;
        chunkDic.TryGetValue(coords, out chunk);
        if (chunk != null) return chunk.GetComponent<Chunk>();
        else return null;
    }

    public void FlattenTerrain(Vector3 point, float speed, float radius)
    {
        List<Chunk> chunksToModify = new List<Chunk>();
        Bounds b = new Bounds(point, new Vector3(radius * 3, radius * 3, radius * 3));

        foreach (GameObject c in activeChunks)
        {
            //Vector2Int chunkPos = new Vector2Int(x, z);
            if (b.Intersects(c.GetComponent<Chunk>().chunkBounds)) chunksToModify.Add(c.GetComponent<Chunk>());
        }

        int r = Mathf.CeilToInt(radius / voxelSize);

        if (chunksToModify.Count > 0)
        {
            foreach (Chunk c in chunksToModify)
            {
                Vector3 relativePoint = (point - c.transform.position) / voxelSize;
                relativePoint.y = Mathf.Round(relativePoint.y - 0.25f);
                int mainIndex = c.GetVoxelIndex(relativePoint);
                Vector3 mainPos = c.GetVoxelPosition(mainIndex);

                for (int x = -r; x <= r; x++)
                {
                    for (int y = -r; y <= r; y++)
                    {
                        for (int z = -r; z <= r; z++)
                        {
                            Vector3 displacement = new Vector3(x, y, z);
                            int index = c.GetVoxelIndex(mainPos + (displacement));
                            Vector3 p = c.GetVoxelPosition(index);

                            //Stop the player from digging or building through the top or bottom of the chunk, or trying to modify voxels which don't exist
                            float newSpeed = speed;
                            if (index < 0 || index > c.voxels.Length - 1) continue;
                            if (p.y < 1 && speed < 0) continue;
                            else if (p.y > chunkHeight - 2 && speed > 0) continue;
                            //Don't subtract from voxels below -1 or add to voxels above 1
                            if (Mathf.Sign(speed) == 1 && c.voxels[index].density >= 1) continue;
                            if (Mathf.Sign(speed) == -1 && c.voxels[index].density <= -1) continue;

                            float dist = Vector3.Distance(relativePoint, p);
                            float mult;
                            if (y >= 0) mult = -1;
                            else continue;

                            float amount = newSpeed * ((-dist / radius) + 1) * 0.5f;
                            if (amount < 0f) continue;
                            //if (c.voxels[index] <= 0f) continue;
                            amount *= mult;
                            if (dist < radius && index <= c.voxels.Length) c.voxels[index].density += amount;
                            //if (dist < radius && index <= c.voxels.Length) c.voxels[index] = Mathf.Lerp(c.voxels[index], 0f, Time.deltaTime*((-dist / radius) + 1) * 10);

                            //Clamp the value
                            //c.voxels[index] = Mathf.Clamp(c.voxels[index], 0f,1f);
                            //if (dist < radius && index <= c.voxels.Length) c.voxels[index] += amount;
                            //if (dist < radius && index <= c.voxels.Length) c.voxels[index] = Mathf.Lerp(c.voxels[index], 0f, Time.deltaTime*((-dist / radius) + 1) * 10);
                        }
                    }
                }
            }
        }
        RefreshChunks(chunksToModify);
    }

    Vector2Int GetChunkCoordinates(Vector3 point)
    {
        Vector2Int coords = new Vector2Int(Mathf.FloorToInt(point.x / (chunkWidth - 1)), Mathf.FloorToInt(point.z / (chunkLength - 1)));
        return coords;
    }

    public void ModifyTerrain(Vector3 point, float speed, float radius)
    {
        //Find which chunks are affected
        var cornerChunks = new Vector2Int[2];
        cornerChunks[0] = GetChunkCoordinates(point - new Vector3(radius, 0, radius));
        cornerChunks[1] = GetChunkCoordinates(point + new Vector3(radius, 0, radius));
        var chunkCoords = new List<Vector2Int>();

        for (int x = cornerChunks[0].x; x <= cornerChunks[1].x; x++)
        {
            for (int y = cornerChunks[0].y; y <= cornerChunks[1].y; y++)
            {
                chunkCoords.Add(new Vector2Int(x, y));
            }
        }

        List<Chunk> chunksToModify = new List<Chunk>();

        //Check for other game objects in radius
        CheckObjects(point, radius);

        foreach (Vector2Int c in chunkCoords)
        {
            if (chunkDic.ContainsKey(c)) chunksToModify.Add(chunkDic[c].GetComponent<Chunk>());
            else return; //If one  of the chunks does not exist, cancel the operation to prevent chunk discrepancies
        }

        float materialCost = 0;
        int3 size = new int3(chunkWidth, chunkHeight, chunkLength);
        var nativeVoxels = new NativeArray<Voxel>(chunksToModify.Count * size.x * size.y * size.z, Allocator.TempJob);
        var chunkPositions = new NativeArray<int2>(chunksToModify.Count, Allocator.TempJob);
        int voxCount = size.x * size.y * size.z;

        if (chunksToModify.Count > 0)
        {
            for (int i = 0; i < chunksToModify.Count; i++)
            {
                int index = i * voxCount;
                NativeArray<Voxel>.Copy(chunksToModify[i].voxels, 0, nativeVoxels, index, voxCount);
                chunkPositions[i] = new int2(chunksToModify[i].chunkPos.x, chunksToModify[i].chunkPos.y);
            }

            var job = new TerrainJobs.ModifyTerrain()
            {
                size = size,
                point = point,
                radius = radius,
                speed = speed,
                chunkPositions = chunkPositions,
                voxels = nativeVoxels,
                materialCost = materialCost,
            };
            JobHandle jobHandle = job.Schedule(chunksToModify.Count, 1);

            jobHandle.Complete();
            Voxel[] voxels = nativeVoxels.ToArray();
            for (int i = 0; i < chunksToModify.Count; i++)
            {
                int index = i * voxCount;
                Array.Copy(voxels, index, chunksToModify[i].voxels, 0, voxCount);
            }
        }

        foreach (Chunk c in chunksToModify)
        {
            chunksToUpdate.Add(c);
        }

        UIManager.UpdateUI();

        nativeVoxels.Dispose();
        chunkPositions.Dispose();

        TerrainChanged?.Invoke();
    }

    public void CheckObjects(Vector3 point, float radius)
    {
        Collider[] hitColliders = Physics.OverlapSphere(point, radius);
        foreach (Collider c in hitColliders)
        {
            switch (c.gameObject.layer)
            {
                case 9: //Objects
                    var o = c.gameObject.GetComponent<Object>();
                    if (o != null && !o.dislodged) o.Dislodge();
                    break;
                case 10: //Foliage
                    Destroy(c.gameObject);
                    break;
                default:
                    break;
            }
        }
    }

    public void PlaceTerrain(Vector3 point, float speed, float radius, Voxel.Material material)
    {
        //Find which chunks are affected
        var cornerChunks = new Vector2Int[2];
        cornerChunks[0] = GetChunkCoordinates(point - new Vector3(radius, 0, radius));
        cornerChunks[1] = GetChunkCoordinates(point + new Vector3(radius, 0, radius));
        var chunkCoords = new List<Vector2Int>();

        for (int x = cornerChunks[0].x; x <= cornerChunks[1].x; x++)
        {
            for (int y = cornerChunks[0].y; y <= cornerChunks[1].y; y++)
            {
                chunkCoords.Add(new Vector2Int(x, y));
            }
        }

        List<Chunk> chunksToModify = new List<Chunk>();

        //Check for other game objects in radius
        CheckObjects(point, radius);

        foreach (Vector2Int c in chunkCoords)
        {
            if (chunkDic.ContainsKey(c)) chunksToModify.Add(chunkDic[c].GetComponent<Chunk>());
            else return; //If one  of the chunks does not exist, cancel the operation to prevent chunk discrepancies
        }

        float materialCost = 0;
        int3 size = new int3(chunkWidth, chunkHeight, chunkLength);
        var nativeVoxels = new NativeArray<Voxel>(chunksToModify.Count * size.x * size.y * size.z, Allocator.TempJob);
        var chunkPositions = new NativeArray<int2>(chunksToModify.Count, Allocator.TempJob);
        int voxCount = size.x * size.y * size.z;

        if (chunksToModify.Count > 0)
        {
            for (int i = 0; i < chunksToModify.Count; i++)
            {
                int index = i * voxCount;
                NativeArray<Voxel>.Copy(chunksToModify[i].voxels, 0, nativeVoxels, index, voxCount);
                chunkPositions[i] = new int2(chunksToModify[i].chunkPos.x, chunksToModify[i].chunkPos.y);
            }

            var job = new TerrainJobs.ModifyTerrain()
            {
                size = size,
                point = point,
                radius = radius,
                speed = speed,
                chunkPositions = chunkPositions,
                voxels = nativeVoxels,
                materialCost = materialCost,
                material = material,
            };
            JobHandle jobHandle = job.Schedule(chunksToModify.Count, 1);

            jobHandle.Complete();
            Voxel[] voxels = nativeVoxels.ToArray();
            for (int i = 0; i < chunksToModify.Count; i++)
            {
                int index = i * voxCount;
                Array.Copy(voxels, index, chunksToModify[i].voxels, 0, voxCount);
            }
        }

        foreach (Chunk c in chunksToModify)
        {
            chunksToUpdate.Add(c);
        }

        UIManager.UpdateUI();

        nativeVoxels.Dispose();
        chunkPositions.Dispose();

        TerrainChanged?.Invoke();
    }

    public void PaintTerrain(Vector3 point, float radius, Voxel.Material material)
    {
        List<Chunk> chunksToModify = new List<Chunk>();
        Bounds b = new Bounds(point, new Vector3(radius * 3, radius * 3, radius * 3));

        //Find which chunks should be altered
        foreach (GameObject c in activeChunks)
        {
            //Vector2Int chunkPos = new Vector2Int(x, z);
            if (b.Intersects(c.GetComponent<Chunk>().chunkBounds)) chunksToModify.Add(c.GetComponent<Chunk>());
        }

        int r = Mathf.CeilToInt(radius / voxelSize);

        if (chunksToModify.Count > 0)
        {
            foreach (Chunk c in chunksToModify)
            {
                Vector3 relativePoint = (point - c.transform.position) / voxelSize;
                int mainIndex = c.GetVoxelIndex(relativePoint);
                Vector3 mainPos = c.GetVoxelPosition(mainIndex);

                for (int x = -r; x <= r; x++)
                {
                    for (int y = -r; y <= r; y++)
                    {
                        for (int z = -r; z <= r; z++)
                        {
                            Vector3 displacement = new Vector3(x, y, z);

                            //If the adjusted point is outside of the chunks bounds, continue
                            //if (!c.chunkBounds.Contains(point + displacement)) continue;

                            int index = c.GetVoxelIndex(mainPos + displacement);
                            Vector3 p = c.GetVoxelPosition(index);

                            float dist = Vector3.Distance(relativePoint, p);
                            if (dist > r) continue;

                            //If index is outside of range, continue
                            if (index < 0 || index > c.voxels.Length - 1) continue;

                            //Don't paint voxels below 0
                            //if (c.voxels[index].density <= 0) continue;
                            c.voxels[index].material = material;
                        }
                    }
                }
            }
        }

        foreach (Chunk c in chunksToModify)
        {
            if (!chunksToUpdate.Contains(c)) chunksToUpdate.Add(c);
        }
    }

    void RefreshChunks(List<Chunk> chunks)
    {

        var nativePos = new NativeList<int2>(Allocator.TempJob);
        var nativeVoxels = new NativeList<Voxel>(Allocator.TempJob);

        var marchingData = new MarchingData(0.0f);
        var windingOrder = new NativeArray<int>(marchingData.windingOrder, Allocator.TempJob);
        var vertexOffset = new NativeArray<int>(marchingData.FlattenedArray(marchingData.vertexOffset), Allocator.TempJob);
        var cubeEdgeFlags = new NativeArray<int>(marchingData.cubeEdgeFlags, Allocator.TempJob);
        var edgeConnection = new NativeArray<int>(marchingData.FlattenedArray(marchingData.edgeConnection), Allocator.TempJob);
        var edgeDirection = new NativeArray<float>(marchingData.FlattenedArray(marchingData.edgeDirection), Allocator.TempJob);
        var triangleConnectionTable = new NativeArray<int>(marchingData.FlattenedArray(marchingData.triangleConnectionTable), Allocator.TempJob);

        foreach (Chunk c in chunks)
        {
            nativePos.Add(new int2(c.chunkPos.x, c.chunkPos.y));
            nativeVoxels.AddRange(new NativeArray<Voxel>(c.voxels, Allocator.Temp));
        }

        var nativeIndices = new NativeArray<int>(chunks.Count * 60000, Allocator.TempJob);
        var nativeVerts = new NativeArray<float3>(chunks.Count * 60000, Allocator.TempJob);
        var nativeColors = new NativeArray<float4>(chunks.Count * 60000, Allocator.TempJob);
        var vertLength = new NativeArray<int>(chunks.Count, Allocator.TempJob);
        var size = new int3(chunkWidth, chunkHeight, chunkLength);

        var job = new TerrainJobs.GenerateMeshes()
        {
            chunkPos = nativePos,
            voxSize = voxelSize,
            size = size,
            voxCount = chunkHeight * chunkWidth * chunkLength,

            verts = nativeVerts,
            indices = nativeIndices,
            colors = nativeColors,
            voxels = nativeVoxels,
            vertLength = vertLength,

            surface = marchingData.surface,
            windingOrder = windingOrder,
            vertexOffset = vertexOffset,
            cubeEdgeFlags = cubeEdgeFlags,
            edgeConnection = edgeConnection,
            edgeDirection = edgeDirection,
            triangleConnectionTable = triangleConnectionTable
        };
        JobHandle jobHandle = job.Schedule(chunks.Count, 1);

        jobHandle.Complete();
        //StartCoroutine(UpdateMesh(jobHandle, chunks, nativeVerts, nativeIndices, nativeColors, vertLength));

        nativePos.Dispose();
        nativeVoxels.Dispose();

        windingOrder.Dispose();
        vertexOffset.Dispose();
        cubeEdgeFlags.Dispose();
        edgeConnection.Dispose();
        edgeDirection.Dispose();
        triangleConnectionTable.Dispose();

        for (int i = 0; i < chunks.Count; i++)
        {
            int index = i * 60000;

            var vertsSlice = new NativeSlice<float3>(nativeVerts, i * 60000, vertLength[i]);
            var indices = new NativeSlice<int>(nativeIndices, index, vertLength[i]).ToList();
            var colorsSlice = new NativeSlice<float4>(nativeColors, i * 60000, vertLength[i]);

            var verts = new NativeArray<float3>(vertLength[i], Allocator.Temp);
            vertsSlice.CopyTo(verts);
            var colors = new NativeArray<float4>(vertLength[i], Allocator.Temp);
            colorsSlice.CopyTo(colors);

            chunks[i].SetMesh(verts, indices, colors);
        }

        nativeVerts.Dispose();
        nativeIndices.Dispose();
        nativeColors.Dispose();
        vertLength.Dispose();
    }

    public Voxel GetVoxel(Vector3 position)
    {
        Vector2Int chunkPos = new Vector2Int(Mathf.FloorToInt(position.x / (chunkWidth - 1)), Mathf.FloorToInt(position.z / (chunkLength - 1)));
        Chunk chunk = GetChunk(chunkPos);
        if (chunk != null)
        {
            Vector3 relativePosition;
            relativePosition = position - chunk.transform.position;
            relativePosition = Vector3Int.RoundToInt(relativePosition);
            return chunk.GetVoxel(relativePosition);
        }
        else return new Voxel();
    }

    public Voxel[] GetVoxels(Vector3Int startPos, Vector3Int endPos)
    {
        int size = Mathf.Abs(endPos.x - startPos.x) * Mathf.Abs(endPos.y - startPos.y) * Mathf.Abs(endPos.z - startPos.z);
        Voxel[] v = new Voxel[size];
        int index = 0;

        for (int z = startPos.z; z < endPos.z; z++)
        {
            for (int y = startPos.y; y < endPos.y; y++)
            {
                for (int x = startPos.x; x < endPos.x; x++)
                {
                    v[index] = GetVoxel(new Vector3(x, y, z));
                    index += 1;
                }
            }
        }

        return v;
    }
}
