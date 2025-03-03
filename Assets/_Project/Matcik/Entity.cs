using UnityEngine;
using System;
using UnityEngine.UI;

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
    Grenade = 1 << 5,
    ExpGem = 1 << 6,
}

public class Entity : MonoBehaviour
{
    [Header("Editor")] public EntityType type;
    public MeshRenderer mr;
    public Material healedMat;
    public Material notHealedMat;
    public Material damagedMat;
    public float rotationSpeed;
    public float speed;
    public AudioClip deathSound;
    public AudioSource deathAudioSource;
    public bool isDead;
    public bool isCollisionEnabled;
    public EntityType collisionEntityType;
    public Color originalColor;
    public bool isTakingDamage;


    [Header("Breakable")] public GameObject replacement;
    public float breakForce;
    public float collisionMultiplier;
    public bool broken;
    public Collision Collision;

    [Header("Explosive")] public Ball ballPrefab;
    public float triggerForce;
    public float explosionRadius;
    public float explosionForce;
    public float upwardsModifier;
    public float forceDamping;
    public GameObject particles;
    public bool IsGrenade;


    [Header("ProjThrow")] public Transform ballSpawn;
    public float projVelocity;


    [Header("BaseStats")] public float health;
    public float defense;
    public float damage;
    public bool canDealDamage;
    public float attackSpeed;
    public float maxHealth;
    public int exp;

    public GameObject hpBarPrefab; // Префаб HP бара
    public GameObject hpBarInstance; // Экземпляр HP бара
    public Image hpBarForeground;
    public Image hpBarBackround;
// ТУДУ хп плеера?(UI) визуал?


    public Vector3 moveDirection;

    [Header("Runtime")] public int boxesCount;
    public bool isHealed;

    public bool HasBox()
    {
        return boxesCount > 0;
    }


    public void OnCollisionEnter(Collision other)
    {
        MyGame game = FindObjectOfType<MyGame>();
        if (game != null)
        {
            game.HandleCollision(this, other);
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

