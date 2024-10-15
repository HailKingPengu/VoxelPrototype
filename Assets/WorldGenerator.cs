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
using NUnit.Framework.Constraints;

public class WorldGenerator : MonoBehaviour
{

    [SerializeField] Transform player;

    [SerializeField] Material tempMat;

    [SerializeField] int viewDistance;

    ChunkGenerator[,,] chunks;

    [SerializeField] int chunkSize;

    GameObject[,,] chunkObjects;

    Vector3 playerPosition;

    List<int3> chunksToUpdate;


    [SerializeField] float noiseFrequency;
    [SerializeField] float noiseThreshold;

    JobHandle[,,] noiseJobHandles;
    JobHandle[,,] chunkJobHandles;

    List<JobHandle> noiseJobHandlesList;
    List<JobHandle> chunkJobHandlesList;
    List<int3> noiseJobHandlesPos;
    List<int3> chunkJobHandlesPos;

    NativeArray<float>[,,] noiseMaps;

    NativeList<float3>[,,] verts;
    NativeList<int>[,,] tris;
    NativeList<float3>[,,] normals;

    int time;

    [SerializeField] int maxNoiseUpdates;
    [SerializeField] int maxChunkUpdates;
    [SerializeField] int maxMeshConstructions;

    //private void Update()
    //{
    //    if (time == 10) Generate();

    //    time++;
    //}

