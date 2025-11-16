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

//the general container class for saving the chunk's data and manipulating it.
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

public class WorldGenerator : MonoBehaviour
{

    [SerializeField] Transform player;
    int3 playerPosition;

    [SerializeField] Material tempMat;


    [SerializeField] int viewDistance = 12;
    [SerializeField] int chunkSize = 30;

    //terrain data containers
    GameObject[,,] chunkObjects;
    Dictionary<int3, GameObject> chunkDictionary;
    Dictionary<int3, NativeArray<bool>> chunkData;
    Dictionary<int3, NativeArray<int>> chunkModData;
    NativeArray<int> modDataHolder;

    List<int3> chunksToUpdate;

    [SerializeField] float noiseFrequency;
    [SerializeField] float noiseThreshold;

    JobHandle[,,] noiseJobHandles;
    JobHandle[,,] chunkJobHandles;

    List<JobHandle> noiseJobHandlesList;
    List<JobHandle> chunkJobHandlesList;
    List<int3> noiseJobHandlesPos;
    List<int3> chunkJobHandlesPos;

    Dictionary<int3, chunkGenData> chunkGenDictionary;

    [Header("generation speed settings")]

    [SerializeField] int maxNoiseUpdates;
    [SerializeField] int maxChunkUpdates;
    [SerializeField] int maxMeshConstructions;


    [Header("noise settings")]

    [SerializeField] ComputeShader cShader;
    RenderTexture texture;

    [SerializeField] float scale;
    [SerializeField] int octaves;
    [SerializeField] float scaleStep;
    [SerializeField] float strengthStep;
    [SerializeField] float strength;

    int removalUpdateFreq = 5;
    int removalUpdateNum;

    //Initialize all data containers
    void Start()
    {

        chunkObjects = new GameObject[viewDistance, viewDistance, viewDistance];
        noiseJobHandles = new JobHandle[viewDistance, viewDistance, viewDistance];
        chunkJobHandles = new JobHandle[viewDistance, viewDistance, viewDistance];

        noiseJobHandlesList = new List<JobHandle>();
        noiseJobHandlesPos = new List<int3>();
        chunkJobHandlesList = new List<JobHandle>();
        chunkJobHandlesPos = new List<int3>();

        chunksToUpdate = new List<int3>();

        Stopwatch sw = Stopwatch.StartNew();

        chunkDictionary = new Dictionary<int3, GameObject>();
        chunkGenDictionary = new Dictionary<int3, chunkGenData>();
        chunkData = new Dictionary<int3, NativeArray<bool>>();
        chunkModData = new Dictionary<int3, NativeArray<int>>();

        modDataHolder = new NativeArray<int>(32 * 32 * 32, Allocator.Persistent);
    }

    //the main meat of the terrain generator uses a queue to first generate noise, then using binary greeding meshing generates a mesh.
    void Update()
    {
        int noiseUpdates = 0;
        int chunkUpdates = 0;
        int meshConstructions = 0;

        playerPosition = new int3((int)player.position.x / 30, (int)player.position.y / 30, (int)player.position.z / 30);

        //ordering the list of chunks to create a priority queue
        chunksToUpdate = chunksToUpdate.OrderByDescending(
            chunk => math.abs(chunk.x - playerPosition.x) + math.abs(chunk.y - playerPosition.y) + math.abs(chunk.z - playerPosition.z)
            ).ToList();

        RearrangeChunks();

        if (removalUpdateNum >= removalUpdateFreq) RemoveChunks();
        removalUpdateNum++;

        //noise generation, which happens through unity jobs
        for (int i = chunksToUpdate.Count - 1; i >= 0 && noiseUpdates < maxNoiseUpdates; i--)
        {

            int3 pos = chunksToUpdate[i];
            chunkGenDictionary[pos].noiseMap = new NativeArray<float>(32 * 32 * 32, Allocator.Persistent);


            var job = new NoiseJob()
            {
                offset = pos * 29,
                totalChunkSize = 32,
                noiseThreshold = noiseThreshold,
                noiseValue = chunkGenDictionary[pos].noiseMap
            };

            noiseJobHandlesList.Add(job.Schedule(32 * 32 * 32, 128));
            noiseJobHandlesPos.Add(new int3(pos.x, pos.y, pos.z));

            chunksToUpdate.RemoveAt(i);

            noiseUpdates++;
        }

        //noise data then gets turned into mesh data using the binary greedy meshing algorithm, in Unity jobs
        for (int i = noiseJobHandlesList.Count - 1; i >= 0 && chunkUpdates < maxChunkUpdates; i--)
        {
            if (noiseJobHandlesList[i].IsCompleted)
            {

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


                var job = new ChunkGenerationJob()
                {
                    modMap = chunkGenDictionary[pos].modData,
                    inputMap = chunkGenDictionary[pos].noiseMap,
                    verts = chunkGenDictionary[pos].verts,
                    tris = chunkGenDictionary[pos].tris,
                    normals = chunkGenDictionary[pos].normals,
                    outputMap = chunkGenDictionary[pos].data,

                    chunkSize = 30,

                };

                chunkJobHandlesList.Add(job.Schedule());
                chunkJobHandlesPos.Add(pos);

                chunkUpdates++;
            }
        }        

        //lastly this data is turned into Unity's Mesh class
        for (int i = chunkJobHandlesList.Count - 1; i >= 0 && meshConstructions < maxMeshConstructions; i--)
        {
            if (chunkJobHandlesList[i].IsCompleted)
            {

                chunkJobHandlesList[i].Complete();
                chunkJobHandlesList.RemoveAt(i);
                int3 pos = chunkJobHandlesPos[i];

                chunkJobHandlesPos.RemoveAt(i);

                Mesh mesh = new Mesh();
                mesh.SetVertices(chunkGenDictionary[pos].verts.AsArray().Reinterpret<Vector3>());
                mesh.SetTriangles(chunkGenDictionary[pos].tris.AsArray().ToArray(), 0);
                mesh.SetNormals(chunkGenDictionary[pos].normals.AsArray());
                mesh.UploadMeshData(false);

                CreateChunkObject(pos, chunkGenDictionary[pos].data);

                chunkGenDictionary[pos].DisposeAll();
                chunkDictionary[pos].GetComponent<MeshFilter>().mesh = mesh;

                meshConstructions++;
            }
        }
    }

    //initialize an empty chunk in the editor
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

    //if the player moves over a chunk border, add potential chunks that came into range to the queue
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
    
    //if the player moves over a chunk border, remove the chunks that left the viewing range of the player.
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
                Destroy(chunkDictionary[flaggedForRemoval[i]].gameObject);
                chunkDictionary.Remove(flaggedForRemoval[i]);

                chunkGenDictionary[flaggedForRemoval[i]].DisposeAll();
                chunkGenDictionary.Remove(flaggedForRemoval[i]);
            }
        }
    }

    //getting central 30x30x30 of 32x32x32 array
    int indexFromInt3(int3 pos)
    {
        return ((pos.x + 1) + (pos.y + 1) * 32 + (pos.z + 1) * 1024);
    }

    //debugging functions for logging binary data of an int
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

    //same as above, but for an unsigned int
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

            //turns out, % isn't modulo, but remainder.
            //made a Mod function to use here.

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

    //if a chunk's data is modified, this function will regenerate the mesh.
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
