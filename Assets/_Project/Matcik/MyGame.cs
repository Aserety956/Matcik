using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

// ReSharper disable All
public class MyGame : MonoBehaviour
{
    // Main idea - Vampire Survivors like-game
    [Header("FlockMove")] public float separationDistance = 10.0f; // Минимальная дистанция между зомби
    public float alignmentWeight = 10f; // Влияние выравнивания
    public float cohesionWeight = 10f; // Влияние притяжения к соседям

    [Header("Entities")] public Entity boxPrefab;
    public Entity buffPrefab;
    public Entity zombiePrefab;
    public Entity player;


    [Header("SpawnIntervals")] public float boxSpawnInterval;
    public float zombieSpawnInterval;

    [Header("SpawnTimers")] public float boxSpawnT;
    public float zombieSpawnT;

    [Header("ShootingDelays")] 
    public bool canPressKey = true;
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

    [Header("Bufftimer")] public float buffT = 5f;

    [Header("Animations")] public Animator playerAnimator;
    public Transform cameraTransform;

    public List<Entity> entities = new(256);

    public void Start()
    {
        playerAnimator = player.GetComponent<Animator>();
        player.transform.position = new Vector3(0, 0, -2);
        entities.Add(player);
        Cursor.lockState = CursorLockMode.Locked;
        // playerAnimator.SetBool("Idle", true);
        cameraTransform = Camera.main.transform;
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

        UpdateBoxes();
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
        List<Entity> boxes = GetEntitiesOfType(EntityType.Box);
        List<Entity> projectiles = GetEntitiesOfType(EntityType.Projectile);

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
                if (zombie.HasBox())
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
                    Entity nearestBox = FindNearestEntity(zombie, boxes);

                    if (nearestBox != null)
                    {
                        FlockMove(zombie, nearestBox, zombies);

                        if (Vector3.Distance(zombie.transform.position, nearestBox.transform.position) < 1.5f)
                        {
                            KillEntity(nearestBox);
                            zombie.boxesCount++;
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
                    if (entityToFollow.boxesCount == 0)
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

                float effectiveDamage = Mathf.Max(0, player.damage - zombie.defense);

                if (zombie.collision.relativeVelocity.magnitude >= zombie.breakForce) //снаряд уничтожать при коллизии
                {                   
                    
                    if (player.canDealDamage)
                    {
                        zombie.health -= effectiveDamage;
                        StartCoroutine(AttackDamageCooldown());
                    }

                    if (zombie.health <= 0)
                    {
                        zombie.broken = true;

                        Instantiate(zombie.particles, zombie.transform.position, zombie.transform.rotation);
                        GameObject replacement = Instantiate(zombie.replacement, zombie.transform.position,
                            zombie.transform.rotation);

                        Rigidbody[] replacementRbs = replacement.GetComponentsInChildren<Rigidbody>();
                        foreach (var rb in replacementRbs)
                        {
                            rb.AddExplosionForce(zombie.explosionForce, zombie.transform.position,
                                zombie.explosionRadius);
                        }

                        Collider[] colliders = Physics.OverlapSphere(zombie.transform.position, zombie.explosionRadius);
                        foreach (Collider hit in colliders)
                        {
                            Rigidbody hitRb = hit.GetComponent<Rigidbody>();

                            if (hitRb != null)
                            {
                                hitRb.AddExplosionForce(zombie.explosionForce, zombie.transform.position,
                                    zombie.explosionRadius);

                                Entity nearbyZombie = hit.GetComponent<Entity>();
                                if (nearbyZombie != null && nearbyZombie.type == EntityType.Zombie &&
                                    !nearbyZombie.broken)
                                {
                                    nearbyZombie.broken = true;

                                    GameObject zombieReplacement = Instantiate(nearbyZombie.replacement,
                                        nearbyZombie.transform.position, nearbyZombie.transform.rotation);
                                    Rigidbody[] zombieReplacementRigidbodies =
                                        zombieReplacement.GetComponentsInChildren<Rigidbody>();

                                    foreach (var rb in zombieReplacementRigidbodies)
                                    {
                                        rb.AddExplosionForce(zombie.explosionForce, nearbyZombie.transform.position,
                                            zombie.explosionRadius);
                                    }

                                    Destroy(zombieReplacement, 3f);
                                    zombies.Remove(nearbyZombie);
                                    entities.Remove(nearbyZombie);
                                    Destroy(nearbyZombie.gameObject);
                                }
                            }

                            zombies.Remove(zombie);
                            entities.Remove(zombie);
                            Destroy(zombie.gameObject);
                            Destroy(replacement.gameObject, 3f);

                            // Здесь можно добавить эффект звука
                        }
                    }

                    continue;
                }
            }
        }
    }

    IEnumerator AttackDamageCooldown()
    {
        player.canDealDamage = false;
        yield return new WaitForSeconds(player.attackSpeed);
        player.canDealDamage = true;
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

            // Нормализуем вектор для соблюдения скорости, игнорируя ось Y
            moveDirection = new Vector3(moveDirection.x, 0, moveDirection.z).normalized;

            e.moveDirection = moveDirection;

            // Поворачиваем зомби в сторону игрока, игнорируя компонент Y
            Vector3 targetDirection = new Vector3(entityToFollow.transform.position.x - e.transform.position.x, 0,
                entityToFollow.transform.position.z - e.transform.position.z);
            if (targetDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                e.transform.rotation =
                    Quaternion.Slerp(e.transform.rotation, targetRotation, e.rotationSpeed * Time.deltaTime);
            }

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
        e.boxesCount--;
        entityToHeal.isHealed = true;
        entityToHeal.speed *= 2;
        entityToHeal.mr.sharedMaterial = entityToHeal.healedMat;
    }