    // Start is called before the first frame update
    void Start()
    {

        chunkObjects = new GameObject[viewDistance, viewDistance, viewDistance];
        chunks = new ChunkGenerator[viewDistance, viewDistance, viewDistance];
        noiseJobHandles = new JobHandle[viewDistance, viewDistance, viewDistance];
        chunkJobHandles = new JobHandle[viewDistance, viewDistance, viewDistance];

        noiseJobHandlesList = new List<JobHandle>();
        noiseJobHandlesPos = new List<int3>();
        chunkJobHandlesList = new List<JobHandle>();
        chunkJobHandlesPos = new List<int3>();

        noiseMaps = new NativeArray<float>[viewDistance, viewDistance, viewDistance];

        verts = new NativeList<float3>[viewDistance, viewDistance, viewDistance];
        tris = new NativeList<int>[viewDistance, viewDistance, viewDistance];
        normals = new NativeList<float3>[viewDistance, viewDistance, viewDistance];

        //chunksToUpdate = new List<ChunkGenerator>();
        chunksToUpdate = new List<int3>();

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

                    chunksToUpdate.Add(new int3(x, y, z));
                }
            }
        }

        {
            //sw.Stop();
            //UnityEngine.Debug.Log("setup time: " + sw.Elapsed.ToString());
            //sw.Restart();

            //for (int x = 0; x < viewDistance; x++)
            //{
            //    for (int y = 0; y < viewDistance; y++)
            //    {
            //        for (int z = 0; z < viewDistance; z++)
            //        {
            //            noiseJobHandles[x,y,z].Complete();

            //            //for(int i = 0; i < 32; i++)
            //            //{
            //            //    UnityEngine.Debug.Log(noiseMaps[x, y, z][i]);
            //            //}


            //        }
            //    }
            //}

            //sw.Stop();
            //UnityEngine.Debug.Log("noise reading and chunk setup time: " + sw.Elapsed.ToString());
            //sw.Restart();

            //for (int x = 0; x < viewDistance; x++)
            //{
            //    for (int y = 0; y < viewDistance; y++)
            //    {
            //        for (int z = 0; z < viewDistance; z++)
            //        {
            //            chunkJobHandles[x, y, z].Complete();

            //            Mesh mesh = new Mesh();
            //            mesh.SetVertices(verts[x, y, z].AsArray().Reinterpret<Vector3>());
            //            mesh.SetTriangles(tris[x, y, z].AsArray().ToArray(), 0);
            //            mesh.SetNormals(normals[x, y, z].AsArray());
            //            mesh.UploadMeshData(false);

            //            chunks[x, y, z].GetComponent<MeshFilter>().mesh = mesh;
            //        }
            //    }
            //}

            //sw.Stop();
            //UnityEngine.Debug.Log("mesh gen time: " + sw.Elapsed.ToString());
        }
    }

    void Update()
    {
        int noiseUpdates = 0;
        int chunkUpdates = 0;
        int meshConstructions = 0;

        for (int i = chunksToUpdate.Count - 1; i >= 0 && noiseUpdates < maxMeshConstructions; i--)
        {
            int3 pos = chunksToUpdate[i];
            noiseMaps[pos.x, pos.y, pos.z] = new NativeArray<float>(32 * 32 * 32, Allocator.Persistent);

            var job = new NoiseJob()
            {
                offset = chunkObjects[pos.x, pos.y, pos.z].transform.position,
                totalChunkSize = 32,
                noiseThreshold = noiseThreshold,
                noiseValue = noiseMaps[pos.x, pos.y, pos.z]
            };

            //noiseJobHandles[x,y,z] = job.Schedule(32 * 32 * 32, 128);
            noiseJobHandlesList.Add(job.Schedule(32 * 32 * 32, 128));
            noiseJobHandlesPos.Add(new int3(pos.x, pos.y, pos.z));

            //ah, there you are
            chunksToUpdate.RemoveAt(i);

            noiseUpdates++;
        }


        for (int i = noiseJobHandlesList.Count - 1; i >= 0 && chunkUpdates < maxNoiseUpdates; i--)
        {
            if (noiseJobHandlesList[i].IsCompleted)
            {

                //UnityEngine.Debug.Log(noiseJobHandlesList[i].IsCompleted);

                noiseJobHandlesList[i].Complete();
                noiseJobHandlesList.RemoveAt(i);
                int3 pos = noiseJobHandlesPos[i];
                noiseJobHandlesPos.RemoveAt(i);

                verts[pos.x, pos.y, pos.z] = new NativeList<float3>(Allocator.Persistent);
                tris[pos.x, pos.y, pos.z] = new NativeList<int>(Allocator.Persistent);
                normals[pos.x, pos.y, pos.z] = new NativeList<float3>(Allocator.Persistent);



                //NativeArray<float> inputArray = new NativeArray<float>(32 * 32 * 32, Allocator.TempJob);
                //noiseMaps[pos.x, pos.y, pos.z].CopyTo(inputArray);

                var job = new ChunkGenerationJob()
                {
                    inputMap = noiseMaps[pos.x, pos.y, pos.z],
                    verts = verts[pos.x, pos.y, pos.z],
                    tris = tris[pos.x, pos.y, pos.z],
                    normals = normals[pos.x, pos.y, pos.z],
                    chunkSize = 30,

                };

                //chunkJobHandles[x, y, z] = job.Schedule();
                chunkJobHandlesList.Add(job.Schedule());
                chunkJobHandlesPos.Add(pos);

                chunkUpdates++;
            }
        }

        for (int i = chunkJobHandlesList.Count - 1; i >= 0 && meshConstructions < maxChunkUpdates; i--)
        {
            if (chunkJobHandlesList[i].IsCompleted)
            {

                chunkJobHandlesList[i].Complete();
                chunkJobHandlesList.RemoveAt(i);
                int3 pos = chunkJobHandlesPos[i];

                //disposing of the noise map as it's done it's thing
                //noiseMaps[pos.x, pos.y, pos.z].Dispose();

                chunkJobHandlesPos.RemoveAt(i);

                Mesh mesh = new Mesh();
                mesh.SetVertices(verts[pos.x, pos.y, pos.z].AsArray().Reinterpret<Vector3>());
                mesh.SetTriangles(tris[pos.x, pos.y, pos.z].AsArray().ToArray(), 0);
                mesh.SetNormals(normals[pos.x, pos.y, pos.z].AsArray());
                mesh.UploadMeshData(false);

                noiseMaps[pos.x, pos.y, pos.z].Dispose();

                verts[pos.x, pos.y, pos.z].Dispose();
                tris[pos.x, pos.y, pos.z].Dispose();
                normals[pos.x, pos.y, pos.z].Dispose();

                chunks[pos.x, pos.y, pos.z].GetComponent<MeshFilter>().mesh = mesh;

                meshConstructions++;
            }
        }
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
