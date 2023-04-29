using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;

public class TerrainJobs
{
    [BurstCompile(DisableSafetyChecks = true)]
    public struct GenerateChunk : IJob
    {
        [ReadOnly] public int2 chunkPos;
        [ReadOnly] public int3 size;
        [ReadOnly] public int voxCount;
        [ReadOnly] public float voxSize;
        [ReadOnly] public float3 noiseScale;
        [ReadOnly] public int seed;

        public NativeArray<Voxel> voxels;

        public void Execute()
        {
            int x, y, z; //Chunk position
            int wx, wy, wz; //World position
            float4 np; //Noise position
            float3 np2d; //2d noise position

            for(x = 0; x < size.x; x++)
            {
                wx = x + (chunkPos.x * (size.x - 1));
                for (z = 0; z < size.z; z++)
                {
                    wz = z + (chunkPos.y * (size.z - 1));
                    np2d = new float3(wx / noiseScale.x, wz / noiseScale.z, seed);

                    //Base terrain
                    float hillLevel = math.abs(noise.cnoise(np2d / 8f)); //Base terrain height, including rivers
                    float hillDetail = noise.cnoise(np2d * 2) * 0.03f + noise.cnoise(np2d * 4) * 0.02f + noise.cnoise(np2d * 8) * 0.01f; //Some additional height variation
                    hillLevel += hillDetail;
                    hillLevel = math.clamp(hillLevel, 0.02f, 0.95f); //Clamp hill level so that it doesn't break through vertical chunk limits

                    float depth = 0;
                    for (y = size.y - 1; y >= 0; y--)
                    {
                        wy = y + (size.y - 1);
                        int i = (z * size.y * size.x) + (y * size.x) + x;

                        //The position to get noise data from
                        np = new float4
                        {
                            x = wx / noiseScale.x,
                            y = wy / noiseScale.y,
                            z = wz / noiseScale.z,
                            w = seed
                        };

                        float heightPercentage = (float)y / size.y;
                        float hills = (1f - (heightPercentage / hillLevel)) * 0.2f;

                        //Fractal Noise
                        float ruggedness = (noise.cnoise(np / 8) + 1) / 2f; //Determines the influence of the higher frequency noise values
                        if (heightPercentage > 0.75f) ruggedness *= ((1 - heightPercentage) / 0.25f); //Smooth out terrain as it approaches the height limit, to prevent clipping
                        float fnoise =
                            ((noise.cnoise(np / 8) * 0.3f) +
                            (noise.cnoise(np / 4) * 0.25f) +
                            (noise.cnoise(np / 2) * 0.2f) +
                            (noise.cnoise(np) * 0.15f) +
                            (noise.cnoise(np * 2) * 0.1f) +
                            (noise.cnoise(np * 4) * 0.05f));
                        fnoise *= ruggedness;

                        float temperature = noise.cnoise(np2d / 10) - heightPercentage;

                        //Density assignment
                        float density = math.clamp(hills + fnoise, -1f, 1f);
                        if (density > 0f || y < 12) depth += 1;
                        else if (depth > 1f) depth -= 1f;

                        //Material assignment
                        Voxel.Material material;
                        if (depth > 6) material = Voxel.Material.Stone;
                        else if (temperature > 0.2f) material = Voxel.Material.Sand;
                        else if (depth > 2) material = Voxel.Material.Dirt;
                        else if (depth > 0)
                        {
                            if (temperature < -0.75f) material = Voxel.Material.Snow;
                            else if (temperature < -0.7f) material = Voxel.Material.Dirt;
                            else material = Voxel.Material.Grass;
                        }
                        else material = Voxel.Material.Air;
                        voxels[i] = new Voxel(density, material);
                    }
                }
            }
        }
    }

    [BurstCompile(DisableSafetyChecks = true)]
    public struct GenerateMesh : IJob
    {
        [ReadOnly] public int3 size;
        [ReadOnly] public int voxCount;
        [ReadOnly] public float voxSize;

