using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;
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
    public Entity healPackagePrefab;
    public GameObject grenadePrefab;
    public Entity gemlvl1Prefab;
    public Entity gemlvl2Prefab;
    public Entity gemlvl3Prefab;

    [Header("PlayerHPbar")]
    public float smoothSpeed;
    public Color lowHealthColor;
    public Color highHealthColor;
    public GameObject damageEffectPrefab;
    public float currentFillAmount;
    public GameObject damageEffectInstance;

    [Header("SpawnIntervals")] 
    public float boxSpawnInterval;
    public float zombieSpawnInterval;

    [Header("SpawnTimers")] 
    public float boxSpawnT;
    public float zombieSpawnT;

    [Header("SpawnChance")] 
    [Range(0, 1)] public float healSpawnChance;

    [Header("ShootingDelays")] 
    public bool canPressKey = true;
    public bool canPressKeyGrenade = true;
    public float baseCooldownShooting = 1.0f;
    public float baseCooldownGrenade = 3.0f;
    public Coroutine shootingBuffCoroutine;
    
    [Header("Death Settings")]
    public Image deathScreenOverlay; 
    public float fadeDuration;
    public AudioSource audioSource;
    public AudioClip deathSound;
    public TextMeshProUGUI deathText; 
    public Button restartButton;
    public Button mainMenuButton;
    public string mainMenuSceneName = "MainMenu";
    public float startAlpha;
    public float targetAlpha;
    

    [Header("MouseControls")] 
    public float mouseSensitivity;
    public float xRotation = 0f;

    [Header("Bufftimer")] 
    public float buffDuration = 5f;

    [Header("DamagedTimer")] 
    public float damageEffectDuration;
    public float damageEffectTimer;

    [Header("Exp LvL Upgrades")]
    public int currentLevel = 1;
    public int currentXP = 0;
    public int xpToNextLevel = 100;
    
    public Image xpFillImage;
    public TextMeshProUGUI xpText;
    public Animator levelIconAnimator;
    
    public GameObject upgradeMenu;
    public List<Button> upgradeButtons;
    public List<Upgrade> availableUpgrades;

    [Header("PlayerDamageSound")] 
    public AudioClip damageSound; 

    [Header("Animations")] 
    public Animator playerAnimator;
    public Transform playerCameraTransform;

    public List<Entity> entities = new(256);

    [Header("Inventory")] 
    public List<InventoryItemInstance> inventory = new(6);
    public GameObject inventorySlotPrefab;
    public Transform inventoryGrid;
    
    [Header("Chunk settings")]
    public GameObject chunkPrefab; 
    public int chunkSize = 100;    
    public int renderDistance = 3;
    public Transform chunksParent;
    
    public Vector2Int currentChunkCoord;
    public Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();


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
    }

    public enum UpgradeType
    {
        Damage,
        Speed,
        HealthRegen,
        Magnet,
    }

    public enum ItemType
    {
        Weapon,
        Support
    }


    public void Start()
    {
        playerAnimator = player.GetComponent<Animator>();
        player.transform.position = new Vector3(0, 0, 0);
        entities.Add(player);
        Cursor.lockState = CursorLockMode.Locked;
        playerCameraTransform = Camera.main.transform;
        
        CreateHealthBarPlayer();
        UpdateXPUI();
        UpdateInventoryUI();
        currentChunkCoord = GetChunkCoord(player.transform.position);
        UpdateChunks();
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
        Vector2Int playerChunk = GetChunkCoord(player.transform.position);
        if (playerChunk != currentChunkCoord)
        {
            currentChunkCoord = playerChunk;
            UpdateChunks();
        }
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
    
    public Vector2Int GetChunkCoord(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / chunkSize),
            Mathf.FloorToInt(position.z / chunkSize)
        );
    }

    public void UpdateChunks()
    {
        HashSet<Vector2Int> neededChunks = new HashSet<Vector2Int>();

        // Генерируем координаты вокруг игрока
        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int y = -renderDistance; y <= renderDistance; y++)
            {
                Vector2Int coord = new Vector2Int(
                    currentChunkCoord.x + x,
                    currentChunkCoord.y + y
                );
                neededChunks.Add(coord);
            }
        }
        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (var chunk in chunks)
        {
            if (!neededChunks.Contains(chunk.Key))
            {
                Destroy(chunk.Value.gameObject);
                toRemove.Add(chunk.Key);
            }
        }
        foreach (var key in toRemove) chunks.Remove(key);

        // Создаем новые чанки
        foreach (var coord in neededChunks)
        {
            if (!chunks.ContainsKey(coord))
            {
                Vector3 position = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);
                
                GameObject chunkObj = Instantiate(
                    chunkPrefab,
                    position,
                    Quaternion.identity,
                    chunksParent
                );
                
                Chunk newChunk = chunkObj.AddComponent<Chunk>();
                newChunk.Initialize(coord);
                chunks.Add(coord, newChunk);
            }
        }
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
                CreateHealthBarEnemy(zombie, new Vector3(0.5f, 0.5f, 0.5f));
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
                float distance = Vector3.Distance(player.transform.position, zombie.transform.position);

                if (distance <= zombie.attackRange && Time.time > zombie.lastAttackTime + zombie.attackCooldown)
                {
                    TakeDamagePlayer(zombie.damage, zombie);
                    zombie.lastAttackTime = Time.time;
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

    //TODO: кнопки для отладки статов
    public void Exp()
    {
        List<Entity> gems = GetEntitiesOfType(EntityType.ExpGem);

        //TODO: learn Обработка камней с обратной итерацией
        for (int i = gems.Count - 1; i >= 0; i--)
        {
            Entity gem = gems[i];

            if (gem == null || gem.gameObject == null)
            {
                gems.RemoveAt(i);
                entities.Remove(gem);
                continue;
            }

            float distance = Vector3.Distance(player.transform.position, gem.transform.position);

            if (distance <= player.magnetRadius)
            {
                Vector3 direction = (player.transform.position - gem.transform.position).normalized;
                gem.transform.position += direction * player.magnetForce * Time.deltaTime;

                if (distance < 3f)
                {
                    currentXP += gem.exp;
                    Destroy(gem.gameObject);
                    gems.RemoveAt(i); // Удаляем из локального списка
                    entities.Remove(gem); // Удаляем из общего списка
                    UpdateXPUI();
                    while (currentXP >= xpToNextLevel)
                    {
                        LevelUp();
                    }
                }
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

    public void CreateHealthBarEnemy(Entity e, Vector3 customScale)
    {
        if (e.hpBarPrefab != null)
        {
            e.hpBarInstance = Instantiate(e.hpBarPrefab, e.transform);
            e.hpBarInstance.transform.localPosition = new Vector3(0, 2, 0);
            e.hpBarInstance.transform.localScale = customScale;
            e.hpBarForeground = e.hpBarInstance.transform.Find("HPBarForeground").GetComponent<Image>();
            e.hpBarBackground = e.hpBarInstance.transform.Find("HPBarBackground").GetComponent<Image>();
        }
    }
    
    public void CreateHealthBarPlayer()
    {
        player.hpBarInstance = Instantiate(player.hpBarPrefab, FindObjectOfType<Canvas>().transform);
        
        player.hpBarForeground = player.hpBarInstance.transform.Find("HPBarForeground").GetComponent<Image>();
        player.hpBarBackground = player.hpBarInstance.transform.Find("HPBarBackground").GetComponent<Image>();
        
        player.hpBarForeground.type = Image.Type.Filled;
        player.hpBarForeground.fillMethod = Image.FillMethod.Vertical;
        player.hpBarForeground.fillOrigin = (int)Image.OriginVertical.Bottom;
        
    }
    
    public void UpdateHealthDisplay() 
    {
        float targetFill = player.health / player.maxHealth;
        
        currentFillAmount = Mathf.Lerp(currentFillAmount, targetFill, smoothSpeed * Time.deltaTime);
        player.hpBarForeground.fillAmount = currentFillAmount;

        
        player.hpBarForeground.color = Color.Lerp(lowHealthColor, highHealthColor, currentFillAmount);
        
    }

    public void UpdateHealthBar(Entity e)
    {
        if (e.hpBarInstance != null)
        {
            e.hpBarForeground.fillAmount = e.health / e.maxHealth;
        }
        
        if(e == player)
        {
            currentFillAmount = e.health / e.maxHealth;
        }
    }

    public void TakeDamageZombie(float damage, Entity zombie)
    {
        float PlayerEffectiveDamage = player.damage - zombie.defense;
        PlayerEffectiveDamage = Mathf.Max(PlayerEffectiveDamage, 0);
        zombie.health -= PlayerEffectiveDamage;
        UpdateHealthBar(zombie);
    }
    //TODO:типы врагов и логику для обработки их статов а не только зомби

    public void TakeDamagePlayer(float damage, Entity attacker)
    {
        if (player.isInvincible || player.health <= 0) return;
        
        if (Time.time - player.lastDamageTime < player.invincibilityTime) return;

        float ZombieEffectiveDamage = attacker.damage - player.defense;
        ZombieEffectiveDamage = Mathf.Max(ZombieEffectiveDamage, 0);
        player.health -= ZombieEffectiveDamage;
        player.lastDamageTime = Time.time;
        
        ApplyHitEffect(player);
        UpdateHealthBar(player);
        if (damageSound != null) audioSource.PlayOneShot(damageSound);
        
        
        if (player.health <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        StartCoroutine(DeathRoutine());
        Debug.Log("Player Died!");
        if (deathScreenOverlay == null || deathText == null)
        {
            Debug.LogError("Назначьте deathScreenOverlay и deathText в инспекторе!");
            return;
        }
    }
    private IEnumerator DeathRoutine()
    {
        player.isDead = true;
        isGamePaused = true;
        if (player.hpBarInstance != null)
        {
            player.hpBarInstance.SetActive(false);
        }
        
        if (deathSound != null)
        {
            audioSource.PlayOneShot(deathSound);
        }
        
        float timer = 0f;
        while (timer < fadeDuration)
        {
            float alpha = Mathf.Lerp(0, 1, timer / fadeDuration);
            deathScreenOverlay.color = new Color(0, 0, 0, alpha);
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
        
        CanvasGroup restartGroup = restartButton.GetComponent<CanvasGroup>();
        CanvasGroup menuGroup = mainMenuButton.GetComponent<CanvasGroup>();
        
        restartGroup.alpha = startAlpha;
        menuGroup.alpha = startAlpha;
        
        restartButton.gameObject.SetActive(true);
        mainMenuButton.gameObject.SetActive(true);
        
        while (timer < fadeDuration)
        {
            float progress = timer / fadeDuration;
            restartGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            menuGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
        
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
        restartGroup.alpha = targetAlpha;
        menuGroup.alpha = targetAlpha;
        
        restartButton.onClick.AddListener(RestartGame);
        mainMenuButton.onClick.AddListener(LoadMainMenu);
        

        deathText.gameObject.SetActive(true);
        deathText.color = new Color(1, 0, 0, 0); 

        
        float textFadeDuration = 1f;
        float textTimer = 0f;
        while (textTimer < textFadeDuration)
        {
            float alpha = Mathf.Lerp(0, 1, textTimer / textFadeDuration);
            deathText.color = new Color(1, 0, 0, alpha); // Плавное появление
            textTimer += Time.unscaledDeltaTime;
            yield return null;
        }
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
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
        
        StartCoroutine(ResetMaterialAfterHit(e.flashDuration, originalMat, e));
        
        if (e.type == EntityType.Player)
        {
            StartCoroutine(PlayerInvincibility());
        }
    }
    public IEnumerator PlayerInvincibility()
    {
        player.isInvincible = true;
        yield return new WaitForSeconds(player.invincibilityTime);
        player.isInvincible = false;
    }

    public void HandleCollision(Entity entity, Collision collision)
    {
        Entity otherEntity = collision.gameObject.GetComponent<Entity>();
        List<Entity> Grenades = GetEntitiesOfType(EntityType.Grenade);

        if (otherEntity == null) return;
        
        if (entity.isCollisionEnabled && (otherEntity.type | entity.collisionEntityType) == entity.collisionEntityType)
        {
            entity.Collision = collision;
            Debug.Log(entity.name + " collided with " + collision.gameObject.name);
        }

        if (otherEntity.type == EntityType.Projectile && collision.relativeVelocity.magnitude >= entity.breakForce)
        {
            Destroy(collision.gameObject);
            ApplyHitEffect(entity);
            TakeDamageZombie(player.damage, entity);
            Debug.Log($"{entity.name} уничтожил объект {collision.gameObject.name} при столкновении.");
        }

        for (int j = 0; j < Grenades.Count; j++)
        {
            Entity grenade = Grenades[j];

            if (otherEntity.type == EntityType.Grenade && collision.relativeVelocity.magnitude >= entity.breakForce)
            {

                Instantiate(grenade.particles, grenade.transform.position, Quaternion.identity);

                // TODO: своя физика?
                Collider[] colliders =
                    Physics.OverlapSphere(grenade.transform.position, grenade.explosionRadius);
                foreach (var hit in colliders)
                {
                    Rigidbody rb = hit.GetComponent<Rigidbody>();

                    // Нанесение урона объектам типа Entity
                    Entity entityG = hit.GetComponent<Entity>();
                    if (entityG != null && entityG.type == EntityType.Zombie)
                    {
                        float distance = Vector3.Distance(grenade.transform.position, entityG.transform.position);
                        float damageMultiplier = Mathf.Clamp01(1 - (distance / grenade.explosionRadius));
                        TakeDamageZombie(grenade.damage * damageMultiplier, entityG);
                        ApplyHitEffect(entityG);

                        if (entityG.health <= 0)
                        {
                            Debug.Log($"{entityG.name} died from grenade explosion.");
                            DestroyZombie(entityG);
                            entities.Remove(entityG);
                        }
                    }
                }

                
                entities.Remove(grenade);
                Destroy(collision.gameObject);
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
        List<Entity> heals = GetEntitiesOfType(EntityType.HealPackage);


        if (boxes.Count < 10)
        {
            boxSpawnT -= Time.deltaTime;

            if (boxSpawnT <= 0)
            {
                boxSpawnT += boxSpawnInterval;
                Entity box = SpawnEntity(boxPrefab, 20f);
            }
        }

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
                
                if (Random.value < healSpawnChance)
                {
                    SpawnEntityOnDestroyed(box, healPackagePrefab);
                }
                else
                {
                    SpawnEntityOnDestroyed(box, buffPrefab);
                }

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

        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            Entity buff = buffs[i];

            if (buff == null || buff.gameObject == null)
            {
                buffs.RemoveAt(i);
                entities.Remove(buff);
                continue;
            }

            float distance = Vector3.Distance(player.transform.position, buff.transform.position);

            if (distance <= player.magnetRadius)
            {
                Vector3 direction = (player.transform.position - buff.transform.position).normalized;
                buff.transform.position += direction * player.magnetForce * Time.deltaTime;

                if (distance < 1f)
                {
                    ApplyBuffEffect(buff);
                    Destroy(buff.gameObject);
                    buffs.RemoveAt(i);
                    entities.Remove(buff);
                }
            }
        }

        for (int i = heals.Count - 1; i >= 0; i--) 
        {
            Entity heal = heals[i];

            if (heal == null || heal.gameObject == null)
            {
                heals.RemoveAt(i);
                entities.Remove(heal);
                continue;
            }
            float distance = Vector3.Distance(player.transform.position, heal.transform.position);

            if (distance <= player.magnetRadius)
            {
                Vector3 direction = (player.transform.position - heal.transform.position).normalized;
                heal.transform.position += direction * player.magnetForce * Time.deltaTime;
            }
            if (distance < 1f)
            {
                ApplyHeal();
                Destroy(heal.gameObject);
                heals.RemoveAt(i);
                entities.Remove(heal);
            }
        }
    }

    public void ApplyHeal()
    {
        player.health += 75f;

        if (player.health > 200)
        {
            player.health = 200;
        }
        UpdateHealthBar(player);
    }
    
    public void HoverObject(Transform obj, float hoverSpeed)
    {
        if (obj == null) return;
        
        float baseY = 5f;
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
    public void ApplyBuffEffect(Entity buff)
    {
        if(shootingBuffCoroutine != null) 
            StopCoroutine(shootingBuffCoroutine);
        
        baseCooldownShooting *= 0.5f;
    
        // Запускаем таймер бафа
        shootingBuffCoroutine = StartCoroutine(ResetShootingSpeed(buffDuration));
    
        // Визуальный эффект
        /*if(buff.buffParticles != null)
            Instantiate(buff.buffParticles, player.transform.position, Quaternion.identity);*/
    }

    private IEnumerator ResetShootingSpeed(float duration)
    {
        yield return new WaitForSeconds(duration);
    
        // Восстанавливаем исходные значения
        baseCooldownShooting /= 0.5f;
        Debug.Log("Buff ended. Shooting speed restored");
    }

    IEnumerator KeyPressCooldownShooting()
    {
        canPressKey = false;
        yield return new WaitForSeconds(baseCooldownShooting);
        canPressKey = true;
    }

    IEnumerator KeyPressCooldownGrenade()
    {
        canPressKeyGrenade = false;
        yield return new WaitForSeconds(baseCooldownGrenade);
        canPressKeyGrenade = true;
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
        levelIconAnimator.SetTrigger("LevelUp");
        UpdateXPUI();

        Debug.Log($"Level Up! Current Level: {currentLevel}");
        
        ShowUpgradeMenu(GetRandomUpgrades(3)); 
    }
    //TODO: exp update time and lvl update time
    
    public void UpdateXPUI()
    {
        if (xpFillImage != null)
        {
            //xpFillImage.value = (float)currentXP / xpToNextLevel;
            xpFillImage.fillAmount = (float)currentXP / xpToNextLevel;
            xpText.text = $"Level {currentLevel} | {currentXP}/{xpToNextLevel}";
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
            
            case UpgradeType.Magnet:
                IncreaseMagnet(upgrade.value);
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
    
    public void IncreaseDamage(float multiplier)
    {
        if (player != null)
        {
            player.damage *= (1f + multiplier);
            Debug.Log($"Damage increased! New damage: {player.damage}");
        }
    }
    
    public void IncreaseSpeed(float multiplier)
    {
        if (player != null)
        {
            player.speed *= (1f + multiplier);
            Debug.Log($"Speed increased! New speed: {player.speed}");
        }
    }
    
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
            UpdateHealthBar(player);
            Debug.Log($"Healed {healAmount} HP. Current health: {player.health}");
        }
    }
    public void IncreaseMagnet(float multiplier)
    {
        if(player != null)
        {
            player.magnetRadius *= (1f + multiplier);
            Debug.Log($"Magnet radius: {player.magnetRadius}");
        }
    }
    //TODO: terrain? something that feels good
}

    

    

