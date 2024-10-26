using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

// ReSharper disable All
public class MyGame : MonoBehaviour
{
    // Main idea - Vampire Survivors like-game
    [Header("FlockMove")] public float separationDistance = 10.0f; // Минимальная дистанция между зомби
    public float alignmentWeight = 10f; // Влияние выравнивания
    public float cohesionWeight = 10f; // Влияние притяжения к соседям

    [Header("Entities")] public Entity potionPrefab;
    public Entity zombiePrefab;
    public Entity player;

    [Header("SpawnIntervals")] public float potionSpawnInterval;
    public float zombieSpawnInterval;

    [Header("SpawnTimers")] public float potionSpawnT;
    public float zombieSpawnT;

    [Header("ShootingDelays")] public bool canPressKey = true;
    public float keyCooldown = 1.0f;

    [Header("InfectStatus")] public float infectTimerT = 10f;
    public bool isInfected = false;

    [Header("InfectedInput")] // Improve idea to infected impact
    public bool inputEnabled = true;

    public float inputDisableTimer = 3f;

    // [Header("RandomInputs")]
    // public KeyCode currentRandomKey;
    // public float keyChangeInterval = 3f;
    // public float keyChangeTimer = 0f;

    [Header("MouseControls")] public float mouseSensitivity;
    public float xRotation = 0f;

    public List<Entity> entities = new(256);

    public void Start()
    {
        player.transform.position = new Vector3(0, 1, -2);
        entities.Add(player);
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Update()
    {
        if (inputEnabled == true && !player.isDead)
        {
            UpdateInput();
        }

        if (!inputEnabled && !player.isDead)
        {
            NewInfectedInput();
        }

        UpdatePotions();
        UpdateZombies();
        UpdateInfectionTimer();
        Healing();
    }

    public List<Entity> GetEntitiesOfType(EntityType type, Func<Entity, bool> filter = null)
    {
        List<Entity> result = new(entities.Count);
        for (int i = 0; i < entities.Count; i++)
        {
            if ((entities[i].type & type) != 0 && (filter == null || filter(entities[i])))
            {
                result.Add(entities[i]);
            }
        }

        return result;
    }

    public Entity FindNearestEntity(Entity e, List<Entity> others, Func<Entity, bool> SomeFunction = null)
    {
        Entity nearestEntity = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < others.Count; i++)
        {
            if (others[i] != e && (SomeFunction == null || SomeFunction(others[i])))
            {
                float distance = Vector3.Distance(e.transform.position, others[i].transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestEntity = others[i];
                }
            }
        }

        return nearestEntity;
    }

