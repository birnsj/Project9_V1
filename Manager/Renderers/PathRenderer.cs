using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Project9
{
    /// <summary>
    /// Renders debug path visualization for player movement
    /// </summary>
    public class PathRenderer
    {
        private GraphicsDevice _graphicsDevice;
        private Texture2D? _pathLineTexture;
        
        public PathRenderer(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }
        
        /// <summary>
        /// Draw debug path for player (only if path debug is enabled)
        /// </summary>
        public void DrawDebugPath(SpriteBatch spriteBatch, Player player)
        {
            // Only draw if player has a target
            if (!player.TargetPosition.HasValue)
                return;
            
            // Create line texture if needed
            if (_pathLineTexture == null)
            {
                _pathLineTexture = new Texture2D(_graphicsDevice, 1, 1);
                _pathLineTexture.SetData(new[] { Color.White });
            }
            
            // If there's a pathfinding path, draw it (this means terrain is blocked)
            // Pathfinding paths use cyan/yellow to indicate going around obstacles
            if (player.Path != null && player.Path.Count > 0)
            {
                // Draw path from player position through all waypoints to target
                Vector2? previousPoint = player.Position;
                
                // Draw lines connecting path waypoints (cyan = going around obstacles)
                foreach (var waypoint in player.Path)
                {
                    if (previousPoint.HasValue)
                    {
                        DrawPathLine(spriteBatch, previousPoint.Value, waypoint, Color.Cyan);
                    }
                    previousPoint = waypoint;
                }
                
                // Draw line to target if it exists and is different from last waypoint
                if (player.TargetPosition.HasValue && previousPoint.HasValue)
                {
                    float distToTarget = Vector2.Distance(previousPoint.Value, player.TargetPosition.Value);
                    if (distToTarget > 5.0f) // Only draw if target is significantly different
                    {
                        DrawPathLine(spriteBatch, previousPoint.Value, player.TargetPosition.Value, Color.Yellow);
                    }
                }
                
                // Draw waypoint markers
                foreach (var waypoint in player.Path)
                {
                    DrawPathWaypoint(spriteBatch, waypoint, Color.Cyan);
                }
            }
            else
            {
                // No pathfinding path - terrain path is clear, draw direct line (green)
                // Enemy collision will be handled during movement via sliding
                // Only draw if target is far enough away to be meaningful
                float distToTarget = Vector2.Distance(player.Position, player.TargetPosition.Value);
                if (distToTarget > 5.0f)
                {
                    // Green = direct path, terrain is clear (enemies will be handled by collision sliding)
                    DrawPathLine(spriteBatch, player.Position, player.TargetPosition.Value, Color.Lime);
                    // Also draw target marker
                    DrawPathWaypoint(spriteBatch, player.TargetPosition.Value, Color.Lime, 8.0f);
                }
            }
            
            // Draw target marker
            if (player.TargetPosition.HasValue)
            {
                DrawPathWaypoint(spriteBatch, player.TargetPosition.Value, Color.Yellow, 8.0f);
            }
        }
        
        private void DrawPathLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color)
        {
            Vector2 edge = end - start;
            float angle = (float)System.Math.Atan2(edge.Y, edge.X);
            float length = edge.Length();
            
            // Use semi-transparent color for path lines
            Color lineColor = new Color(color.R, color.G, color.B, (byte)180);
            
            spriteBatch.Draw(
                _pathLineTexture!,
                start,
                null,
                lineColor,
                angle,
                Vector2.Zero,
                new Vector2(length, 3.0f), // 3 pixel thick line
                SpriteEffects.None,
                0.0f
            );
        }
        
        private void DrawPathWaypoint(SpriteBatch spriteBatch, Vector2 position, Color color, float size = 6.0f)
        {
            if (_pathLineTexture == null)
            {
                _pathLineTexture = new Texture2D(_graphicsDevice, 1, 1);
                _pathLineTexture.SetData(new[] { Color.White });
            }
            
            // Draw a small circle/square at waypoint position
            Color waypointColor = new Color(color.R, color.G, color.B, (byte)220);
            
            // Draw a small diamond shape (matching isometric style)
            float halfSize = size / 2.0f;
            
            // Draw 4 lines forming a diamond
            Vector2[] diamondPoints = new Vector2[]
            {
                position + new Vector2(0, -halfSize),      // Top
                position + new Vector2(halfSize, 0),       // Right
                position + new Vector2(0, halfSize),        // Bottom
                position + new Vector2(-halfSize, 0)       // Left
            };
            
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                DrawPathLine(spriteBatch, diamondPoints[i], diamondPoints[next], waypointColor);
            }
        }
    }
}



