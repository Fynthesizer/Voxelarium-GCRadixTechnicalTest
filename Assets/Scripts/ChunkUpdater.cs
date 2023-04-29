using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using System.Linq;

public class ChunkUpdater : MonoBehaviour
{
    World world;
    MarchingData marchingData;
    HashSet<Chunk> chunksToUpdate;

    NativeList<int2> nativePos;
    NativeList<Voxel> nativeVoxels;
    NativeArray<int> windingOrder;
    NativeArray<int> vertexOffset;
    NativeArray<int> cubeEdgeFlags;
    NativeArray<int> edgeConnection;
    NativeArray<float> edgeDirection;
    NativeArray<int> triangleConnectionTable;
    NativeArray<int> nativeIndices;
    NativeArray<float3> nativeVerts;
    NativeArray<float4> nativeColors;
    NativeArray<int> vertLength;
    int3 size;

    JobHandle jobHandle;

    void Start()
    {
        world = gameObject.GetComponent<World>();
        chunksToUpdate = world.chunksToUpdate;
        marchingData = new MarchingData(0.0f);

        windingOrder = new NativeArray<int>(marchingData.windingOrder, Allocator.Persistent);
        vertexOffset = new NativeArray<int>(marchingData.FlattenedArray(marchingData.vertexOffset), Allocator.Persistent);
        cubeEdgeFlags = new NativeArray<int>(marchingData.cubeEdgeFlags, Allocator.Persistent);
        edgeConnection = new NativeArray<int>(marchingData.FlattenedArray(marchingData.edgeConnection), Allocator.Persistent);
        edgeDirection = new NativeArray<float>(marchingData.FlattenedArray(marchingData.edgeDirection), Allocator.Persistent);
        triangleConnectionTable = new NativeArray<int>(marchingData.FlattenedArray(marchingData.triangleConnectionTable), Allocator.Persistent);
        size = new int3(world.chunkWidth, world.chunkHeight, world.chunkLength);

        StartCoroutine(UpdateChunks());
    }

    private void OnDisable()
    {
        DisposeNativeCollections();
    }

    void OnDestroy()
    {
        DisposeNativeCollections();
    }

    void DisposeNativeCollections()
    {
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

    IEnumerator UpdateChunks()
    {
        while (true)
        {
            List<Chunk> chunks = new List<Chunk>();
            if (chunksToUpdate.Count > 0)
            {
                foreach (Chunk c in chunksToUpdate)
                {
                    chunks.Add(c);
                }
                chunksToUpdate.Clear();

                nativePos = new NativeList<int2>(Allocator.Persistent);
                nativeVoxels = new NativeList<Voxel>(Allocator.Persistent);

                foreach (Chunk c in chunks)
                {
                    nativePos.Add(new int2(c.chunkPos.x, c.chunkPos.y));
                    nativeVoxels.AddRange(new NativeArray<Voxel>(c.voxels, Allocator.Temp));
                }

                nativeIndices = new NativeArray<int>(chunks.Count * 60000, Allocator.Persistent);
                nativeVerts = new NativeArray<float3>(chunks.Count * 60000, Allocator.Persistent);
                nativeColors = new NativeArray<float4>(chunks.Count * 60000, Allocator.Persistent);
                vertLength = new NativeArray<int>(chunks.Count, Allocator.Persistent);

                var job = new TerrainJobs.GenerateMeshes()
                {
                    chunkPos = nativePos,
                    voxSize = 1,
                    size = size,
                    voxCount = size.x * size.y * size.z,

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
                jobHandle = job.Schedule(chunks.Count, 1);

                while (!jobHandle.IsCompleted) yield return null;
                jobHandle.Complete();

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

                    verts.Dispose();
                    colors.Dispose();
                }

                nativeVoxels.Dispose();
                nativePos.Dispose();
                nativeIndices.Dispose();
                nativeVerts.Dispose();
                nativeColors.Dispose();
                vertLength.Dispose();
            }

            yield return null;
        }
    }
}
