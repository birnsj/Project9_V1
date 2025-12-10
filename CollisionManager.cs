using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Project9.Shared;

namespace Project9
{
    /// <summary>
    /// Manages all collision detection including spatial hash grid and entity collision
    /// </summary>
    public class CollisionManager
    {
        private List<CollisionCellData> _collisionCells = new List<CollisionCellData>();
        private Dictionary<(int, int), List<CollisionCellData>> _collisionGrid = new Dictionary<(int, int), List<CollisionCellData>>();
        private List<Enemy> _enemies; // Reference to enemies for collision checking
        
        // Collision cache for static terrain (positions are rounded to grid cells)
        private Dictionary<(int, int), bool> _staticCollisionCache = new Dictionary<(int, int), bool>();
        private const float CACHE_GRID_SIZE = 16.0f; // Cache granularity
        
        // Performance tracking
        private float _lastCollisionCheckTimeMs = 0.0f;
        private int _cacheHits = 0;
        private int _cacheMisses = 0;

        public CollisionManager(List<Enemy> enemies)
        {
            _enemies = enemies;
        }
        
        public float LastCollisionCheckTimeMs => _lastCollisionCheckTimeMs;
        public int CacheHitRate => _cacheMisses > 0 ? (_cacheHits * 100) / (_cacheHits + _cacheMisses) : 0;

        /// <summary>
        /// Load collision cells from JSON file
        /// </summary>
        public void LoadCollisionCells()
        {
            const string collisionPath = "Content/world/collision.json";
            string? resolvedPath = ResolveCollisionPath(collisionPath);
            
            if (resolvedPath != null && File.Exists(resolvedPath))
            {
                try
                {
                    string json = File.ReadAllText(resolvedPath);
                    var collisionData = System.Text.Json.JsonSerializer.Deserialize<CollisionData>(json);
                    if (collisionData?.Cells != null)
                    {
                        _collisionCells = collisionData.Cells;
                        Console.WriteLine($"[CollisionManager] Loaded {_collisionCells.Count} collision cells");
                        
                        // Build spatial hash grid for fast collision lookups
                        BuildCollisionGrid();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CollisionManager] Error loading collision cells: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Build spatial hash grid for fast collision lookups
        /// </summary>
        private void BuildCollisionGrid()
        {
            _collisionGrid.Clear();
            
            foreach (var cell in _collisionCells)
            {
                // Calculate grid coordinates for this collision cell
                int gridX = (int)(cell.X / GameConfig.CollisionGridSize);
                int gridY = (int)(cell.Y / GameConfig.CollisionGridSize);
                
                var key = (gridX, gridY);
                if (!_collisionGrid.ContainsKey(key))
                {
                    _collisionGrid[key] = new List<CollisionCellData>();
                }
                _collisionGrid[key].Add(cell);
            }
            
            Console.WriteLine($"[CollisionManager] Built collision grid with {_collisionGrid.Count} regions");
        }

        /// <summary>
        /// Check collision at position (includes enemies by default)
        /// </summary>
        public bool CheckCollision(Vector2 position)
        {
            return CheckCollision(position, true);
        }

        /// <summary>
        /// Check collision at position with option to include/exclude enemies
        /// Uses sphere (circle) collision for all moving entities
        /// </summary>
        public bool CheckCollision(Vector2 position, bool includeEnemies)
        {
            // Entity collision radius with buffer to keep away from walls
            float effectiveRadius = GameConfig.EntityCollisionRadius + GameConfig.CollisionBuffer;
            
            // Check collision with enemies if requested (sphere-sphere collision)
            if (includeEnemies)
            {
                foreach (var enemy in _enemies)
                {
                    // Sphere-sphere collision: distance < sum of radii
                    float distanceBetweenCenters = Vector2.Distance(position, enemy.Position);
                    float combinedRadius = effectiveRadius * 2; // Both have same radius
                    
                    if (distanceBetweenCenters < combinedRadius)
                    {
                        return true; // Sphere collision detected
                    }
                }
            }
            
            // Check static terrain collision with cache
            return CheckTerrainCollision(position, effectiveRadius);
        }
        
        /// <summary>
        /// Check terrain collision with caching for static positions
        /// </summary>
        private bool CheckTerrainCollision(Vector2 position, float radius)
        {
            // Round position to cache grid for lookup
            int cacheX = (int)Math.Floor(position.X / CACHE_GRID_SIZE);
            int cacheY = (int)Math.Floor(position.Y / CACHE_GRID_SIZE);
            var cacheKey = (cacheX, cacheY);
            
            // Check cache first
            if (_staticCollisionCache.TryGetValue(cacheKey, out bool cachedResult))
            {
                _cacheHits++;
                return cachedResult;
            }
            
            _cacheMisses++;
            
            // Cache miss - perform full collision check
            bool hasCollision = CheckTerrainCollisionInternal(position, radius);
            
            // Cache the result
            _staticCollisionCache[cacheKey] = hasCollision;
            
            return hasCollision;
        }
        
        /// <summary>
        /// Internal terrain collision check (circle vs diamond cells)
        /// </summary>
        private bool CheckTerrainCollisionInternal(Vector2 position, float radius)
        {
            // Use spatial hash grid for fast lookup
            int gridX = (int)(position.X / GameConfig.CollisionGridSize);
            int gridY = (int)(position.Y / GameConfig.CollisionGridSize);
            
            // Check current grid cell and neighboring cells
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    var key = (gridX + dx, gridY + dy);
                    if (_collisionGrid.TryGetValue(key, out var cellsInRegion))
                    {
                        foreach (var cell in cellsInRegion)
                        {
                            // Circle vs diamond collision
                            float cellDx = Math.Abs(position.X - cell.X);
                            float cellDy = Math.Abs(position.Y - cell.Y);
                            
                            // Quick circle-circle check first (fast rejection)
                            float distanceSquared = cellDx * cellDx + cellDy * cellDy;
                            float maxDistSquared = (radius + GameConfig.CollisionCellHalfWidth) * 
                                                  (radius + GameConfig.CollisionCellHalfWidth);
                            
                            if (distanceSquared > maxDistSquared)
                                continue; // Too far away
                            
                            // Diamond shape check (Manhattan distance) - reduced tolerance for precision
                            float normalizedX = cellDx / GameConfig.CollisionCellHalfWidth;
                            float normalizedY = cellDy / GameConfig.CollisionCellHalfHeight;
                            
                            if (normalizedX + normalizedY <= 1.02f) // Minimal tolerance for smooth movement
                            {
                                return true; // Collision detected
                            }
                        }
                    }
                }
            }
            
            return false; // No collision
        }
        
        /// <summary>
        /// Get closest collision normal at position (for sliding mechanics)
        /// Returns the direction to push the entity out of collision
        /// </summary>
        public Vector2 GetCollisionNormal(Vector2 position, float radius)
        {
            int gridX = (int)(position.X / GameConfig.CollisionGridSize);
            int gridY = (int)(position.Y / GameConfig.CollisionGridSize);
            
            Vector2 closestNormal = Vector2.Zero;
            float closestDistance = float.MaxValue;
            
            // Check current grid cell and neighboring cells
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    var key = (gridX + dx, gridY + dy);
                    if (_collisionGrid.TryGetValue(key, out var cellsInRegion))
                    {
                        foreach (var cell in cellsInRegion)
                        {
                            Vector2 cellPos = new Vector2(cell.X, cell.Y);
                            Vector2 toCell = cellPos - position;
                            float distance = toCell.Length();
                            
                            if (distance < closestDistance && distance > 0.01f)
                            {
                                closestDistance = distance;
                                // Normal points away from collision
                                closestNormal = -toCell / distance;
                            }
                        }
                    }
                }
            }
            