        [ReadOnly] public float surface;
        [ReadOnly] public NativeArray<int> windingOrder;
        [ReadOnly] public NativeArray<int> vertexOffset;
        [ReadOnly] public NativeArray<int> cubeEdgeFlags;
        [ReadOnly] public NativeArray<int> edgeConnection;
        [ReadOnly] public NativeArray<float> edgeDirection;
        [ReadOnly] public NativeArray<int> triangleConnectionTable;

        [ReadOnly] public int2 chunkPos;
        [ReadOnly] public NativeArray<Voxel> voxels;
        public NativeList<float3> verts;
        public NativeList<int> indices;
        public NativeList<float3> colors;

        public void Execute()
        {
            int vertCount = 0;

            //Generate mesh data
            for (int x = 0; x < size.x - 1; x++)
            {
                for (int y = 0; y < size.y - 1; y++)
                {
                    for (int z = 0; z < size.z - 1; z++)
                    {
                        var cube = new NativeArray<float>(8, Allocator.Temp);

                        //Get the values in the 8 neighbours which make up a cube
                        for (int i = 0; i < 8; i++)
                        {
                            int ix = x + vertexOffset[i * 3 + 0];
                            int iy = y + vertexOffset[i * 3 + 1];
                            int iz = z + vertexOffset[i * 3 + 2];

                            cube[i] = voxels[(ix + iy * size.x + iz * size.x * size.y)].density;
                        }

                        var edgeVertex = new NativeArray<float3>(12, Allocator.Temp);

                        float offset = 0.0f;

                        int flagIndex = 0;
                        for (int i = 0; i < 8; i++) if (cube[i] <= surface) flagIndex |= 1 << i;
                        int edgeFlags = cubeEdgeFlags[flagIndex];
                        if (edgeFlags == 0) continue;

                        for (int i = 0; i < 12; i++)
                        {
                            //if there is an intersection on this edge
                            if ((edgeFlags & (1 << i)) != 0)
                            {
                                offset = GetOffset(cube[edgeConnection[i * 2]], cube[edgeConnection[i * 2 + 1]]);
                                //edgeVertex[i] = float3.zero;
                                float3 edgeVert = new float3(
                                    x + (vertexOffset[3 * edgeConnection[i * 2] + 0] + offset * edgeDirection[i * 3 + 0]),
                                    y + (vertexOffset[3 * edgeConnection[i * 2] + 1] + offset * edgeDirection[i * 3 + 1]),
                                    z + (vertexOffset[3 * edgeConnection[i * 2] + 2] + offset * edgeDirection[i * 3 + 2]));

                                edgeVertex[i] = edgeVert;
                            }
                        }

                        cube.Dispose();

                        for (int i = 0; i < 5; i++)
                        {
                            if (triangleConnectionTable[(16 * flagIndex) + (3 * i)] < 0) break;

                            int idx = vertCount;

                            //Run once for each vertex
                            for (int j = 0; j < 3; j++)
                            {
                                float3 vert = edgeVertex[triangleConnectionTable[(16 * flagIndex) + (3 * i + j)]] * voxSize;
                                int ind = idx + windingOrder[j];
                                verts.Add(vert);
                                indices.Add(ind);
                                vertCount++;
                            }
                        }

                        edgeVertex.Dispose();
                    }
                }
            }

            //Weld duplicate verts
            var hashMap = new NativeHashMap<float3, int>(60000, Allocator.Temp);
            var newVerts = new NativeList<float3>(Allocator.Temp);
            var newIndices = new NativeList<int>(Allocator.Temp);
            int idy = 0;

            for (int i = 0; i < verts.Length; i++)
            {
                float3 vert = verts[i];
                int sharedVertIndex;

                if (hashMap.TryGetValue(vert, out sharedVertIndex))
                {
                    newIndices.Add(sharedVertIndex);
                }
                else
                {
                    hashMap.Add(vert, idy);
                    newVerts.Add(vert);
                    newIndices.Add(idy);

                    idy++;
                }

            }

            verts.Clear();
            indices.Clear();

            for (int i = 0; i < newIndices.Length; i++)
            {
                indices.Add(newIndices[i]);
                if (newVerts.Length > i)
                {
                    verts.Add(newVerts[i]);
                    colors.Add(GetVertexColor(newVerts[i], voxels));
                }
            }
        }

