using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;
using UnityEngine.TextCore;
using Unity.Collections;
using System.Reflection;

[BurstCompile]
public class ChunkGenerator : MonoBehaviour
{

    Mesh mesh;
    List<Vector3> verts;
    List<int> tris;
    List<Vector3> normals;
    byte[] data;

    int[] axisCols;
    int[] faceCols;
    int[] faceRows;

    public int chunkSize;
    int totalChunkSize;
    int totalChunkSize2;

    [SerializeField] float noiseFrequency;
    [SerializeField] float noiseThreshold;

    // Slow meshing
    public void Generate(int chunkSize, float noiseFreq, float noiseThres, Vector3 offset)
    {



        Stopwatch sw = Stopwatch.StartNew();

        totalChunkSize = chunkSize + 2;

        var noiseMap = new NativeArray<float>(totalChunkSize * totalChunkSize * totalChunkSize, Allocator.TempJob);

        var job = new NoiseJob()
        {
            offset = offset,
            totalChunkSize = totalChunkSize,
            noiseThreshold = noiseThres,
            noiseValue = noiseMap
        };

        JobHandle jobHandle = job.Schedule(totalChunkSize * totalChunkSize * totalChunkSize, 1024);

        sw.Stop();
        UnityEngine.Debug.Log("multithreading setup time: " + sw.Elapsed.ToString());
        sw.Restart();

        this.chunkSize = chunkSize;
        noiseFrequency = noiseFreq;
        noiseThreshold = noiseThres;

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        totalChunkSize2 = totalChunkSize * totalChunkSize;
        data = new byte[totalChunkSize * totalChunkSize * totalChunkSize];
        verts = new List<Vector3>();
        normals = new List<Vector3>();
        tris = new List<int>();

        axisCols = new int[totalChunkSize * totalChunkSize * 3];
        faceCols = new int[totalChunkSize * totalChunkSize * 3 * 2];
        faceRows = new int[totalChunkSize * totalChunkSize * 3 * 2];

        sw.Stop();
        UnityEngine.Debug.Log("data setup time: " + sw.Elapsed.ToString());
        sw.Restart();

        //for (int i = 0; i < data.Length; i++)
        //{
        //    //data[i] = (byte)Random.Range(0, 2);

        //    //if (i < 9216 || UnityEngine.Random.Range(0, 102) > 90)
        //    //if ((positionFromIndex(i) - new Vector3(chunkSize / 2, chunkSize / 2, chunkSize / 2)).magnitude < totalChunkSize / 3)
        //    if(perlinNoise.get3DPerlinNoise(positionFromIndex(i) + offset, noiseFrequency) > noiseThreshold)
        //    {
        //        data[i] = 1;
        //    }
        //}

        //for (int x = 0; x < totalChunkSize; x++)
        //{
        //    for (int y = 0; y < totalChunkSize; y++)
        //    {
        //        for (int z = 0; z < totalChunkSize; z++)
        //        {
        //            //if (y < 5 || UnityEngine.Random.Range(0, 102) > 100)
        //            if (perlinNoise.get3DPerlinNoise(new Vector3(x, y, z) + offset, noiseFrequency) > noiseThreshold)
        //            {
        //                data[x + y * totalChunkSize + z * totalChunkSize2] = 1;
        //            }
        //        }
        //    }
        //}

        sw.Stop();
        UnityEngine.Debug.Log("grid generation TYPE 1 time: " + sw.Elapsed.ToString());
        sw.Restart();

        jobHandle.Complete();

        //assigning three axes of ints filled with the same data in a different direction
        for (int x = 0; x < totalChunkSize; x++)
        {
            for (int y = 0; y < totalChunkSize; y++)
            {
                for (int z = 0; z < totalChunkSize; z++)
                {
                    //if (y < 3 || x < 3 || z < 3 || UnityEngine.Random.Range(0, 202) > 200)
                    //if ((new Vector3(x, y, z) - new Vector3(chunkSize / 2, chunkSize / 2, chunkSize / 2)).magnitude < totalChunkSize / 2.5f)
                    //if (perlinNoise.get3DPerlinNoise(new Vector3(x, y, z) + offset, noiseFrequency) > noiseThreshold)

                    //float oh = noise.cnoise(new float3(x, y, z));

                    //UnityEngine.Debug.Log(oh);

                    //if (Perlin.Noise(x, y, z) > noiseThreshold)

                        

                    //UnityEngine.Debug.Log(noiseMap[x + y * totalChunkSize + z * totalChunkSize2]);

                    if (noiseMap[x + y * totalChunkSize + z * totalChunkSize2] > noiseThres)
                    {
                        //x axis, y-z
                        axisCols[y + (z * totalChunkSize)] |= 1 << x;

                        //y axis, x-z
                        axisCols[x + (z * totalChunkSize) + totalChunkSize2] |= 1 << y;

                        //UnityEngine.Debug.Log(GetIntBinaryString(axisCols[x + (z * totalChunkSize) + totalChunkSize]));

                        //z axis, x-y
                        axisCols[x + (y * totalChunkSize) + totalChunkSize2 * 2] |= 1 << z;
                    }
                }
            }
        }

        sw.Stop();
        UnityEngine.Debug.Log("grid generation TYPE 2 time: " + sw.Elapsed.ToString());
        sw.Restart();

        //Stopwatch sw = Stopwatch.StartNew();

        ////MESH GEN TYPE 1: MARCHING THROUGH GRID
        //for (int x = 1; x < chunkSize; x++)
        //{
        //    for (int y = 1; y < chunkSize; y++)
        //    {
        //        for (int z = 1; z < chunkSize; z++)
        //        {
        //            voxelConstructor(x + y * totalChunkSize * totalChunkSize + z * totalChunkSize);
        //        }
        //    }
        //}

        sw.Stop();
        UnityEngine.Debug.Log("mesh generation time 1: " + sw.Elapsed.ToString());
        sw.Restart();

        //MESH GEN TYPE 2: BINARY 
        for (int axis = 0; axis < 3; axis++)
        {
            for (int i = 0; i < totalChunkSize2; i++)
            {
                int col = axisCols[(totalChunkSize2 * axis) + i];
                //my honest reaction



                faceCols[i + totalChunkSize2 * (axis * 2 + 1)] = col & ~(col >> 1);

                //UnityEngine.Debug.Log(GetIntBinaryString(faceCols[i + totalChunkSize * (axis * 2 + 1)]));

                faceCols[i + totalChunkSize2 * (axis * 2 + 0)] = col & ~(col << 1);
            }
        }

        voxelConstructorBinary();
        //greedyFaceConstructor();

        sw.Stop();
        UnityEngine.Debug.Log("mesh generation time 2: " + sw.Elapsed.ToString());
        sw.Restart();

        ChunkGenerationJob chunkGenJob = new ChunkGenerationJob()
        {
            inputMap = noiseMap
        };

        chunkGenJob.Execute();
        //noiseMap.Dispose();

        sw.Stop();
        UnityEngine.Debug.Log("burst compiled generation time: " + sw.Elapsed.ToString());
        sw.Restart();

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetNormals(normals);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);

