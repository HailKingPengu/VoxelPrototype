using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Collections;
using System.Diagnostics;

public class WorldGenerator : MonoBehaviour
{

    [SerializeField] Material tempMat;

    [SerializeField] int viewDistance;

    ChunkGenerator[,,] chunks;

    [SerializeField] int chunkSize;

    GameObject[,,] chunkObjects;

    Vector3 playerPosition;

    List<ChunkGenerator> chunksToUpdate;


    [SerializeField] float noiseFrequency;
    [SerializeField] float noiseThreshold;

    JobHandle[,,] noiseJobHandles;
    JobHandle[,,] chunkJobHandles;

    NativeArray<float>[,,] noiseMaps;

    NativeList<float3>[,,] verts;
    NativeList<int>[,,] tris;
    NativeList<float3>[,,] normals;

    int time;

    private void Update()
    {
        if (time == 10) Generate();

        time++;
    }

    // Start is called before the first frame update
    void Generate()
    {

        chunkObjects = new GameObject[viewDistance, viewDistance, viewDistance];
        chunks = new ChunkGenerator[viewDistance, viewDistance, viewDistance];
        noiseJobHandles = new JobHandle[viewDistance, viewDistance, viewDistance];
        chunkJobHandles = new JobHandle[viewDistance, viewDistance, viewDistance];

        noiseMaps = new NativeArray<float>[viewDistance, viewDistance, viewDistance];

        verts = new NativeList<float3>[viewDistance, viewDistance, viewDistance];
        tris = new NativeList<int>[viewDistance, viewDistance, viewDistance];
        normals = new NativeList<float3>[viewDistance, viewDistance, viewDistance];

        chunksToUpdate = new List<ChunkGenerator>();

        Stopwatch sw = Stopwatch.StartNew();

        for (int x = 0; x < viewDistance; x++)
        {
            for (int y = 0; y < viewDistance; y++)
            {
                for (int z = 0; z < viewDistance; z++)
                {
                    chunkObjects[x, y, z] = new GameObject("chunk: " + x + ", " + y + ", " + z);
                    chunkObjects[x, y, z].transform.position = new Vector3(x * (chunkSize - 1), y * (chunkSize - 1), z * (chunkSize - 1));
                    chunks[x, y, z] = chunkObjects[x, y, z].AddComponent<ChunkGenerator>();
                    chunks[x, y, z].AddComponent<MeshFilter>();
                    chunks[x, y, z].AddComponent<MeshRenderer>();
                    chunks[x, y, z].GetComponent<MeshRenderer>().material = tempMat;

                    chunksToUpdate.Add(chunks[x, y, z]);

                    noiseMaps[x,y,z] = new NativeArray<float>(32 * 32 * 32, Allocator.TempJob);

                    var job = new NoiseJob()
                    {
                        offset = chunkObjects[x, y, z].transform.position,
                        totalChunkSize = 32,
                        noiseThreshold = noiseThreshold,
                        noiseValue = noiseMaps[x, y, z]
                    };

                    noiseJobHandles[x,y,z] = job.Schedule(32 * 32 * 32, 128);

                }
            }
        }

        sw.Stop();
        UnityEngine.Debug.Log("setup time: " + sw.Elapsed.ToString());
        sw.Restart();

        for (int x = 0; x < viewDistance; x++)
        {
            for (int y = 0; y < viewDistance; y++)
            {
                for (int z = 0; z < viewDistance; z++)
                {
                    noiseJobHandles[x,y,z].Complete();

                    //for(int i = 0; i < 32; i++)
                    //{
                    //    UnityEngine.Debug.Log(noiseMaps[x, y, z][i]);
                    //}

                    verts[x, y, z] = new NativeList<float3>(Allocator.TempJob);
                    tris[x, y, z] = new NativeList<int>(Allocator.TempJob);
                    normals[x, y, z] = new NativeList<float3>(Allocator.TempJob);



                    var job = new ChunkGenerationJob()
                    {
                        inputMap = noiseMaps[x, y, z],
                        verts = verts[x, y, z],
                        tris = tris[x, y, z],
                        normals = normals[x, y, z],
                        chunkSize = 30,
                        axisCols = new NativeArray<int>(32 * 32 * 3, Allocator.TempJob),
                        faceCols = new NativeArray<int>(32 * 32 * 3 * 2, Allocator.TempJob),
                        faceRows = new NativeArray<int>(32 * 32 * 3 * 2, Allocator.TempJob)
                    };

                    chunkJobHandles[x, y, z] = job.Schedule();
                }
            }
        }

        sw.Stop();
        UnityEngine.Debug.Log("noise reading and chunk setup time: " + sw.Elapsed.ToString());
        sw.Restart();

        for (int x = 0; x < viewDistance; x++)
        {
            for (int y = 0; y < viewDistance; y++)
            {
                for (int z = 0; z < viewDistance; z++)
                {
                    chunkJobHandles[x, y, z].Complete();

                    Mesh mesh = new Mesh();
                    mesh.SetVertices(verts[x, y, z].AsArray().Reinterpret<Vector3>());
                    mesh.SetTriangles(tris[x, y, z].AsArray().ToArray(), 0);
                    mesh.SetNormals(normals[x, y, z].AsArray());
                    mesh.UploadMeshData(false);

                    chunks[x, y, z].GetComponent<MeshFilter>().mesh = mesh;
                }
            }
        }

        sw.Stop();
        UnityEngine.Debug.Log("mesh gen time: " + sw.Elapsed.ToString());
    }

    // Update is called once per frame
    //void Update()
    //{
    //    if (chunksToUpdate.Count > 0)
    //    {
    //        //chunksToUpdate[0].Generate(chunkSize, noiseFrequency, noiseThreshold, chunksToUpdate[0].transform.position + Vector3.one);
    //        chunksToUpdate.RemoveAt(0);
    //    }
    //    if (chunksToUpdate.Count > 0)
    //    {
    //        //chunksToUpdate[0].Generate(chunkSize, noiseFrequency, noiseThreshold, chunksToUpdate[0].transform.position + Vector3.one);
    //        chunksToUpdate.RemoveAt(0);
    //    }
    //    if (chunksToUpdate.Count > 0)
    //    {
    //        //chunksToUpdate[0].Generate(chunkSize, noiseFrequency, noiseThreshold, chunksToUpdate[0].transform.position + Vector3.one);
    //        chunksToUpdate.RemoveAt(0);
    //    }
    //}

    int indexFromInt3(int x, int y, int z)
    {
        return(x + y * viewDistance + z * viewDistance * viewDistance);
    }
}
