using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using System;
using System.Linq;

[BurstCompile]
public struct ChunkGenerationJob : IJob
{

    public NativeArray<float> inputMap;

    public NativeList<float3> verts;
    public NativeList<int> tris;
    public NativeList<float3> normals;

    public int chunkSize;
    int totalChunkSize;
    int totalChunkSize2;

    //public NativeArray<int> axisCols;
    //public NativeArray<int> faceCols;
    //public NativeArray<int> faceRows;

    int numVerts;

    public void Execute()
    {

        //verts = new NativeList<float3>();
        //tris = new NativeList<int>();
        //normals = new NativeList<float3>();

        totalChunkSize = chunkSize + 2;
        totalChunkSize2 = totalChunkSize * totalChunkSize;

        NativeArray<int> axisCols = new NativeArray<int>(totalChunkSize * totalChunkSize * 3, Allocator.Temp);
        NativeArray<int> faceCols = new NativeArray<int>(totalChunkSize * totalChunkSize * 3 * 2, Allocator.Temp);

        numVerts = 0;

        //assigning three axes of ints filled with the same data in a different direction
        for (int x = 0; x < totalChunkSize; x++)
        {
            for (int y = 0; y < totalChunkSize; y++)
            {
                for (int z = 0; z < totalChunkSize; z++)
                {
                    if (inputMap[x + y * totalChunkSize + z * totalChunkSize2] > 5)
                    {
                        //x axis, y-z
                        axisCols[y + (z * totalChunkSize)] |= 1 << x;

                        //y axis, x-z
                        axisCols[x + (z * totalChunkSize) + totalChunkSize2] |= 1 << y;

                        //z axis, x-y
                        axisCols[x + (y * totalChunkSize) + totalChunkSize2 * 2] |= 1 << z;
                    }
                }
            }
        }

        //inputMap.Dispose();

        //setting up face columns by bitshifting and comparing in both directions for each axis
        for (int axis = 0; axis < 3; axis++)
        {
            for (int i = 0; i < totalChunkSize2; i++)
            {
                int col = axisCols[(totalChunkSize2 * axis) + i];

                faceCols[i + totalChunkSize2 * (axis * 2 + 1)] = col & ~(col >> 1);

                faceCols[i + totalChunkSize2 * (axis * 2 + 0)] = col & ~(col << 1);
            }
        }

        //VOXELCONSTRUCTOR

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

                    while (col != 0)
                    {

                        int y = math.tzcnt(col);
                        //clear least significant set bit
                        col &= col - 1;

                        //faceRows[y + (z * totalChunkSize) + (totalChunkSize2 * axis)] |= 1 << x;

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

        axisCols.Dispose();
        faceCols.Dispose();

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

                numVerts += 4;

                normals.Add(new Vector3(1, 0, 0)); normals.Add(new Vector3(1, 0, 0));
                normals.Add(new Vector3(1, 0, 0)); normals.Add(new Vector3(1, 0, 0));

                tris.Add(numVerts - 4); tris.Add(numVerts - 3); tris.Add(numVerts - 2);
                tris.Add(numVerts - 1); tris.Add(numVerts - 2); tris.Add(numVerts - 3);
                break;
            //x-
            case 1:
                verts.Add(new Vector3(x, y, z));
                verts.Add(new Vector3(x, y + 1, z));
                verts.Add(new Vector3(x, y, z + 1));
                verts.Add(new Vector3(x, y + 1, z + 1));

                numVerts += 4;

                normals.Add(new Vector3(-1, 0, 0)); normals.Add(new Vector3(-1, 0, 0));
                normals.Add(new Vector3(-1, 0, 0)); normals.Add(new Vector3(-1, 0, 0));

                tris.Add(numVerts - 4); tris.Add(numVerts - 2); tris.Add(numVerts - 3);
                tris.Add(numVerts - 1); tris.Add(numVerts - 3); tris.Add(numVerts - 2);
                break;
            //y+
            case 2:
                verts.Add(new Vector3(x, y + 1, z));
                verts.Add(new Vector3(x + 1, y + 1, z));
                verts.Add(new Vector3(x, y + 1, z + 1));
                verts.Add(new Vector3(x + 1, y + 1, z + 1));

                numVerts += 4;

                normals.Add(new Vector3(0, 1, 0)); normals.Add(new Vector3(0, 1, 0));
                normals.Add(new Vector3(0, 1, 0)); normals.Add(new Vector3(0, 1, 0));

                tris.Add(numVerts - 4); tris.Add(numVerts - 2); tris.Add(numVerts - 3);
                tris.Add(numVerts - 3); tris.Add(numVerts - 2); tris.Add(numVerts - 1);
                break;
            //y-
            case 3:
                verts.Add(new Vector3(x, y, z));
                verts.Add(new Vector3(x + 1, y, z));
                verts.Add(new Vector3(x, y, z + 1));
                verts.Add(new Vector3(x + 1, y, z + 1));

                numVerts += 4;

                normals.Add(new Vector3(0, -1, 0)); normals.Add(new Vector3(0, -1, 0));
                normals.Add(new Vector3(0, -1, 0)); normals.Add(new Vector3(0, -1, 0));

                tris.Add(numVerts - 4); tris.Add(numVerts - 3); tris.Add(numVerts - 2);
                tris.Add(numVerts - 2); tris.Add(numVerts - 3); tris.Add(numVerts - 1);
                break;
            //z+
            case 4:
                verts.Add(new Vector3(x, y, z + 1));
                verts.Add(new Vector3(x + 1, y, z + 1));
                verts.Add(new Vector3(x, y + 1, z + 1));
                verts.Add(new Vector3(x + 1, y + 1, z + 1));

                numVerts += 4;

                normals.Add(new Vector3(0, 0, 1)); normals.Add(new Vector3(0, 0, 1));
                normals.Add(new Vector3(0, 0, 1)); normals.Add(new Vector3(0, 0, 1));

                tris.Add(numVerts - 2); tris.Add(numVerts - 4); tris.Add(numVerts - 3);
                tris.Add(numVerts - 1); tris.Add(numVerts - 2); tris.Add(numVerts - 3);
                break;
            //z-
            case 5:
                verts.Add(new Vector3(x, y, z));
                verts.Add(new Vector3(x + 1, y, z));
                verts.Add(new Vector3(x, y + 1, z));
                verts.Add(new Vector3(x + 1, y + 1, z));

                numVerts += 4;

                normals.Add(new Vector3(0, 0, -1)); normals.Add(new Vector3(0, 0, -1));
                normals.Add(new Vector3(0, 0, -1)); normals.Add(new Vector3(0, 0, -1));

                tris.Add(numVerts - 2); tris.Add(numVerts - 3); tris.Add(numVerts - 4);
                tris.Add(numVerts - 1); tris.Add(numVerts - 3); tris.Add(numVerts - 2);
                break;
        }
    }

}