        sw.Stop();
        UnityEngine.Debug.Log("mesh construction time: " + sw.Elapsed.ToString());

        //int randomInt = UnityEngine.Random.Range(0, int.MaxValue);
        //UnityEngine.Debug.Log(GetIntBinaryString(randomInt));
        //int faceInt = randomInt & ~(randomInt >> 1);
        //UnityEngine.Debug.Log(GetIntBinaryString(faceInt));
        //int ColInt = faceInt >> 1;
        //ColInt = ColInt << 1 << 1 >> 1;
        //faceInt &= faceInt - 1;
        //UnityEngine.Debug.Log(GetIntBinaryString(faceInt));
        //faceInt &= faceInt - 1;
        //UnityEngine.Debug.Log(GetIntBinaryString(faceInt));
        //faceInt &= faceInt - 1;
        //UnityEngine.Debug.Log(GetIntBinaryString(faceInt));

        //int integer = 0 >> 1;
        //UnityEngine.Debug.Log(GetIntBinaryString(~(1 << 0 | 1 << 31)));

        //UnityEngine.Debug.Log(math.tzcnt(0b_1011_0011));

    }

    void voxelConstructorBinary()
    {

        //int with only first and last bit unset
        int boundaryInt = ~(1 << 0 | 1 << 31);

        //x+
        for (int axis = 0; axis < 6; axis++)
        {
            for (int z = 1; z < chunkSize; z++)
            {
                for (int x = 1; x < chunkSize; x++)
                {
                    //loading correct int and calling AND operator to remove first and last bit
                    int col = faceCols[x + (z * totalChunkSize) + (totalChunkSize2 * axis)] & boundaryInt;

                    //UnityEngine.Debug.Log(GetIntBinaryString(col));

                    //shifting back and removing first bit
                    //col = col >> 2;
                    //col = col << 1;
                    //col = 
                    //col = col & ~(1 << totalChunkSize);

                    //UnityEngine.Debug.Log(GetIntBinaryString(col));

                    while (col != 0)
                    {
                        int y = math.tzcnt(col);
                        //clear least significant set bit
                        col &= col - 1;

                        //faceRows[y + (z * totalChunkSize) + (totalChunkSize2 * axis)] |= 1 << x;

                        //x+, y-z

                        //x-, y-z

                        //y+, x-z

                        //y-, x-z

                        //z+, x-y

                        //z-, x-y


                        //regular ole meshing
                        switch (axis)
                        {
                            //x+
                            case 0:
                                faceConstructor(y, x, z, 1);
                                break;
                            //x-
                            case 1:
                                faceConstructor(y, x, z, 0);
                                break;
                            //y+
                            case 2:
                                faceConstructor(x, y, z, 3);
                                break;
                            //y-
                            case 3:
                                faceConstructor(x, y, z, 2);
                                break;
                            //z+
                            case 4:
                                faceConstructor(x, z, y, 5);
                                break;
                            //z-
                            case 5:
                                faceConstructor(x, z, y, 4);
                                break;
                        }

                    }
                }
            }
        }
    }

    void greedyFaceConstructor()
    {
        for (int axis = 0; axis < 6; axis++)
        {
            for (int height = 1; height < chunkSize; height++)
            {
                for (int row = 1; row < chunkSize; row++)
                {

                    int currentRow = faceRows[row + (height * totalChunkSize) + (axis * totalChunkSize2)];

                    //UnityEngine.Debug.Log(GetIntBinaryString(currentRow));

                    int y = 0;
                    while (y < totalChunkSize)
                    {
                        y = math.tzcnt(currentRow);
                        if (y >= totalChunkSize)
                        {
                            continue;
                        }

                        //int currentRow2 = currentRow;

                        //to get the trailing ones, shift to the right by the amount of trailing zeros, then flip and get trailing zeros of new number.
                        int h = math.tzcnt(~(currentRow >> y));

                        //EXPAND HERE

                        faceConstructor(row, height, y, axis, h);

                        //shifting to the right and left to remove the bits we just encountered
                        currentRow = currentRow >> y + h;
                        currentRow = currentRow << y + h;

                        //UnityEngine.Debug.Log(GetIntBinaryString(currentRow));
                    }
                }
            }
        }
    }

    void faceConstructor(int x, int y, int z, int dir, int height)
    {
        switch (dir)
        {
            //x+
            case 1:
                verts.Add(new Vector3(x + 1, y + height, z));
                verts.Add(new Vector3(x + 1, y + 1 + height, z));
                verts.Add(new Vector3(x + 1, y + height, z + height));
                verts.Add(new Vector3(x + 1, y + 1 + height, z + height));

                normals.Add(new Vector3(1, 0, 0)); normals.Add(new Vector3(1, 0, 0));
                normals.Add(new Vector3(1, 0, 0)); normals.Add(new Vector3(1, 0, 0));

                tris.Add(verts.Count - 4); tris.Add(verts.Count - 3); tris.Add(verts.Count - 2);
                tris.Add(verts.Count - 1); tris.Add(verts.Count - 2); tris.Add(verts.Count - 3);
                break;
            //x-
            case 0:
                verts.Add(new Vector3(x, y, z));
                verts.Add(new Vector3(x, y + 1, z));
                verts.Add(new Vector3(x, y, z + height));
                verts.Add(new Vector3(x, y + 1, z + height));

                normals.Add(new Vector3(-1, 0, 0)); normals.Add(new Vector3(-1, 0, 0));
                normals.Add(new Vector3(-1, 0, 0)); normals.Add(new Vector3(-1, 0, 0));

                tris.Add(verts.Count - 4); tris.Add(verts.Count - 2); tris.Add(verts.Count - 3);
                tris.Add(verts.Count - 1); tris.Add(verts.Count - 3); tris.Add(verts.Count - 2);
                break;
            //y+
            case 3:
                verts.Add(new Vector3(x, y + 1, z));
                verts.Add(new Vector3(x + 1, y + 1, z));
                verts.Add(new Vector3(x, y + 1, z + height));
                verts.Add(new Vector3(x + 1, y + 1, z + height));

                normals.Add(new Vector3(0, 1, 0)); normals.Add(new Vector3(0, 1, 0));
                normals.Add(new Vector3(0, 1, 0)); normals.Add(new Vector3(0, 1, 0));

                tris.Add(verts.Count - 4); tris.Add(verts.Count - 2); tris.Add(verts.Count - 3);
                tris.Add(verts.Count - 3); tris.Add(verts.Count - 2); tris.Add(verts.Count - 1);
                break;
            //y-
            case 2:
                verts.Add(new Vector3(x, y, z));
                verts.Add(new Vector3(x + 1, y, z));
                verts.Add(new Vector3(x, y, z + height));
                verts.Add(new Vector3(x + 1, y, z + height));

                normals.Add(new Vector3(0, -1, 0)); normals.Add(new Vector3(0, -1, 0));
                normals.Add(new Vector3(0, -1, 0)); normals.Add(new Vector3(0, -1, 0));

                tris.Add(verts.Count - 4); tris.Add(verts.Count - 3); tris.Add(verts.Count - 2);
                tris.Add(verts.Count - 2); tris.Add(verts.Count - 3); tris.Add(verts.Count - 1);
                break;
            //z+
            case 5:
                verts.Add(new Vector3(x, y, z + 1));
                verts.Add(new Vector3(x + 1, y, z + 1));
                verts.Add(new Vector3(x, y + height, z + 1));
                verts.Add(new Vector3(x + 1, y + height, z + 1));

                normals.Add(new Vector3(0, 0, 1)); normals.Add(new Vector3(0, 0, 1));
                normals.Add(new Vector3(0, 0, 1)); normals.Add(new Vector3(0, 0, 1));

                tris.Add(verts.Count - 2); tris.Add(verts.Count - 4); tris.Add(verts.Count - 3);
                tris.Add(verts.Count - 1); tris.Add(verts.Count - 2); tris.Add(verts.Count - 3);
                break;
            //z-
            case 4:
                verts.Add(new Vector3(x, y, z));
                verts.Add(new Vector3(x + 1, y, z));
                verts.Add(new Vector3(x, y + height, z));
                verts.Add(new Vector3(x + 1, y + height, z));

                normals.Add(new Vector3(0, 0, -1)); normals.Add(new Vector3(0, 0, -1));
                normals.Add(new Vector3(0, 0, -1)); normals.Add(new Vector3(0, 0, -1));

                tris.Add(verts.Count - 2); tris.Add(verts.Count - 3); tris.Add(verts.Count - 4);
                tris.Add(verts.Count - 1); tris.Add(verts.Count - 3); tris.Add(verts.Count - 2);
                break;
        }
    }

    void faceConstructor(int x, int y, int z, int dir)
    {
        switch (dir)
        {
            //x+
            case 0:
                verts.Add(new Vector3(x + 1, y, z));
                verts.Add(new Vector3(x + 1, y + 1, z));
                verts.Add(new Vector3(x + 1, y, z + 1));
                verts.Add(new Vector3(x + 1, y + 1, z + 1));

                normals.Add(new Vector3(1, 0, 0)); normals.Add(new Vector3(1, 0, 0));
                normals.Add(new Vector3(1, 0, 0)); normals.Add(new Vector3(1, 0, 0));

                tris.Add(verts.Count - 4); tris.Add(verts.Count - 3); tris.Add(verts.Count - 2);
                tris.Add(verts.Count - 1); tris.Add(verts.Count - 2); tris.Add(verts.Count - 3);
            break;
            //x-
            case 1:
                verts.Add(new Vector3(x, y, z));
                verts.Add(new Vector3(x, y + 1, z));
                verts.Add(new Vector3(x, y, z + 1));
                verts.Add(new Vector3(x, y + 1, z + 1));

                normals.Add(new Vector3(-1, 0, 0)); normals.Add(new Vector3(-1, 0, 0));
                normals.Add(new Vector3(-1, 0, 0)); normals.Add(new Vector3(-1, 0, 0));

                tris.Add(verts.Count - 4); tris.Add(verts.Count - 2); tris.Add(verts.Count - 3);
                tris.Add(verts.Count - 1); tris.Add(verts.Count - 3); tris.Add(verts.Count - 2);
                break;
            //y+
            case 2:
                verts.Add(new Vector3(x, y + 1, z));
                verts.Add(new Vector3(x + 1, y + 1, z));
                verts.Add(new Vector3(x, y + 1, z + 1));
                verts.Add(new Vector3(x + 1, y + 1, z + 1));

                normals.Add(new Vector3(0, 1, 0)); normals.Add(new Vector3(0, 1, 0));
                normals.Add(new Vector3(0, 1, 0)); normals.Add(new Vector3(0, 1, 0));

                tris.Add(verts.Count - 4); tris.Add(verts.Count - 2); tris.Add(verts.Count - 3);
                tris.Add(verts.Count - 3); tris.Add(verts.Count - 2); tris.Add(verts.Count - 1);
                break;
            //y-
            case 3:
                verts.Add(new Vector3(x, y, z));
                verts.Add(new Vector3(x + 1, y, z));
                verts.Add(new Vector3(x, y, z + 1));
                verts.Add(new Vector3(x + 1, y, z + 1));

                normals.Add(new Vector3(0, -1, 0)); normals.Add(new Vector3(0, -1, 0));
                normals.Add(new Vector3(0, -1, 0)); normals.Add(new Vector3(0, -1, 0));

                tris.Add(verts.Count - 4); tris.Add(verts.Count - 3); tris.Add(verts.Count - 2);
                tris.Add(verts.Count - 2); tris.Add(verts.Count - 3); tris.Add(verts.Count - 1);
                break;
            //z+
            case 4:
                verts.Add(new Vector3(x, y, z + 1));
                verts.Add(new Vector3(x + 1, y, z + 1));
                verts.Add(new Vector3(x, y + 1, z + 1));
                verts.Add(new Vector3(x + 1, y + 1, z + 1));

                normals.Add(new Vector3(0, 0, 1)); normals.Add(new Vector3(0, 0, 1));
                normals.Add(new Vector3(0, 0, 1)); normals.Add(new Vector3(0, 0, 1));

                tris.Add(verts.Count - 2); tris.Add(verts.Count - 4); tris.Add(verts.Count - 3);
                tris.Add(verts.Count - 1); tris.Add(verts.Count - 2); tris.Add(verts.Count - 3);
                break;
            //z-
            case 5:
                verts.Add(new Vector3(x, y, z));
                verts.Add(new Vector3(x + 1, y, z));
                verts.Add(new Vector3(x, y + 1, z));
                verts.Add(new Vector3(x + 1, y + 1, z));

                normals.Add(new Vector3(0, 0, -1)); normals.Add(new Vector3(0, 0, -1));
                normals.Add(new Vector3(0, 0, -1)); normals.Add(new Vector3(0, 0, -1));

                tris.Add(verts.Count - 2); tris.Add(verts.Count - 3); tris.Add(verts.Count - 4);
                tris.Add(verts.Count - 1); tris.Add(verts.Count - 3); tris.Add(verts.Count - 2);
                break;
        }
    }

    void voxelConstructor(int index)
    {
        if (data[index] > 0)
        {
            //top, y+
            if (data[index + (totalChunkSize * totalChunkSize)] == 0)
            {
                verts.Add(positionFromIndex(index) + new Vector3(40, 1, 0));
                verts.Add(positionFromIndex(index) + new Vector3(41, 1, 0));
                verts.Add(positionFromIndex(index) + new Vector3(40, 1, 1));
                verts.Add(positionFromIndex(index) + new Vector3(41, 1, 1));

                normals.Add(new Vector3(0, 1, 0)); normals.Add(new Vector3(0, 1, 0));
                normals.Add(new Vector3(0, 1, 0)); normals.Add(new Vector3(0, 1, 0));

                tris.Add(verts.Count - 4); tris.Add(verts.Count - 2); tris.Add(verts.Count - 3);
                tris.Add(verts.Count - 2); tris.Add(verts.Count - 1); tris.Add(verts.Count - 3);
            }
            //front, x +
            if (data[index + 1] == 0)
            {
                verts.Add(positionFromIndex(index) + new Vector3(41, 0, 0));
                verts.Add(positionFromIndex(index) + new Vector3(41, 1, 0));
                verts.Add(positionFromIndex(index) + new Vector3(41, 0, 1));
                verts.Add(positionFromIndex(index) + new Vector3(41, 1, 1));

                normals.Add(new Vector3(1, 0, 0)); normals.Add(new Vector3(1, 0, 0));
                normals.Add(new Vector3(1, 0, 0)); normals.Add(new Vector3(1, 0, 0));

                tris.Add(verts.Count - 4); tris.Add(verts.Count - 3); tris.Add(verts.Count - 2);
                tris.Add(verts.Count - 1); tris.Add(verts.Count - 2); tris.Add(verts.Count - 3);
            }
            //left, z+
            if (data[index + totalChunkSize] == 0)
            {
                verts.Add(positionFromIndex(index) + new Vector3(40, 0, 1));
                verts.Add(positionFromIndex(index) + new Vector3(41, 0, 1));
                verts.Add(positionFromIndex(index) + new Vector3(40, 1, 1));
                verts.Add(positionFromIndex(index) + new Vector3(41, 1, 1));

                normals.Add(new Vector3(0, 0, 1)); normals.Add(new Vector3(0, 0, 1));
                normals.Add(new Vector3(0, 0, 1)); normals.Add(new Vector3(0, 0, 1));

                tris.Add(verts.Count - 2); tris.Add(verts.Count - 4); tris.Add(verts.Count - 3);
                tris.Add(verts.Count - 1); tris.Add(verts.Count - 2); tris.Add(verts.Count - 3);
            }
            //bottom, y-
            if (data[index - (totalChunkSize * totalChunkSize)] == 0)
            {
                verts.Add(positionFromIndex(index) + new Vector3(40, 0, 0));
                verts.Add(positionFromIndex(index) + new Vector3(41, 0, 0));
                verts.Add(positionFromIndex(index) + new Vector3(40, 0, 1));
                verts.Add(positionFromIndex(index) + new Vector3(41, 0, 1));

                normals.Add(new Vector3(0, -1, 0)); normals.Add(new Vector3(0, -1, 0));
                normals.Add(new Vector3(0, -1, 0)); normals.Add(new Vector3(0, -1, 0));

                tris.Add(verts.Count - 4); tris.Add(verts.Count - 3); tris.Add(verts.Count - 2);
                tris.Add(verts.Count - 2); tris.Add(verts.Count - 3); tris.Add(verts.Count - 1);
            }
            //back, x-
            if (data[index - 1] == 0)
            {
                verts.Add(positionFromIndex(index) + new Vector3(40, 0, 0));
                verts.Add(positionFromIndex(index) + new Vector3(40, 1, 0));
                verts.Add(positionFromIndex(index) + new Vector3(40, 0, 1));
                verts.Add(positionFromIndex(index) + new Vector3(40, 1, 1));

                normals.Add(new Vector3(-1, 0, 0)); normals.Add(new Vector3(-1, 0, 0));
                normals.Add(new Vector3(-1, 0, 0)); normals.Add(new Vector3(-1, 0, 0));

                tris.Add(verts.Count - 4); tris.Add(verts.Count - 2); tris.Add(verts.Count - 3);
                tris.Add(verts.Count - 1); tris.Add(verts.Count - 3); tris.Add(verts.Count - 2);
            }
            //right, z-
            if (data[index - totalChunkSize] == 0)
            {
                verts.Add(positionFromIndex(index) + new Vector3(40, 0, 0));
                verts.Add(positionFromIndex(index) + new Vector3(41, 0, 0));
                verts.Add(positionFromIndex(index) + new Vector3(40, 1, 0));
                verts.Add(positionFromIndex(index) + new Vector3(41, 1, 0));

                normals.Add(new Vector3(0, 0, -1)); normals.Add(new Vector3(0, 0, -1));
                normals.Add(new Vector3(0, 0, -1)); normals.Add(new Vector3(0, 0, -1));

                tris.Add(verts.Count - 2); tris.Add(verts.Count - 3); tris.Add(verts.Count - 4);
                tris.Add(verts.Count - 1); tris.Add(verts.Count - 3); tris.Add(verts.Count - 2);
            }
        }
    }

    Vector3 positionFromIndex(int index)
    {
        //Debug.Log(index);
        //Debug.Log(totalChunkSize * totalChunkSize);
        //Debug.Log(index / (totalChunkSize * totalChunkSize));
        //Debug.Log(new Vector3(index % (totalChunkSize), (index % (totalChunkSize * 2)) / (totalChunkSize), index / (totalChunkSize * 2)));
        return new Vector3(index % (totalChunkSize), index / (totalChunkSize * totalChunkSize), (index % (totalChunkSize * totalChunkSize)) / (totalChunkSize));
    }

    // Update is called once per frame
    void Update()
    {
        
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
}

