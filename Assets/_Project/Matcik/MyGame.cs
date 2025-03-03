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
    public Entity gemlvl1Prefab;
    public Entity gemlvl2Prefab;
    public Entity gemlvl3Prefab;
        


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
    public int currentLevel = 1;      
    public int currentXP = 0;         
    public int xpToNextLevel = 100;  
    public Slider xpBar;             
    // Меню улучшений
    public GameObject upgradeMenu;              
    public List<Button> upgradeButtons;         
    public List<Upgrade> availableUpgrades;

    

    [Header("Animations")] 
    public Animator playerAnimator;
    public Transform playerCameraTransform;

    public List<Entity> entities = new(256);
    
    [Header("Inventory")] 
    public List<InventoryItemInstance> inventory = new (6); 
    public GameObject inventorySlotPrefab; 
    public Transform inventoryGrid; 
    
    [Header("Game State")]
    public bool isGamePaused = false;
    
    [CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
    public class InventoryItem : ScriptableObject
    {
        public string itemName; 
        public Sprite icon; 
        public ItemType type; 
    }
    
    [System.Serializable]
    public class InventoryItemInstance
    {
        public InventoryItem data; // Ссылка на ScriptableObject?
        public Upgrade appliedUpgrade;
        public int lvl; 
    }
    
    [System.Serializable]
    public class Upgrade
    {
        public string name;          
        public string description;   
        public Sprite icon;          
        public UpgradeType upgradeType; 
        public float value; 
        
        /*public Upgrade Clone()
        {
            return new Upgrade()
            {
                name = this.name,
                description = this.description,
                icon = this.icon,
                upgradeType = this.upgradeType,
                value = this.value
            };
        }*/
    }
    public enum UpgradeType
    {
        Damage,
        Speed,
        HealthRegen,
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
        playerCameraTransform = Camera.main.transform;
        
        UpdateXPUI();
        UpdateInventoryUI(); // Обновляем интерфейс
    }

    public void Update()
    {
        if (isGamePaused) return;
        
        if (!player.isDead)
        {
            UpdateInput();
        }

        UpdateBoxes();
        UpdateZombies();
        Exp();
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
        List<Entity> gems = GetEntitiesOfType(EntityType.ExpGem);
        

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
                if (zombie.Collision == null) continue;


                if (zombie.Collision.relativeVelocity.magnitude >= zombie.breakForce)
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
                i--;
            }
        }
    }
    public void Exp() //TODO: Magnetism and item for it
        {
            List<Entity> gems = GetEntitiesOfType(EntityType.ExpGem);
            
            if (gems.Count == 0)
                {
                    Debug.Log("No experience gems found!");
                    return;
                }
            
            for (int i = 0; i < gems.Count; i++)
            {
                Entity gem = gems[i];
                
            if (Vector3.Distance(player.transform.position, gem.transform.position) <= 5)
            {
       //TODO: кнопки для отладки статов
       
                currentXP += gem.exp;
                UpdateXPUI();
                while (currentXP >= xpToNextLevel)
                {
                    LevelUp();
                }
                Destroy(gem.gameObject);
                gems.Remove(gem);
                entities.Remove(gem);
                i--;
            }
                          
            }
                
        }
        
    

    public void DestroyZombie(Entity zombie)
    {
        List<Entity> gems = GetEntitiesOfType(EntityType.ExpGem);
        
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
        Entity gem = SpawnEntityOnDestroyed(zombie, gemlvl1Prefab);
        gems.Add(gem);
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
            entity.Collision = collision;
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
            // Вектор направления зомби
            Vector3 moveDirection = Vector3.zero;

            // flocking behavior
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
            if (box.Collision == null) continue;

            if (box.Collision.relativeVelocity.magnitude >= box.breakForce)
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

                if (box.health <= -1)
                {
                    Destroy(box.gameObject);
                    entities.Remove(box);
                    boxes.Remove(box);
                    Destroy(boxReplacement.gameObject, 2f);
                    //add sound effect
                }
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
        Vector3 spawnPosition = destroyedEntity.transform.position - new Vector3(0, 2, 0);
        Quaternion spawnRotation = prefabToSpawn.transform.rotation;

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
        
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 inputDirection = new Vector3(horizontal, 0, vertical);

        if (inputDirection.magnitude > 0.1f) // проверяем, есть ли движение
        {
            // Устанавливаем значения posX и posY на основе направления ввода
            playerAnimator.SetFloat("posX", horizontal);
            playerAnimator.SetFloat("posY", vertical);
        }
        else
        {
            // Если нет движения, устанавливаем posX и posY в 0
            playerAnimator.SetFloat("posX", 0);
            playerAnimator.SetFloat("posY", 0);
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
    
    public void PlayerHeal(float amount) // TODO: heal on pickup
    {
        player.health = Mathf.Min(player.health + amount, 100f); 
    }

    public void EnableFall()
    {
        if (!player.deathAudioSource.isPlaying)
        {
            player.deathAudioSource.Play();
            player.isDead = true;
        }
    }

    public void AddUpgradeToInventory(Upgrade upgrade)
    {
        // Проверяем, есть ли уже такое улучшение
        InventoryItemInstance existing = 
            inventory.Find(item => item.appliedUpgrade != null && 
                                   item.appliedUpgrade.upgradeType == upgrade.upgradeType);

        if (existing != null)
        {
            existing.lvl++; 
        }
        else
        {
            
            InventoryItemInstance newUpgrade = new InventoryItemInstance
            {
                appliedUpgrade = upgrade,
                lvl = 1,
                data = null // Можно создать специальный InventoryItem для улучшений
            };
            inventory.Add(newUpgrade);
        }
    
        UpdateInventoryUI();
    }

    public void UpdateInventoryUI()
    {
        foreach (Transform child in inventoryGrid)
        {
            Destroy(child.gameObject);
        }

        foreach (var item in inventory)
        {
            GameObject slot = Instantiate(inventorySlotPrefab, inventoryGrid);
            Image icon = slot.GetComponent<Image>();
            TextMeshProUGUI lvlText = slot.GetComponentInChildren<TextMeshProUGUI>();

            // Для улучшений
            if (item.appliedUpgrade != null)
            {
                icon.sprite = item.appliedUpgrade.icon;
                lvlText.text = $"Lvl {item.lvl}";
            
                // TODO: Добавляем Tooltip
                /*TooltipTrigger tooltip = slot.AddComponent<TooltipTrigger>();
                tooltip.header = item.appliedUpgrade.name;
                tooltip.content = item.appliedUpgrade.description;*/
            }
            //TODO?: Для обычных предметов
            else if (item.data != null)
            {
                icon.sprite = item.data.icon;
                lvlText.text = $"Lvl {item.lvl}";
            }
        }
    }

    public void UpdateItemIcon(InventoryItem itemData, Sprite newIcon)
    {
        InventoryItemInstance existingItem = inventory.Find(i => i.data == itemData);
        if (existingItem != null)
        {
            existingItem.data.icon = newIcon;

            Debug.Log($"Updated icon for item: {existingItem.data.itemName}");
            UpdateInventoryUI();
        }
        else
        {
            Debug.LogWarning($"Item {itemData.itemName} not found in inventory!");
        }
    }
    
    public void LevelUp()
    {
        currentXP -= xpToNextLevel;
        currentLevel++;             
        xpToNextLevel = Mathf.FloorToInt(xpToNextLevel * 1.25f); 

        Debug.Log($"Level Up! Current Level: {currentLevel}");
        
        ShowUpgradeMenu(GetRandomUpgrades(3)); 
    }

    // Обновление UI прогресса
    public void UpdateXPUI()
    {
        if (xpBar != null)
        {
            xpBar.value = (float)currentXP / xpToNextLevel;
        }
    }

    
    public void ShowUpgradeMenu(List<Upgrade> upgrades)
    {
        
        Time.timeScale = 0f;
        isGamePaused = true;
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        upgradeMenu.SetActive(true);

        for (int i = 0; i < upgradeButtons.Count; i++)
        {
            if (i < upgrades.Count)
            {
                Upgrade upgrade = upgrades[i];
                Button button = upgradeButtons[i];
                
                // Находим текстовые компоненты
                TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>();
                TMP_Text nameText = texts[0];
                TMP_Text descText = texts[1];
                
                nameText.text = upgrade.name;
                descText.text = upgrade.description;
                
                Image icon = button.GetComponent<Image>();
                if (icon != null && upgrade.icon != null)
                {
                    icon.sprite = upgrade.icon;
                }

                // Удаляем старые события и добавляем новое
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ApplyUpgrade(upgrade));
               
                button.gameObject.SetActive(true);
            }
            else
            {
                // Скрываем неиспользуемые кнопки
                upgradeButtons[i].gameObject.SetActive(false);
            }
        }
    }
    
    public void ApplyUpgrade(Upgrade upgrade)
    {
        switch(upgrade.upgradeType)
        {
            case UpgradeType.Damage:
                IncreaseDamage(upgrade.value);
                break;
            
            case UpgradeType.Speed:
                IncreaseSpeed(upgrade.value);
                break;
            
            case UpgradeType.HealthRegen:
                RegenerateHealth(upgrade.value);
                break;
            
            default:
                Debug.LogWarning("Unknown upgrade type: " + upgrade.upgradeType);
                break;
        }
        AddUpgradeToInventory(upgrade); // Добавляем в инвентарь
        
        Time.timeScale = 1f;
        isGamePaused = false;
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        upgradeMenu.SetActive(false); 
    }
    
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
    // Увеличение урона
    public void IncreaseDamage(float multiplier)
    {
        if (player != null)
        {
            player.damage *= (1f + multiplier);
            Debug.Log($"Damage increased! New damage: {player.damage}");
        }
    }

// Увеличение скорости движения
    public void IncreaseSpeed(float multiplier)
    {
        if (player != null)
        {
            player.speed *= (1f + multiplier);
            Debug.Log($"Speed increased! New speed: {player.speed}");
        }
    }

// Регенерация здоровья
    public void RegenerateHealth(float percentPerSecond)
    {
        if (player != null)
        {
            StartCoroutine(HealthRegenerationCoroutine(percentPerSecond));
        }
    }
    private IEnumerator HealthRegenerationCoroutine(float percentPerSecond)
    {
        while (!player.isDead && player != null)
        {
            yield return new WaitForSeconds(1f);
            float healAmount = player.maxHealth * percentPerSecond;
            player.health = Mathf.Clamp(player.health + healAmount, 0, player.maxHealth);
            Debug.Log($"Healed {healAmount} HP. Current health: {player.health}");
        }
    }
}

    

    

