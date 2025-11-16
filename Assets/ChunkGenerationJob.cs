using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using System;
using System.Linq;
using Unity.VisualScripting;

[BurstCompile]
public struct ChunkGenerationJob : IJob
{

    public NativeArray<int> modMap;
    public NativeArray<float> inputMap;

    public NativeList<float3> verts;
    public NativeList<int> tris;
    public NativeList<float3> normals;

    public NativeArray<bool> outputMap;

    public int chunkSize;
    int totalChunkSize;
    int totalChunkSize2;

    int numVerts;

    public void Execute()
    {

        totalChunkSize = chunkSize + 2;
        totalChunkSize2 = totalChunkSize * totalChunkSize;

        NativeArray<int> axisCols = new NativeArray<int>(totalChunkSize * totalChunkSize * 3, Allocator.Temp);
        NativeArray<int> faceCols = new NativeArray<int>(totalChunkSize * totalChunkSize * 3 * 2, Allocator.Temp);
        NativeArray<int> faceRows = new NativeArray<int>(totalChunkSize * totalChunkSize * 3 * 2, Allocator.Temp);

        numVerts = 0;

        //assigning three axes of ints filled with the same data in a different direction
        for (int x = 0; x < totalChunkSize; x++)
        {
            for (int y = 0; y < totalChunkSize; y++)
            {
                for (int z = 0; z < totalChunkSize; z++)
                {
                    if (inputMap[x + y * totalChunkSize + z * totalChunkSize2] > 5 || modMap[x + y * totalChunkSize + z * totalChunkSize2] >= 1)
                    //test bench for getting coordinates right
                    //(math.distance(new float3(x,y * 0.8f,z * 1.2f), new float3(totalChunkSize/2, totalChunkSize/2, totalChunkSize/2)) < 10 && x < 20 && y < 20 && z < 20)
                    {
                        //x axis, y-z
                        axisCols[y + (z * totalChunkSize)] |= 1 << x;

                        //y axis, x-z
                        axisCols[x + (z * totalChunkSize) + totalChunkSize2] |= 1 << y;

                        //z axis, x-y
                        axisCols[x + (y * totalChunkSize) + totalChunkSize2 * 2] |= 1 << z;

                        outputMap[x + y * totalChunkSize + z * totalChunkSize2] = true;
                    }
                    else
                    {
                        outputMap[x + y * totalChunkSize + z * totalChunkSize2] = false;
                    }
                }
            }
        }

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
        //empty insigned integer
        uint uEmpty = 0;

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

                        //greedy meshing
                        faceRows[y + (z * totalChunkSize) + (totalChunkSize2 * axis)] |= 1 << x;

                        //regular ole meshing
                        //switch (axis)
                        //{
                        //    //x+
                        //    case 0:
                        //        faceConstructor(y, x, z, 1);
                        //        break;
                        //    //x-
                        //    case 1:
                        //        faceConstructor(y, x, z, 0);
                        //        break;
                        //    //y+
                        //    case 2:
                        //        faceConstructor(x, y, z, 3);
                        //        break;
                        //    //y-
                        //    case 3:
                        //        faceConstructor(x, y, z, 2);
                        //        break;
                        //    //z+
                        //    case 4:
                        //        faceConstructor(x, z, y, 5);
                        //        break;
                        //    //z-
                        //    case 5:
                        //        faceConstructor(x, z, y, 4);
                        //        break;
                        //}

                    }
                }
            }
        }

        for (int axis = 0; axis < 6; axis++)
        {
            for (int height = 1; height < chunkSize; height++)
            {
                for (int row = 1; row < chunkSize; row++)
                {

                    int currentRow = faceRows[row + (height * totalChunkSize) + (axis * totalChunkSize2)];

                    int y = 0;
                    while (y < totalChunkSize)
                    {
                        y = math.tzcnt(currentRow);
                        if (y >= totalChunkSize)
                        {
                            continue;
                        }

                        //to get the trailing ones, shift to the right by the amount of trailing zeros, then flip and get trailing zeros of new number.
                        int h = math.tzcnt(~(currentRow >> y));

                        ////14 billion years ago, expansion started here
                        ////first get the height mask of the current face we are creating
                        //uint umask = ((~uEmpty << 32 - h) >> y);
                        //int hMask = (int)umask;

                        ////expand whilst we are within the size of a single chunk
                        //int w = 1;
                        //while (row + w < chunkSize)
                        //{
                        //    //get the next row and call AND operator on the mask
                        //    int hNext = faceRows[row + w + (height * totalChunkSize) + (axis * totalChunkSize2)] & hMask;

                        //    //if the next row is not the same as current row, we can't expand
                        //    if (hNext != hMask) break;

                        //    //remove the bits we just expanded into
                        //    faceRows[row + w + (height * totalChunkSize) + (axis * totalChunkSize2)] =
                        //    faceRows[row + w + (height * totalChunkSize) + (axis * totalChunkSize2)] & ~hMask;
                        //    w++;
                        //}


                        faceConstructor(row, height, y, axis, h);

                        //shifting to the right and left to remove the bits we just encountered
                        currentRow = currentRow >> y + h;
                        currentRow = currentRow << y + h;
                    }
                }
            }
        }

        axisCols.Dispose();
        faceCols.Dispose();
        faceRows.Dispose();

    }

    //greedy meshing with width
    void faceConstructor(int x, int y, int z, int dir, int height, int width)
    {
        switch (dir)
        {
            ////x+
            case 1:
                verts.Add(new Vector3(x + 1, z, y));
                verts.Add(new Vector3(x + 1, z, y + width));
                verts.Add(new Vector3(x + 1, z + height, y));
                verts.Add(new Vector3(x + 1, z + height, y + width));

                numVerts += 4;

                normals.Add(new Vector3(1, 0, 0)); normals.Add(new Vector3(1, 0, 0));
                normals.Add(new Vector3(1, 0, 0)); normals.Add(new Vector3(1, 0, 0));

                tris.Add(numVerts - 4); tris.Add(numVerts - 2); tris.Add(numVerts - 3);
                tris.Add(numVerts - 1); tris.Add(numVerts - 3); tris.Add(numVerts - 2);
                break;
            ////x-
            case 0:
                verts.Add(new Vector3(x, z, y));
                verts.Add(new Vector3(x, z, y + width));
                verts.Add(new Vector3(x, z + height, y));
                verts.Add(new Vector3(x, z + height, y + width));

                numVerts += 4;

                normals.Add(new Vector3(-1, 0, 0)); normals.Add(new Vector3(-1, 0, 0));
                normals.Add(new Vector3(-1, 0, 0)); normals.Add(new Vector3(-1, 0, 0));

                tris.Add(numVerts - 4); tris.Add(numVerts - 3); tris.Add(numVerts - 2);
                tris.Add(numVerts - 1); tris.Add(numVerts - 2); tris.Add(numVerts - 3);
                break;
            ////y+
            case 3:
                verts.Add(new Vector3(z, x + 1, y));
                verts.Add(new Vector3(z, x + 1, y + width));
                verts.Add(new Vector3(z + height, x + 1, y));
                verts.Add(new Vector3(z + height, x + 1, y + width));

                numVerts += 4;

                normals.Add(new Vector3(0, 1, 0)); normals.Add(new Vector3(0, 1, 0));
                normals.Add(new Vector3(0, 1, 0)); normals.Add(new Vector3(0, 1, 0));

                tris.Add(numVerts - 4); tris.Add(numVerts - 3); tris.Add(numVerts - 2);
                tris.Add(numVerts - 3); tris.Add(numVerts - 1); tris.Add(numVerts - 2);
                break;
            ////y-
            case 2:
                verts.Add(new Vector3(z, x, y));
                verts.Add(new Vector3(z, x, y + width));
                verts.Add(new Vector3(z + height, x, y));
                verts.Add(new Vector3(z + height, x, y + width));

                numVerts += 4;

                normals.Add(new Vector3(0, -1, 0)); normals.Add(new Vector3(0, -1, 0));
                normals.Add(new Vector3(0, -1, 0)); normals.Add(new Vector3(0, -1, 0));

                tris.Add(numVerts - 4); tris.Add(numVerts - 2); tris.Add(numVerts - 3);
                tris.Add(numVerts - 2); tris.Add(numVerts - 1); tris.Add(numVerts - 3);
                break;
            ////z+
            case 4:
                verts.Add(new Vector3(z, y, x));
                verts.Add(new Vector3(z, y + width, x));
                verts.Add(new Vector3(z + height, y, x));
                verts.Add(new Vector3(z + height, y + width, x));

                numVerts += 4;

                normals.Add(new Vector3(0, 0, -1)); normals.Add(new Vector3(0, 0, -1));
                normals.Add(new Vector3(0, 0, -1)); normals.Add(new Vector3(0, 0, -1));

                tris.Add(numVerts - 2); tris.Add(numVerts - 4); tris.Add(numVerts - 3);
                tris.Add(numVerts - 1); tris.Add(numVerts - 2); tris.Add(numVerts - 3);
                break;
            ////z-
            case 5:
                verts.Add(new Vector3(z, y, x + 1));
                verts.Add(new Vector3(z, y + width, x + 1));
                verts.Add(new Vector3(z + height, y, x + 1));
                verts.Add(new Vector3(z + height, y + width, x + 1));

                numVerts += 4;

                normals.Add(new Vector3(0, 0, 1)); normals.Add(new Vector3(0, 0, 1));
                normals.Add(new Vector3(0, 0, 1)); normals.Add(new Vector3(0, 0, 1));

                tris.Add(numVerts - 2); tris.Add(numVerts - 3); tris.Add(numVerts - 4);
                tris.Add(numVerts - 1); tris.Add(numVerts - 3); tris.Add(numVerts - 2);
                break;
        }
    }

    //greedy meshing
    void faceConstructor(int x, int y, int z, int dir, int height)
    {
        switch (dir)
        {
            ////x+
            case 1:
                verts.Add(new Vector3(x + 1, z, y));
                verts.Add(new Vector3(x + 1, z, y + 1));
                verts.Add(new Vector3(x + 1, z + height, y));
                verts.Add(new Vector3(x + 1, z + height, y + 1));

                numVerts += 4;

                normals.Add(new Vector3(1, 0, 0)); normals.Add(new Vector3(1, 0, 0));
                normals.Add(new Vector3(1, 0, 0)); normals.Add(new Vector3(1, 0, 0));

                tris.Add(numVerts - 4); tris.Add(numVerts - 2); tris.Add(numVerts - 3);
                tris.Add(numVerts - 1); tris.Add(numVerts - 3); tris.Add(numVerts - 2);
                break;
            ////x-
            case 0:
                verts.Add(new Vector3(x, z, y));
                verts.Add(new Vector3(x, z, y + 1));
                verts.Add(new Vector3(x, z + height, y));
                verts.Add(new Vector3(x, z + height, y + 1));

                numVerts += 4;

                normals.Add(new Vector3(-1, 0, 0)); normals.Add(new Vector3(-1, 0, 0));
                normals.Add(new Vector3(-1, 0, 0)); normals.Add(new Vector3(-1, 0, 0));

                tris.Add(numVerts - 4); tris.Add(numVerts - 3); tris.Add(numVerts - 2);
                tris.Add(numVerts - 1); tris.Add(numVerts - 2); tris.Add(numVerts - 3);
                break;
            ////y+
            case 3:
                verts.Add(new Vector3(z, x + 1, y));
                verts.Add(new Vector3(z, x + 1, y + 1));
                verts.Add(new Vector3(z + height, x + 1, y));
                verts.Add(new Vector3(z + height, x + 1, y + 1));

                numVerts += 4;

                normals.Add(new Vector3(0, 1, 0)); normals.Add(new Vector3(0, 1, 0));
                normals.Add(new Vector3(0, 1, 0)); normals.Add(new Vector3(0, 1, 0));

                tris.Add(numVerts - 4); tris.Add(numVerts - 3); tris.Add(numVerts - 2);
                tris.Add(numVerts - 3); tris.Add(numVerts - 1); tris.Add(numVerts - 2);
                break;
            ////y-
            case 2:
                verts.Add(new Vector3(z, x, y));
                verts.Add(new Vector3(z, x, y + 1));
                verts.Add(new Vector3(z + height, x, y));
                verts.Add(new Vector3(z + height, x, y + 1));

                numVerts += 4;

                normals.Add(new Vector3(0, -1, 0)); normals.Add(new Vector3(0, -1, 0));
                normals.Add(new Vector3(0, -1, 0)); normals.Add(new Vector3(0, -1, 0));

                tris.Add(numVerts - 4); tris.Add(numVerts - 2); tris.Add(numVerts - 3);
                tris.Add(numVerts - 2); tris.Add(numVerts - 1); tris.Add(numVerts - 3);
                break;
            ////z+
            case 4:
                verts.Add(new Vector3(z, y, x));
                verts.Add(new Vector3(z, y + 1, x));
                verts.Add(new Vector3(z + height, y, x));
                verts.Add(new Vector3(z + height, y + 1, x));

                numVerts += 4;

                normals.Add(new Vector3(0, 0, -1)); normals.Add(new Vector3(0, 0, -1));
                normals.Add(new Vector3(0, 0, -1)); normals.Add(new Vector3(0, 0, -1));

                tris.Add(numVerts - 2); tris.Add(numVerts - 4); tris.Add(numVerts - 3);
                tris.Add(numVerts - 1); tris.Add(numVerts - 2); tris.Add(numVerts - 3);
                break;
            ////z-
            case 5:
                verts.Add(new Vector3(z, y, x + 1));
                verts.Add(new Vector3(z, y + 1, x + 1));
                verts.Add(new Vector3(z + height, y, x + 1));
                verts.Add(new Vector3(z + height, y + 1, x + 1));

                numVerts += 4;

                normals.Add(new Vector3(0, 0, 1)); normals.Add(new Vector3(0, 0, 1));
                normals.Add(new Vector3(0, 0, 1)); normals.Add(new Vector3(0, 0, 1));

                tris.Add(numVerts - 2); tris.Add(numVerts - 3); tris.Add(numVerts - 4);
                tris.Add(numVerts - 1); tris.Add(numVerts - 3); tris.Add(numVerts - 2);
                break;
        }
    }

    //regular meshing
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

