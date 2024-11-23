using UnityEngine;
using System;
using System.Collections;
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
}

public class Entity : MonoBehaviour
{
    [Header("Editor")] 
    public EntityType type;
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
    public Collision collision;

    [Header("Explosive")] public Ball ballPrefab;
    public float triggerForce;
    public float explosionRadius;
    public float explosionForce;
    public float upwardsModifier;
    public float forceDamping;
    public GameObject particles;
    public bool IsGrenade;


    [Header("ProjThrow")] 
    public Transform ballSpawn;
    public float projVelocity;
    
    
    [Header ("BaseStats")]
    public float health;
    public float defense;
    public float damage;
    public bool canDealDamage;
    public float attackSpeed;
    public float maxHealth;
    
    public GameObject hpBarPrefab; // Префаб HP бара
    public GameObject hpBarInstance; // Экземпляр HP бара
    public Image hpBarForeground;
    public Image hpBarBackround;
// ТУДУ хп плеера?(UI) визуал?


    public Vector3 moveDirection;

    [Header("Runtime")] 
    public int boxesCount;
    public bool isHealed;

    public bool HasBox()
    {
        return boxesCount > 0;
    }
    
    // public void Start()
    // {
    //     health = maxHealth;
    //     CreateHealthBar();
    //     UpdateHealthBar();
    // }
    public void OnCollisionEnter(Collision other)
    {
        MyGame game = FindObjectOfType<MyGame>();
        if (game != null)
        {
            game.HandleCollision(this, other);
        }
    }

    // public void OnCollisionEnter(Collision other)
    // {
    //     Entity e = other.gameObject.GetComponent<Entity>();
    //
    //     if (e == null) return;
    //
    //     // Проверяем, соответствует ли тип столкновения, инициализируем взрыв
    //     if (isCollisionEnabled && (e.type | collisionEntityType) == collisionEntityType)
    //     {
    //         collision = other;
    //         Debug.Log(gameObject.name + " collided with " + other.gameObject.name);
    //     }
    //     if (e.type == EntityType.Projectile && other.relativeVelocity.magnitude >= breakForce)
    //         {
    //             Destroy(other.gameObject); // Уничтожаем Ball при столкновении
    //             ApplyHitEffect();
    //             TakeDamage(35);
    //             Debug.Log($"{gameObject.name} уничтожил объект {other.gameObject.name} при столкновении.");
    //         }
    // }
    //
    // public void ApplyHitEffect()
    // {
    //     if (mr == null || damagedMat == null)
    //     {
    //         return; // Прерываем выполнение, если материал или MeshRenderer не инициализирован
    //     }
    //
    //     Material originalMat = mr.material;
    //     mr.material = damagedMat;
    //     StartCoroutine(ResetMaterialAfterHit(0.1f, originalMat));
    // }
    //
    // public void CreateHealthBar()
    // {
    //     if (hpBarPrefab != null)
    //     {
    //         // Создаем экземпляр HP бара и помещаем его в Canvas
    //         hpBarInstance = Instantiate(hpBarPrefab, transform);
    //         hpBarInstance.transform.localPosition = new Vector3(0, 2, 0); // Сдвигаем HP бар выше зомби
    //         hpBarForeground = hpBarInstance.transform.Find("HPBarForeground").GetComponent<Image>();
    //         hpBarBackround = hpBarInstance.transform.Find("HPBarBackground").GetComponent<Image>();
    //     }
    // }
    //
    //
    // public IEnumerator ResetMaterialAfterHit(float delay, Material originalMat)
    // {
    //     yield return new WaitForSeconds(delay);
    //     mr.material = originalMat; // Без дополнительных проверок
    // }
    //
    // public void TakeDamage(float damage)
    // {
    //     float effectiveDamage = damage - defense; // Учёт защиты
    //     effectiveDamage = Mathf.Max(effectiveDamage, 0); // Урон не может быть отрицательным
    //     health -= effectiveDamage;
    //     UpdateHealthBar();
    //     
    // }
    // public void UpdateHealthBar()
    // {
    //     if (hpBarInstance != null)
    //     {
    //         hpBarForeground.fillAmount = health / maxHealth;
    //     }
    // }
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

