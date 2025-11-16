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
using System.Linq;

public class chunkGenData
{
    public NativeArray<float> noiseMap;
    public NativeList<float3> verts;
    public NativeList<int> tris;
    public NativeList<float3> normals;
    public NativeArray<bool> data;
    public NativeArray<int> modData;

    public void ForceDisposeAll()
    {
        noiseMap.Dispose();

        verts.Dispose();
        tris.Dispose();
        normals.Dispose();
        modData.Dispose();
    }

    public void DisposeAll()
    {
        if(noiseMap.IsCreated) noiseMap.Dispose();

        if (verts.IsCreated) verts.Dispose();
        if (tris.IsCreated) tris.Dispose();
        if (normals.IsCreated) normals.Dispose();
        if (modData.IsCreated) modData.Dispose();
    }
}

//public class ChunkJobData
//{
//    public JobHandle JobHandle;

//    public NativeList<float3> verts;
//    public NativeList<int> tris;
//    public NativeList<float3> normals;

//    public ChunkJobData()
//    {
//        verts = new NativeList<float3>(Allocator.Persistent);
//        tris = new NativeList<int>(Allocator.Persistent);
//        normals = new NativeList<float3>(Allocator.Persistent);
//    }

//    public void AttachJob(JobHandle inJobHandle)
//    {
//        JobHandle = inJobHandle;
//    }

//    public void DisposeAll()
//    {
//        verts.Dispose();
//        tris.Dispose();
//        normals.Dispose();
//    }
//}

public class WorldGenerator : MonoBehaviour
{

    [SerializeField] Transform player;
    int3 playerPosition;

    [SerializeField] Material tempMat;

    [SerializeField] int viewDistance;

    //ChunkGenerator[,,] chunks;

    [SerializeField] int chunkSize;

    GameObject[,,] chunkObjects;
    Dictionary<int3, GameObject> chunkDictionary;
    Dictionary<int3, NativeArray<bool>> chunkData;
    Dictionary<int3, NativeArray<int>> chunkModData;
    NativeArray<int> modDataHolder;

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

    Dictionary<int3, chunkGenData> chunkGenDictionary;

    //NativeArray<float>[,,] noiseMaps;

    //NativeList<float3>[,,] verts;
    //NativeList<int>[,,] tris;
    //NativeList<float3>[,,] normals;


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

    int removalUpdateFreq = 5;
    int removalUpdateNum;

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

        //noiseMaps = new NativeArray<float>[viewDistance, viewDistance, viewDistance];

        //verts = new NativeList<float3>[viewDistance, viewDistance, viewDistance];
        //tris = new NativeList<int>[viewDistance, viewDistance, viewDistance];
        //normals = new NativeList<float3>[viewDistance, viewDistance, viewDistance];

        chunksToUpdate = new List<int3>();

        Stopwatch sw = Stopwatch.StartNew();

        chunkDictionary = new Dictionary<int3, GameObject>();
        chunkGenDictionary = new Dictionary<int3, chunkGenData>();
        chunkData = new Dictionary<int3, NativeArray<bool>>();
        chunkModData = new Dictionary<int3, NativeArray<int>>();

        modDataHolder = new NativeArray<int>(32 * 32 * 32, Allocator.Persistent);

        for (int x = 0; x < viewDistance; x++)
        {
            for (int y = 0; y < viewDistance; y++)
            {
                for (int z = 0; z < viewDistance; z++)
                {
                    //chunkObjects[x, y, z] = new GameObject("chunk: " + x + ", " + y + ", " + z);
                    //chunkObjects[x, y, z].transform.position = new Vector3(x * (chunkSize - 1), y * (chunkSize - 1), z * (chunkSize - 1));
                    //chunkObjects[x, y, z].AddComponent<MeshFilter>();
                    //chunkObjects[x, y, z].AddComponent<MeshRenderer>();
                    //chunkObjects[x, y, z].GetComponent<MeshRenderer>().material = tempMat;

                    //chunksToUpdate.Add(new int3(x, y, z));
                }
            }
        }

        //int h = 3;
        //int y1 = 2;

        //uint uEmpty = 0;
        //uint hMask = ((~uEmpty << 32 - h) >> y1);

        //UnityEngine.Debug.Log(GetIntBinaryString(hMask));

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


        playerPosition = new int3((int)player.position.x / 30, (int)player.position.y / 30, (int)player.position.z / 30);

        chunksToUpdate = chunksToUpdate.OrderByDescending(
            chunk => math.abs(chunk.x - playerPosition.x) + math.abs(chunk.y - playerPosition.y) + math.abs(chunk.z - playerPosition.z)
            ).ToList();

        RearrangeChunks();

        if (removalUpdateNum >= removalUpdateFreq)
        {
            RemoveChunks();
        }
        removalUpdateNum++;

