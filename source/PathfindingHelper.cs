using System;
using Microsoft.Xna.Framework;

namespace Project9
{
    /// <summary>
    /// Helper class for shared pathfinding and collision sliding logic
    /// </summary>
    public static class PathfindingHelper
    {
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
        
        /// <summary>
        /// Check if there's a direct path between two points (no obstacles)
        /// Uses denser sampling (every 8 pixels) to catch more obstacles
        /// </summary>
        public static bool CheckDirectPath(Vector2 from, Vector2 target, Func<Vector2, bool>? checkCollision)
        {
            if (checkCollision == null) return true;
            
            Vector2 direction = target - from;
            float distanceSquared = direction.LengthSquared();
            // Use denser sampling (every 8 pixels) to catch more obstacles
            float distance = (float)Math.Sqrt(distanceSquared);
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
        
        /// <summary>
        /// Try to slide along collision using isometric-aware directions
        /// Returns a position that avoids collision, or currentPos if no valid slide found
        /// </summary>
        public static Vector2 TrySlideAlongCollision(Vector2 currentPos, Vector2 targetPos, Vector2 direction, float moveDistance, Func<Vector2, bool>? checkCollision)
        {
            if (checkCollision == null) return currentPos;
            
            Vector2 movement = targetPos - currentPos;
            
            if (movement.LengthSquared() < 0.001f)
                return currentPos;
            
            // Isometric-aware slide directions (aligned with diamond collision cells)
            // These match the 2:1 aspect ratio of isometric tiles
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
    }
}



