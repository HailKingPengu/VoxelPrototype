using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    Camera cam;
    Vector3 anchorPoint;
    Quaternion anchorRot;

    [SerializeField]
    float moveSpeed;
    float updateMoveSpeed;

    [SerializeField]
    float sensitivity;

    bool mouseDown;

    [SerializeField]
    VoxelRaycast VoxelRaycast;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {

        if (Input.GetKey(KeyCode.LeftShift))
            updateMoveSpeed = moveSpeed * 10;
        else
            updateMoveSpeed = moveSpeed;

        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W))
            move += Vector3.forward * updateMoveSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.S))
            move -= Vector3.forward * updateMoveSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.D))
            move += Vector3.right * updateMoveSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.A))
            move -= Vector3.right * updateMoveSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E))
            move += Vector3.up * updateMoveSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.Q))
            move -= Vector3.up * updateMoveSpeed * Time.deltaTime;
        transform.Translate(move);

        if (Input.GetMouseButton(1))
        {
            if (mouseDown)
            {
                Quaternion rot = anchorRot;
                Vector3 dif = anchorPoint - new Vector3(Input.mousePosition.y, -Input.mousePosition.x);
                rot.eulerAngles += dif * sensitivity * Time.deltaTime;
                transform.rotation = rot;
            }

            mouseDown = true;
            anchorPoint = new Vector3(Input.mousePosition.y, -Input.mousePosition.x);
            anchorRot = transform.rotation;
        }
        else
        {
            mouseDown = false;
        }

        //if(Input.GetMouseButton(0))
        //{
        //    VoxelRaycast.Raycast(400, Camera.main.transform.position, Camera.main.ScreenPointToRay(Input.mousePosition).direction);
        //}
    }
}