        private float GetOffset(float v1, float v2)
        {
            float delta = v2 - v1;
            return (delta == 0.0f) ? surface : (surface - v1) / delta;
        }
        private float3 GetVertexColor(float3 vert, NativeArray<Voxel> vox)
        {
            float3 c = new float3(0, 0, 0);
            float weightSum = 0f;

            for (int i = 0; i < 8; i++)
            {
                int3 offset = new int3((i / 4) % 2, (i / 2) % 2, i % 2);
                int3 v = new int3((int)math.floor(vert.x + offset.x), (int)math.floor(vert.y + offset.y), (int)math.floor(vert.z + offset.z));
                int voxIndex = (v.z * size.y * size.x) + (v.y * size.x) + (v.x);

                float weight = 1f - math.distance(vert, v);
                weight = math.clamp(weight, 0f, 1f);

                switch (vox[voxIndex].material)
                {
                    case Voxel.Material.Dirt:
                        c += new float3(0.31f, 0.22f, 0.13f) * weight; //Grass
                        weightSum += weight;
                        break;
                    case Voxel.Material.Grass:
                        c += new float3(0.24f, 0.42f, 0.18f) * weight; //Dirt
                        weightSum += weight;
                        break;
                    case Voxel.Material.Stone:
                        c += new float3(0.3f, 0.3f, 0.3f) * weight; //Stone
                        weightSum += weight;
                        break;
                    case Voxel.Material.Sand:
                        c += new float3(0.75f, 0.76f, 0.42f) * weight; //Sand
                        weightSum += weight;
                        break;
                    case Voxel.Material.Snow:
                        c += new float3(1f, 1f, 1f) * weight; //Snow
                        weightSum += weight;
                        break;
                    default:
                        break;
                }
            }
            c /= weightSum;
            c = math.clamp(c, float3.zero, new float3(1, 1, 1));
            return c;
        }
    }

    [BurstCompile(DisableSafetyChecks = true)]
    public struct ModifyTerrain : IJobParallelFor
    {
        [ReadOnly] public int3 size;
        [ReadOnly] public float3 point;
        [ReadOnly] public float radius;
        [ReadOnly] public float speed;

        [ReadOnly] public NativeArray<int2> chunkPositions;

        [NativeDisableParallelForRestriction] public NativeArray<Voxel> voxels;

        public float materialCost;
        public Voxel.Material material;

        public void Execute(int index)
        {
            int r = (int)math.ceil(radius);
            int voxCount = size.x * size.y * size.z;
            int2 chunkPos = chunkPositions[index];

            float3 relativePoint = (point - new float3(chunkPos.x * (size.x - 1), 0, chunkPos.y * (size.z - 1)));
            int3 mainVox = new int3((int)math.round(relativePoint.x), (int)math.round(relativePoint.y), (int)math.round(relativePoint.z));

            for (int x = mainVox.x - r; x <= mainVox.x + r; x++)
            {
                for (int y = mainVox.y - r; y <= mainVox.y + r; y++)
                {
                    for (int z = mainVox.z - r; z <= mainVox.z + r; z++)
                    {
                        int voxIndex = x + y * size.x + z * size.x * size.y;
                        int i = voxIndex + (index * voxCount);
                        float3 voxPos = new float3(x, y, z);
                        Voxel v = voxels[i];

                        //Stop the player from digging or building through the top or bottom of the chunk, or trying to modify voxels which don't exist
                        if (x < 0 || x > size.x - 1 || z < 0 || z > size.z - 1) continue;
                        if (voxIndex < 0 || voxIndex > voxCount - 1) continue;
                        if (y < 1 && speed < 0) continue;
                        else if (y > size.y - 2 && speed > 0) continue;
                        //Don't subtract from voxels below -1 or add to voxels above 1
                        if (math.sign(speed) == 1 && v.density >= 1) continue;
                        if (math.sign(speed) == -1 && v.density <= -1) continue;

                        float dist = math.distance(relativePoint, voxPos);

                        float amount = speed * ((-dist / radius) + 1);
                        if (math.sign(amount) != math.sign(speed)) continue;
                        if (dist < radius && index <= voxCount) v.density += amount;
                        if (speed > 0 && material != Voxel.Material.Air) v.material = material;

                        voxels[i] = v;
                        materialCost += amount;
                    }
                }
            }
        }
    }