    public void UpdateZombies()
    {
        List<Entity> zombies = GetEntitiesOfType(EntityType.Zombie);
        List<Entity> potions = GetEntitiesOfType(EntityType.Potion);

        if (zombies.Count < 10)
        {
            zombieSpawnT -= Time.deltaTime;

            if (zombieSpawnT <= 0)
            {
                zombieSpawnT += zombieSpawnInterval;
                Entity zombie = SpawnEntity(zombiePrefab, 100f);
                zombies.Add(zombie);
            }
        }

        for (int i = 0; i < zombies.Count; i++)
        {
            Entity zombie = zombies[i];

            if (zombie.isHealed)
            {
                if (zombie.HasPotion())
                {
                    Entity nearestZombie = FindNearestEntity(zombie, zombies, (Entity e) => !e.isHealed);

                    if (nearestZombie != null)
                    {
                        // PERFORMANCE(sqd): Find a way not to call this;
                        List<Entity> healedEntities = GetEntitiesOfType(EntityType.Zombie, (e) => e.isHealed);
                        FlockMove(zombie, nearestZombie, healedEntities);

                        if (Vector3.Distance(zombie.transform.position, nearestZombie.transform.position) < 1.5f)
                        {
                            EntityHealEntity(zombie, nearestZombie);
                        }
                    }
                }
                else
                {
                    Entity nearestPotion = FindNearestEntity(zombie, potions);

                    if (nearestPotion != null)
                    {
                        FlockMove(zombie, nearestPotion, zombies);

                        if (Vector3.Distance(zombie.transform.position, nearestPotion.transform.position) < 1.5f)
                        {
                            KillEntity(nearestPotion);
                            zombie.potionsCount++;
                        }
                    }
                }
            }
            else
            {
                List<Entity> entitiesToFilter = GetEntitiesOfType(EntityType.Player | EntityType.Zombie);
                Entity entityToFollow = FindNearestEntity(zombie, entitiesToFilter, (Entity e) => e.isHealed);
                List<Entity> infectedEntities = GetEntitiesOfType(EntityType.Zombie, (e) => !e.isHealed);
                FlockMove(zombie, entityToFollow, infectedEntities);

                if (entityToFollow != null &&
                    Vector3.Distance(zombie.transform.position, entityToFollow.transform.position) < 3)
                {
                    if (entityToFollow.potionsCount == 0)
                    {
                        if (entityToFollow.type == EntityType.Player)
                        {
                            isInfected = true;
                            player.isHealed = false;
                        }
                        else
                        {
                            EntityInfectEntity(zombie, entityToFollow);
                        }
                    }
                    else
                    {
                        EntityHealEntity(entityToFollow, zombie);
                    }
                }
            }

            {
                if (zombie.broken) continue;
                if (zombie.collision == null) continue;

                if (zombie.collision.relativeVelocity.magnitude >= zombie.breakForce)
                {
                    zombie.broken = true;
                    GameObject replacement;
                    replacement = Instantiate(zombie.replacement, zombie.transform.position, zombie.transform.rotation);
                    Rigidbody[] rbs = replacement.GetComponentsInChildren<Rigidbody>();
                    foreach (var rb in rbs)
                    {
                        rb.AddExplosionForce(5000, zombie.transform.position, 50);
                    }


                    zombies.Remove(zombie);
                    entities.Remove(zombie);

                    Destroy(zombie.gameObject);

                    Destroy(replacement.gameObject, 3);


                    continue;
                }
            }
        }
    }


    public void FlockMove(Entity e, Entity entityToFollow, List<Entity> entitiesToAvoid)
    {
        if (entityToFollow != null)
        {
            // Вектор направления, куда двигаться зомби
            Vector3 moveDirection = Vector3.zero;

            // Применение правил flocking behavior
            Vector3 separation = Vector3.zero;
            Vector3 alignment = Vector3.zero;
            Vector3 cohesion = Vector3.zero;

            // Параметры для регулировки силы эффектов

            foreach (var neighbor in entitiesToAvoid)
            {
                if (neighbor != e)
                {
                    float distanceToNeighbor = Vector3.Distance(e.transform.position, neighbor.transform.position);

                    // Правило разделения: избегаем других зомби
                    if (distanceToNeighbor < separationDistance)
                    {
                        separation += (e.transform.position - neighbor.transform.position).normalized /
                                      distanceToNeighbor;
                    }

                    alignment += neighbor.moveDirection;

                    // Правило притяжения (cohesion): стремимся к средней позиции соседей
                    cohesion += neighbor.transform.position;
                }
            }

            if (entitiesToAvoid.Count > 0)
            {
                // Учитываем среднюю позицию для притяжения
                cohesion /= entitiesToAvoid.Count;
                cohesion = (cohesion - e.transform.position).normalized;

                // Учитываем направление движения соседей
                alignment = alignment.normalized;
            }

            // Итоговое направление с учётом всех правил
            moveDirection = separation.normalized * separationDistance + alignment * alignmentWeight +
                            cohesion * cohesionWeight;

            // Следование за игроком (EntityToFollow) как основное поведение
            moveDirection += (entityToFollow.transform.position - e.transform.position).normalized;

            // Нормализуем вектор для соблюдения скорости
            moveDirection = new Vector3(moveDirection.x, 0, moveDirection.z).normalized;

            e.moveDirection = moveDirection;

            // Поворачиваем зомби в сторону движения
            e.transform.LookAt(entityToFollow.transform);

            // Движение зомби
            float moveDistance = e.speed * Time.deltaTime;
            e.transform.position += moveDirection * moveDistance;
        }
    }