[BurstCompile]
public struct GenerateJob : IJobParallelFor
{

    public int chunkSize;

    public void Execute(int index)
    {

    }
}

[BurstCompile]
public struct NoiseJob : IJobParallelFor
{

    public float3 offset;
    public int totalChunkSize;
    public float noiseThreshold;
    public NativeArray<float> noiseValue;

    public void Execute(int index)
    {
        noiseValue[index] = NoiseValue(positionFromIndex(index) + offset);

        //noiseValue[index] = noise.cnoise(positionFromIndex(index) + offset);
    }

    float3 positionFromIndex(int index)
    {
        return new float3(index % (totalChunkSize), (index % (totalChunkSize * totalChunkSize)) / (totalChunkSize), index / (totalChunkSize * totalChunkSize));
    }

    public float NoiseValue(float3 samplePos)
    {
        float noiseValue = 0;

        float surfaceHeight = 50f;

        float frequency = 0.01f;
        float amplitude = 1;
        float persistence = 0.84f;
        float roughness = 1.4f;
        float strength = 2f;
        int numLayers = 10;
        float recede = 0;

        for (int i = 0; i < numLayers; i++)
        {
            float v = noise.cnoise(samplePos * frequency);
            noiseValue += (v + 1) * 0.5f * amplitude;
            frequency *= roughness;
            amplitude *= persistence;
            //noiseValue = v;
        }

        //noiseValue = Mathf.Max(0, noiseValue - recede);
        float finalValue = noiseValue * strength;
        //finalValue += ((offset + samplePos).y * (offset + samplePos).y * (offset + samplePos).y) - surfaceHeight;
        return finalValue;
    }
}