            // If we found a collision normal, return it
            if (closestNormal.LengthSquared() > 0.01f)
            {
                closestNormal.Normalize();
                return closestNormal;
            }
            
            return Vector2.Zero;
        }
        
        /// <summary>
        /// Perform swept collision detection and return the safe position
        /// This prevents tunneling through thin walls
        /// </summary>
        public Vector2 SweptCollisionCheck(Vector2 fromPos, Vector2 toPos, bool includeEnemies = true)
        {
            Vector2 direction = toPos - fromPos;
            float distance = direction.Length();
            
            if (distance < 0.1f)
                return toPos;
            
            direction.Normalize();
            
            // Check along the path with adaptive step size
            float stepSize = 4.0f; // Smaller step = more accurate but slower
            int steps = (int)(distance / stepSize) + 1;
            
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector2 testPos = fromPos + direction * (distance * t);
                
                if (CheckCollision(testPos, includeEnemies))
                {
                    // Found collision - binary search for exact collision point
                    float minT = (float)(i - 1) / steps;
                    float maxT = t;
                    
                    for (int j = 0; j < 4; j++) // 4 iterations of binary search
                    {
                        float midT = (minT + maxT) * 0.5f;
                        Vector2 midPos = fromPos + direction * (distance * midT);
                        
                        if (CheckCollision(midPos, includeEnemies))
                        {
                            maxT = midT;
                        }
                        else
                        {
                            minT = midT;
                        }
                    }
                    
                    // Return the last safe position
                    return fromPos + direction * (distance * minT);
                }
            }
            
