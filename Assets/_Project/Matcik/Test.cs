using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class Test : MonoBehaviour
{
    public float Power = 5000;
    public float Radius = 50;


    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Reset();
        }
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Rigidbody[] rbs = gameObject.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs)
            {
                rb.AddExplosionForce(Power,transform.localPosition, Radius);
            }
        }
    }

    private Vector3[] StartPositions;

    public void Awake()
    {
        StartPositions = new Vector3[transform.childCount];

        for (int i = 0; i < transform.childCount; i++)
        {
            StartPositions[i] = transform.GetChild(i).transform.localPosition;
        }
        
    }

    public void Reset()
    {

        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).transform.localPosition = StartPositions[i];
        }
    }
}