    public void EntityInfectEntity(Entity e, Entity entityToInfect)
    {
        entityToInfect.isHealed = false;
        entityToInfect.speed /= 2;
        entityToInfect.mr.sharedMaterial = entityToInfect.notHealedMat;
    }

    public void EntityHealEntity(Entity e, Entity entityToHeal)
    {
        Debug.Log($"{e.name} healing {entityToHeal.name}");
        e.potionsCount--;
        entityToHeal.isHealed = true;
        entityToHeal.speed *= 2;
        entityToHeal.mr.sharedMaterial = entityToHeal.healedMat;
    }

    public void UpdatePotions()
    {
        List<Entity> potions = GetEntitiesOfType(EntityType.Potion);
        // NOTE(sqd): Spawn potions
        if (potions.Count < 10)
        {
            potionSpawnT -= Time.deltaTime;

            if (potionSpawnT <= 0)
            {
                potionSpawnT += potionSpawnInterval;
                Entity potion = SpawnEntity(potionPrefab, 20f);
            }
        }

        // NOTE(sqd): Rotate potions over time
        for (int i = 0; i < potions.Count; i++)
        {
            potions[i].transform.Rotate(0, potions[i].rotationSpeed * Time.deltaTime, 0);

            // NOTE(sqd): Check if player near by
            if (Vector3.Distance(player.transform.position, potions[i].transform.position) < 5)
            {
                KillEntity(potions[i]);
                // TODO(sqd): Should we remove potion from potions list?
                player.potionsCount++;
            }
        }
    }

    public Entity SpawnEntity(Entity prefab, float minimumDistance = 70.0f)
    {
        Vector3 randomPosition;
        float distanceToPlayer;

        do
        {
            float randomX = Random.Range(-100, 100);
            float randomZ = Random.Range(-100, 100);

            randomPosition = new Vector3(randomX, 0, randomZ);

            distanceToPlayer = Vector3.Distance(randomPosition, player.transform.position);
        } while (distanceToPlayer < minimumDistance);

        Entity result = Instantiate(prefab, randomPosition, Quaternion.identity);
        entities.Add(result);

        return result;
    }

    public void KillEntity(Entity e)
    {
        GameObject.Destroy(e.gameObject);
        entities.Remove(e);
    }