            return toPos; // No collision detected
        }
        
        /// <summary>
        /// Move with collision resolution - returns final position after sliding
        /// This is the main method entities should use for smooth movement
        /// </summary>
        public Vector2 MoveWithCollision(Vector2 fromPos, Vector2 toPos, bool includeEnemies = true, int maxIterations = 3)
        {
            Vector2 currentPos = fromPos;
            Vector2 remainingMovement = toPos - fromPos;
            float effectiveRadius = GameConfig.EntityCollisionRadius + GameConfig.CollisionBuffer;
            
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                if (remainingMovement.LengthSquared() < 0.01f)
                    break;
                
                Vector2 targetPos = currentPos + remainingMovement;
                
                // Check if we can move directly
                if (!CheckCollision(targetPos, includeEnemies))
                {
                    return targetPos;
                }
                
                // Swept collision to find where we hit
                Vector2 hitPos = SweptCollisionCheck(currentPos, targetPos, includeEnemies);
                
                // If we didn't move at all, try to get unstuck
                if (Vector2.DistanceSquared(currentPos, hitPos) < 0.1f)
                {
                    // We're stuck - try to push out of collision
                    Vector2 pushNormal = GetCollisionNormal(currentPos, effectiveRadius);
                    if (pushNormal.LengthSquared() > 0.01f)
                    {
                        // Push out slightly
                        Vector2 pushOut = currentPos + pushNormal * 2.0f;
                        if (!CheckCollision(pushOut, includeEnemies))
                        {
                            currentPos = pushOut;
                            continue;
                        }
                    }
                    
                    // Still stuck - give up on this iteration
                    break;
                }
                
                // Move to hit position
                currentPos = hitPos;
                
                // Calculate slide direction
                Vector2 moveDir = remainingMovement;
                moveDir.Normalize();
                
                Vector2 normal = GetCollisionNormal(currentPos, effectiveRadius);
                if (normal.LengthSquared() < 0.01f)
                {
                    // No normal found - can't slide
                    break;
                }
                
                // Project remaining movement onto the surface (perpendicular to normal)
                // This creates smooth sliding along walls
                float dotProduct = Vector2.Dot(remainingMovement, normal);
                Vector2 slideMovement = remainingMovement - normal * dotProduct;
                
                // Reduce slide movement slightly to prevent getting stuck in corners
                slideMovement *= 0.95f;
                
                // Update remaining movement for next iteration
                remainingMovement = slideMovement;
                
                // If slide movement is too small, stop
                if (slideMovement.LengthSquared() < 0.1f)
                    break;
            }
            
            return currentPos;
        }

        /// <summary>
        /// Check if line of sight is blocked by collision
        /// </summary>
        public bool IsLineOfSightBlocked(Vector2 from, Vector2 to)
        {
            Vector2 direction = to - from;
            float distance = direction.Length();
            direction.Normalize();
            
            int samples = (int)(distance / 16.0f) + 1;
            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples;
                Vector2 samplePoint = from + direction * (distance * t);
                
                if (CheckCollision(samplePoint))
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Get collision cells for rendering
        /// </summary>
        public List<CollisionCellData> GetCollisionCells()
        {
            return _collisionCells;
        }
        
        /// <summary>
        /// Clear collision cache (useful when map changes or for testing)
        /// </summary>
        public void ClearCollisionCache()
        {
            _staticCollisionCache.Clear();
            _cacheHits = 0;
            _cacheMisses = 0;
            Console.WriteLine("[CollisionManager] Collision cache cleared");
        }
        
        /// <summary>
        /// Get cache statistics
        /// </summary>
        public (int hits, int misses, int cacheSize, int hitRate) GetCacheStats()
        {
            int hitRate = _cacheMisses > 0 ? (_cacheHits * 100) / (_cacheHits + _cacheMisses) : 0;
            return (_cacheHits, _cacheMisses, _staticCollisionCache.Count, hitRate);
        }

        private static string? ResolveCollisionPath(string relativePath)
        {
            string currentDir = Directory.GetCurrentDirectory();
            string fullPath = Path.GetFullPath(Path.Combine(currentDir, relativePath));
            if (File.Exists(fullPath))
                return fullPath;

            string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
            fullPath = Path.GetFullPath(Path.Combine(exeDir, relativePath));
            if (File.Exists(fullPath))
                return fullPath;

            var dir = new DirectoryInfo(exeDir);
            while (dir != null && dir.Parent != null)
            {
                string testPath = Path.GetFullPath(Path.Combine(dir.FullName, relativePath));
                if (File.Exists(testPath))
                    return testPath;
                dir = dir.Parent;
            }

            return null;
        }
    }
}
