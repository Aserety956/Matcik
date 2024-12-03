using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using Random = UnityEngine.Random;

// ReSharper disable All
public class MyGame : MonoBehaviour
{
    // Main idea - Vampire Survivors like-game
    [Header("FlockMove")] 
    public float separationDistance = 10.0f; // Минимальная дистанция между зомби
    public float alignmentWeight = 10f; // Влияние выравнивания
    public float cohesionWeight = 10f; // Влияние притяжения к соседям

    [Header("Entities")] 
    public Entity boxPrefab;
    public Entity buffPrefab;
    public Entity zombiePrefab;
    public Entity player;
    public GameObject grenadePrefab;


    [Header("SpawnIntervals")] 
    public float boxSpawnInterval;
    public float zombieSpawnInterval;

    [Header("SpawnTimers")] 
    public float boxSpawnT;
    public float zombieSpawnT;

    [Header("ShootingDelays")] 
    public bool canPressKey = true;
    public bool canPressKeyGrenade = true;
    public float keyCooldownShooting = 1.0f;
    public float keyCooldownGrenade = 3.0f;

    
    public float inputDisableTimer = 3f; // idea?

    [Header("MouseControls")] 
    public float mouseSensitivity;
    public float xRotation = 0f;

    [Header("Bufftimer")] public float buffT = 5f;

    [Header("DamagedTimer")] 
    public float damageEffectDuration;
    public float damageEffectTimer;
    
    [Header("Exp LvL Upgrades")]
    // Опыт и уровень
    public int currentLevel = 1;      // Текущий уровень игрока
    public int currentXP = 0;         // Текущее количество XP
    public int xpToNextLevel = 100;  // XP для следующего уровня
    public Slider xpBar;             // Прогресс-бар для XP
    // Меню улучшений
    public GameObject upgradeMenu;              // Панель выбора улучшений
    public List<Button> upgradeButtons;         // Кнопки для выбора улучшений
    // Список доступных улучшений
    public List<Upgrade> availableUpgrades;
    

    [Header("Animations")] 
    public Animator playerAnimator;
    public Transform cameraTransform;

    public List<Entity> entities = new(256);
    
    [Header("Inventory")] 
    public List<InventoryItemInstance> inventory = new (6); // Инвентарь игрока
    public GameObject inventorySlotPrefab; // Префаб ячейки UI
    public Transform inventoryGrid; // Сетка для отображения предметов
    
    [CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
    public class InventoryItem : ScriptableObject
    {
        public string itemName; // Название предмета
        public Sprite icon; // Иконка предмета
        public ItemType type; // Тип предмета
    }
    
    [System.Serializable]
    public class InventoryItemInstance
    {
        public InventoryItem data; // Ссылка на ScriptableObject
        public int lvl; // Уровень предмета
    }
    
    [System.Serializable]
    public class Upgrade
    {
        public string name;          // Название улучшения
        public string description;   // Описание улучшения
        public Sprite icon;          // Иконка улучшения
        public System.Action applyEffect; // Действие при выборе улучшения
    }

    public enum ItemType
    {
        Weapon,
        Support
    }


    public void Start()
    {
        playerAnimator = player.GetComponent<Animator>();
        player.transform.position = new Vector3(0, 0, -2);
        entities.Add(player);
        Cursor.lockState = CursorLockMode.Locked;
        cameraTransform = Camera.main.transform;
        
        UpdateXPUI();

        InventoryItem testItem = Resources.Load<InventoryItem>("Items/PomeGrenade");

        if (testItem != null)
        {
            InventoryItemInstance testItemInstance = new InventoryItemInstance
            {
                data = testItem,
                lvl = 1 // Уровень по умолчанию
            };

            inventory.Add(testItemInstance); // Добавляем в инвентарь
            Debug.Log($"Added '{testItemInstance.data.itemName}' with level {testItemInstance.lvl}");
        }
        

        UpdateInventoryUI(); // Обновляем интерфейс
    }

    public void Update()
    {
        if (!player.isDead)
        {
            UpdateInput();
        }

        UpdateBoxes();
        UpdateZombies();
        //Healing(); TODO: heal method?
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
                zombie.health = zombie.maxHealth;
                CreateHealthBar(zombie, new Vector3(0.5f, 0.5f, 0.5f));
                UpdateHealthBar(zombie);
            }
        }

