using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Project9
{
    public class Player : Entity
    {
        // Player-specific fields
        private float _sneakSpeed;
        private float _distanceThreshold;
        private Color _sneakColor;
        private bool _isSneaking;

        public bool IsSneaking => _isSneaking;

        public float WalkSpeed
        {
            get => _walkSpeed;
            set => _walkSpeed = value;
        }

        public float RunSpeed
        {
            get => _runSpeed;
            set => _runSpeed = value;
        }

        public Player(Vector2 startPosition) 
            : base(startPosition, Color.Red, GameConfig.PlayerWalkSpeed, GameConfig.PlayerRunSpeed)
        {
            _sneakSpeed = _walkSpeed * GameConfig.PlayerSneakSpeedMultiplier;
            _distanceThreshold = 100.0f;
            _sneakColor = Color.Purple;
            _isSneaking = false;
        }

        public void ToggleSneak()
        {
            _isSneaking = !_isSneaking;
            _color = _isSneaking ? _sneakColor : _normalColor;
        }

        public void SetTarget(Vector2 target, Func<Vector2, bool>? checkCollision = null, Func<Vector2, bool>? checkTerrainOnly = null)
        {
            // Check if target location itself is blocked by TERRAIN collision only
            if (checkTerrainOnly != null && checkTerrainOnly(target))
            {
                Console.WriteLine("[Player] Target location is blocked by terrain - click ignored");
                return;
            }
            else if (checkCollision != null && checkTerrainOnly == null && checkCollision(target))
            {
                Console.WriteLine("[Player] Target location is blocked by collision - click ignored");
                return;
            }
            
            _targetPosition = target;
            _waypoint = null;
            _path?.Clear();
            _stuckTimer = 0.0f;
            
            // Check if direct path is clear
            if (checkCollision != null)
            {
                Vector2 direction = target - _position;
                float distance = direction.Length();
                
                bool pathClear = true;
                int samples = (int)(distance / 16.0f) + 1;
                for (int i = 0; i <= samples; i++)
                {
                    float t = (float)i / samples;
                    Vector2 samplePoint = _position + (target - _position) * t;
                    if (checkCollision(samplePoint))
                    {
                        pathClear = false;
                        break;
                    }
                }
                
                if (!pathClear)
                {
                    _path = PathfindingService.FindPath(
                        _position, 
                        target, 
                        checkCollision,
                        GameConfig.PathfindingGridCellWidth,
                        GameConfig.PathfindingGridCellHeight
                    );
                    
                    if (_path == null || _path.Count == 0)
                    {
                        Console.WriteLine("[Player] Pathfinding failed - no path found to target");
                    }
                    else
                    {
                        // Smooth the path to remove unnecessary waypoints
                        _path = PathfindingService.SimplifyPath(_path);
                    }
                }
            }
        }

        public override void Update(float deltaTime)
        {
            Update(null, deltaTime, null, null);
        }

        public void Update(Vector2? followPosition, float deltaTime, Func<Vector2, bool>? checkCollision = null, Func<Vector2, Vector2, bool>? checkLineOfSight = null, CollisionManager? collisionManager = null)
        {
            UpdateFlashing(deltaTime);

            Vector2? moveTarget = null;

            if (followPosition.HasValue)
            {
                float deadZone = _isSneaking ? 8.0f : 2.0f;
                Vector2 direction = followPosition.Value - _position;
                float distance = direction.Length();
                
                if (distance > deadZone)
                {
                    moveTarget = followPosition.Value;
                }
                else if (_targetPosition.HasValue)
                {
                    moveTarget = _targetPosition.Value;
                }
            }
            else if (_targetPosition.HasValue)
            {
                if (_path != null && _path.Count > 0)
                {
                    while (_path.Count > 0)
                    {
                        float distToNext = Vector2.Distance(_position, _path[0]);
                        if (distToNext < 10.0f)
                        {
                            _path.RemoveAt(0);
                        }
                        else
                        {
                            break;
                        }
                    }
                    
                    if (_path.Count > 0)
                    {
                        moveTarget = _path[0];
                    }
                    else
                    {
                        moveTarget = _targetPosition.Value;
                    }
                }
                else
                {
                    moveTarget = _waypoint ?? _targetPosition.Value;
                }
            }

            if (moveTarget.HasValue)
            {
                Vector2 direction = moveTarget.Value - _position;
                float distance = direction.Length();
                
                float distanceToFinalTarget = _targetPosition.HasValue 
                    ? Vector2.Distance(_position, _targetPosition.Value) 
                    : distance;

                bool isFinalTarget = !followPosition.HasValue && 
                                    _targetPosition.HasValue && 
                                    (moveTarget == _targetPosition.Value || (_path != null && _path.Count == 0));

                // Determine speed with smooth deceleration
                float baseSpeed = _isSneaking ? _sneakSpeed : _runSpeed;
                
                if (isFinalTarget && distanceToFinalTarget < GameConfig.PlayerSlowdownRadius)
                {
                    float slowdownFactor = distanceToFinalTarget / GameConfig.PlayerSlowdownRadius;
                    slowdownFactor = MathHelper.Max(slowdownFactor, 0.2f);
                    _currentSpeed = baseSpeed * slowdownFactor;
                }
                else
                {
                    _currentSpeed = baseSpeed;
                }

                float stopThreshold = isFinalTarget ? GameConfig.PlayerStopThreshold : (_isSneaking ? 10.0f : 5.0f);
                
                if (distance <= stopThreshold && isFinalTarget && _targetPosition.HasValue)
                {
                    _position = _targetPosition.Value;
                    _targetPosition = null;
                    _waypoint = null;
                    _path?.Clear();
                    _currentSpeed = 0.0f;
                }
                else if (distance > stopThreshold)
                {
                    direction.Normalize();
                    float moveDistance = _currentSpeed * deltaTime;
                    
                    if (moveDistance > distance)
                    {
                        moveDistance = distance;
                    }

                    Vector2 nextPosition = _position + direction * moveDistance;
                    
                    // Use CollisionManager's advanced collision resolution if available
                    if (collisionManager != null)
                    {
                        // Move with swept collision and sliding
                        Vector2 newPos = collisionManager.MoveWithCollision(_position, nextPosition, true);
                        
                        if (Vector2.DistanceSquared(_position, newPos) > 0.01f)
                        {
                            _position = newPos;
                            _stuckTimer = 0.0f;
                        }
                        else
                        {
                            _stuckTimer += deltaTime;
                            
                            // If stuck and have target, try pathfinding
                            if (_stuckTimer > STUCK_THRESHOLD && _targetPosition.HasValue && checkCollision != null)
                            {
                                if (_path == null || _path.Count == 0)
                                {
                                    _path = PathfindingService.FindPath(
                                        _position, _targetPosition.Value, checkCollision,
                                        GameConfig.PathfindingGridCellWidth, GameConfig.PathfindingGridCellHeight
                                    );
                                    if (_path != null && _path.Count > 0)
                                    {
                                        _path = PathfindingService.SimplifyPath(_path);
                                    }
                                    _stuckTimer = 0.0f;
                                }
                            }
                        }
                    }
                    // Fallback: Handle pathfinding and collision (old method)
                    else if (_path != null && _path.Count > 0)
                    {
                        if (_targetPosition.HasValue && checkCollision != null)
                        {
                            Vector2 directDirection = _targetPosition.Value - _position;
                            float directDistance = directDirection.Length();
                            
                            if (directDistance < GameConfig.PathfindingGridCellWidth * 3)
                            {
                                bool directPathClear = true;
                                int samples = (int)(directDistance / 16.0f) + 1;
                                for (int i = 0; i <= samples; i++)
                                {
                                    float t = (float)i / samples;
                                    Vector2 samplePoint = _position + directDirection * t;
                                    if (checkCollision(samplePoint))
                                    {
                                        directPathClear = false;
                                        break;
                                    }
                                }
                                
                                if (directPathClear)
                                {
                                    _path.Clear();
                                }
                            }
                        }
                        
                        if (_path != null && _path.Count > 0)
                        {
                            bool hasCollision = checkCollision != null && checkCollision(nextPosition);
                            
                            if (!hasCollision)
                            {
                                _position = nextPosition;
                                _stuckTimer = 0.0f;
                            }
                            else
                            {
                                Vector2 slidePosition = TrySlideAlongCollision(_position, nextPosition, direction, moveDistance, checkCollision);
                                if (slidePosition != _position)
                                {
                                    _position = slidePosition;
                                    _stuckTimer = 0.0f;
                                }
                                else
                                {
                                if (checkCollision != null && _targetPosition.HasValue)
                                {
                                    _path = PathfindingService.FindPath(
                                        _position, 
                                        _targetPosition.Value, 
                                        checkCollision,
                                        GameConfig.PathfindingGridCellWidth,
                                        GameConfig.PathfindingGridCellHeight
                                    );
                                    if (_path != null && _path.Count > 0)
                                    {
                                        _path = PathfindingService.SimplifyPath(_path);
                                    }
                                }
                                    _stuckTimer += deltaTime;
                                }
                            }
                            return;
                        }
                    }
                    
                    bool hasCollision2 = checkCollision != null && checkCollision(nextPosition);
                    
                    if (hasCollision2)
                    {
                        if (_targetPosition.HasValue && !followPosition.HasValue && checkCollision != null)
                        {
                            if (_path == null || _path.Count == 0)
                            {
                                _path = PathfindingService.FindPath(
                                    _position, 
                                    _targetPosition.Value, 
                                    checkCollision,
                                    GameConfig.PathfindingGridCellWidth,
                                    GameConfig.PathfindingGridCellHeight
                                );
                                if (_path != null && _path.Count > 0)
                                {
                                    _path = PathfindingService.SimplifyPath(_path);
                                }
                            }
                            
                            if (_path != null && _path.Count > 0)
                            {
                                _stuckTimer = 0.0f;
                                return;
                            }
                        }
                        
                        Vector2 slidePosition = TrySlideAlongCollision(_position, nextPosition, direction, moveDistance, checkCollision);
                        if (slidePosition != _position)
                        {
                            _position = slidePosition;
                            _stuckTimer = 0.0f;
                        }
                        else
                        {
                            _stuckTimer += deltaTime;
                            if (_stuckTimer > STUCK_THRESHOLD && !followPosition.HasValue)
                            {
                                ClearTarget();
                            }
                        }
                    }
                    else
                    {
                        _position = nextPosition;
                        _stuckTimer = 0.0f;
                    }
                }
                else
                {
                    if (_waypoint.HasValue && moveTarget == _waypoint.Value)
                    {
                        _waypoint = null;
                        _stuckTimer = 0.0f;
                    }
                }
            }
            else
            {
                _currentSpeed = 0.0f;
            }
        }

        private Vector2 TrySlideAlongCollision(Vector2 currentPos, Vector2 targetPos, Vector2 direction, float moveDistance, Func<Vector2, bool>? checkCollision)
        {
            if (checkCollision == null) return currentPos;
            
            Vector2 movement = targetPos - currentPos;
            
            if (movement.LengthSquared() < 0.001f)
                return currentPos;
            
            // Isometric-aware slide directions (aligned with diamond collision cells)
            // These match the 2:1 aspect ratio of isometric tiles
            Vector2[] isometricAxes = new Vector2[]
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
            
            direction.Normalize();
            
            // Find which isometric axis is most aligned with our movement
            int bestAxis = 0;
            float bestDot = float.MinValue;
            for (int i = 0; i < isometricAxes.Length; i++)
            {
                float dot = Vector2.Dot(direction, isometricAxes[i]);
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
                Vector2 slideDir = isometricAxes[axisIndex];
                
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
                Vector2 drawPosition = _position - new Vector2(32, 16);
                Color drawColor = _isSneaking ? _sneakColor : _normalColor;
                spriteBatch.Draw(_diamondTexture, drawPosition, drawColor);
            }
        }
    }
}
