using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ball : MonoBehaviour {
    public Rigidbody _rb;

    public void Init(float velocity) 
    {
        _rb.velocity = transform.forward * velocity;
    }
}