    [BurstCompile(DisableSafetyChecks = true)]
    public struct GenerateMeshes : IJobParallelFor
    {
        [ReadOnly] public int3 size;
        [ReadOnly] public int voxCount;
        [ReadOnly] public float voxSize;

        [ReadOnly] public float surface;
        [ReadOnly] public NativeArray<int> windingOrder;
        [ReadOnly] public NativeArray<int> vertexOffset;
        [ReadOnly] public NativeArray<int> cubeEdgeFlags;
        [ReadOnly] public NativeArray<int> edgeConnection;
        [ReadOnly] public NativeArray<float> edgeDirection;
        [ReadOnly] public NativeArray<int> triangleConnectionTable;

        [ReadOnly] public NativeList<int2> chunkPos;

        [ReadOnly] public NativeList<Voxel> voxels;
        public NativeArray<float3> verts;
        public NativeArray<int> indices;
        public NativeArray<float4> colors;
        public NativeArray<int> vertLength;

        public void Execute(int index)
        {
            int2 cp = chunkPos[index];

            var chunkVoxels = new NativeArray<Voxel>(voxCount, Allocator.Temp);
            for (int i = 0; i < voxCount; i++)
            {
                int j = (voxCount * index) + i;
                chunkVoxels[i] = voxels[j];
            }

            int vertCount = 0;

            var chunkVerts = new NativeList<float3>(60000, Allocator.Temp);

            var chunkIndices = new NativeList<int>(60000, Allocator.Temp);

            //Generate mesh data
            for (int x = 0; x < size.x - 1; x++)
            {
                for (int y = 0; y < size.y - 1; y++)
                {
                    for (int z = 0; z < size.z - 1; z++)
                    {
                        var cube = new NativeArray<float>(8, Allocator.Temp);

                        //Get the values in the 8 neighbours which make up a cube
                        for (int i = 0; i < 8; i++)
                        {
                            int ix = x + vertexOffset[i * 3 + 0];
                            int iy = y + vertexOffset[i * 3 + 1];
                            int iz = z + vertexOffset[i * 3 + 2];

                            cube[i] = chunkVoxels[(ix + iy * size.x + iz * size.x * size.y)].density;
                        }

                        var edgeVertex = new NativeArray<float3>(12, Allocator.Temp);

                        float offset = 0.0f;

                        int flagIndex = 0;
                        for (int i = 0; i < 8; i++) if (cube[i] <= surface) flagIndex |= 1 << i;
                        int edgeFlags = cubeEdgeFlags[flagIndex];
                        if (edgeFlags == 0) continue;

                        for (int i = 0; i < 12; i++)
                        {
                            //if there is an intersection on this edge
                            if ((edgeFlags & (1 << i)) != 0)
                            {
                                offset = GetOffset(cube[edgeConnection[i * 2]], cube[edgeConnection[i * 2 + 1]]);
                                //edgeVertex[i] = float3.zero;
                                float3 edgeVert = new float3(
                                    x + (vertexOffset[3 * edgeConnection[i * 2] + 0] + offset * edgeDirection[i * 3 + 0]),
                                    y + (vertexOffset[3 * edgeConnection[i * 2] + 1] + offset * edgeDirection[i * 3 + 1]),
                                    z + (vertexOffset[3 * edgeConnection[i * 2] + 2] + offset * edgeDirection[i * 3 + 2]));

                                edgeVertex[i] = edgeVert;
                            }
                        }

                        cube.Dispose();

                        for (int i = 0; i < 5; i++)
                        {
                            if (triangleConnectionTable[(16 * flagIndex) + (3 * i)] < 0) break;

                            int idx = vertCount;

                            //Run once for each vertex
                            for (int j = 0; j < 3; j++)
                            {
                                float3 vert = edgeVertex[triangleConnectionTable[(16 * flagIndex) + (3 * i + j)]] * voxSize;
                                int ind = idx + windingOrder[j];
                                chunkVerts.Add(vert);
                                chunkIndices.Add(ind);
                                vertCount++;
                            }
                        }

                        edgeVertex.Dispose();

                    }
                }
            }

            //Weld duplicate verts
            var hashMap = new NativeHashMap<float3, int>(60000, Allocator.Temp);
            var newVerts = new NativeList<float3>(Allocator.Temp);
            var newIndices = new NativeList<int>(Allocator.Temp);
            int idy = 0;

            for (int i = 0; i < chunkVerts.Length; i++)
            {
                float3 vert = chunkVerts[i];
                int sharedVertIndex;

                if (hashMap.TryGetValue(vert, out sharedVertIndex))
                {
                    newIndices.Add(sharedVertIndex);
                }
                else
                {
                    hashMap.Add(vert, idy);
                    newVerts.Add(vert);
                    newIndices.Add(idy);

                    idy++;
                }

            }

            for (int i = 0; i < newIndices.Length; i++)
            {
                int vertIndex = index * 60000 + i;
                indices[vertIndex] = newIndices[i];
                if (newVerts.Length > i)
                {
                    verts[vertIndex] = newVerts[i];
                    colors[vertIndex] = GetVertexColor(newVerts[i], chunkVoxels);
                }
            }

            vertLength[index] = newIndices.Length;
        }

