using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Project9
{
    /// <summary>
    /// Security camera that detects player and alerts nearby enemies
    /// Note: This is SecurityCamera, not to be confused with ViewportCamera
    /// </summary>
    public class SecurityCamera : Enemy
    {
        internal Project9.Shared.CameraData? _cameraData;
        
        // Camera-specific fields (inherits detection range, sight cone angle, etc. from Enemy)
        private float _sightConeLength;
        private float _cameraRotation; // Camera's own rotation (different from Enemy's rotation)
        private float _rotationSpeed;
        private bool _cameraHasDetectedPlayer; // Camera's own detection state
        private float _alertRadius; // Radius to alert enemies
        private float _lastAlertTime;
        private float _alertCooldown;
        private Random _random;
        
        // Back-and-forth rotation state
        private float _baseRotation; // Starting rotation angle
        private float _targetRotation; // Target rotation angle
        private bool _rotatingRight; // Direction of rotation
        private float _pauseTimer; // Timer for pause at endpoints
        private float _sweepAngle; // Sweep angle in radians
        private float _pauseDuration; // Pause duration at endpoints
        
        // Rendering textures
        private Texture2D? _sightConeTexture;
        
        public new float Rotation => _cameraRotation; // Hide Enemy's Rotation
        public new bool HasDetectedPlayer => _cameraHasDetectedPlayer; // Hide Enemy's HasDetectedPlayer
        public float AlertRadius => _alertRadius;
        
        /// <summary>
        /// Check if camera is currently detecting the player (in sight cone with line of sight)
        /// </summary>
        public bool IsCurrentlyDetecting(Vector2 playerPosition, bool playerIsSneaking, Func<Vector2, Vector2, bool>? checkLineOfSight)
        {
            Vector2 directionToPlayer = playerPosition - _position;
            float distanceToPlayer = directionToPlayer.Length();
            
            float effectiveDetectionRange = playerIsSneaking ? DetectionRange * GameConfig.EnemySneakDetectionMultiplier : DetectionRange;
            bool playerInRange = distanceToPlayer <= effectiveDetectionRange;
            
            if (!playerInRange)
                return false;
            
            bool lineOfSightBlocked = checkLineOfSight != null && checkLineOfSight(_position, playerPosition);
            bool inSightCone = IsPointInSightCone(playerPosition);
            
            return inSightCone && !lineOfSightBlocked;
        }

        public SecurityCamera(Project9.Shared.CameraData cameraData) 
            : base(new Vector2(cameraData.X, cameraData.Y), cameraData)
        {
            _cameraData = cameraData;
            // Override color to blue for cameras
            _normalColor = Color.Blue;
            _color = Color.Blue;
            
            // Set camera health to 50
            _maxHealth = 50.0f;
            _currentHealth = 50.0f;
            
            // Initialize camera-specific properties from CameraData
            float sweepAngleRad = MathHelper.ToRadians(cameraData.SweepAngle);
            float rotationSpeedRad = MathHelper.ToRadians(cameraData.CameraRotationSpeed);
            
            // Normalize base rotation to 0-2PI
            _baseRotation = cameraData.Rotation;
            while (_baseRotation >= Math.PI * 2) _baseRotation -= (float)(Math.PI * 2);
            while (_baseRotation < 0) _baseRotation += (float)(Math.PI * 2);
            
            _cameraRotation = _baseRotation;
            _sweepAngle = sweepAngleRad;
            _pauseDuration = cameraData.PauseDuration;
            _targetRotation = _baseRotation + _sweepAngle;
            // Normalize target rotation
            while (_targetRotation >= Math.PI * 2) _targetRotation -= (float)(Math.PI * 2);
            while (_targetRotation < 0) _targetRotation += (float)(Math.PI * 2);
            
            _rotatingRight = true;
            _pauseTimer = 0.0f;
            _rotationSpeed = rotationSpeedRad;
            _random = new Random();
            _cameraHasDetectedPlayer = false;
            _alertRadius = cameraData.AlertRadius;
            _lastAlertTime = -10.0f; // Start with negative value so first detection can trigger
            _alertCooldown = cameraData.AlertCooldown;
            
            // Set sight cone length
            if (cameraData.CameraSightConeLength > 0)
            {
                _sightConeLength = cameraData.CameraSightConeLength;
            }
            else
            {
                _sightConeLength = DetectionRange; // Use inherited detection range from Enemy
            }
        }
        
        // Legacy constructor for backward compatibility
        public SecurityCamera(Vector2 position, float rotation = 0.0f, float detectionRange = 300.0f, float sightConeAngleDegrees = 60.0f) 
            : this(new Project9.Shared.CameraData
            {
                X = position.X,
                Y = position.Y,
                Rotation = rotation,
                DetectionRange = detectionRange,
                SightConeAngle = sightConeAngleDegrees
            })
        {
        }
        
        /// <summary>
        /// Initialize all textures for this camera (call during LoadContent, not Draw)
        /// </summary>
        public override void InitializeTextures(GraphicsDevice graphicsDevice)
        {
            base.InitializeTextures(graphicsDevice);
            // Sight cone texture is created on-demand when needed
        }
        
        private bool IsPointInSightCone(Vector2 point)
        {
            Vector2 directionToPoint = point - _position;
            float distance = directionToPoint.Length();
            
            if (distance > _sightConeLength || distance == 0)
                return false;
            
            directionToPoint.Normalize();
            
            float pointAngle = (float)Math.Atan2(directionToPoint.Y, directionToPoint.X);
            
            float cameraAngle = _cameraRotation;
            while (cameraAngle < 0) cameraAngle += (float)(Math.PI * 2);
            while (cameraAngle >= Math.PI * 2) cameraAngle -= (float)(Math.PI * 2);
            while (pointAngle < 0) pointAngle += (float)(Math.PI * 2);
            while (pointAngle >= Math.PI * 2) pointAngle -= (float)(Math.PI * 2);
            
            float angleDiff = Math.Abs(pointAngle - cameraAngle);
            if (angleDiff > Math.PI)
                angleDiff = (float)(Math.PI * 2 - angleDiff);
            
            // Use inherited sight cone angle from Enemy base class
            float sightConeAngle = MathHelper.ToRadians(_cameraData?.SightConeAngle ?? 60.0f);
            return angleDiff <= sightConeAngle / 2.0f;
        }

        public override void Update(float deltaTime)
        {
            UpdateFlashing(deltaTime);
            
            // Back-and-forth rotation with pause
            if (_pauseTimer > 0.0f)
            {
                // Pausing at endpoint
                _pauseTimer -= deltaTime;
            }
            else
            {
                // Rotating towards target
                float rotationDirection = _rotatingRight ? 1.0f : -1.0f;
                float rotationDelta = _rotationSpeed * rotationDirection * deltaTime;
                float newRotation = _cameraRotation + rotationDelta;
                
                // Calculate shortest angular distance to target
                float angleToTarget = _targetRotation - _cameraRotation;
                
                // Normalize angle difference to -PI to PI range
                while (angleToTarget > Math.PI) angleToTarget -= (float)(Math.PI * 2);
                while (angleToTarget < -Math.PI) angleToTarget += (float)(Math.PI * 2);
                
                // Check if we've reached or passed the target
                bool reachedTarget = false;
                if (_rotatingRight)
                {
                    // Rotating right (positive direction) - check if angle to target is <= 0
                    reachedTarget = angleToTarget <= 0;
                }
                else
                {
                    // Rotating left (negative direction) - check if angle to target is >= 0
                    reachedTarget = angleToTarget >= 0;
                }
                
                if (reachedTarget)
                {
                    // Reached endpoint - snap to target and pause
                    _cameraRotation = _targetRotation;
                    _pauseTimer = _pauseDuration;
                    
                    // Reverse direction and set new target
                    _rotatingRight = !_rotatingRight;
                    if (_rotatingRight)
                    {
                        _targetRotation = _baseRotation + _sweepAngle;
                    }
                    else
                    {
                        _targetRotation = _baseRotation;
                    }
                    // Normalize target rotation
                    while (_targetRotation >= Math.PI * 2) _targetRotation -= (float)(Math.PI * 2);
                    while (_targetRotation < 0) _targetRotation += (float)(Math.PI * 2);
                }
                else
                {
                    _cameraRotation = newRotation;
                }
            }
            
            // Normalize rotation to 0-2PI range
            while (_cameraRotation >= Math.PI * 2) _cameraRotation -= (float)(Math.PI * 2);
            while (_cameraRotation < 0) _cameraRotation += (float)(Math.PI * 2);
        }

        /// <summary>
        /// Update camera detection and alert enemies if player is detected
        /// </summary>
        public bool UpdateDetection(Vector2 playerPosition, float deltaTime, bool playerIsSneaking, 
            Func<Vector2, Vector2, bool>? checkLineOfSight, System.Collections.Generic.List<Enemy> enemies)
        {
            // Don't detect if camera is destroyed
            if (!IsAlive)
            {
                _cameraHasDetectedPlayer = false;
                return false;
            }
            
            _lastAlertTime += deltaTime;
            
            Vector2 directionToPlayer = playerPosition - _position;
            float distanceToPlayer = directionToPlayer.Length();
            
            float effectiveDetectionRange = playerIsSneaking ? DetectionRange * GameConfig.EnemySneakDetectionMultiplier : DetectionRange;
            bool playerInRange = distanceToPlayer <= effectiveDetectionRange;
            
            bool detectedThisFrame = false;
            bool isFirstDetection = false;
            
            if (playerInRange)
            {
                bool lineOfSightBlocked = checkLineOfSight != null && checkLineOfSight(_position, playerPosition);
                bool inSightCone = IsPointInSightCone(playerPosition);
                
                if (inSightCone && !lineOfSightBlocked)
                {
                    if (!_cameraHasDetectedPlayer)
                    {
                        // First detection - always alert immediately
                        detectedThisFrame = true;
                        isFirstDetection = true;
                        _cameraHasDetectedPlayer = true;
                    }
                    else if (_lastAlertTime >= _alertCooldown)
                    {
                        // Re-detection after cooldown
                        detectedThisFrame = true;
                    }
                }
                else
                {
                    // Player not in sight cone or line of sight blocked
                    // Keep detection state for a bit before losing it
                    if (_cameraHasDetectedPlayer && distanceToPlayer > effectiveDetectionRange * 1.5f)
                    {
                        _cameraHasDetectedPlayer = false;
                    }
                }
            }
            else
            {
                // Player out of range - lose detection after a delay
                if (_cameraHasDetectedPlayer && distanceToPlayer > effectiveDetectionRange * 1.5f)
                {
                    _cameraHasDetectedPlayer = false;
                }
            }
            
            // Alert nearby enemies if detected
            // First detection always alerts immediately, subsequent detections respect cooldown
            if (detectedThisFrame && (isFirstDetection || _lastAlertTime >= _alertCooldown))
            {
                Console.WriteLine($"[Camera] Detected player! Calling AlertNearbyEnemies. First detection: {isFirstDetection}");
                AlertNearbyEnemies(enemies, playerPosition);
                _lastAlertTime = 0.0f;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Alert all enemies within alert radius to attack the player
        /// </summary>
        private void AlertNearbyEnemies(System.Collections.Generic.List<Enemy> enemies, Vector2 playerPosition)
        {
            int alertedCount = 0;
            int totalEnemies = enemies.Count;
            Console.WriteLine($"[Camera] Checking {totalEnemies} enemies for alert, camera at ({_position.X:F1}, {_position.Y:F1}), alert radius: {_alertRadius:F1}");
            
            foreach (var enemy in enemies)
            {
                float distanceToEnemy = Vector2.Distance(_position, enemy.Position);
                Console.WriteLine($"[Camera] Enemy at ({enemy.Position.X:F1}, {enemy.Position.Y:F1}), distance: {distanceToEnemy:F1}");
                
                if (distanceToEnemy <= _alertRadius)
                {
                    // Force enemy to detect player
                    Console.WriteLine($"[Camera] Alerting enemy at distance {distanceToEnemy:F1}");
                    enemy.ForceDetectPlayer(playerPosition);
                    alertedCount++;
                }
            }
            
            if (alertedCount > 0)
            {
                Console.WriteLine($"[Camera] SUCCESS: Alerted {alertedCount} enemies within {_alertRadius:F0}px radius");
                LogOverlay.Log($"[Camera] ALERT! {alertedCount} enemies alerted", LogLevel.Warning);
            }
            else
            {
                Console.WriteLine($"[Camera] No enemies within alert radius");
            }
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
                Vector2 drawPosition = _position - new Vector2(_diamondWidth / 2, _diamondHeight / 2);
                // Use blue color for camera, red tint if detected player
                Color drawColor = _cameraHasDetectedPlayer ? Color.Red : Color.Blue;
                spriteBatch.Draw(_diamondTexture, drawPosition, drawColor);
            }
        }
        
        public new void DrawSightCone(SpriteBatch spriteBatch)
        {
            // Always draw sight cone (unlike enemies which hide it after detection)
            if (_sightConeTexture == null)
            {
                CreateSightConeTexture(spriteBatch.GraphicsDevice);
            }
            
            if (_sightConeTexture != null)
            {
                Vector2 origin = new Vector2(_sightConeTexture.Width / 2.0f, _sightConeTexture.Height / 2.0f);
                // Red tint if detected, yellow if not
                Color coneColor = _cameraHasDetectedPlayer ? Color.OrangeRed : Color.Yellow;
                
                spriteBatch.Draw(
                    _sightConeTexture,
                    _position,
                    null,
                    coneColor,
                    _cameraRotation,
                    origin,
                    1.0f,
                    SpriteEffects.None,
                    0.0f
                );
            }
        }
        
        private void CreateSightConeTexture(GraphicsDevice graphicsDevice)
        {
            int size = (int)_sightConeLength * 2;
            _sightConeTexture = new Texture2D(graphicsDevice, size, size);
            Color[] colorData = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2.0f, size / 2.0f);
            // Use inherited sight cone angle from Enemy base class
            float sightConeAngle = MathHelper.ToRadians(_cameraData?.SightConeAngle ?? 60.0f);
            float halfAngle = sightConeAngle / 2.0f;
            
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
                    float distance = dir.Length();
                    
                    if (distance > 0 && distance <= _sightConeLength)
                    {
                        dir.Normalize();
                        
                        float crossLeft = Vector2.Dot(new Vector2(-leftEdge.Y, leftEdge.X), dir);
                        float crossRight = Vector2.Dot(new Vector2(rightEdge.Y, -rightEdge.X), dir);
                        
                        if (crossLeft >= 0 && crossRight >= 0)
                        {
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
        
    }
}