    public void UpdateBoxes()
    {
        List<Entity> boxes = GetEntitiesOfType(EntityType.Box);
        List<Entity> buffs = GetEntitiesOfType(EntityType.Buff);
        // NOTE(sqd): Spawn potions
        if (boxes.Count < 10)
        {
            boxSpawnT -= Time.deltaTime;

            if (boxSpawnT <= 0)
            {
                boxSpawnT += boxSpawnInterval;
                Entity box = SpawnEntity(boxPrefab, 20f);
            }
        }

        // NOTE(sqd): Rotate boxes over time
        for (int i = 0; i < boxes.Count; i++)
        {
            boxes[i].transform.Rotate(0, boxes[i].rotationSpeed * Time.deltaTime, 0);
            Entity box = boxes[i];

            // NOTE(sqd): Check if player near by
            if (Vector3.Distance(player.transform.position, boxes[i].transform.position) < 5)
            {
                KillEntity(boxes[i]);
                // TODO(sqd): Should we remove box from boxes list?
                player.boxesCount++;
            }


            if (box.broken) continue;
            if (box.collision == null) continue;

            if (box.collision.relativeVelocity.magnitude >= box.breakForce)
            {
                box.broken = true;

                GameObject boxReplacement =
                    Instantiate(box.replacement, box.transform.position, box.transform.rotation);
                Rigidbody[] boxReplacmentRbs = boxReplacement.GetComponentsInChildren<Rigidbody>();

                Entity buff = SpawnEntityOnDestroyed(box, buffPrefab);

                foreach (var rb in boxReplacmentRbs)
                {
                    rb.AddExplosionForce(box.explosionForce, box.transform.position, box.explosionRadius);
                }

                Destroy(box.gameObject);
                entities.Remove(box);
                boxes.Remove(box);
                Destroy(boxReplacement.gameObject, 2f);
                //add sound effect
            }
        }


        for (int i = 0; i < buffs.Count; i++)
        {
            buffs[i].transform.Rotate(0, buffs[i].rotationSpeed * Time.deltaTime, 0);
            Entity buff = buffs[i];

            if (Vector3.Distance(player.transform.position, buffs[i].transform.position) < 5f && keyCooldown >= 1.0f)
            {
                keyCooldown = 0.5f;
                Destroy(buff.gameObject);
                buffs.Remove(buff);
                entities.Remove(buff);
                i--;
            }
        }

        if (keyCooldown <= 0.5f)
        {
            buffT -= Time.deltaTime;
        }

        if (buffT <= 0f)
        {
            buffT = 5.0f;
            keyCooldown = 1.0f;
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

            randomPosition = new Vector3(randomX, 3, randomZ);

            distanceToPlayer = Vector3.Distance(randomPosition, player.transform.position);
        } while (distanceToPlayer < minimumDistance);

        Entity result = Instantiate(prefab, randomPosition, Quaternion.identity);
        entities.Add(result);

        return result;
    }

    public Entity SpawnEntityOnDestroyed(Entity destroyedEntity, Entity prefabToSpawn)
    {
        Vector3 spawnPosition = destroyedEntity.transform.position;
        Quaternion spawnRotation = destroyedEntity.transform.rotation;

        Entity result = Instantiate(prefabToSpawn, spawnPosition, spawnRotation);

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

        // Получаем ввод от игрока
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 inputDirection = new Vector3(horizontal, 0, vertical);

        if (inputDirection.magnitude > 0.1f) // проверяем, есть ли движение
        {
            // Устанавливаем значения posX и posY на основе направления ввода
            playerAnimator.SetFloat("posX", horizontal);
            playerAnimator.SetFloat("posY", vertical);

            // Выводим значения posX и posY для проверки
            Debug.Log($"posX: {horizontal}, posY: {vertical}");
        }
        else
        {
            // Если нет движения, устанавливаем posX и posY в 0
            playerAnimator.SetFloat("posX", 0);
            playerAnimator.SetFloat("posY", 0);

            // Выводим значения posX и posY для проверки
            Debug.Log("No movement - posX: 0, posY: 0");
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
        Vector3 spawnPosition = player.ballSpawn.transform.position + new Vector3(0, 2, 0);
        ball = Instantiate(player.ballPrefab, spawnPosition, player.ballSpawn.rotation);
        ball.Init(player.projVelocity);
    }
    

    IEnumerator KeyPressCooldown()
    {
        canPressKey = false;
        yield return new WaitForSeconds(keyCooldown);
        canPressKey = true;
    }

    // Метод для нанесения урона другой сущности


    // Метод для восстановления здоровья
    public void PlayerHeal(float amount) // amount?
    {
        player.health = Mathf.Min(player.health + amount, 100f); // Ограничение на максимальное здоровье
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
        if (isInfected && !player.isHealed && player.HasBox())
        {
            isInfected = false;
            player.boxesCount--;
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