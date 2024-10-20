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
    public AudioSource DeathAudioSource;
    public Entity targetEntity;
    public bool isDead = false; 

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

