using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(); 
    public float smoothSpeed ; 

    void Start()
    {
        transform.position = target.position + offset;
        transform.rotation = Quaternion.Euler(45, 45, 0); // Изометрический угол
    }

    void LateUpdate()
    {
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        
        transform.rotation = Quaternion.Euler(45, 45, 0);
    }
}
