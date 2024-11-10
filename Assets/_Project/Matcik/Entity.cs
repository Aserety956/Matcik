using UnityEngine;
using System;

// NOTE(sqd): Sparse Entity System

[Flags]
public enum EntityType
{
    None = 0,
    Player = 1 << 0,
    Zombie = 1 << 1,
    Box = 1 << 2,
    Buff = 1 << 3,
    Projectile = 1 << 4,
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
    public bool isDead;
    public bool isCollisionEnabled;
    public EntityType collisionEntityType;


    [Header("Breakable")] public GameObject replacement;
    public float breakForce;
    public float collisionMultiplier;
    public bool broken;
    public Collision collision;

    [Header("Explosive")] public Ball ballPrefab;
    public float triggerForce;
    public float explosionRadius;
    public float explosionForce;
    public float upwardsModifier;
    public float forceDamping;
    public GameObject particles;


    [Header("ProjThrow")] 
    public Transform ballSpawn;
    public float projVelocity;
    
    
    [Header ("BaseStats")]
    public float health;
    public float defense;
    public float damage;
    public bool canDealDamage;
    public float attackSpeed;


    public Vector3 moveDirection;

    [Header("Runtime")] 
    public int boxesCount;
    public bool isHealed;

    public bool HasBox()
    {
        return boxesCount > 0;
    }

    public void OnCollisionEnter(Collision other)
    {
        Entity e = other.gameObject.GetComponent<Entity>();

        if (e == null) return;

        // Проверяем, соответствует ли тип столкновения, инициализируем взрыв
        if (isCollisionEnabled && (e.type | collisionEntityType) == collisionEntityType)
        {
            collision = other;
            Debug.Log(gameObject.name + " collided with " + other.gameObject.name);
        }
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