        private float GetOffset(float v1, float v2)
        {
            float delta = v2 - v1;
            return (delta == 0.0f) ? surface : (surface - v1) / delta;
        }

        private float4 GetVertexColor(float3 vert, NativeArray<Voxel> vox)
        {
            float4 c = new float4(0, 0, 0, 0);
            float weightSum = 0f;

            for (int i = 0; i < 8; i++)
            {
                int3 offset = new int3((i / 4) % 2, (i / 2) % 2, i % 2);
                int3 v = new int3((int)math.floor(vert.x + offset.x), (int)math.floor(vert.y + offset.y), (int)math.floor(vert.z + offset.z));
                int voxIndex = (v.z * size.y * size.x) + (v.y * size.x) + (v.x);

                float weight = 1f - math.distance(vert, v);
                weight = math.clamp(weight, 0f, 1f);

                switch (vox[voxIndex].material)
                {
                    case Voxel.Material.Dirt:
                        c += new float4(0.31f, 0.22f, 0.13f, 0f) * weight; //Grass
                        weightSum += weight;
                        break;
                    case Voxel.Material.Grass:
                        c += new float4(0.24f, 0.42f, 0.18f, 0f) * weight; //Dirt
                        weightSum += weight;
                        break;
                    case Voxel.Material.Stone:
                        c += new float4(0.3f, 0.3f, 0.3f, 0f) * weight; //Stone
                        weightSum += weight;
                        break;
                    case Voxel.Material.Sand:
                        c += new float4(0.75f, 0.76f, 0.42f, 0f) * weight; //Sand
                        weightSum += weight;
                        break;
                    case Voxel.Material.Snow:
                        c += new float4(0.9f, 0.9f, 0.9f, 0f) * weight; //Snow
                        weightSum += weight;
                        break;
                    default:
                        break;
                }
            }
            c /= weightSum;
            return c;
        }
    }
}