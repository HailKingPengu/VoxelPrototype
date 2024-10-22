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
using static UnityEditor.PlayerSettings;

public class ChunkJobData
{
    public JobHandle JobHandle;

    public NativeList<float3> verts;
    public NativeList<int> tris;
    public NativeList<float3> normals;

    public ChunkJobData()
    {
        verts = new NativeList<float3>(Allocator.Persistent);
        tris = new NativeList<int>(Allocator.Persistent);
        normals = new NativeList<float3>(Allocator.Persistent);
    }

    public void AttachJob(JobHandle inJobHandle)
    {
        JobHandle = inJobHandle;
    }

    public void DisposeAll()
    {
        verts.Dispose();
        tris.Dispose();
        normals.Dispose();
    }
}

public class WorldGenerator : MonoBehaviour
{

    [SerializeField] Transform player;

    [SerializeField] Material tempMat;

    [SerializeField] int viewDistance;

    //ChunkGenerator[,,] chunks;

    [SerializeField] int chunkSize;

    GameObject[,,] chunkObjects;

    int3 mapOffset;

    List<int3> chunksToUpdate;


    [SerializeField] float noiseFrequency;
    [SerializeField] float noiseThreshold;

    JobHandle[,,] noiseJobHandles;
    JobHandle[,,] chunkJobHandles;

    //List<ChunkJobData> chunkJobs;
    List<JobHandle> noiseJobHandlesList;
    List<JobHandle> chunkJobHandlesList;
    List<int3> noiseJobHandlesPos;
    List<int3> chunkJobHandlesPos;

    NativeArray<float>[,,] noiseMaps;

    NativeList<float3>[,,] verts;
    NativeList<int>[,,] tris;
    NativeList<float3>[,,] normals;


    [Header("generation speed settings")]

    [SerializeField] int maxNoiseUpdates;
    [SerializeField] int maxChunkUpdates;
    [SerializeField] int maxMeshConstructions;


    [Header("noise settings")]

    [SerializeField] ComputeShader cShader;
    RenderTexture texture;

    int noiseSize;

    [SerializeField] float scale;
    [SerializeField] int octaves;
    [SerializeField] float scaleStep;
    [SerializeField] float strengthStep;
    [SerializeField] float strength;


    // Start is called before the first frame update
    void Start()
    {
        mapOffset = new int3((int)player.transform.position.x, (int)player.transform.position.y, (int)player.transform.position.z);

        chunkObjects = new GameObject[viewDistance, viewDistance, viewDistance];
        noiseJobHandles = new JobHandle[viewDistance, viewDistance, viewDistance];
        chunkJobHandles = new JobHandle[viewDistance, viewDistance, viewDistance];

        //chunkJobs = new List<ChunkJobData>();

        noiseJobHandlesList = new List<JobHandle>();
        noiseJobHandlesPos = new List<int3>();
        chunkJobHandlesList = new List<JobHandle>();
        chunkJobHandlesPos = new List<int3>();

        noiseMaps = new NativeArray<float>[viewDistance, viewDistance, viewDistance];

        verts = new NativeList<float3>[viewDistance, viewDistance, viewDistance];
        tris = new NativeList<int>[viewDistance, viewDistance, viewDistance];
        normals = new NativeList<float3>[viewDistance, viewDistance, viewDistance];

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
                    chunkObjects[x, y, z].AddComponent<MeshFilter>();
                    chunkObjects[x, y, z].AddComponent<MeshRenderer>();
                    chunkObjects[x, y, z].GetComponent<MeshRenderer>().material = tempMat;

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

        for (int i = chunksToUpdate.Count - 1; i >= 0 && noiseUpdates < maxNoiseUpdates; i--)
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

            noiseJobHandlesList.Add(job.Schedule(32 * 32 * 32, 128));
            noiseJobHandlesPos.Add(new int3(pos.x, pos.y, pos.z));

            //ComputeNoiseSection(chunksToUpdate[i], 4);



            //ah, there you are
            chunksToUpdate.RemoveAt(i);

            noiseUpdates++;
        }


        for (int i = noiseJobHandlesList.Count - 1; i >= 0 && chunkUpdates < maxChunkUpdates; i--)
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

                //ChunkJobData currentJob = new ChunkJobData();
                //chunkJobs.Add(currentJob);

                //NativeArray<float> inputArray = new NativeArray<float>(32 * 32 * 32, Allocator.TempJob);
                //noiseMaps[pos.x, pos.y, pos.z].CopyTo(inputArray);

                var job = new ChunkGenerationJob()
                {
                    inputMap = noiseMaps[pos.x, pos.y, pos.z],
                    verts = verts[pos.x, pos.y, pos.z],
                    tris = tris[pos.x, pos.y, pos.z],
                    normals = normals[pos.x, pos.y, pos.z],

                    //verts = currentJob.verts,
                    //tris = currentJob.tris,
                    //normals = currentJob.normals,

                    chunkSize = 30,

                };

                chunkJobHandlesList.Add(job.Schedule());
                chunkJobHandlesPos.Add(pos);

                //currentJob.AttachJob(job.Schedule());

                chunkUpdates++;
            }
        }

        for (int i = chunkJobHandlesList.Count - 1; i >= 0 && meshConstructions < maxMeshConstructions; i--)
        {
            if (chunkJobHandlesList[i].IsCompleted)
            {
                //chunkJobs[i].JobHandle.Complete();

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

                //mesh.SetVertices(chunkJobs[i].verts.AsArray().Reinterpret<Vector3>());
                //mesh.SetTriangles(chunkJobs[i].tris.AsArray().ToArray(), 0);
                //mesh.SetNormals(chunkJobs[i].normals.AsArray());

                mesh.UploadMeshData(false);

                noiseMaps[pos.x, pos.y, pos.z].Dispose();

                verts[pos.x, pos.y, pos.z].Dispose();
                tris[pos.x, pos.y, pos.z].Dispose();
                normals[pos.x, pos.y, pos.z].Dispose();

                //chunkJobs[i].DisposeAll();

                chunkObjects[pos.x, pos.y, pos.z].GetComponent<MeshFilter>().mesh = mesh;

                meshConstructions++;
            }
        }
    }

    void RearrangeChunks(int3 newOffset)
    {
        int3 movement = mapOffset - newOffset;

        for (int x = 0; x < viewDistance; x++)
        {
            for (int y = 0; y < viewDistance; y++)
            {
                for (int z = 0; z < viewDistance; z++)
                {
                    chunkObjects[x, y, z] = new GameObject("chunk: " + x + ", " + y + ", " + z);
                    chunkObjects[x, y, z].transform.position = new Vector3(x * (chunkSize - 1), y * (chunkSize - 1), z * (chunkSize - 1));
                    chunkObjects[x, y, z].AddComponent<MeshFilter>();
                    chunkObjects[x, y, z].AddComponent<MeshRenderer>();
                    chunkObjects[x, y, z].GetComponent<MeshRenderer>().material = tempMat;

                    chunksToUpdate.Add(new int3(x, y, z));
                }
            }
        }
    }

    //void ComputeNoiseSection(int3 startingPos, int maxSize)
    //{
    //    for(int x = 0; x < maxSize; x++)
    //    {
    //        if(noiseSe)
    //    }
    //}

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