        for (int i = 0; i < zombies.Count; i++)
        {
            Entity zombie = zombies[i];


            
            {
                List<Entity> entitiesToFilter = GetEntitiesOfType(EntityType.Player | EntityType.Zombie);
                Entity entityToFollow = FindNearestEntity(zombie, entitiesToFilter, (Entity e) => e.isHealed);
                List<Entity> infectedEntities = GetEntitiesOfType(EntityType.Zombie, (e) => !e.isHealed);
                FlockMove(zombie, entityToFollow, infectedEntities);
                HoverObject(zombie.transform, 3f);

                if (entityToFollow != null && 
                    Vector3.Distance(zombie.transform.position, entityToFollow.transform.position) < 3)
                {
                        if (entityToFollow.type == EntityType.Player)
                        {
                            player.isHealed = false;
                        }
                }
            }

            {
                if (zombie.broken) continue;
                if (zombie.collision == null) continue;


                if (zombie.collision.relativeVelocity.magnitude >= zombie.breakForce) //снаряд уничтожать при коллизии
                {
                    float effectiveDamage = Mathf.Max(0, player.damage - zombie.defense);
                    
                    // Здесь можно добавить эффект звука


                }
            }
            if (zombie.health <= 0)
            {
                DestroyZombie(zombie);
                zombies.Remove(zombie);
                entities.Remove(zombie);
            }
        }
    }

    public void DestroyZombie(Entity zombie)
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

        Destroy(zombie.gameObject);
        Destroy(replacement.gameObject, 3f);
    }

    IEnumerator AttackDamageCooldown() // TODO на подумать
    {
        player.canDealDamage = false;
        yield return new WaitForSeconds(player.attackSpeed);
        player.canDealDamage = true;
    }

    public void CreateHealthBar(Entity e, Vector3 customScale)
    {
        if (e.hpBarPrefab != null)
        {
            e.hpBarInstance = Instantiate(e.hpBarPrefab, e.transform);
            e.hpBarInstance.transform.localPosition = new Vector3(0, 2, 0);
            e.hpBarInstance.transform.localScale = customScale;
            e.hpBarForeground = e.hpBarInstance.transform.Find("HPBarForeground").GetComponent<Image>();
            e.hpBarBackround = e.hpBarInstance.transform.Find("HPBarBackground").GetComponent<Image>();
        }
    }

    public void UpdateHealthBar(Entity e)
    {
        if (e.hpBarInstance != null)
        {
            e.hpBarForeground.fillAmount = e.health / e.maxHealth;
        }
    }

    public void TakeDamage(float damage, Entity zombie)
    {
        float effectiveDamage = player.damage - zombie.defense;
        effectiveDamage = Mathf.Max(effectiveDamage, 0);
        zombie.health -= effectiveDamage;
        UpdateHealthBar(zombie);
    }

    public IEnumerator ResetMaterialAfterHit(float delay, Material originalMat, Entity e)
    {
        yield return new WaitForSeconds(delay);
        if (e != null && e.mr != null)
        {
            e.mr.material = originalMat;
        }
    }

    public void ApplyHitEffect(Entity e)
    {
        if (e.mr == null || e.damagedMat == null)
        {
            return;
        }

        Material originalMat = e.mr.sharedMaterial;
        e.mr.material = e.damagedMat;
        StartCoroutine(ResetMaterialAfterHit(0.1f, originalMat, e));
    }

    public void HandleCollision(Entity entity, Collision collision)
    {
        Entity otherEntity = collision.gameObject.GetComponent<Entity>();
        List<Entity> Grenades = GetEntitiesOfType(EntityType.Grenade);

        if (otherEntity == null) return;

        // Проверяем, соответствует ли тип столкновения
        if (entity.isCollisionEnabled && (otherEntity.type | entity.collisionEntityType) == entity.collisionEntityType)
        {
            entity.collision = collision;
            Debug.Log(entity.name + " collided with " + collision.gameObject.name);
        }

        if (otherEntity.type == EntityType.Projectile && collision.relativeVelocity.magnitude >= entity.breakForce)
        {
            Destroy(collision.gameObject);
            ApplyHitEffect(entity);
            TakeDamage(35, entity);
            Debug.Log($"{entity.name} уничтожил объект {collision.gameObject.name} при столкновении.");
        }

        for (int j = 0; j < Grenades.Count; j++)
        {
            Entity grenade = Grenades[j];

            if (otherEntity.type == EntityType.Grenade && collision.relativeVelocity.magnitude >= entity.breakForce)
            {

                Instantiate(grenade.particles, grenade.transform.position, Quaternion.identity);

                // Обработка физического воздействия
                Collider[] colliders =
                    Physics.OverlapSphere(grenade.transform.position, grenade.explosionRadius);
                foreach (var hit in colliders)
                {
                    Rigidbody rb = hit.GetComponent<Rigidbody>();

                    // Нанесение урона объектам типа Entity
                    Entity entityG = hit.GetComponent<Entity>();
                    if (entityG != null && entityG.type == EntityType.Zombie) // Проверяем тип Zombie
                    {
                        float distance = Vector3.Distance(grenade.transform.position, entityG.transform.position);
                        float damageMultiplier = Mathf.Clamp01(1 - (distance / grenade.explosionRadius));
                        TakeDamage(grenade.damage * damageMultiplier, entityG);
                        ApplyHitEffect(entityG);

                        if (entityG.health <= 0)
                        {
                            Debug.Log($"{entityG.name} died from grenade explosion.");
                            DestroyZombie(entityG);
                            entities.Remove(entityG);
                        }
                    }
                }

                // Удаляем гранату из списка и уничтожаем объект
                entities.Remove(grenade);
                Destroy(collision.gameObject); // Уничтожаем гранату
            }

            Debug.Log($"{entity.name} уничтожил объект {collision.gameObject.name} при столкновении.");
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
            Entity buff = buffs[i];

            buff.transform.Rotate(0, buff.rotationSpeed * Time.deltaTime, 0);

            float distance = Vector3.Distance(player.transform.position, buff.transform.position);
            Debug.Log($"Distance to buff {buff.name}: {distance}");

            if (distance < 8.0f)
            {
                Debug.Log($"Picking up buff at distance {distance}");

                keyCooldownShooting = 0.5f;
                keyCooldownGrenade = 1.5f;

                Destroy(buff.gameObject);
                buffs.Remove(buff);
                entities.Remove(buff);
                i--;
            }
        }

        if (keyCooldownShooting <= 0.5f | keyCooldownGrenade <= 1.5f)
        {
            buffT -= Time.deltaTime;
        }

        if (buffT <= 0f)
        {
            buffT = 5.0f;
            keyCooldownShooting = 1.0f;
            keyCooldownGrenade = 3f;
        }
    }

    public void HoverObject(Transform obj, float hoverSpeed)
    {
        if (obj == null) return;

        // Рассчитываем смещение в пределах 4 до 7
        float baseY = 5f; // Средняя точка
        float hoverHeight = 1.5f;
        float offset = Mathf.Sin(Time.time * hoverSpeed) * hoverHeight;

        // Обновляем позицию объекта
        obj.position = new Vector3(obj.position.x, baseY + offset, obj.position.z);
    }


    public Entity SpawnEntity(Entity prefab, float minimumDistance = 70.0f)
    {
        Vector3 randomPosition;
        float distanceToPlayer;

        do
        {
            float randomX = Random.Range(-100, 100);
            float randomZ = Random.Range(-100, 100);

            randomPosition = new Vector3(randomX, 5, randomZ);

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
            //Debug.Log($"posX: {horizontal}, posY: {vertical}");
        }
        else
        {
            // Если нет движения, устанавливаем posX и posY в 0
            playerAnimator.SetFloat("posX", 0);
            playerAnimator.SetFloat("posY", 0);

            // Выводим значения posX и posY для проверки
            //Debug.Log("No movement - posX: 0, posY: 0");
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
            StartCoroutine(KeyPressCooldownShooting());
        }

        if (canPressKeyGrenade && Input.GetKey(KeyCode.Mouse1))
        {
            ThrowGrenade();
            StartCoroutine(KeyPressCooldownGrenade()); // fix
        }
    }

    public void Shooting()
    {
        {
            Ball ball;
            player.moveDirection = player.transform.forward;
            Vector3 spawnPosition = player.ballSpawn.transform.position + new Vector3(0, 4, 0);
            ball = Instantiate(player.ballPrefab, spawnPosition, player.ballSpawn.rotation);
            ball.Init(player.projVelocity);
        }
    }

    public void ThrowGrenade()
    {
        if (grenadePrefab != null)
        {
            Vector3 spawnPosition = player.ballSpawn.transform.position + new Vector3(0, 2, 0);
            GameObject Grenade = Instantiate(grenadePrefab, spawnPosition, Quaternion.identity);
            Rigidbody rb = Grenade.GetComponent<Rigidbody>();
            Entity grenade = Grenade.GetComponent<Entity>();

            if (rb != null)
            {
                //Vector3 throwDirection = player.transform.forward * 10f + Vector3.up * 5f;
                rb.velocity = player.transform.forward * grenade.projVelocity + Vector3.up * 15f;
            }

            if (grenade != null)
            {
                entities.Add(grenade);
            }
        }
    }

    IEnumerator KeyPressCooldownShooting()
    {
        canPressKey = false;
        yield return new WaitForSeconds(keyCooldownShooting);
        canPressKey = true;
    }

    IEnumerator KeyPressCooldownGrenade()
    {
        canPressKeyGrenade = false;
        yield return new WaitForSeconds(keyCooldownGrenade);
        canPressKeyGrenade = true;
    }

    // Метод для нанесения урона другой сущности


    // Метод для восстановления здоровья
    public void PlayerHeal(float amount) // TODO: heal on pickup
    {
        player.health = Mathf.Min(player.health + amount, 100f); // Ограничение на максимальное здоровье
    }

    public void EnableFall()
    {
        if (!player.deathAudioSource.isPlaying)
        {
            player.deathAudioSource.Play();
            player.isDead = true;
        }
    }

    public void AddItem(InventoryItem itemData)
    {
        // Проверяем, есть ли уже предмет в инвентаре
        InventoryItemInstance existingItem = inventory.Find(i => i.data == itemData);
        if (existingItem != null)
        {
            existingItem.lvl++;
        }
        else
        {
            // Создаём новую запись
            inventory.Add(new InventoryItemInstance { data = itemData, lvl = 1 });
        }

        UpdateInventoryUI();
    }

    public void UpdateInventoryUI()
    {
        // Очищаем старый интерфейс
        foreach (Transform child in inventoryGrid)
        {
            Destroy(child.gameObject);
        }

        // Создаём новые слоты
        foreach (var instance in inventory)
        {
            // Создаём слот из префаба
            GameObject slot = Instantiate(inventorySlotPrefab, inventoryGrid);

            // Устанавливаем иконку предмета (на корневом объекте Button)
            Image icon = slot.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = instance.data.icon; // Берём иконку из ScriptableObject
            }
            else
            {
                Debug.LogError("Image component missing on inventorySlotPrefab!");
            }
 
            // Настраиваем уровень предмета (Lvl) — ищем дочерний объект
            TextMeshProUGUI lvlText = slot.GetComponentInChildren<TextMeshProUGUI>();
            if (lvlText != null)
            {
                lvlText.text = $"Lvl {instance.lvl}"; // Уровень предмета из InventoryItemInstance
            }
            else
            {
                Debug.LogError("Text component missing for Lvl in inventorySlotPrefab!");
            }
        }
    }

    public void UpdateItemIcon(InventoryItem itemData, Sprite newIcon)
    {
        // Проверяем, есть ли предмет в инвентаре
        InventoryItemInstance existingItem = inventory.Find(i => i.data == itemData);
        if (existingItem != null)
        {
            // Обновляем иконку в данных предмета
            existingItem.data.icon = newIcon;

            Debug.Log($"Updated icon for item: {existingItem.data.itemName}");
            UpdateInventoryUI(); // Обновляем интерфейс
        }
        else
        {
            Debug.LogWarning($"Item {itemData.itemName} not found in inventory!");
        }
    }
    public void AddXP(int xp)
    {
        currentXP += xp;

        // Проверяем переход на новый уровень
        while (currentXP >= xpToNextLevel)
        {
            LevelUp();
        }

        UpdateXPUI(); // Обновляем интерфейс
    }

    // Обновление уровня
    private void LevelUp()
    {
        currentXP -= xpToNextLevel; // Переносим лишний XP
        currentLevel++;             // Увеличиваем уровень
        xpToNextLevel = Mathf.FloorToInt(xpToNextLevel * 1.25f); // Увеличиваем требования

        Debug.Log($"Level Up! Current Level: {currentLevel}");

        // Показываем меню улучшений
        ShowUpgradeMenu(GetRandomUpgrades(3)); // Выбираем 3 случайных улучшения
    }

    // Обновление UI прогресса
    private void UpdateXPUI()
    {
        if (xpBar != null)
        {
            xpBar.value = (float)currentXP / xpToNextLevel; // Устанавливаем прогресс
        }
    }

    // Показ меню улучшений
    public void ShowUpgradeMenu(List<Upgrade> upgrades)
    {
        upgradeMenu.SetActive(true); // Показываем меню

        for (int i = 0; i < upgradeButtons.Count; i++)
        {
            if (i < upgrades.Count)
            {
                Upgrade upgrade = upgrades[i];
                upgradeButtons[i].gameObject.SetActive(true);
                upgradeButtons[i].GetComponentInChildren<Text>().text = upgrade.name;
                upgradeButtons[i].GetComponent<Image>().sprite = upgrade.icon;

                // Удаляем предыдущие события и добавляем новые
                upgradeButtons[i].onClick.RemoveAllListeners();
                upgradeButtons[i].onClick.AddListener(() => ApplyUpgrade(upgrade));
            }
            else
            {
                upgradeButtons[i].gameObject.SetActive(false);
            }
        }
    }

    // Применение улучшения
    public void ApplyUpgrade(Upgrade upgrade)
    {
        upgrade.applyEffect?.Invoke(); // Применяем эффект улучшения
        Debug.Log($"Applied upgrade: {upgrade.name}");

        upgradeMenu.SetActive(false); // Скрываем меню
    }

    // Получение случайных улучшений
    public List<Upgrade> GetRandomUpgrades(int count)
    {
        List<Upgrade> randomUpgrades = new List<Upgrade>();
        List<Upgrade> pool = new List<Upgrade>(availableUpgrades);

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int randomIndex = Random.Range(0, pool.Count);
            randomUpgrades.Add(pool[randomIndex]);
            pool.RemoveAt(randomIndex); // Убираем выбранное улучшение из пула
        }

        return randomUpgrades;
    }
}

    

    

