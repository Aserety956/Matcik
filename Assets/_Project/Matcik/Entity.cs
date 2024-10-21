using UnityEngine;
using System;

// NOTE(sqd): Sparse Entity System

[Flags]
public enum EntityType
{
    None = 0,
    Player = 1 << 0,
    Zombie = 1 << 1,
    Potion = 1 << 2,
    Gun = 1 << 3,
}

public class Entity : MonoBehaviour
{
    [Header("Editor")]
    public EntityType type;
    public MeshRenderer mr;
    public Material healedMat;
    public Material notHealedMat;
    public float rotationSpeed;
    public float speed;
    public AudioClip deathSound;
    public AudioSource deathAudioSource;
    public Entity targetEntity;
    public bool isDead;
    
    [Header("Breakable")]
    public GameObject replacement;
    public float breakForce;
    public float collisionMultiplier;
    public bool broken;
    public Collision collision;
    
    [Header("Explosive")]
    public float triggerForce;
    public float explosionRadius;
    public float explosionForce;
    public GameObject particles;
    

    public Vector3 moveDirection;

    [Header("Runtime")]
    public int potionsCount;
    public bool isHealed;

    public bool HasPotion()
    {
        return potionsCount > 0;
    }
}

// public void DoGame()
// {
//     GoToProstitutochnoyaFor(Sex);
//     GoToProstitutochnoyaFor(Masturbation);
// }

// public bool GoToProstitutochnoyaFor(Func<bool> SomeFunction)
// {
//     PrepareForKonchit();
//     bool isSuccess = SomeFunction();
//     return isSuccess;
// }

// public void PrepareForKonchit()
// {
// }

// public bool Sex()
// {
//     // logic for sex
//     if (aids)
//     {
//         return false;
//     }
//     else
//     {
//         return true;
//     }
// }

// public bool Masturbation()
// {
//     return gotPregnant;
// }

