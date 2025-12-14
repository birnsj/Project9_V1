using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Project9
{
    public class Enemy : Entity
    {
        // Reference to EnemyData for property editing
        internal Project9.Shared.EnemyData? _enemyData;
        
        // Enemy-specific fields (AI behavior)
        private Vector2 _originalPosition;
        private float _attackRange;
        private float _detectionRange;
        private float _attackCooldown;
        private float _currentAttackCooldown;
        private bool _isAttacking;
        private bool _hasDetectedPlayer;
        private float _exclamationTimer;
        private float _exclamationDuration = GameConfig.EnemyExclamationDuration;
        
        // Search behavior (when player goes out of view during alarm)
        private bool _isSearching = false;
        private Vector2 _lastKnownPlayerPosition;
        private Vector2 _searchTarget;
        private float _searchTimer = 0.0f;
        private bool _previouslyHadLineOfSight = false; // Track if we had line of sight previously
        private float _searchDuration;
        private float _searchRadius;
        
        // Chase behavior - track how long player has been out of range
        private float _outOfRangeTimer = 0.0f;
        private float _outOfRangeThreshold;
        private float _maxChaseRange;
        
        // Death animation
        private float _deathPulseTimer = 0.0f;
        private float _deathPulseSpeed = 2.0f; // Pulses per second
        private bool _isDead = false;
        
        // Knockback/stun when hit
        private float _knockbackTimer = 0.0f;
        public bool IsStunned => _knockbackTimer > 0.0f;
        
        // Path simplification throttling
        private float _lastSimplifyTime = 0.0f;
        private const float SIMPLIFY_INTERVAL = 0.5f; // Only simplify every 0.5s
        
        // Sight cone and rotation
        private float _rotation;
        
        // Cached sight cone direction for performance
        private Vector2 _cachedDirection;
        private float _lastRotation = float.MinValue;
        
        // Pre-allocated static array to avoid allocations (matches 2:1 aspect ratio of isometric tiles)
        private static readonly Vector2[] IsometricAxesStatic = new Vector2[]
        {
            new Vector2(1, 0),            // East
            new Vector2(0, 1),            // South
            new Vector2(-1, 0),           // West
            new Vector2(0, -1),           // North
            new Vector2(0.894f, 0.447f),  // Southeast (isometric diagonal)
            new Vector2(0.894f, -0.447f), // Northeast (isometric diagonal)
            new Vector2(-0.894f, 0.447f), // Southwest (isometric diagonal)
            new Vector2(-0.894f, -0.447f) // Northwest (isometric diagonal)
        };
        
        public float Rotation => _rotation;
        
        /// <summary>
        /// Set rotation to face a target position
        /// </summary>
        public void FaceTarget(Vector2 targetPosition)
        {
            Vector2 direction = targetPosition - _position;
            if (direction.LengthSquared() > 0.01f)
            {
                direction.Normalize();
                _rotation = (float)Math.Atan2(direction.Y, direction.X);
            }
        }
        
        private float _sightConeAngle;
        private float _sightConeLength;
        private float _rotationSpeed;
        private float _behaviorTimer;
        private float _behaviorChangeInterval;
        private bool _isRotating;
        private Random _random;
        
        // Rendering textures
        private Texture2D? _circleTexture;
        private Texture2D? _sightConeTexture;
        private Texture2D? _exclamationTexture;

        public float AttackRange => _attackRange;
        public float DetectionRange => _detectionRange;
        public bool IsAttacking => _isAttacking;
        public bool HasDetectedPlayer => _hasDetectedPlayer;
        
        /// <summary>
        /// Apply knockback/stun effect when hit by a weapon
        /// </summary>
        public void ApplyKnockback(float duration)
        {
            _knockbackTimer = Math.Max(_knockbackTimer, duration); // Use max to prevent shorter stuns from overriding longer ones
        }
        
        /// <summary>
        /// Check if enemy is at its original position (within threshold)
        /// </summary>
        public bool IsAtOriginalPosition()
        {
            float distanceSquared = Vector2.DistanceSquared(_position, _originalPosition);
            const float thresholdSquared = 25.0f; // 5.0f * 5.0f
            return distanceSquared <= thresholdSquared;
        }
        
        /// <summary>
        /// Force the enemy to detect the player (called by cameras when they detect the player)
        /// </summary>
        public void ForceDetectPlayer(Vector2 playerPosition)
        {
            // Always set detection, even if already detected (refreshes the alert)
            Console.WriteLine($"[Enemy] ForceDetectPlayer called at ({_position.X:F1}, {_position.Y:F1}). Was detected: {_hasDetectedPlayer}");
            _hasDetectedPlayer = true;
            _exclamationTimer = _exclamationDuration;
            _lastKnownPlayerPosition = playerPosition; // Set last known position so enemy knows where to go
            _isSearching = false; // Stop searching if we're alerted
            _searchTimer = 0.0f; // Reset search timer
            Console.WriteLine($"[Enemy] Now HasDetectedPlayer = {_hasDetectedPlayer}, lastKnownPos = ({_lastKnownPlayerPosition.X:F1}, {_lastKnownPlayerPosition.Y:F1})");
        }
        
        /// <summary>
        /// Reset enemy detection (called when alarm expires and enemy doesn't have direct detection)
        /// </summary>
        public void ResetDetection()
        {
            _hasDetectedPlayer = false;
            _exclamationTimer = 0.0f;
            _isSearching = false; // Stop searching so enemy can return to original position
            _searchTimer = 0.0f; // Reset search timer
            _path?.Clear(); // Clear path so they can return to original position
        }

        public Enemy(Vector2 startPosition) 
            : this(startPosition, null)
        {
        }
        
        public Enemy(Vector2 startPosition, Project9.Shared.EnemyData? enemyData) 
            : base(startPosition, Color.DarkRed, 
                  enemyData?.ChaseSpeed ?? GameConfig.EnemyChaseSpeed, 
                  enemyData?.ChaseSpeed ?? GameConfig.EnemyChaseSpeed, 
                  maxHealth: enemyData?.MaxHealth ?? 50f)
        {
            _enemyData = enemyData;
            _originalPosition = startPosition;
            _attackRange = enemyData?.AttackRange ?? GameConfig.EnemyAttackRange;
            _detectionRange = enemyData?.DetectionRange ?? GameConfig.EnemyDetectionRange;
            _attackCooldown = enemyData?.AttackCooldown ?? GameConfig.EnemyAttackCooldown;
            _currentAttackCooldown = 0.0f;
            _isAttacking = false;
            _hasDetectedPlayer = false;
            
            // Initialize rotation and sight cone
            _random = new Random();
            if (enemyData != null && enemyData.InitialRotation >= 0)
            {
                _rotation = enemyData.InitialRotation;
            }
            else
            {
            _rotation = (float)(_random.NextDouble() * Math.PI * 2);
            }
            
            _sightConeAngle = MathHelper.ToRadians(enemyData?.SightConeAngle ?? 60.0f);
            
            if (enemyData != null && enemyData.SightConeLength > 0)
            {
                _sightConeLength = enemyData.SightConeLength;
            }
            else
            {
            _sightConeLength = _detectionRange * 0.8f;
            }
            
            _rotationSpeed = MathHelper.ToRadians(enemyData?.RotationSpeed ?? 45.0f);
            _exclamationDuration = enemyData?.ExclamationDuration ?? GameConfig.EnemyExclamationDuration;
            _searchDuration = enemyData?.SearchDuration ?? GameConfig.EnemySearchDuration;
            _searchRadius = enemyData?.SearchRadius ?? GameConfig.EnemySearchRadius;
            _outOfRangeThreshold = enemyData?.OutOfRangeThreshold ?? 3.0f;
            _maxChaseRange = enemyData?.MaxChaseRange ?? GameConfig.EnemyMaxChaseRange;
            _behaviorTimer = 0.0f;
            _behaviorChangeInterval = 2.0f + (float)(_random.NextDouble() * 3.0f);
            _isRotating = _random.Next(2) == 0;
            _exclamationTimer = 0.0f;
        }
        
        /// <summary>
        /// Update properties from EnemyData (called when properties are edited in editor)
        /// </summary>
        public void UpdateFromEnemyData()
        {
            if (_enemyData == null) return;
            
            // Update runtime values from EnemyData
            _attackRange = _enemyData.AttackRange;
            _detectionRange = _enemyData.DetectionRange;
            _attackCooldown = _enemyData.AttackCooldown;
            _walkSpeed = _enemyData.ChaseSpeed;
            _runSpeed = _enemyData.ChaseSpeed;
            _maxHealth = _enemyData.MaxHealth;
            _sightConeAngle = MathHelper.ToRadians(_enemyData.SightConeAngle);
            
            if (_enemyData.SightConeLength > 0)
            {
                _sightConeLength = _enemyData.SightConeLength;
            }
            else
            {
                _sightConeLength = _detectionRange * 0.8f;
            }
            
            _rotationSpeed = MathHelper.ToRadians(_enemyData.RotationSpeed);
            _exclamationDuration = _enemyData.ExclamationDuration;
            _searchDuration = _enemyData.SearchDuration;
            _searchRadius = _enemyData.SearchRadius;
            _outOfRangeThreshold = _enemyData.OutOfRangeThreshold;
            _maxChaseRange = _enemyData.MaxChaseRange;
            
            if (_enemyData.InitialRotation >= 0)
            {
                _rotation = _enemyData.InitialRotation;
            }
        }
        
        /// <summary>
        /// Initialize all textures for this enemy (call during LoadContent, not Draw)
        /// </summary>
        public override void InitializeTextures(GraphicsDevice graphicsDevice)
        {
            base.InitializeTextures(graphicsDevice);
            // Sight cone and exclamation textures are created on-demand when needed
        }
        
        private bool IsPointInSightCone(Vector2 point)
        {
            Vector2 directionToPoint = point - _position;
            float distanceSquared = directionToPoint.LengthSquared();
            float sightConeLengthSquared = _sightConeLength * _sightConeLength;
            
            if (distanceSquared > sightConeLengthSquared || distanceSquared < 0.01f)
                return false;
            
            directionToPoint.Normalize();
            
            // Cache direction vector when rotation changes
            if (Math.Abs(_rotation - _lastRotation) > 0.01f)
            {
                _cachedDirection = new Vector2(
                    (float)Math.Cos(_rotation),
                    (float)Math.Sin(_rotation)
                );
                _lastRotation = _rotation;
            }
            
            float pointAngle = (float)Math.Atan2(directionToPoint.Y, directionToPoint.X);
            
            float enemyAngle = _rotation;
            while (enemyAngle < 0) enemyAngle += (float)(Math.PI * 2);
            while (enemyAngle >= Math.PI * 2) enemyAngle -= (float)(Math.PI * 2);
            while (pointAngle < 0) pointAngle += (float)(Math.PI * 2);
            while (pointAngle >= Math.PI * 2) pointAngle -= (float)(Math.PI * 2);
            
            float angleDiff = Math.Abs(pointAngle - enemyAngle);
            if (angleDiff > Math.PI)
                angleDiff = (float)(Math.PI * 2 - angleDiff);
            
            return angleDiff <= _sightConeAngle / 2.0f;
        }

        public override void Update(float deltaTime)
        {
            // This signature for base compatibility - use full Update below
        }
        
        /// <summary>
        /// Update death animation (pulsing effect)
        /// </summary>
        public void UpdateDeathAnimation(float deltaTime)
        {
            if (!_isDead && !IsAlive)
            {
                _isDead = true;
            }
            
            if (_isDead)
            {
                _deathPulseTimer += deltaTime * _deathPulseSpeed;
            }
        }
        
        public bool IsDead => _isDead;

        public void Update(Vector2 playerPosition, float deltaTime, bool playerIsSneaking = false, Func<Vector2, bool>? checkCollision = null, Func<Vector2, Vector2, bool>? checkLineOfSight = null, CollisionManager? collisionManager = null, Func<Vector2, bool>? checkTerrainOnly = null, bool alarmActive = false, bool playerIsAlive = true)
        {
            UpdateFlashing(deltaTime);
            
            // Update knockback/stun timer
            if (_knockbackTimer > 0.0f)
            {
                _knockbackTimer -= deltaTime;
                if (_knockbackTimer < 0.0f)
                    _knockbackTimer = 0.0f;
            }

            if (_currentAttackCooldown > 0.0f)
            {
                _currentAttackCooldown -= deltaTime;
            }

            if (_exclamationTimer > 0.0f)
            {
                _exclamationTimer -= deltaTime;
            }

            // If player is dead, return to original position and clear detection
            if (!playerIsAlive)
            {
                if (_hasDetectedPlayer)
                {
                    _hasDetectedPlayer = false;
                    _isAttacking = false;
                }
                ReturnToOriginal(deltaTime, checkCollision, collisionManager);
                return;
            }

            Vector2 directionToPlayer = playerPosition - _position;
            float distanceToPlayerSquared = directionToPlayer.LengthSquared();

            float effectiveDetectionRange;
            if (_hasDetectedPlayer)
            {
                effectiveDetectionRange = _detectionRange;
            }
            else
            {
                effectiveDetectionRange = playerIsSneaking ? _detectionRange * GameConfig.EnemySneakDetectionMultiplier : _detectionRange;
            }
            float effectiveRangeSquared = effectiveDetectionRange * effectiveDetectionRange;

            bool playerInRange = distanceToPlayerSquared <= effectiveRangeSquared;
            bool hasLineOfSight = false;
            
            if (playerInRange)
            {
                bool lineOfSightBlocked = checkLineOfSight != null && checkLineOfSight(_position, playerPosition);
                hasLineOfSight = !lineOfSightBlocked;
                
                if (playerIsSneaking && distanceToPlayerSquared <= effectiveRangeSquared && !_hasDetectedPlayer)
                {
                    if (IsPointInSightCone(playerPosition) && hasLineOfSight)
                    {
                        _hasDetectedPlayer = true;
                        _exclamationTimer = _exclamationDuration;
                        _lastKnownPlayerPosition = playerPosition;
                        _isSearching = false; // Stop searching if we see player
                    }
                }
                else
                {
                    if (hasLineOfSight)
                    {
                        if (!_hasDetectedPlayer)
                        {
                            _exclamationTimer = _exclamationDuration;
                        }
                        _hasDetectedPlayer = true;
                        _lastKnownPlayerPosition = playerPosition;
                        _isSearching = false; // Stop searching if we see player
                    }
                }
            }
            
            // Handle search behavior during alarm
            // Only start searching if we previously had line of sight and then lost it
            // Don't search immediately if we were alerted by camera without line of sight
            if (alarmActive && _hasDetectedPlayer && !hasLineOfSight && !_isSearching && _previouslyHadLineOfSight)
            {
                // Player lost - start searching (we had line of sight before, now we don't)
                _isSearching = true;
                _searchTimer = _searchDuration;
                _lastKnownPlayerPosition = playerPosition;
                // Set first search target near last known position
                float randomAngle = (float)(_random.NextDouble() * Math.PI * 2);
                float randomDistance = (float)(_random.NextDouble() * _searchRadius);
                _searchTarget = _lastKnownPlayerPosition + new Vector2(
                    (float)Math.Cos(randomAngle) * randomDistance,
                    (float)Math.Sin(randomAngle) * randomDistance
                );
            }
            
            // Track line of sight state for next frame
            _previouslyHadLineOfSight = hasLineOfSight;
            
            // Update search timer
            if (_isSearching)
            {
                _searchTimer -= deltaTime;
                if (_searchTimer <= 0.0f)
                {
                    // Search time expired - return to original position
                    _isSearching = false;
                    _hasDetectedPlayer = false;
                    _path?.Clear();
                }
            }

            // If stunned, don't move or attack
            if (IsStunned)
            {
                _currentSpeed = 0.0f;
                _isAttacking = false;
                return; // Skip all AI behavior while stunned
            }
            
            // If enemy has detected player (either directly or via camera alert), chase them
            // Use a large chase range when alerted (1024 pixels, same as camera alert radius)
            // During alarm, enemies should chase even without direct line of sight
            float maxChaseRange = _maxChaseRange;
            
            // If alarm is active, enemies chase even without line of sight
            // Otherwise, they need line of sight to chase
            float maxChaseRangeSquared = maxChaseRange * maxChaseRange;
            bool isInRange = distanceToPlayerSquared <= maxChaseRangeSquared;
            bool hasValidLineOfSight = alarmActive || hasLineOfSight; // During alarm, don't require line of sight
            bool shouldChaseImmediate = _hasDetectedPlayer && !_isSearching && isInRange && hasValidLineOfSight;
            
            // Update out-of-range timer
            if (_hasDetectedPlayer && !_isSearching)
            {
                if (isInRange && hasValidLineOfSight)
                {
                    // Player is in range and visible - reset timer
                    _outOfRangeTimer = 0.0f;
                }
                else
                {
                    // Player is out of range or lost line of sight - increment timer
                    _outOfRangeTimer += deltaTime;
                }
            }
            else
            {
                // Not detected or searching - reset timer
                _outOfRangeTimer = 0.0f;
            }
            
            // Continue chasing if player is in range and visible, OR if within 3 second grace period
            bool withinGracePeriod = _outOfRangeTimer < _outOfRangeThreshold;
            bool shouldChase = _hasDetectedPlayer && !_isSearching && (shouldChaseImmediate || withinGracePeriod);
            
            // Debug logging
            if (_hasDetectedPlayer && !shouldChase)
            {
                float distanceToPlayer = (float)Math.Sqrt(distanceToPlayerSquared); // Only calculate for logging
                Console.WriteLine($"[Enemy] HasDetectedPlayer=true but not chasing. isSearching={_isSearching}, distance={distanceToPlayer:F1}, maxRange={maxChaseRange}, hasLoS={hasLineOfSight}, alarmActive={alarmActive}, outOfRangeTimer={_outOfRangeTimer:F2}");
            }
            
            if (shouldChase)
            {
                float attackRangeSquared = _attackRange * _attackRange;
                if (distanceToPlayerSquared <= attackRangeSquared)
                {
                    // Face the player when in attack range
                    directionToPlayer.Normalize();
                    _rotation = (float)Math.Atan2(directionToPlayer.Y, directionToPlayer.X);
                    
                    if (_currentAttackCooldown <= 0.0f)
                    {
                        _isAttacking = true;
                        _currentAttackCooldown = _attackCooldown;
                    }
                    else
                    {
                        _isAttacking = false;
                    }
                }
                else
                {
                    _isAttacking = false;
                    float distanceToPlayer = (float)Math.Sqrt(distanceToPlayerSquared); // Calculate when needed
                    ChaseTarget(playerPosition, distanceToPlayer, deltaTime, checkCollision, collisionManager, checkTerrainOnly);
                }
            }
            else if (_isSearching)
            {
                // Searching for player during alarm
                _isAttacking = false;
                SearchBehavior(deltaTime, checkCollision, collisionManager, checkTerrainOnly);
            }
            else if (_hasDetectedPlayer && _outOfRangeTimer >= _outOfRangeThreshold)
            {
                // Player has been out of range for 3 seconds, return to original position
                _isAttacking = false;
                _outOfRangeTimer = 0.0f; // Reset timer
                ReturnToOriginal(deltaTime, checkCollision, collisionManager, checkTerrainOnly);
            }
            else
            {
                _isAttacking = false;
                PatrolBehavior(deltaTime, checkCollision, collisionManager, checkTerrainOnly);
            }
        }

        private void ChaseTarget(Vector2 target, float distanceToTarget, float deltaTime, Func<Vector2, bool>? checkCollision, CollisionManager? collisionManager, Func<Vector2, bool>? checkTerrainOnly = null)
        {
            if (distanceToTarget <= _attackRange)
                return;

            // Face the player while chasing
            FaceTarget(target);

            // Use terrain-only check for direct path validation (like player)
            Func<Vector2, bool> terrainCheck = checkTerrainOnly ?? ((pos) => checkCollision != null ? checkCollision(pos) : false);
            bool pathClear = CheckDirectPath(target, terrainCheck);
            
            // Recalculate path if blocked or if path is empty/invalid
            // Use terrain-only collision for pathfinding - enemy collision handled during movement
            if (!pathClear && (_path == null || _path.Count == 0) && checkTerrainOnly != null)
            {
                _path = PathfindingService.FindPath(
                    _position, 
                    target, 
                    checkTerrainOnly,
                    GameConfig.PathfindingGridCellWidth,
                    GameConfig.PathfindingGridCellHeight
                );
                if (_path != null && _path.Count > 0)
                {
                    // Throttle path simplification to avoid unnecessary work
                    float currentTime = (float)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
                    if (currentTime - _lastSimplifyTime > SIMPLIFY_INTERVAL)
                    {
                        // Use much less aggressive simplification to keep most waypoints needed for obstacles
                        var originalPath = PathfindingService.RentPath();
                        originalPath.AddRange(_path);
                        _path = PathfindingService.SimplifyPath(_path, 0.2f); // Reduced threshold to keep more waypoints around corners
                        if (_path == null || _path.Count == 0)
                        {
                            _path = originalPath; // Restore if simplification removed everything
                        }
                        else
                        {
                            PathfindingService.ReturnPath(originalPath);
                        }
                        _lastSimplifyTime = currentTime;
                    }
                }
            }
            else if (pathClear)
            {
                _path?.Clear();
            }
            
            if (_path != null && _path.Count > 0)
            {
                FollowPath(target, deltaTime, checkCollision, collisionManager, checkTerrainOnly, faceFinalTarget: true);
            }
            else
            {
                MoveDirectly(target, distanceToTarget, deltaTime, checkCollision, collisionManager, checkTerrainOnly);
            }
        }

        private void ReturnToOriginal(float deltaTime, Func<Vector2, bool>? checkCollision, CollisionManager? collisionManager, Func<Vector2, bool>? checkTerrainOnly = null)
        {
            Vector2 directionToOriginal = _originalPosition - _position;
            float distanceToOriginalSquared = directionToOriginal.LengthSquared();
            const float thresholdSquared = 25.0f; // 5.0f * 5.0f
            
            if (distanceToOriginalSquared > thresholdSquared)
            {
                // Use terrain-only check for pathfinding
                Func<Vector2, bool> terrainCheck = checkTerrainOnly ?? ((pos) => checkCollision != null ? checkCollision(pos) : false);
                bool pathClear = CheckDirectPath(_originalPosition, terrainCheck);
                
                // If stuck for too long, always try pathfinding to avoid deadlocks with other enemies
                bool shouldUsePathfinding = !pathClear || (_stuckTimer > 0.2f && checkTerrainOnly != null);
                
                if (shouldUsePathfinding && (_path == null || _path.Count == 0) && checkTerrainOnly != null)
                {
                    _path = PathfindingService.FindPath(
                        _position, 
                        _originalPosition, 
                        checkTerrainOnly,
                        GameConfig.PathfindingGridCellWidth,
                        GameConfig.PathfindingGridCellHeight
                    );
                    if (_path != null && _path.Count > 0)
                    {
                        var originalPath = PathfindingService.RentPath();
                        originalPath.AddRange(_path);
                        _path = PathfindingService.SimplifyPath(_path, 0.2f); // Reduced threshold to keep more waypoints around corners
                        if (_path == null || _path.Count == 0)
                        {
                            _path = originalPath; // Restore if simplification removed everything
                        }
                        else
                        {
                            PathfindingService.ReturnPath(originalPath);
                        }
                        // Reset stuck timer when we get a new path
                        _stuckTimer = 0.0f;
                    }
                }
                else if (pathClear && _stuckTimer < 0.1f)
                {
                    // Only clear path if path is clear and not stuck
                    _path?.Clear();
                }
                
                if (_path != null && _path.Count > 0)
                {
                    FollowPath(_originalPosition, deltaTime, checkCollision, collisionManager, checkTerrainOnly, false, includeEnemies: false);
                }
                else
                {
                    float distanceToOriginal = (float)Math.Sqrt(distanceToOriginalSquared); // Calculate when needed
                    MoveDirectly(_originalPosition, distanceToOriginal, deltaTime, checkCollision, collisionManager, checkTerrainOnly, faceTarget: false, includeEnemies: false);
                }
            }
            else
            {
                _position = _originalPosition;
                _stuckTimer = 0.0f; // Reset stuck timer when at original position
                _hasDetectedPlayer = false;
                IdleBehavior(deltaTime);
            }
        }

        private void SearchBehavior(float deltaTime, Func<Vector2, bool>? checkCollision, CollisionManager? collisionManager, Func<Vector2, bool>? checkTerrainOnly = null)
        {
            // Move towards search target
            float distanceToTarget = Vector2.Distance(_position, _searchTarget);
            
            if (distanceToTarget < 30.0f)
            {
                // Reached search target - pick a new random search point
                float randomAngle = (float)(_random.NextDouble() * Math.PI * 2);
                float randomDistance = (float)(_random.NextDouble() * _searchRadius);
                _searchTarget = _lastKnownPlayerPosition + new Vector2(
                    (float)Math.Cos(randomAngle) * randomDistance,
                    (float)Math.Sin(randomAngle) * randomDistance
                );
            }
            else
            {
                // Move towards search target
                Vector2 directionToTarget = _searchTarget - _position;
                float distanceSquared = directionToTarget.LengthSquared();
                
                if (distanceSquared > 0.01f)
                {
                    directionToTarget.Normalize();
                    Vector2 desiredPosition = _position + directionToTarget * _currentSpeed * deltaTime;
                    
                    if (collisionManager != null)
                    {
                        Vector2 finalPos = collisionManager.MoveWithCollision(_position, desiredPosition, true, 3, _position);
                        if (Vector2.DistanceSquared(_position, finalPos) > 0.01f)
                        {
                            _position = finalPos;
                        }
                    }
                    else if (checkCollision == null || !checkCollision(desiredPosition))
                    {
                        _position = desiredPosition;
                    }
                    
                    // Update rotation to face search target
                    _rotation = (float)Math.Atan2(directionToTarget.Y, directionToTarget.X);
                }
            }
        }

        private void PatrolBehavior(float deltaTime, Func<Vector2, bool>? checkCollision, CollisionManager? collisionManager, Func<Vector2, bool>? checkTerrainOnly = null)
        {
            Vector2 directionToOriginal = _originalPosition - _position;
            float distanceToOriginalSquared = directionToOriginal.LengthSquared();
            const float thresholdSquared = 25.0f; // 5.0f * 5.0f
            
            if (distanceToOriginalSquared > thresholdSquared)
            {
                // Use terrain-only check for pathfinding
                Func<Vector2, bool> terrainCheck = checkTerrainOnly ?? ((pos) => checkCollision != null ? checkCollision(pos) : false);
                bool pathClear = CheckDirectPath(_originalPosition, terrainCheck);
                
                // If stuck for too long, always try pathfinding to avoid deadlocks with other enemies
                bool shouldUsePathfinding = !pathClear || (_stuckTimer > 0.2f && checkTerrainOnly != null);
                
                if (shouldUsePathfinding && (_path == null || _path.Count == 0) && checkTerrainOnly != null)
                {
                    _path = PathfindingService.FindPath(
                        _position, 
                        _originalPosition, 
                        checkTerrainOnly,
                        GameConfig.PathfindingGridCellWidth,
                        GameConfig.PathfindingGridCellHeight
                    );
                    if (_path != null && _path.Count > 0)
                    {
                        var originalPath = PathfindingService.RentPath();
                        originalPath.AddRange(_path);
                        _path = PathfindingService.SimplifyPath(_path, 0.2f); // Reduced threshold to keep more waypoints around corners
                        if (_path == null || _path.Count == 0)
                        {
                            _path = originalPath; // Restore if simplification removed everything
                        }
                        else
                        {
                            PathfindingService.ReturnPath(originalPath);
                        }
                        // Reset stuck timer when we get a new path
                        _stuckTimer = 0.0f;
                    }
                }
                else if (pathClear && _stuckTimer < 0.1f)
                {
                    // Only clear path if path is clear and not stuck
                    _path?.Clear();
                }
                
                if (_path != null && _path.Count > 0)
                {
                    FollowPath(_originalPosition, deltaTime, checkCollision, collisionManager, checkTerrainOnly, false, includeEnemies: false);
                }
                else
                {
                    float distanceToOriginal = (float)Math.Sqrt(distanceToOriginalSquared); // Calculate when needed
                    MoveDirectly(_originalPosition, distanceToOriginal, deltaTime, checkCollision, collisionManager, checkTerrainOnly, faceTarget: false, includeEnemies: false);
                }
            }
            else
            {
                _position = _originalPosition;
                _stuckTimer = 0.0f; // Reset stuck timer when at original position
                IdleBehavior(deltaTime);
            }
        }

        private void IdleBehavior(float deltaTime)
        {
            _behaviorTimer += deltaTime;
            if (_behaviorTimer >= _behaviorChangeInterval)
            {
                _isRotating = _random.Next(2) == 0;
                _behaviorTimer = 0.0f;
                _behaviorChangeInterval = 2.0f + (float)(_random.NextDouble() * 3.0f);
            }
            
            if (_isRotating)
            {
                _rotation += _rotationSpeed * deltaTime;
                if (_rotation > Math.PI * 2)
                    _rotation -= (float)(Math.PI * 2);
                if (_rotation < 0)
                    _rotation += (float)(Math.PI * 2);
            }
        }

        private bool CheckDirectPath(Vector2 target, Func<Vector2, bool>? checkCollision)
        {
            return CheckDirectPath(_position, target, checkCollision);
        }
        
        private bool CheckDirectPath(Vector2 from, Vector2 target, Func<Vector2, bool>? checkCollision)
        {
            if (checkCollision == null) return true;
            
            Vector2 direction = target - from;
            float distanceSquared = direction.LengthSquared();
            // Use denser sampling (every 8 pixels) like player to catch more obstacles
            float distance = (float)Math.Sqrt(distanceSquared); // Need actual distance for sampling calculation
            int samples = Math.Max(3, (int)(distance / 8.0f) + 1);
            
            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples;
                // Skip exact start and end to avoid checking current position
                if (t < 0.001f || t > 0.999f)
                    continue;
                    
                Vector2 samplePoint = from + (target - from) * t;
                if (checkCollision(samplePoint))
                {
                    return false;
                }
            }
            return true;
        }

        private void FollowPath(Vector2 finalTarget, float deltaTime, Func<Vector2, bool>? checkCollision, CollisionManager? collisionManager, Func<Vector2, bool>? checkTerrainOnly = null, bool faceFinalTarget = false, bool includeEnemies = true)
        {
            if (_path == null)
                return;
            
            // Only face the final target (player) if alerted/chasing
            if (faceFinalTarget)
            {
                FaceTarget(finalTarget);
            }
            
            while (_path.Count > 0 && Vector2.Distance(_position, _path[0]) < 10.0f)
            {
                _path.RemoveAt(0);
            }
            
            // Proactively check if path ahead is blocked before moving
            if (_path != null && _path.Count > 0 && checkTerrainOnly != null)
            {
                // Check next 1-2 waypoints to see if path is clear
                bool pathAheadBlocked = false;
                int waypointsToCheck = Math.Min(2, _path.Count);
                
                for (int i = 0; i < waypointsToCheck; i++)
                {
                    Vector2 waypointToCheck = _path[i];
                    Vector2 checkFrom = i == 0 ? _position : _path[i - 1];
                    
                    // Check if path to this waypoint is clear
                    Func<Vector2, bool> terrainCheck = checkTerrainOnly;
                    if (!CheckDirectPath(checkFrom, waypointToCheck, terrainCheck))
                    {
                        pathAheadBlocked = true;
                        break;
                    }
                }
                
                // If path ahead is blocked, recalculate proactively before getting stuck
                if (pathAheadBlocked)
                {
                    _path = PathfindingService.FindPath(
                        _position, 
                        finalTarget, 
                        checkTerrainOnly,
                        GameConfig.PathfindingGridCellWidth,
                        GameConfig.PathfindingGridCellHeight
                    );
                    if (_path != null && _path.Count > 0)
                    {
                        var originalPath = PathfindingService.RentPath();
                        originalPath.AddRange(_path);
                        _path = PathfindingService.SimplifyPath(_path, 0.2f); // Reduced threshold to keep more waypoints around corners
                        if (_path == null || _path.Count == 0)
                        {
                            _path = originalPath;
                        }
                        else
                        {
                            PathfindingService.ReturnPath(originalPath);
                        }
                    }
                    // Reset stuck timer since we found an alternative path
                    _stuckTimer = 0.0f;
                }
            }
            
            if (_path != null && _path.Count > 0)
            {
                Vector2 waypoint = _path[0];
                Vector2 direction = waypoint - _position;
                float distanceSquared = direction.LengthSquared();
                const float thresholdSquared = 25.0f; // 5.0f * 5.0f
                
                if (distanceSquared > thresholdSquared)
                {
                    float distance = (float)Math.Sqrt(distanceSquared); // Calculate when needed
                    direction.Normalize();
                    
                    // If not facing final target, face the movement direction (waypoint)
                    if (!faceFinalTarget)
                    {
                        _rotation = (float)Math.Atan2(direction.Y, direction.X);
                    }
                    
                    float moveDistance = _runSpeed * deltaTime;
                    if (moveDistance > distance) moveDistance = distance;
                    
                    Vector2 newPosition = _position + direction * moveDistance;
                    
                    // Use MoveWithCollision if available, otherwise fall back to simple check
                    if (collisionManager != null)
                    {
                        Vector2 finalPos = collisionManager.MoveWithCollision(_position, newPosition, includeEnemies, 3, _position);
                        if (Vector2.DistanceSquared(_position, finalPos) > 0.01f)
                        {
                            _position = finalPos;
                            _stuckTimer = 0.0f;
                        }
                        else
                        {
                            // Stuck - recalculate path using terrain-only
                            if (checkTerrainOnly != null)
                            {
                                _path = PathfindingService.FindPath(
                                    _position, 
                                    finalTarget, 
                                    checkTerrainOnly,
                                    GameConfig.PathfindingGridCellWidth,
                                    GameConfig.PathfindingGridCellHeight
                                );
                                if (_path != null && _path.Count > 0)
                                {
                                    var originalPath = PathfindingService.RentPath();
                                    originalPath.AddRange(_path);
                                    _path = PathfindingService.SimplifyPath(_path, 0.2f); // Reduced threshold to keep more waypoints around corners
                                    if (_path == null || _path.Count == 0)
                                    {
                                        _path = originalPath;
                                    }
                                    else
                                    {
                                        PathfindingService.ReturnPath(originalPath);
                                    }
                                }
                            }
                            _stuckTimer += deltaTime;
                        }
                    }
                    else if (checkCollision == null || !checkCollision(newPosition))
                    {
                        _position = newPosition;
                        _stuckTimer = 0.0f;
                    }
                    else
                    {
                        // Recalculate path using terrain-only
                        if (checkTerrainOnly != null)
                        {
                            _path = PathfindingService.FindPath(
                                _position, 
                                finalTarget, 
                                checkTerrainOnly,
                                GameConfig.PathfindingGridCellWidth,
                                GameConfig.PathfindingGridCellHeight
                            );
                            if (_path != null && _path.Count > 0)
                            {
                                var originalPath = PathfindingService.RentPath();
                                originalPath.AddRange(_path);
                                _path = PathfindingService.SimplifyPath(_path, 0.2f); // Reduced threshold to keep more waypoints around corners
                                if (_path == null || _path.Count == 0)
                                {
                                    _path = originalPath;
                                }
                                else
                                {
                                    PathfindingService.ReturnPath(originalPath);
                                }
                            }
                        }
                        _stuckTimer += deltaTime;
                    }
                }
            }
        }

        private void MoveDirectly(Vector2 target, float distanceToTarget, float deltaTime, Func<Vector2, bool>? checkCollision, CollisionManager? collisionManager, Func<Vector2, bool>? checkTerrainOnly = null, bool faceTarget = true, bool includeEnemies = true)
        {
            Vector2 direction = (target - _position);
            direction.Normalize();
            
            // Face the target while moving (only if faceTarget is true)
            if (faceTarget)
            {
                FaceTarget(target);
            }
            else
            {
                // Face the direction of movement
                _rotation = (float)Math.Atan2(direction.Y, direction.X);
            }
            
            float moveDistance = _runSpeed * deltaTime;
            if (_isAttacking && distanceToTarget > _attackRange)
            {
                if (moveDistance > distanceToTarget - _attackRange)
                {
                    moveDistance = MathHelper.Max(0, distanceToTarget - _attackRange);
                }
            }
            else if (moveDistance > distanceToTarget)
            {
                moveDistance = distanceToTarget;
            }

            if (moveDistance > 0)
            {
                Vector2 newPosition = _position + direction * moveDistance;
                
                // Use MoveWithCollision if available for smooth sliding
                if (collisionManager != null)
                {
                    Vector2 finalPos = collisionManager.MoveWithCollision(_position, newPosition, includeEnemies, 3, _position);
                    if (Vector2.DistanceSquared(_position, finalPos) > 0.01f)
                    {
                        _position = finalPos;
                        _stuckTimer = 0.0f;
                    }
                    else
                    {
                        _stuckTimer += deltaTime;
                        
                        if (_stuckTimer > STUCK_THRESHOLD && checkTerrainOnly != null)
                        {
                            Console.WriteLine("[Enemy] Stuck for too long, recalculating path");
                            _path = PathfindingService.FindPath(
                                _position, 
                                target, 
                                checkTerrainOnly,
                                GameConfig.PathfindingGridCellWidth,
                                GameConfig.PathfindingGridCellHeight
                            );
                            if (_path != null && _path.Count > 0)
                            {
                                var originalPath = PathfindingService.RentPath();
                                originalPath.AddRange(_path);
                                _path = PathfindingService.SimplifyPath(_path, 0.2f); // Reduced threshold to keep more waypoints around corners
                                if (_path == null || _path.Count == 0)
                                {
                                    _path = originalPath;
                                }
                                else
                                {
                                    PathfindingService.ReturnPath(originalPath);
                                }
                            }
                            _stuckTimer = 0.0f;
                        }
                    }
                }
                else if (checkCollision == null || !checkCollision(newPosition))
                {
                    _position = newPosition;
                    _stuckTimer = 0.0f;
                }
                else
                {
                    Vector2 slidePosition = TrySlideAlongCollision(_position, newPosition, direction, moveDistance, checkCollision);
                    if (slidePosition != _position)
                    {
                        _position = slidePosition;
                        _stuckTimer = 0.0f;
                    }
                    else
                    {
                        _stuckTimer += deltaTime;
                        
                        if (_stuckTimer > STUCK_THRESHOLD && checkTerrainOnly != null)
                        {
                            Console.WriteLine("[Enemy] Stuck for too long, recalculating path");
                            _path = PathfindingService.FindPath(
                                _position, 
                                target, 
                                checkTerrainOnly,
                                GameConfig.PathfindingGridCellWidth,
                                GameConfig.PathfindingGridCellHeight
                            );
                            if (_path != null && _path.Count > 0)
                            {
                                var originalPath = PathfindingService.RentPath();
                                originalPath.AddRange(_path);
                                _path = PathfindingService.SimplifyPath(_path, 0.2f); // Reduced threshold to keep more waypoints around corners
                                if (_path == null || _path.Count == 0)
                                {
                                    _path = originalPath;
                                }
                                else
                                {
                                    PathfindingService.ReturnPath(originalPath);
                                }
                            }
                            _stuckTimer = 0.0f;
                        }
                    }
                }
            }
        }

        private Vector2 TrySlideAlongCollision(Vector2 currentPos, Vector2 targetPos, Vector2 direction, float moveDistance, Func<Vector2, bool>? checkCollision)
        {
            if (checkCollision == null) return currentPos;
            
            Vector2 movement = targetPos - currentPos;
            
            if (movement.LengthSquared() < 0.001f)
                return currentPos;
            
            // Isometric-aware slide directions (aligned with diamond collision cells)
            direction.Normalize();
            
            // Find which isometric axis is most aligned with our movement
            int bestAxis = 0;
            float bestDot = float.MinValue;
            for (int i = 0; i < IsometricAxesStatic.Length; i++)
            {
                float dot = Vector2.Dot(direction, IsometricAxesStatic[i]);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestAxis = i;
                }
            }
            
            // Try perpendicular isometric directions (left and right of movement)
            int[] perpAxes = new int[]
            {
                (bestAxis + 2) % 8,  // 90 degrees right
                (bestAxis + 6) % 8   // 90 degrees left (counter-clockwise)
            };
            
            // Try sliding along isometric axes at various scales
            float[] scales = { 1.0f, 0.8f, 0.6f, 0.4f, 0.3f };
            
            foreach (int axisIndex in perpAxes)
            {
                Vector2 slideDir = IsometricAxesStatic[axisIndex];
                
                foreach (float scale in scales)
                {
                    // Pure slide along isometric axis
                    Vector2 testPos = currentPos + slideDir * (moveDistance * scale);
                    if (!checkCollision(testPos))
                    {
                        return testPos;
                    }
                    
                    // Blended slide (original direction + isometric axis)
                    Vector2 blendedDir = (direction * 0.5f + slideDir * 0.5f);
                    blendedDir.Normalize();
                    testPos = currentPos + blendedDir * (moveDistance * scale);
                    if (!checkCollision(testPos))
                    {
                        return testPos;
                    }
                }
            }
            
            return currentPos;
        }

        public void DrawAggroRadius(SpriteBatch spriteBatch, float effectiveRange)
        {
            int radius = (int)effectiveRange;
            if (_circleTexture == null || _circleTexture.Width != radius * 2)
            {
                CreateCircleTexture(spriteBatch.GraphicsDevice, radius);
            }

            Vector2 drawPosition = _position - new Vector2(effectiveRange, effectiveRange);
            spriteBatch.Draw(_circleTexture, drawPosition, Color.White);
        }

        private void CreateCircleTexture(GraphicsDevice graphicsDevice, int radius)
        {
            int diameter = radius * 2;
            _circleTexture = new Texture2D(graphicsDevice, diameter, diameter);
            Color[] colorData = new Color[diameter * diameter];
            
            Vector2 center = new Vector2(radius, radius);
            
            for (int x = 0; x < diameter; x++)
            {
                for (int y = 0; y < diameter; y++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);
                    
                    if (distance >= radius - 2 && distance <= radius)
                    {
                        colorData[y * diameter + x] = new Color(255, 0, 0, 100);
                    }
                    else
                    {
                        colorData[y * diameter + x] = Color.Transparent;
                    }
                }
            }
            
            _circleTexture.SetData(colorData);
        }

        public void DrawSightCone(SpriteBatch spriteBatch)
        {
            if (!_hasDetectedPlayer)
            {
                if (_sightConeTexture == null)
                {
                    CreateSightConeTexture(spriteBatch.GraphicsDevice);
                }
                
                if (_sightConeTexture != null)
                {
                    Vector2 origin = new Vector2(_sightConeTexture.Width / 2.0f, _sightConeTexture.Height / 2.0f);
                    
                    spriteBatch.Draw(
                        _sightConeTexture,
                        _position,
                        null,
                        Color.White,
                        _rotation,
                        origin,
                        1.0f,
                        SpriteEffects.None,
                        0.0f
                    );
                }
            }
        }

        private void CreateSightConeTexture(GraphicsDevice graphicsDevice)
        {
            int size = (int)_sightConeLength * 2;
            _sightConeTexture = new Texture2D(graphicsDevice, size, size);
            Color[] colorData = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2.0f, size / 2.0f);
            float halfAngle = _sightConeAngle / 2.0f;
            
            Vector2 forwardDir = new Vector2(1, 0);
            Vector2 leftEdge = new Vector2(
                (float)Math.Cos(-halfAngle),
                (float)Math.Sin(-halfAngle)
            );
            Vector2 rightEdge = new Vector2(
                (float)Math.Cos(halfAngle),
                (float)Math.Sin(halfAngle)
            );
            
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    Vector2 pos = new Vector2(x, y);
                    Vector2 dir = pos - center;
                    float distanceSquared = dir.LengthSquared();
                    float sightConeLengthSquared = _sightConeLength * _sightConeLength;
                    
                    if (distanceSquared > 0.01f && distanceSquared <= sightConeLengthSquared)
                    {
                        dir.Normalize();
                        
                        float crossLeft = Vector2.Dot(new Vector2(-leftEdge.Y, leftEdge.X), dir);
                        float crossRight = Vector2.Dot(new Vector2(rightEdge.Y, -rightEdge.X), dir);
                        
                        if (crossLeft >= 0 && crossRight >= 0)
                        {
                            float distance = (float)Math.Sqrt(distanceSquared); // Calculate when needed
                            float alpha = 1.0f - (distance / _sightConeLength) * 0.5f;
                            byte alphaByte = (byte)(80 * alpha);
                            colorData[y * size + x] = new Color((byte)255, (byte)255, (byte)0, alphaByte);
                        }
                        else
                        {
                            colorData[y * size + x] = Color.Transparent;
                        }
                    }
                    else
                    {
                        colorData[y * size + x] = Color.Transparent;
                    }
                }
            }
            
            _sightConeTexture.SetData(colorData);
        }

        private void CreateExclamationTexture(GraphicsDevice graphicsDevice)
        {
            int size = 32;
            _exclamationTexture = new Texture2D(graphicsDevice, size, size);
            Color[] colorData = new Color[size * size];
            
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    if (x >= size / 2 - 2 && x < size / 2 + 2)
                    {
                        if (y >= size / 4 && y < size * 3 / 4)
                        {
                            colorData[y * size + x] = Color.Yellow;
                        }
                        else if (y >= size * 3 / 4 && y < size * 7 / 8)
                        {
                            colorData[y * size + x] = Color.Yellow;
                        }
                        else
                        {
                            colorData[y * size + x] = Color.Transparent;
                        }
                    }
                    else
                    {
                        colorData[y * size + x] = Color.Transparent;
                    }
                }
            }
            
            _exclamationTexture.SetData(colorData);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (_diamondTexture == null)
            {
                CreateDiamondTexture(spriteBatch.GraphicsDevice);
            }

            bool visible = true;
            if (_isFlashing)
            {
                int flashCycle = (int)(_flashTime / _flashInterval);
                visible = (flashCycle % 2 == 0);
            }

            if (visible && _diamondTexture != null)
            {
                Color drawColor;
                
                // Show different color when stunned (knockback effect)
                if (IsStunned)
                {
                    drawColor = Color.Cyan; // Cyan color when stunned/knockback
                }
                else if (_isAttacking)
                {
                    drawColor = Color.OrangeRed;
                }
                else
                {
                    drawColor = _color;
                }
                
                Vector2 drawPosition = _position - new Vector2(64, 32); // Offset for 128x64 diamond
                
                // Apply pulsing effect for dead enemies
                if (_isDead)
                {
                    float pulse = (float)(Math.Sin(_deathPulseTimer * Math.PI * 2) * 0.3f + 0.7f); // Pulse between 0.7 and 1.0
                    drawColor = new Color(
                        (byte)(drawColor.R * pulse),
                        (byte)(drawColor.G * pulse),
                        (byte)(drawColor.B * pulse),
                        (byte)(drawColor.A * pulse)
                    );
                }
                
                spriteBatch.Draw(_diamondTexture, drawPosition, drawColor);
            }
            
            if (_hasDetectedPlayer && !_isDead)
            {
                if (_exclamationTexture == null)
                {
                    CreateExclamationTexture(spriteBatch.GraphicsDevice);
                }
                
                if (_exclamationTexture != null)
                {
                    Vector2 exclamationPos = _position - new Vector2(_exclamationTexture.Width / 2.0f, _size + 10);
                    spriteBatch.Draw(_exclamationTexture, exclamationPos, Color.Yellow);
                }
            }
        }
    }
}