    public void UpdateInput()
    {
        player.speed = 50f;
        player.rotationSpeed = 200f;
        float rotationY = player.rotationSpeed * Time.deltaTime;
        float moveDistance = player.speed * Time.deltaTime;

        if (Input.GetKey(KeyCode.W))
        {
            player.transform.position += player.transform.forward * moveDistance;
        }

        if (Input.GetKey(KeyCode.A))
        {
            player.transform.position -= player.transform.right * moveDistance;
        }

        if (Input.GetKey(KeyCode.S))
        {
            player.transform.position -= player.transform.forward * moveDistance;
        }

        if (Input.GetKey(KeyCode.D))
        {
            player.transform.position += player.transform.right * moveDistance;
        }

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;

        xRotation = Mathf.Clamp(xRotation, -60f, 60f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        player.transform.Rotate(Vector3.up * mouseX);

        if (canPressKey && Input.GetKey(KeyCode.Mouse0))
        {
            Shooting();
            StartCoroutine(KeyPressCooldown());
        }
    }

    public void Shooting()
    {
        Ball ball;
        player.moveDirection = player.transform.forward;
        ball = Instantiate(player.ballPrefab, player.ballSpawn.transform.position, player.ballSpawn.rotation);
        ball.Init(player.projVelocity);
        Destroy(ball.gameObject, 2);
    }


    IEnumerator KeyPressCooldown()
    {
        canPressKey = false;
        yield return new WaitForSeconds(keyCooldown);
        canPressKey = true;
    }

    public void UpdateInfectionTimer()
    {
        if (isInfected)
        {
            if (infectTimerT > 0)
            {
                infectTimerT -= Time.deltaTime;
                inputDisableTimer -= Time.deltaTime;

                if (inputDisableTimer <= 0)
                {
                    inputEnabled = !inputEnabled;
                    inputDisableTimer = 3f;
                }
            }
            else if (!player.isDead)
            {
                EnableFall();

                // TODO(sqd): Ragdoll
            }
        }
    }

    public void Healing()
    {
        if (isInfected && !player.isHealed && player.HasPotion())
        {
            isInfected = false;
            player.potionsCount--;
            player.isHealed = true;
            inputDisableTimer = 3f;
            inputEnabled = true;
            infectTimerT = 10f;
        }
    }


    public void EnableFall()
    {
        if (!player.deathAudioSource.isPlaying)
        {
            player.deathAudioSource.Play();
            player.isDead = true;
        }
    }


    public void NewInfectedInput()
    {
        player.speed = 15f;
        player.rotationSpeed = 20f;
        float rotationY = player.rotationSpeed * Time.deltaTime;
        float moveDistance = player.speed * Time.deltaTime;

        if (Input.GetKey(KeyCode.W))
        {
            player.transform.position += player.transform.forward * moveDistance;
        }

        if (Input.GetKey(KeyCode.A))
        {
            player.transform.position -= player.transform.right * moveDistance;
        }

        if (Input.GetKey(KeyCode.S))
        {
            player.transform.position -= player.transform.forward * moveDistance;
        }

        if (Input.GetKey(KeyCode.D))
        {
            player.transform.position += player.transform.right * moveDistance;
        }

        if (Input.GetKey(KeyCode.E))
        {
            player.transform.Rotate(0, rotationY, 0);
        }

        if (Input.GetKey(KeyCode.Q))
        {
            player.transform.Rotate(0, -rotationY, 0);
        }
    }
}


//public static KeyCode[] inputKeys = new[] { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.E, KeyCode.Q };
//public static KeyCode[] infectedKeys;

//public static KeyCode[] ShuffleArray(KeyCode[] array)
//{
//KeyCode[] newArray = (KeyCode[])array.Clone();
//int n = newArray.Length;

//for (int i = n - 1; i > 0; i--)
//{
//int j = Random.Range(0, i + 1);
//KeyCode temp = newArray[i];
//newArray[i] = newArray[j];
//newArray[j] = temp;
//}

//return newArray;
//}

//public void InfectedInput()
//{
//float rotationY = player.rotationSpeed * Time.deltaTime;
// float moveDistance = player.speed * Time.deltaTime;

//keyChangeTimer -= Time.deltaTime;


//if (keyChangeTimer <= 0)
//{
//keyChangeTimer = keyChangeInterval;
//infectedKeys = ShuffleArray(inputKeys);
//}

//if (Input.GetKey(infectedKeys[0]))
//{
//player.transform.position += player.transform.forward * moveDistance;
//}

//if (Input.GetKey(infectedKeys[1]))
//{
//player.transform.position -= player.transform.right * moveDistance;
//}

//if (Input.GetKey(infectedKeys[2]))
//{
//player.transform.position -= player.transform.forward * moveDistance;
//}

//if (Input.GetKey(infectedKeys[3]))
//{
//player.transform.position += player.transform.right * moveDistance;
//}

//if (Input.GetKey(infectedKeys[4]))
//{
//player.transform.Rotate(0, rotationY, 0);
//}

//if (Input.GetKey(infectedKeys[5]))
//{
//player.transform.Rotate(0, -rotationY, 0);
//}
//}