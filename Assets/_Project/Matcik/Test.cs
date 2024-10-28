using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.SocialPlatforms;

public class Test : MonoBehaviour
{
    public float Power = 5000;
    public float Radius = 50;


    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Rigidbody[] rbs = gameObject.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs)
            {
                rb.useGravity = false;
                rb.isKinematic = true;
            }
            Reset();
        }
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Rigidbody[] rbs = gameObject.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs)
            {
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.AddExplosionForce(Power,transform.localPosition, Radius);
            }
        }
    }

    private Vector3[] startPositions;
    private Quaternion[] startRotation;

    public void Awake()
    {
        startPositions = new Vector3[transform.childCount];
        startRotation = new Quaternion[transform.childCount];

        for (int i = 0; i < transform.childCount; i++)
        {
            startPositions[i] = transform.GetChild(i).transform.localPosition;
            startRotation[i] = transform.GetChild(i).transform.localRotation;
        }
    }

    public void Reset()
    {

        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).transform.localPosition = startPositions[i];
            transform.GetChild(i).transform.localRotation = startRotation[i];
        }
        
    }
}



