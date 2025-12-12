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
        private CollisionManager? _collisionManager;
        
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
                    _enemies.Add(new Enemy(enemyPosition));
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
                    Vector2 cameraPosition = new Vector2(cameraData.X, cameraData.Y);
                    _cameras.Add(new SecurityCamera(cameraPosition, cameraData.Rotation, cameraData.DetectionRange, cameraData.SightConeAngle));
                }
                Console.WriteLine($"[EntityManager] Loaded {_cameras.Count} cameras");
            }
        }

        /// <summary>
        /// Get the enemy the player is currently in combat with (if any)
        /// An enemy is considered "in combat" if it has detected the player and is within combat range
        /// </summary>
        private Enemy? GetEnemyInCombat()
        {
            const float combatRange = 200.0f; // Range within which enemy is considered "in combat"
            
            Enemy? closestCombatEnemy = null;
            float closestDistance = float.MaxValue;
            
            foreach (var enemy in _enemies)
            {
                if (enemy.HasDetectedPlayer)
                {
                    float distanceToPlayer = Vector2.Distance(_player.Position, enemy.Position);
                    if (distanceToPlayer <= combatRange && distanceToPlayer < closestDistance)
                    {
                        closestCombatEnemy = enemy;
                        closestDistance = distanceToPlayer;
                    }
                }
            }
            
            return closestCombatEnemy;
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
            _player.Update(
                followPosition, 
                deltaTime, 
                playerCollisionCheck, 
                (from, to) => _collisionManager.IsLineOfSightBlocked(from, to),
                _collisionManager,
                combatEnemyList
            );
            
            // Count active pathfinding
            if (_player.TargetPosition.HasValue)
            {
                _activePathfindingCount++;
            }

            // Update all cameras first (they can alert enemies)
            bool anyCameraDetecting = false;
            foreach (var camera in _cameras)
            {
                camera.Update(deltaTime);
                bool cameraDetected = camera.UpdateDetection(
                    _player.Position,
                    deltaTime,
                    _player.IsSneaking,
                    (from, to) => _collisionManager.IsLineOfSightBlocked(from, to),
                    _enemies
                );
                
                // Check if camera is currently detecting player (in sight cone with line of sight)
                if (camera.IsCurrentlyDetecting(_player.Position, _player.IsSneaking, (from, to) => _collisionManager.IsLineOfSightBlocked(from, to)))
                {
                    anyCameraDetecting = true;
                }
            }
            
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
                            float distanceToPlayer = directionToPlayer.Length();
                            float effectiveRange = _player.IsSneaking 
                                ? enemy.DetectionRange * GameConfig.EnemySneakDetectionMultiplier 
                                : enemy.DetectionRange;
                            
                            bool inRange = distanceToPlayer <= effectiveRange;
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

            // Update all enemies
            foreach (var enemy in _enemies)
            {
                // Capture enemy position for collision checking (to exclude self from collision)
                Vector2 enemyCurrentPos = enemy.Position;
                
                // Create terrain-only collision check for pathfinding (like player)
                // Enemy collision will be handled during movement via MoveWithCollision sliding
                Func<Vector2, bool> terrainOnlyCheck = (pos) => _collisionManager.CheckCollision(pos, false);
                
                enemy.Update(
                    _player.Position, 
                    deltaTime, 
                    _player.IsSneaking, 
                    (pos) => _collisionManager.CheckCollision(pos, true, enemyCurrentPos), 
                    (from, to) => _collisionManager.IsLineOfSightBlocked(from, to, enemyCurrentPos),
                    _collisionManager,
                    terrainOnlyCheck, // Pass terrain-only check for pathfinding
                    _alarmActive
                );
                
                // Count active pathfinding
                if (enemy.TargetPosition.HasValue)
                {
                    _activePathfindingCount++;
                }

                // Check if enemy hits player
                float distanceToPlayer = Vector2.Distance(_player.Position, enemy.Position);
                if (enemy.IsAttacking && distanceToPlayer <= enemy.AttackRange)
                {
                    _player.TakeHit();
                    break; // Only take one hit per frame
                }
            }
            
            _pathfindingStopwatch.Stop();
            _lastPathfindingTimeMs = (float)_pathfindingStopwatch.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// Handle player movement command
        /// </summary>
        public void MovePlayerTo(Vector2 target)
        {
            if (_collisionManager == null)
                throw new InvalidOperationException("CollisionManager must be set before calling MovePlayerTo");
            
            // Determine which enemy the player is in combat with (if any)
            Enemy? combatEnemy = GetEnemyInCombat();
            
            // Create collision check function that only checks the combat enemy (or no enemies if not in combat)
            // This allows the player to move away from non-combat enemies without collision blocking
            Func<Vector2, bool> playerCollisionCheck;
            if (combatEnemy != null)
            {
                // Only check collision with the enemy in combat
                var combatEnemyList = new List<Enemy> { combatEnemy };
                playerCollisionCheck = (pos) => _collisionManager.CheckCollision(pos, combatEnemyList);
            }
            else
            {
                // Not in combat - only check terrain collision (no enemy collision)
                playerCollisionCheck = (pos) => _collisionManager.CheckCollision(pos, false);
            }
            
            // Check if enemies are blocking the path (they move, so timing matters)
            bool enemyNearTarget = false;
            foreach (var enemy in _enemies)
            {
                float distToTarget = Vector2.Distance(enemy.Position, target);
                if (distToTarget < GameConfig.EnemyNearTargetThreshold)
                {
                    enemyNearTarget = true;
                    LogOverlay.Log($"[EntityManager] Enemy near target at ({enemy.Position.X:F1}, {enemy.Position.Y:F1}), dist={distToTarget:F1}px", LogLevel.Warning);
                }
            }
            
            Console.WriteLine($"[EntityManager] Move player to ({target.X:F0}, {target.Y:F0}), EnemyNear={enemyNearTarget}, CombatEnemy={(combatEnemy != null ? "Yes" : "No")}");
            _player.SetTarget(
                target, 
                playerCollisionCheck,
                (pos) => _collisionManager.CheckCollision(pos, false) // Terrain-only for target validation
            );
        }

        /// <summary>
        /// Handle player attack enemy
        /// </summary>
        public void AttackEnemy(Enemy enemy)
        {
            Console.WriteLine("[EntityManager] Attacked enemy");
            enemy.TakeHit();
        }

        /// <summary>
        /// Clear player movement target
        /// </summary>
        public void ClearPlayerTarget()
        {
            _player.ClearTarget();
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