        for (int i = chunksToUpdate.Count - 1; i >= 0 && noiseUpdates < maxNoiseUpdates; i--)
        {

            int3 pos = chunksToUpdate[i];
            //noiseMaps[pos.x, pos.y, pos.z] = new NativeArray<float>(32 * 32 * 32, Allocator.Persistent);
            chunkGenDictionary[pos].noiseMap = new NativeArray<float>(32 * 32 * 32, Allocator.Persistent);
            //noiseDictionary.Add(pos, new NativeArray<float>(32 * 32 * 32, Allocator.Persistent));



            var job = new NoiseJob()
            {
                offset = pos * 29,
                //offset = chunkObjects[pos.x, pos.y, pos.z].transform.position,
                totalChunkSize = 32,
                noiseThreshold = noiseThreshold,
                noiseValue = chunkGenDictionary[pos].noiseMap
                //noiseMaps[pos.x, pos.y, pos.z]
            };

            noiseJobHandlesList.Add(job.Schedule(32 * 32 * 32, 128));
            noiseJobHandlesPos.Add(new int3(pos.x, pos.y, pos.z));

            //ComputeNoiseSection(chunksToUpdate[i], 4);



            //ah, there you are
            chunksToUpdate.RemoveAt(i);

            //UnityEngine.Debug.Log(math.abs(pos.x - playerPosition.x) + math.abs(pos.y - playerPosition.y) + math.abs(pos.z - playerPosition.z));

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



                chunkGenDictionary[pos].verts = new NativeList<float3>(Allocator.Persistent);
                chunkGenDictionary[pos].tris = new NativeList<int>(Allocator.Persistent);
                chunkGenDictionary[pos].normals = new NativeList<float3>(Allocator.Persistent);
                chunkGenDictionary[pos].data = new NativeArray<bool>(32 * 32 * 32, Allocator.Persistent);
                chunkGenDictionary[pos].modData = new NativeArray<int>(32 * 32 * 32, Allocator.Persistent);

                if (chunkModData.ContainsKey(pos)) chunkGenDictionary[pos].modData.CopyFrom(chunkModData[pos]);

                //ChunkJobData currentJob = new ChunkJobData();
                //chunkJobs.Add(currentJob);

                //NativeArray<float> inputArray = new NativeArray<float>(32 * 32 * 32, Allocator.TempJob);
                //noiseMaps[pos.x, pos.y, pos.z].CopyTo(inputArray);



                var job = new ChunkGenerationJob()
                {
                    modMap = chunkGenDictionary[pos].modData,
                    inputMap = chunkGenDictionary[pos].noiseMap,
                    verts = chunkGenDictionary[pos].verts,
                    tris = chunkGenDictionary[pos].tris,
                    normals = chunkGenDictionary[pos].normals,
                    outputMap = chunkGenDictionary[pos].data,

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
                mesh.SetVertices(chunkGenDictionary[pos].verts.AsArray().Reinterpret<Vector3>());
                mesh.SetTriangles(chunkGenDictionary[pos].tris.AsArray().ToArray(), 0);
                mesh.SetNormals(chunkGenDictionary[pos].normals.AsArray());

                //mesh.SetVertices(chunkJobs[i].verts.AsArray().Reinterpret<Vector3>());
                //mesh.SetTriangles(chunkJobs[i].tris.AsArray().ToArray(), 0);
                //mesh.SetNormals(chunkJobs[i].normals.AsArray());

                mesh.UploadMeshData(false);

                CreateChunkObject(pos, chunkGenDictionary[pos].data);

                chunkGenDictionary[pos].DisposeAll();

                chunkDictionary[pos].GetComponent<MeshFilter>().mesh = mesh;

                //chunkObjects[pos.x, pos.y, pos.z].GetComponent<MeshFilter>().mesh = mesh;

                meshConstructions++;
            }
        }
    }

    void CreateChunkObject(int3 pos, NativeArray<bool> data)
    {
        if (!chunkDictionary.ContainsKey(pos))
        {
            GameObject newChunk = new GameObject("chunk: " + pos.x + ", " + pos.y + ", " + pos.z);
            newChunk.transform.position = new Vector3(pos.x * (chunkSize - 1), pos.y * (chunkSize - 1), pos.z * (chunkSize - 1));
            newChunk.AddComponent<MeshFilter>();
            newChunk.AddComponent<MeshRenderer>();
            newChunk.GetComponent<MeshRenderer>().material = tempMat;

            chunkDictionary.Add(pos, newChunk);
            chunkData[pos] = data;
        }
    }

    void RearrangeChunks()
    {
        for (int x = -viewDistance + playerPosition.x; x < viewDistance + playerPosition.x; x++)
        {
            for (int y = -viewDistance + playerPosition.y; y < viewDistance + playerPosition.y; y++)
            {
                for (int z = -viewDistance + playerPosition.z; z < viewDistance + playerPosition.z; z++)
                {
                    if(!chunkGenDictionary.ContainsKey(new int3(x, y, z)))
                    {
                        chunksToUpdate.Add(new int3(x, y, z));

                        chunkGenDictionary.Add(new int3(x, y, z), new chunkGenData());
                    }
                }
            }
        }
    }

    void RemoveChunks()
    {

        List<int3> flaggedForRemoval = new List<int3>();

        foreach(KeyValuePair<int3, GameObject> entry in chunkDictionary)
        {
            int3 pos = entry.Key;

            if(pos.x > playerPosition.x + viewDistance * 1.4 || pos.y > playerPosition.y + viewDistance * 1.4 || pos.z > playerPosition.z + viewDistance * 1.4 || pos.x < playerPosition.x - viewDistance * 1.4 || pos.y < playerPosition.y - viewDistance * 1.4 || pos.z < playerPosition.z - viewDistance * 1.4)
            {
                flaggedForRemoval.Add(pos);
            }
        }

        for(int i = 0; i < flaggedForRemoval.Count; i++)
        {
            if (chunkDictionary.ContainsKey(flaggedForRemoval[i]))
            {
                //UnityEngine.Debug.Log("Something deleted");
                Destroy(chunkDictionary[flaggedForRemoval[i]].gameObject);
                chunkDictionary.Remove(flaggedForRemoval[i]);

                chunkGenDictionary[flaggedForRemoval[i]].DisposeAll();
                chunkGenDictionary.Remove(flaggedForRemoval[i]);
            }
        }

        //for (int x = 0; x < viewDistance; x++)
        //{
        //    for (int y = 0; y < viewDistance; y++)
        //    {
        //        for (int z = 0; z < viewDistance; z++)
        //        {
        //            chunkObjects[x, y, z] = new GameObject("chunk: " + x + ", " + y + ", " + z);
        //            chunkObjects[x, y, z].transform.position = new Vector3(x * (chunkSize - 1), y * (chunkSize - 1), z * (chunkSize - 1));
        //            chunkObjects[x, y, z].AddComponent<MeshFilter>();
        //            chunkObjects[x, y, z].AddComponent<MeshRenderer>();
        //            chunkObjects[x, y, z].GetComponent<MeshRenderer>().material = tempMat;

        //            chunksToUpdate.Add(new int3(x, y, z));
        //        }
        //    }
        //}
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

    //getting central 30x30x30 of 32x32x32 array
    int indexFromInt3(int3 pos)
    {
        return ((pos.x + 1) + (pos.y + 1) * 32 + (pos.z + 1) * 1024);
    }

    static string GetIntBinaryString(int n)
    {
        char[] b = new char[32];
        int pos = 31;
        int i = 0;

        while (i < 32)
        {
            if ((n & (1 << i)) != 0)
            {
                b[pos] = '1';
            }
            else
            {
                b[pos] = '0';
            }
            pos--;
            i++;
        }
        return new string(b);
    }

    static string GetIntBinaryString(uint n)
    {
        char[] b = new char[32];
        int pos = 31;
        int i = 0;

        while (i < 32)
        {
            if ((n & (1 << i)) != 0)
            {
                b[pos] = '1';
            }
            else
            {
                b[pos] = '0';
            }
            pos--;
            i++;
        }
        return new string(b);
    }

    public bool checkVoxel(int3 position)
    {

        int3 chunkPos = new int3(Mathf.FloorToInt((float)position.x / 29), Mathf.FloorToInt((float)position.y / 29), Mathf.FloorToInt((float)position.z / 29));

        if (chunkData.ContainsKey(chunkPos))
        {
            //UnityEngine.Debug.Log(position);
            //UnityEngine.Debug.Log(position / 30);
            //UnityEngine.Debug.Log(position % 30);
            //UnityEngine.Debug.Log(new int3 ((int)Mod(position.x, 30), (int)Mod(position.y, 30), (int)Mod(position.z, 30)));

            //turns out, % isn't modulo, but remainder. good to know.

            if (chunkModData.ContainsKey(chunkPos))
            {
                if(chunkModData[chunkPos][indexFromInt3(new int3((int)Mod(position.x, 29), (int)Mod(position.y, 29), (int)Mod(position.z, 29)))] == 1)
                {
                    return true;
                }
            }

            return chunkData[chunkPos][indexFromInt3(new int3((int)Mod(position.x, 29), (int)Mod(position.y, 29), (int)Mod(position.z, 29)))];
        }

        return false;
    }

    public static float Mod(float a, float b)
    {
        float c = a % b;
        if ((c < 0 && b > 0) || (c > 0 && b < 0))
        {
            c += b;
        }
        return c;
    }

    public void modifyTerrain(int3 position)
    {

        int3 chunkPos = new int3(Mathf.FloorToInt((float)position.x / 29), Mathf.FloorToInt((float)position.y / 29), Mathf.FloorToInt((float)position.z / 29));

        if (!chunkModData.ContainsKey(chunkPos))
        {
            NativeArray<int> newArray = new NativeArray<int>(32 * 32 * 32, Allocator.Persistent);
            chunkModData.Add(chunkPos, newArray);
        }

        NativeArray<int> tempArray = new NativeArray<int>(32 * 32 * 32, Allocator.Persistent);
        
        chunkModData[chunkPos].CopyTo(tempArray);
            
        tempArray[indexFromInt3(new int3((int)Mod(position.x, 29), (int)Mod(position.y, 29), (int)Mod(position.z, 29)))] = 1;

        chunkModData[chunkPos].CopyFrom(tempArray);

        tempArray.Dispose();

        chunksToUpdate.Add(chunkPos);

    }
}
