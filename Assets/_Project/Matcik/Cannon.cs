using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cannon : MonoBehaviour {
     public Ball ballPrefab;

     public Transform ballSpawn;

     public float velocity = 10;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) 
        {
            Ball ball;
            ball = Instantiate(ballPrefab, ballSpawn.position, ballSpawn.rotation);
            ball.Init(velocity);
        }
    }
}
