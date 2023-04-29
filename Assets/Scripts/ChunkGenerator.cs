using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using System.Linq;

public class ChunkGenerator : MonoBehaviour
{
    World world;
    MarchingData marchingData;
    HashSet<Vector2Int> chunksToGenerate;
    Dictionary<Vector2Int, GameObject> chunkDic;

    NativeList<int2> nativePos;
    NativeArray<Voxel> nativeVoxels;
    NativeArray<int> windingOrder;
    NativeArray<int> vertexOffset;
    NativeArray<int> cubeEdgeFlags;
    NativeArray<int> edgeConnection;
    NativeArray<float> edgeDirection;
    NativeArray<int> triangleConnectionTable;
    NativeList<int> nativeIndices;
    NativeList<float3> nativeVerts;
    NativeList<float3> nativeColors;
    NativeArray<int> vertLength;
    
    int3 size;
    int voxCount;

    JobHandle jobHandle;

    void Start()
    {
        world = gameObject.GetComponent<World>();
        chunksToGenerate = world.chunksToGenerate;
        chunkDic = world.chunkDic;
        marchingData = new MarchingData(0.0f);

        windingOrder = new NativeArray<int>(marchingData.windingOrder, Allocator.Persistent);
        vertexOffset = new NativeArray<int>(marchingData.FlattenedArray(marchingData.vertexOffset), Allocator.Persistent);
        cubeEdgeFlags = new NativeArray<int>(marchingData.cubeEdgeFlags, Allocator.Persistent);
        edgeConnection = new NativeArray<int>(marchingData.FlattenedArray(marchingData.edgeConnection), Allocator.Persistent);
        edgeDirection = new NativeArray<float>(marchingData.FlattenedArray(marchingData.edgeDirection), Allocator.Persistent);
        triangleConnectionTable = new NativeArray<int>(marchingData.FlattenedArray(marchingData.triangleConnectionTable), Allocator.Persistent);
        size = new int3(world.chunkWidth, world.chunkHeight, world.chunkLength);
        voxCount = size.x * size.y * size.z;

        StartCoroutine(ChunkGeneration());
    }

    private void OnDisable()
    {
        DisposeNativeCollections();
    }

    void DisposeNativeCollections()
    {
        StopCoroutine(ChunkGeneration());

        jobHandle.Complete();
        if (nativePos.IsCreated) nativePos.Dispose();
        if (nativeVoxels.IsCreated) nativeVoxels.Dispose();
        if (windingOrder.IsCreated) windingOrder.Dispose();
        if (vertexOffset.IsCreated) vertexOffset.Dispose();
        if (cubeEdgeFlags.IsCreated) cubeEdgeFlags.Dispose();
        if (edgeConnection.IsCreated) edgeConnection.Dispose();
        if (edgeDirection.IsCreated) edgeDirection.Dispose();
        if (triangleConnectionTable.IsCreated) triangleConnectionTable.Dispose();
        if (nativeIndices.IsCreated) nativeIndices.Dispose();
        if (nativeVerts.IsCreated) nativeVerts.Dispose();
        if (nativeColors.IsCreated) nativeColors.Dispose();
        if (vertLength.IsCreated) vertLength.Dispose();
    }

    IEnumerator ChunkGeneration()
    {
        while (true)
        {
            Vector2Int playerChunk = world.playerChunk;

            if (chunksToGenerate.Count > 0)
            {
                Chunk c;

                Vector2Int p;

                while (true)
                {
                    //Get the closest ungenerated chunk to the player
                    p = chunksToGenerate.OrderBy(x => Vector2.Distance(playerChunk, x)).First();
                    chunksToGenerate.Remove(p);
                    if (Mathf.Abs(playerChunk.x - p.x) < world.renderDistance && Mathf.Abs(playerChunk.y - p.y) < world.renderDistance && !chunkDic.ContainsKey(p)) break;
                    else if (chunksToGenerate.Count == 0) goto LoopEnd;
                }

                nativeVoxels = new NativeArray<Voxel>(voxCount, Allocator.Persistent);

                var job = new TerrainJobs.GenerateChunk()
                {
                    voxels = nativeVoxels,
                    voxSize = 1,
                    noiseScale = new float3(world.noiseScale.x, world.noiseScale.y, world.noiseScale.z),
                    size = size,
                    voxCount = voxCount,
                    seed = world.worldSettings.seed,
                    chunkPos = new int2(p.x, p.y)
                };
                jobHandle = job.Schedule();

                //Wait until job is completed
                while (true)
                {
                    if (jobHandle.IsCompleted) break;
                    else yield return new WaitForSeconds(0.1f);
                }
                jobHandle.Complete();

                var voxels = nativeVoxels.ToArray();

                c = world.GenerateChunk(p, voxels);

                //Generate mesh
                nativeVerts = new NativeList<float3>(Allocator.Persistent);
                nativeIndices = new NativeList<int>(Allocator.Persistent);
                nativeColors = new NativeList<float3>(Allocator.Persistent);

                var meshJob = new TerrainJobs.GenerateMesh()
                {
                    size = size,
                    voxCount = voxCount,
                    voxSize = 1,

                    surface = marchingData.surface,
                    windingOrder = windingOrder,
                    vertexOffset = vertexOffset,
                    cubeEdgeFlags = cubeEdgeFlags,
                    edgeConnection = edgeConnection,
                    edgeDirection = edgeDirection,
                    triangleConnectionTable = triangleConnectionTable,

                    chunkPos = new int2(p.x, p.y),
                    voxels = nativeVoxels,
                    verts = nativeVerts,
                    indices = nativeIndices,
                    colors = nativeColors,
                };

                jobHandle = meshJob.Schedule();
                while (true)
                {
                    if (jobHandle.IsCompleted) break;
                    else yield return null;
                }
                jobHandle.Complete();

                var verts = new List<Vector3>();
                var indices = new List<int>();
                var colors = new List<Color>();

                for (int i = 0; i < nativeIndices.Length; i++)
                {
                    indices.Add(nativeIndices[i]);
                    if (nativeVerts.Length > i)
                    {
                        verts.Add((Vector3)nativeVerts[i]);
                    }
                    if (nativeColors.Length > i)
                    {
                        float3 col = nativeColors[i];
                        colors.Add(new Color(col.x, col.y, col.z));
                    }
                }

                nativeVoxels.Dispose();
                nativeVerts.Dispose();
                nativeColors.Dispose();
                nativeIndices.Dispose();

                if (c != null) c.SetMesh(verts, indices, colors);
            }

            LoopEnd:
            yield return null;
        }
    }
}
