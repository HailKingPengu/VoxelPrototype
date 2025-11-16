using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.UI.Image;

public class VoxelRaycast : MonoBehaviour
{

    [SerializeField] WorldGenerator worldGenerator;

    //List<Vector3> hitPoints;
    //List<Vector3> digPoints;
        
    // Start is called before the first frame update
    void Start()
    {
        //hitPoints = new List<Vector3>(); 
        //digPoints = new List<Vector3>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public int3 Raycast(int maxSteps, Vector3 startingPos, Vector3 rayDir)
    {

        int stepsLeft = maxSteps;

        int3 startingCube = new int3(startingPos);

        Vector3 pos = startingPos;
        Vector3 dir = rayDir;

        int3 currentPos = startingCube;
        int3 nextPos;

        Vector3 dX;
        Vector3 dY;
        Vector3 dZ;

        float fX;
        float fY;
        float fZ;

        int delta;

        while (stepsLeft > 0)
        {
            //positive X axis
            if (dir.x >= 0)
            {
                float nextX = MathF.Floor(pos.x) + 1;
                dX = (dir / dir.x) * (nextX - pos.x);
                fX = dX.magnitude;
                delta = 1;
            }
            //negative X axis
            else
            {
                float nextX = MathF.Ceiling(pos.x) - 1;
                dX = (dir / dir.x) * (nextX - pos.x);
                fX = dX.magnitude;
                delta = -1;
            }

            //positive Y axis
            if (dir.y >= 0)
            {
                float nextY = MathF.Floor(pos.y) + 1;
                dY = (dir / dir.y) * (nextY - pos.y);
                fY = dY.magnitude;
                delta = 1;
            }
            //negative Y axis
            else
            {
                float nextY = MathF.Ceiling(pos.y) - 1;
                dY = (dir / dir.y) * (nextY - pos.y);
                fY = dY.magnitude;
                delta = -1;
            }

            //positive Z axis
            if (dir.z >= 0)
            {
                float nextZ = MathF.Floor(pos.z) + 1;
                dZ = (dir / dir.z) * (nextZ - pos.z);
                fZ = dZ.magnitude;
                delta = 1;
            }
            //negative Z axis
            else
            {
                float nextZ = MathF.Ceiling(pos.z) - 1;
                dZ = (dir / dir.z) * (nextZ - pos.z);
                fZ = dZ.magnitude;
                delta = -1;
            }

            if (fX < fY && fX < fZ)
            {
                pos += dX * delta;
                nextPos = currentPos + new int3(delta, 0, 0);

                //Debug.Log(dX);
            }
            else if (fY < fX && fY < fZ)
            {
                pos += dY * delta;
                nextPos = currentPos + new int3(0, delta, 0);

                //Debug.Log(dY);
            }
            else
            {
                pos += dZ * delta;
                nextPos = currentPos + new int3(0, 0, delta);

                //Debug.Log(dZ);
            }

            //// X is smaller than Y
            //if (fX < fY)
            //{
            //    //X is smallest
            //    if (fX < fZ)
            //    {
            //        pos += dX * delta;
            //        nextPos = currentPos + new int3(delta, 0, 0);

            //        Debug.Log(dX);
            //    }
            //    //Z is smallest
            //    else
            //    {
            //        pos += dZ * delta;
            //        nextPos = currentPos + new int3(0, 0, delta);

            //        Debug.Log(dZ);
            //    }
            //}
            ////Z is smallest
            //else if (fY < fZ)
            //{
            //    pos += dZ * delta;
            //    nextPos = currentPos + new int3(0, 0, delta);

            //    Debug.Log(dZ);
            //}
            ////Y is smallest
            //else
            //{
            //    pos += dY * delta;
            //    nextPos = currentPos + new int3(0, delta, 0);

            //    Debug.Log(dY);
            //}

            //hitPoints.Add(pos);

            if (worldGenerator.checkVoxel(new int3(pos)))
            {
                Debug.Log(currentPos);
                Debug.DrawLine(startingPos, pos, Color.black, 1000);
                //digPoints.Add(pos);

                worldGenerator.modifyTerrain(new int3(pos - 0.4f * dir.normalized));

                return nextPos;
            }
            else
            {
                currentPos = nextPos;
            }

            stepsLeft--;
        }

        Debug.Log("No hit!");
        return new int3(0, 0, int.MaxValue);

    }

    //private void OnDrawGizmosSelected()
    //{

    //    //Debug.Log(hitPoints.Count);

    //    Gizmos.color = Color.yellow;
    //    foreach (Vector3 hit in hitPoints)
    //    {
    //        Gizmos.DrawSphere(hit, 0.2f);
    //    }

    //    Gizmos.color = Color.red;
    //    foreach (Vector3 hit in digPoints)
    //    {
    //        Gizmos.DrawSphere(hit, 0.2f);
    //    }
    //}
}
