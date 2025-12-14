using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Project9
{
    /// <summary>
    /// Manages all game entities (player and enemies)
    /// </summary>
    public class EntityManager
    {
        private Player _player;
        private List<Enemy> _enemies = new List<Enemy>();
        private List<SecurityCamera> _cameras = new List<SecurityCamera>();
        private List<WeaponPickup> _weaponPickups = new List<WeaponPickup>();
        private List<Projectile> _projectiles = new List<Projectile>();
        private CollisionManager? _collisionManager;
        private RenderSystem? _renderSystem;
        
        // Track enemy to attack when player gets in range
        private Enemy? _pendingAttackTarget = null;
        private Enemy? _autoAttackTarget = null; // Enemy to auto-attack when in range (left-click)
        private bool _shouldStopOnAttack = false; // Whether to stop movement when attacking (right-click)
        private bool _isAttackingWithProjectile = false; // Whether player is currently attacking with projectile weapon
        
        // Debug: Track if damage numbers are being called
        private int _damageNumberCallCount = 0;
        
        // Performance tracking
        private System.Diagnostics.Stopwatch _pathfindingStopwatch = new System.Diagnostics.Stopwatch();
        private float _lastPathfindingTimeMs = 0.0f;
        private int _activePathfindingCount = 0;
        
        // Track if player is following cursor (for UI purposes)
        private bool _isFollowingCursor = false;
        
        // Alarm system
        private float _alarmTimer = 0.0f;
        private const float ALARM_DURATION = 15.0f; // 15 seconds
        private bool _alarmActive = false;

        public Player Player => _player;
        public List<Enemy> Enemies => _enemies;
        public List<SecurityCamera> Cameras => _cameras;
        public List<WeaponPickup> WeaponPickups => _weaponPickups;
        public List<Projectile> Projectiles => _projectiles;
        public float LastPathfindingTimeMs => _lastPathfindingTimeMs;
        public int ActivePathfindingCount => _activePathfindingCount;
        public bool IsFollowingCursor => _isFollowingCursor;
        public float AlarmTimer => _alarmTimer;
        public bool AlarmActive => _alarmActive;

        /// <summary>
        /// Create EntityManager with player (CollisionManager can be set later)
        /// </summary>
        public EntityManager(Player player)
        {
            _player = player;
        }
        
        /// <summary>
        /// Set the CollisionManager (must be called before Update)
        /// </summary>
        public void SetCollisionManager(CollisionManager collisionManager)
        {
            _collisionManager = collisionManager;
        }
        
        /// <summary>
        /// Set the RenderSystem (for showing damage numbers)
        /// </summary>
        public void SetRenderSystem(RenderSystem renderSystem)
        {
            _renderSystem = renderSystem;
        }

        /// <summary>
        /// Load enemies from map data
        /// </summary>
        public void LoadEnemies(Project9.Shared.MapData? mapData)
        {
            _enemies.Clear();
            
            if (mapData?.Enemies != null)
            {
                foreach (var enemyData in mapData.Enemies)
                {
                    Vector2 enemyPosition = new Vector2(enemyData.X, enemyData.Y);
                    _enemies.Add(new Enemy(enemyPosition, enemyData));
                }
                Console.WriteLine($"[EntityManager] Loaded {_enemies.Count} enemies");
            }
        }

        /// <summary>
        /// Load cameras from map data
        /// </summary>
        public void LoadCameras(Project9.Shared.MapData? mapData)
        {
            _cameras.Clear();
            
            if (mapData?.Cameras != null)
            {
                foreach (var cameraData in mapData.Cameras)
                {
                    _cameras.Add(new SecurityCamera(cameraData));
                }
                Console.WriteLine($"[EntityManager] Loaded {_cameras.Count} cameras");
            }
        }

        /// <summary>
        /// Load weapons from map data
        /// </summary>
        public void LoadWeapons(Project9.Shared.MapData? mapData)
        {
            _weaponPickups.Clear();
            
            if (mapData?.Weapons != null)
            {
                foreach (var weaponData in mapData.Weapons)
                {
                    Weapon weapon;
                    // Create weapon based on data type with properties from data
                    Color weaponColor = new Color(weaponData.WeaponColorR, weaponData.WeaponColorG, weaponData.WeaponColorB);
                    
                    weapon = weaponData switch
                    {
                        Project9.Shared.SwordData swordData => new Sword(
                            swordData.Damage,
                            string.IsNullOrEmpty(swordData.Name) ? "Sword" : swordData.Name,
                            weaponColor,
                            swordData.KnockbackDuration
                        ),
                        Project9.Shared.GunData gunData => new Gun(
                            gunData.Damage,
                            string.IsNullOrEmpty(gunData.Name) ? "Gun" : gunData.Name,
                            weaponColor,
                            gunData.KnockbackDuration,
                            gunData.ProjectileSpeed,
                            gunData.FireRate
                        ),
                        _ => throw new InvalidOperationException($"Unknown weapon data type: {weaponData.GetType().Name}")
                    };
                    
                    Vector2 weaponPosition = new Vector2(weaponData.X, weaponData.Y);
                    _weaponPickups.Add(new WeaponPickup(weaponPosition, weapon));
                }
                Console.WriteLine($"[EntityManager] Loaded {_weaponPickups.Count} weapons");
            }
        }

        /// <summary>
        /// Get the enemy the player is currently in combat with (if any)
        /// An enemy is considered "in combat" if it has detected the player and is within combat range
        /// </summary>
        private Enemy? GetEnemyInCombat()
        {
            const float combatRange = GameConfig.EnemyCombatRange;
            const float combatRangeSquared = combatRange * combatRange; // Pre-calculate squared for comparison
            
            Enemy? closestCombatEnemy = null;
            float closestDistanceSquared = float.MaxValue;
            
            foreach (var enemy in _enemies)
            {
                if (enemy.HasDetectedPlayer)
                {
                    float distanceSquared = Vector2.DistanceSquared(_player.Position, enemy.Position);
                    if (distanceSquared <= combatRangeSquared && distanceSquared < closestDistanceSquared)
                    {
                        closestCombatEnemy = enemy;
                        closestDistanceSquared = distanceSquared;
                    }
                }
            }
            
            return closestCombatEnemy;
        }

        /// <summary>
        /// Check if the player is currently in combat
        /// </summary>
        public bool IsPlayerInCombat()
        {
            return GetEnemyInCombat() != null;
        }

        /// <summary>
        /// Update all entities
        /// </summary>
        public void Update(float deltaTime, Vector2? followPosition)
        {
            if (_collisionManager == null)
                throw new InvalidOperationException("CollisionManager must be set before calling Update");
            
            _pathfindingStopwatch.Restart();
            _activePathfindingCount = 0;
            
            // Update enemy spatial hash grid for efficient collision detection
            _collisionManager.UpdateEnemyGrid();
            
            // Track if player is following cursor
            _isFollowingCursor = followPosition.HasValue;
            
            // Determine which enemy the player is in combat with (if any)
            Enemy? combatEnemy = GetEnemyInCombat();
            
            // Create collision check function that only checks the combat enemy (or no enemies if not in combat)
            // This allows the player to move away from non-combat enemies without collision blocking
            Func<Vector2, bool> playerCollisionCheck;
            System.Collections.Generic.IEnumerable<Enemy>? combatEnemyList = null;
            
            if (combatEnemy != null)
            {
                // Only check collision with the enemy in combat
                combatEnemyList = new List<Enemy> { combatEnemy };
                playerCollisionCheck = (pos) => _collisionManager.CheckCollision(pos, combatEnemyList);
            }
            else
            {
                // Not in combat - only check terrain collision (no enemy collision)
                playerCollisionCheck = (pos) => _collisionManager.CheckCollision(pos, false);
            }
            
            // Update player movement with CollisionManager for perfect collision resolution
            // Only update if player is alive
            if (_player.IsAlive)
            {
                _player.Update(
                    followPosition, 
                    deltaTime, 
                    playerCollisionCheck, 
                    (from, to) => _collisionManager.IsLineOfSightBlocked(from, to),
                    _collisionManager,
                    combatEnemyList
                );
            }
            else
            {
                // Player is dead - reset alarms and enemy detection
                if (_alarmActive)
                {
                    _alarmTimer = 0.0f;
                    _alarmActive = false;
                    LogOverlay.Log("[EntityManager] Alarm reset - player died", LogLevel.Info);
                    
                    // Reset all enemy detection states when alarm is reset
                    foreach (var enemy in _enemies)
                    {
                        if (enemy.HasDetectedPlayer)
                        {
                            enemy.ResetDetection();
                        }
                    }
                }
                
                // Update death animation if dead
                _player.UpdateDeathAnimation(deltaTime);
            }
            
            // Count active pathfinding
            if (_player.TargetPosition.HasValue)
            {
                _activePathfindingCount++;
            }

            // Check for weapon pickups
            CheckWeaponPickups();
            
            // Check if player reached attack target and attack if in range
            CheckPendingAttack();
            CheckAutoAttack(); // Check if player should auto-attack while moving
            
            // Update projectiles
            UpdateProjectiles(deltaTime);

            // Update all cameras first (they can alert enemies)
            bool anyCameraDetecting = false;
            foreach (var camera in _cameras)
            {
                // Only update if camera is alive
                if (camera.IsAlive)
                {
                    camera.Update(deltaTime);
                }
                bool cameraDetected = camera.UpdateDetection(
                    _player.Position,
                    deltaTime,
                    _player.IsSneaking,
                    (from, to) => _collisionManager.IsLineOfSightBlocked(from, to),
                    _enemies
                );
                
                // Check if camera is currently detecting player (in sight cone with line of sight)
                if (camera.IsAlive && camera.IsCurrentlyDetecting(_player.Position, _player.IsSneaking, (from, to) => _collisionManager.IsLineOfSightBlocked(from, to)))
                {
                    anyCameraDetecting = true;
                }
            }
            
            // Cache player position for distance calculations (avoid repeated property access)
            Vector2 playerPosition = _player.Position;
            bool playerIsSneaking = _player.IsSneaking;
            
            // Handle alarm state
            if (anyCameraDetecting)
            {
                // Camera currently detecting player - start/reset alarm timer
                _alarmTimer = ALARM_DURATION;
                _alarmActive = true;
            }
            else if (_alarmActive)
            {
                // No cameras currently detecting - count down alarm timer
                _alarmTimer -= deltaTime;
                if (_alarmTimer <= 0.0f)
                {
                    // Alarm expired - reset everything
                    _alarmTimer = 0.0f;
                    _alarmActive = false;
                    
                    // Check enemies for direct detection
                    // Enemies without direct detection should return to original positions
                    foreach (var enemy in _enemies)
                    {
                        if (enemy.HasDetectedPlayer)
                        {
                            // Check if enemy has direct line of sight to player
                            Vector2 directionToPlayer = _player.Position - enemy.Position;
                            float distanceSquared = directionToPlayer.LengthSquared();
                            float effectiveRange = _player.IsSneaking 
                                ? enemy.DetectionRange * GameConfig.EnemySneakDetectionMultiplier 
                                : enemy.DetectionRange;
                            float effectiveRangeSquared = effectiveRange * effectiveRange;
                            
                            bool inRange = distanceSquared <= effectiveRangeSquared;
                            bool hasLineOfSight = inRange && !_collisionManager.IsLineOfSightBlocked(enemy.Position, _player.Position, enemy.Position);
                            
                            // If enemy doesn't have direct detection, reset their detection
                            if (!hasLineOfSight)
                            {
                                // Reset enemy detection so they return to original position
                                enemy.ResetDetection();
                            }
                        }
                    }
                }
            }

            // Update enemies with range-based batching (only update nearby enemies)
            // This significantly improves performance with many enemies
            const float ENEMY_UPDATE_RANGE = GameConfig.EnemyUpdateRange;
            const float ENEMY_UPDATE_RANGE_SQUARED = ENEMY_UPDATE_RANGE * ENEMY_UPDATE_RANGE;
            
            // Update all enemies (with range-based optimization)
            foreach (var enemy in _enemies)
            {
                // Only update alive enemies
                if (!enemy.IsAlive)
                {
                    // Update death animation/pulse for dead enemies
                    enemy.UpdateDeathAnimation(deltaTime);
                    continue;
                }
                
                // Skip updating distant idle enemies (unless they've detected the player or are returning to original position)
                if (!enemy.HasDetectedPlayer)
                {
                    // Always update enemies that are not at their original positions (so they can return home)
                    bool atOriginalPosition = enemy.IsAtOriginalPosition();
                    if (atOriginalPosition)
                    {
                        float distanceSquared = Vector2.DistanceSquared(playerPosition, enemy.Position);
                        if (distanceSquared > ENEMY_UPDATE_RANGE_SQUARED)
                        {
                            continue; // Skip updating this enemy - too far away and already at home
                        }
                    }
                    // If not at original position, always update so enemy can return home
                }
                
                // Capture enemy position for collision checking (to exclude self from collision)
                Vector2 enemyCurrentPos = enemy.Position;
                
                // Create terrain-only collision check for pathfinding (same as player uses)
                // Enemy collision will be handled during movement via MoveWithCollision sliding
                Func<Vector2, bool> terrainOnlyCheck = (pos) => _collisionManager.CheckMovementCollision(pos);
                
                enemy.Update(
                    playerPosition, // Use cached position
                    deltaTime, 
                    playerIsSneaking, // Use cached sneaking state
                    (pos) => _collisionManager.CheckCollision(pos, true, enemyCurrentPos), 
                    (from, to) => _collisionManager.IsLineOfSightBlocked(from, to, enemyCurrentPos),
                    _collisionManager,
                    terrainOnlyCheck, // Pass terrain-only check for pathfinding
                    _alarmActive,
                    _player.IsAlive // Pass player alive status
                );
                
                // Count active pathfinding
                if (enemy.TargetPosition.HasValue)
                {
                    _activePathfindingCount++;
                }

                // Check if enemy hits player (only if player is alive)
                if (_player.IsAlive)
                {
                    float distanceSquared = Vector2.DistanceSquared(playerPosition, enemy.Position); // Use cached position
                    float attackRangeSquared = enemy.AttackRange * enemy.AttackRange;
                    if (enemy.IsAttacking && distanceSquared <= attackRangeSquared)
                    {
                        // Make enemy face the player when attacking
                        enemy.FaceTarget(playerPosition); // Use cached position
                        
                        _player.TakeDamage(GameConfig.EnemyAttackDamage);
                        if (_renderSystem != null)
                        {
                            _renderSystem.ShowDamageNumber(playerPosition, GameConfig.EnemyAttackDamage); // Use cached position
                            _damageNumberCallCount++;
                        }
                        #if DEBUG
                        LogOverlay.Log($"[EntityManager] Player took {GameConfig.EnemyAttackDamage} damage! Health: {_player.CurrentHealth:F1}/{_player.MaxHealth:F1}", LogLevel.Warning);
                        #endif
                        break; // Only take one hit per frame
                    }
                }
            }
            
            _pathfindingStopwatch.Stop();
            _lastPathfindingTimeMs = (float)_pathfindingStopwatch.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// Set auto-attack target (left-click on enemy)
        /// </summary>
        public void SetAutoAttackTarget(Enemy enemy)
        {
            _autoAttackTarget = enemy;
        }
        
        /// <summary>
        /// Clear auto-attack target
        /// </summary>
        public void ClearAutoAttackTarget()
        {
            _autoAttackTarget = null;
        }
        
        /// <summary>
        /// Handle player movement command
        /// </summary>
        public void MovePlayerTo(Vector2 target)
        {
            if (_collisionManager == null)
                throw new InvalidOperationException("CollisionManager must be set before calling MovePlayerTo");
            
            // Don't allow movement if attacking with projectile weapon
            if (_isAttackingWithProjectile && _autoAttackTarget != null && _autoAttackTarget.IsAlive)
            {
                // Check if still in range
                float distanceToEnemy = Vector2.Distance(_player.Position, _autoAttackTarget.Position);
                const float veryCloseRange = 30.0f;
                const float meleeRange = 80.0f;
                
                if (_player.EquippedWeapon is Gun gun)
                {
                    const float projectileLifetime = 0.5f;
                    float projectileRange = gun.ProjectileSpeed * projectileLifetime;
                    
                    // If very close, check melee range
                    if (distanceToEnemy <= veryCloseRange)
                    {
                        if (distanceToEnemy <= meleeRange)
                        {
                            // Still in melee range - don't allow movement
                            return;
                        }
                    }
                    else if (distanceToEnemy <= projectileRange)
                    {
                        // Still in projectile range - don't allow movement
                        return;
                    }
                }
                // Out of range - allow movement
                _isAttackingWithProjectile = false;
            }
            
            // CRITICAL: Always allow movement - never block it
            // In Diablo 2 style, player can always move, even when being attacked
            // Use movement-only collision (terrain only, no enemies)
            
            Console.WriteLine($"[EntityManager] Move player to ({target.X:F0}, {target.Y:F0})");
            _player.SetTarget(
                target, 
                (pos) => _collisionManager.CheckMovementCollision(pos), // Movement collision - terrain only, no enemies
                (pos) => _collisionManager.CheckMovementCollision(pos) // Terrain-only for target validation
            );
        }

        /// <summary>
        /// Handle player attack enemy (right-click: move to enemy until in range, then stop and attack)
        /// </summary>
        public void AttackEnemy(Enemy enemy)
        {
            if (!enemy.IsAlive)
            {
                _pendingAttackTarget = null;
                return;
            }
            
            // Calculate distance to enemy (used for both projectile and melee range checks)
            float distanceToEnemy = Vector2.Distance(_player.Position, enemy.Position);
            const float meleeRange = 80.0f;
            
            // If gun is equipped
            if (_player.EquippedWeapon is Gun gun)
            {
                // Calculate projectile effective range (speed * lifetime)
                const float projectileLifetime = 0.5f;
                float projectileRange = gun.ProjectileSpeed * projectileLifetime;
                
                // Only check projectile range - don't use melee behavior
                if (distanceToEnemy <= projectileRange)
                {
                    // In projectile range - stop and fire
                    _player.ClearTarget();
                    _pendingAttackTarget = enemy; // Keep attacking
                    _shouldStopOnAttack = false; // Already stopped, just keep firing
                    // Fire immediately if possible (CheckPendingAttack will handle continuous firing)
                    if (_player.CanFire())
                    {
                        _player.FaceTarget(enemy.Position);
                        Vector2 fireDirection = enemy.Position - _player.Position;
                        FireProjectile(fireDirection);
                    }
                    return;
                }
                else
                {
                    // Outside projectile range - move to enemy
                    MovePlayerTo(enemy.Position);
                    _pendingAttackTarget = enemy;
                    _shouldStopOnAttack = true;
                    return;
                }
            }
            
            // Melee weapon (sword or no weapon)
            if (distanceToEnemy <= meleeRange)
            {
                // In range - stop and attack
                _player.ClearTarget();
                PerformAttack(enemy);
                _pendingAttackTarget = enemy; // Keep attacking
                return;
            }
            else
            {
                // Not in range - move to enemy
                MovePlayerTo(enemy.Position);
                _pendingAttackTarget = enemy;
                _shouldStopOnAttack = true;
                return;
            }
        }
        
        /// <summary>
        /// Perform the actual attack on an enemy
        /// </summary>
        private void PerformAttack(Enemy enemy)
        {
            if (!enemy.IsAlive)
            {
                _pendingAttackTarget = null;
                return;
            }
            
            // Make player face the enemy when attacking and keep facing during attack
            _player.SetAttackTarget(enemy.Position);
                
            // Calculate attack damage: use weapon damage if equipped, otherwise use player data or default
            float attackDamage;
            if (_player.EquippedWeapon != null)
            {
                attackDamage = _player.EquippedWeapon.Damage;
            }
            else
            {
                attackDamage = _player._playerData?.AttackDamage ?? GameConfig.PlayerAttackDamage;
            }
            enemy.TakeDamage(attackDamage);
            
            // Apply knockback/stun from weapon
            if (_player.EquippedWeapon != null)
            {
                enemy.ApplyKnockback(_player.EquippedWeapon.KnockbackDuration);
            }
            
            if (_renderSystem != null)
            {
                _renderSystem.ShowDamageNumber(enemy.Position, attackDamage);
            }
            LogOverlay.Log($"[EntityManager] Player attacked enemy! Enemy health: {enemy.CurrentHealth:F1}/{enemy.MaxHealth:F1}, Knockback: {(_player.EquippedWeapon?.KnockbackDuration ?? 0.0f):F2}s", LogLevel.Info);
            
            if (!enemy.IsAlive)
            {
                LogOverlay.Log("[EntityManager] Enemy defeated!", LogLevel.Info);
                _pendingAttackTarget = null;
            }
        }
        
        /// <summary>
        /// Check if player should auto-attack enemy while moving (left-click behavior)
        /// </summary>
        private void CheckAutoAttack()
        {
            if (_autoAttackTarget == null || !_autoAttackTarget.IsAlive)
            {
                _autoAttackTarget = null;
                _isAttackingWithProjectile = false;
                return;
            }
            
            float distanceToEnemy = Vector2.Distance(_player.Position, _autoAttackTarget.Position);
            
            // Check if in weapon range
            bool inRange = false;
            const float meleeRange = 80.0f;
            const float veryCloseRange = 30.0f; // Very close range for projectile weapons to act as melee
            
            if (_player.EquippedWeapon is Gun equippedGun)
            {
                // Calculate projectile range
                const float projectileLifetime = 0.5f;
                float projectileRange = equippedGun.ProjectileSpeed * projectileLifetime;
                
                // If very close, use melee behavior
                if (distanceToEnemy <= veryCloseRange)
                {
                    inRange = distanceToEnemy <= meleeRange;
                }
                else
                {
                    inRange = distanceToEnemy <= projectileRange;
                }
                
                if (inRange)
                {
                    // Stop movement and start attacking
                    _player.ClearTarget();
                    _isAttackingWithProjectile = true; // Mark that we're attacking with projectile
                    
                    // If very close, use melee attack
                    if (distanceToEnemy <= veryCloseRange && distanceToEnemy <= meleeRange)
                    {
                        _isAttackingWithProjectile = false; // Not using projectile, using melee
                        PerformAttack(_autoAttackTarget);
                    }
                    else if (_player.CanFire())
                    {
                        // Fire projectile
                        _player.FaceTarget(_autoAttackTarget.Position);
                        Vector2 fireDirection = _autoAttackTarget.Position - _player.Position;
                        FireProjectile(fireDirection);
                    }
                    // Keep attacking continuously while in range
                }
                else
                {
                    // Out of range - allow movement
                    _isAttackingWithProjectile = false;
                }
            }
            else if (_player.EquippedWeapon is Sword)
            {
                inRange = distanceToEnemy <= meleeRange;
                
                if (inRange)
                {
                    // Stop movement and start attacking
                    _player.ClearTarget();
                    PerformAttack(_autoAttackTarget);
                    // Keep attacking continuously while in range
                }
            }
            else
            {
                // No weapon - use melee range
                inRange = distanceToEnemy <= meleeRange;
                
                if (inRange)
                {
                    // Stop movement and start attacking
                    _player.ClearTarget();
                    PerformAttack(_autoAttackTarget);
                    // Keep attacking continuously while in range
                }
            }
        }
        
        /// <summary>
        /// Check if player reached pending attack target and attack if in range (right-click behavior)
        /// </summary>
        private void CheckPendingAttack()
        {
            if (_pendingAttackTarget == null || !_pendingAttackTarget.IsAlive)
            {
                _pendingAttackTarget = null;
                _shouldStopOnAttack = false;
                return;
            }
            
            float distanceToEnemy = Vector2.Distance(_player.Position, _pendingAttackTarget.Position);
            const float meleeRange = 80.0f;
            
            // If gun is equipped
            if (_player.EquippedWeapon is Gun gun)
            {
                const float projectileLifetime = 0.5f;
                float projectileRange = gun.ProjectileSpeed * projectileLifetime;
                
                // Only check projectile range - stop only when in pistol range
                if (distanceToEnemy <= projectileRange)
                {
                    // In projectile range - stop and fire continuously
                    _player.ClearTarget(); // Always stop when in pistol range
                    _shouldStopOnAttack = false;
                    
                    if (_player.CanFire())
                    {
                        _player.FaceTarget(_pendingAttackTarget.Position);
                        Vector2 fireDirection = _pendingAttackTarget.Position - _player.Position;
                        FireProjectile(fireDirection);
                    }
                    return; // Keep attacking
                }
                // Otherwise continue moving towards enemy
                return;
            }
            
            // Melee weapon
            if (distanceToEnemy <= meleeRange)
            {
                // In range - stop and attack continuously
                if (_shouldStopOnAttack)
                {
                    _player.ClearTarget();
                    _shouldStopOnAttack = false;
                }
                PerformAttack(_pendingAttackTarget);
                return; // Keep attacking
            }
            // Otherwise continue moving
        }

        /// <summary>
        /// Check if player is near any weapon pickups and pick them up
        /// </summary>
        private void CheckWeaponPickups()
        {
            if (!_player.IsAlive)
                return;

            const float pickupRadius = 40.0f; // Distance to pick up weapon
            const float pickupRadiusSquared = pickupRadius * pickupRadius;

            foreach (var weaponPickup in _weaponPickups)
            {
                if (weaponPickup.IsPickedUp)
                    continue;

                float distanceSquared = Vector2.DistanceSquared(_player.Position, weaponPickup.Position);
                if (distanceSquared <= pickupRadiusSquared)
                {
                    // Add weapon to inventory and equip it
                    _player.AddWeaponToInventory(weaponPickup.Weapon);
                    _player.EquipWeapon(weaponPickup.Weapon);
                    weaponPickup.PickUp();
                    LogOverlay.Log($"[EntityManager] Player picked up {weaponPickup.Weapon.Name}!", LogLevel.Info);
                    break; // Only pick up one weapon per frame
                }
            }
        }

        /// <summary>
        /// Hold fire: stop movement, face mouse cursor, and fire continuously
        /// </summary>
        public void HoldFireProjectile(Vector2 mouseWorldPosition)
        {
            if (!_player.IsAlive)
            {
                return;
            }
            
            if (_player.EquippedWeapon == null || !(_player.EquippedWeapon is Gun))
            {
                return;
            }

            // Stop player movement
            _player.ClearTarget();
            
            // Make player face the mouse cursor
            _player.FaceTarget(mouseWorldPosition);
            
            // Fire projectile in the direction player is facing (if cooldown allows)
            if (_player.CanFire())
            {
                float rotation = _player.Rotation;
                Vector2 fireDirection = new Vector2((float)Math.Cos(rotation), (float)Math.Sin(rotation));
                FireProjectile(fireDirection);
            }
        }

        /// <summary>
        /// Clear player movement target
        /// </summary>
        public void ClearPlayerTarget()
        {
            _player.ClearTarget();
        }

        /// <summary>
        /// Fire a projectile from the player's equipped weapon
        /// </summary>
        public void FireProjectile(Vector2 direction)
        {
            if (!_player.IsAlive)
            {
                LogOverlay.Log("[EntityManager] Cannot fire - player is dead", LogLevel.Warning);
                return;
            }
            
            if (_player.EquippedWeapon == null)
            {
                LogOverlay.Log("[EntityManager] Cannot fire - no weapon equipped", LogLevel.Warning);
                return;
            }

            // Check if weapon is a gun
            if (_player.EquippedWeapon is Gun gun)
            {
                // Check fire rate cooldown
                if (!_player.CanFire())
                {
                    LogOverlay.Log("[EntityManager] Cannot fire - cooldown active", LogLevel.Warning);
                    return;
                }
                
                Vector2 fireDirection = direction;
                if (fireDirection.LengthSquared() < 0.01f)
                {
                    // No direction specified, use player's facing direction
                    float rotation = _player.Rotation;
                    fireDirection = new Vector2((float)Math.Cos(rotation), (float)Math.Sin(rotation));
                    LogOverlay.Log($"[EntityManager] Using player rotation for fire direction: {rotation:F2}", LogLevel.Info);
                }
                else
                {
                    fireDirection.Normalize();
                }

                // Create projectile
                Projectile projectile = new Projectile(
                    _player.Position,
                    fireDirection,
                    gun.ProjectileSpeed,
                    gun.Damage,
                    isPlayerProjectile: true,
                    lifetime: 0.5f // 250 pixel range (500 * 0.5 = 250)
                );

                _projectiles.Add(projectile);
                _player.OnFired();
                
                // Stop player movement when firing projectile
                _player.ClearTarget();
                
                LogOverlay.Log($"[EntityManager] Player fired {gun.Name}! Projectile created at ({_player.Position.X:F1}, {_player.Position.Y:F1}) direction ({fireDirection.X:F2}, {fireDirection.Y:F2})", LogLevel.Info);
            }
            else
            {
                LogOverlay.Log($"[EntityManager] Cannot fire - equipped weapon is not a gun (type: {_player.EquippedWeapon.GetType().Name})", LogLevel.Warning);
            }
        }

        /// <summary>
        /// Update all projectiles and handle collisions
        /// </summary>
        private void UpdateProjectiles(float deltaTime)
        {
            if (_collisionManager == null)
                return;

            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = _projectiles[i];
                
                // Update projectile
                projectile.Update(deltaTime);

                // Remove expired projectiles
                if (projectile.IsExpired)
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }

                // Check collision with terrain
                if (_collisionManager.CheckCollision(projectile.Position, false))
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }

                // Check collision with enemies (if player projectile) or player (if enemy projectile)
                if (projectile.IsPlayerProjectile)
                {
                    // Check collision with enemies
                    foreach (var enemy in _enemies)
                    {
                        if (!enemy.IsAlive)
                            continue;

                        float distance = Vector2.Distance(projectile.Position, enemy.Position);
                        if (distance < 20.0f) // Hit radius
                        {
                            enemy.TakeDamage(projectile.Damage);
                            
                            // Apply knockback/stun from weapon
                            if (_player.EquippedWeapon != null)
                            {
                                enemy.ApplyKnockback(_player.EquippedWeapon.KnockbackDuration);
                            }
                            
                            if (_renderSystem != null)
                            {
                                _renderSystem.ShowDamageNumber(enemy.Position, projectile.Damage);
                            }
                            LogOverlay.Log($"[EntityManager] Projectile hit enemy! Damage: {projectile.Damage:F1}, Knockback: {(_player.EquippedWeapon?.KnockbackDuration ?? 0.0f):F2}s", LogLevel.Info);
                            _projectiles.RemoveAt(i);
                            break;
                        }
                    }
                    
                    // Check collision with cameras
                    foreach (var camera in _cameras)
                    {
                        if (!camera.IsAlive)
                            continue;

                        float distance = Vector2.Distance(projectile.Position, camera.Position);
                        if (distance < 20.0f) // Hit radius
                        {
                            camera.TakeDamage(projectile.Damage);
                            
                            // Apply knockback/stun from weapon
                            if (_player.EquippedWeapon != null)
                            {
                                camera.ApplyKnockback(_player.EquippedWeapon.KnockbackDuration);
                            }
                            
                            if (_renderSystem != null)
                            {
                                _renderSystem.ShowDamageNumber(camera.Position, projectile.Damage);
                            }
                            LogOverlay.Log($"[EntityManager] Projectile hit camera! Damage: {projectile.Damage:F1}", LogLevel.Info);
                            _projectiles.RemoveAt(i);
                            break;
                        }
                    }
                }
                else
                {
                    // Enemy projectile - check collision with player
                    if (_player.IsAlive)
                    {
                        float distance = Vector2.Distance(projectile.Position, _player.Position);
                        if (distance < 20.0f) // Hit radius
                        {
                            _player.TakeDamage(projectile.Damage);
                            if (_renderSystem != null)
                            {
                                _renderSystem.ShowDamageNumber(_player.Position, projectile.Damage);
                            }
                            LogOverlay.Log($"[EntityManager] Player hit by projectile! Damage: {projectile.Damage:F1}", LogLevel.Info);
                            _projectiles.RemoveAt(i);
                            continue;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Toggle player sneak mode
        /// </summary>
        public void TogglePlayerSneak()
        {
            _player.ToggleSneak();
        }

        /// <summary>
        /// Reset player position
        /// </summary>
        public void ResetPlayerPosition(Vector2 position)
        {
            _player.Position = position;
            _player.ClearTarget();
        }
    }
}

