using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Project9
{
    /// <summary>
    /// Renders health bars for player and enemies
    /// </summary>
    public class HealthBarRenderer
    {
        private GraphicsDevice _graphicsDevice;
        private SpriteFont? _uiFont;
        private Texture2D? _healthBarBackgroundTexture;
        private Texture2D? _healthBarForegroundTexture;
        
        public HealthBarRenderer(GraphicsDevice graphicsDevice, SpriteFont? uiFont)
        {
            _graphicsDevice = graphicsDevice;
            _uiFont = uiFont;
        }
        
        /// <summary>
        /// Draw player health bar in screen space (lower left corner)
        /// </summary>
        public void DrawPlayerHealthBar(SpriteBatch spriteBatch, Player player)
        {
            const int barWidth = 200;
            const int barHeight = 20;
            const int padding = 10;
            const int borderThickness = 2;
            
            // Position in lower left corner
            Vector2 barPosition = new Vector2(
                padding,
                _graphicsDevice.Viewport.Height - barHeight - padding
            );
            
            // Create textures if needed
            if (_healthBarBackgroundTexture == null)
            {
                _healthBarBackgroundTexture = new Texture2D(_graphicsDevice, barWidth, barHeight);
                Color[] bgData = new Color[barWidth * barHeight];
                for (int i = 0; i < bgData.Length; i++)
                {
                    bgData[i] = new Color(40, 40, 40, 230); // Dark gray background
                }
                _healthBarBackgroundTexture.SetData(bgData);
            }
            
            if (_healthBarForegroundTexture == null)
            {
                _healthBarForegroundTexture = new Texture2D(_graphicsDevice, 1, 1);
                _healthBarForegroundTexture.SetData(new[] { Color.White });
            }
            
            // Draw background
            spriteBatch.Draw(_healthBarBackgroundTexture, barPosition, Color.White);
            
            // Calculate health percentage
            float healthPercent = player.MaxHealth > 0 ? player.CurrentHealth / player.MaxHealth : 0f;
            healthPercent = MathHelper.Clamp(healthPercent, 0f, 1f);
            
            // Determine health bar color based on health percentage
            Color healthColor;
            if (healthPercent > 0.6f)
            {
                // Green when above 60%
                healthColor = Color.Green;
            }
            else if (healthPercent > 0.3f)
            {
                // Yellow when between 30% and 60%
                healthColor = Color.Yellow;
            }
            else
            {
                // Red when below 30%
                healthColor = Color.Red;
            }
            
            // Draw health bar foreground
            int healthBarWidth = (int)((barWidth - borderThickness * 2) * healthPercent);
            if (healthBarWidth > 0)
            {
                Rectangle healthRect = new Rectangle(
                    (int)barPosition.X + borderThickness,
                    (int)barPosition.Y + borderThickness,
                    healthBarWidth,
                    barHeight - borderThickness * 2
                );
                spriteBatch.Draw(_healthBarForegroundTexture, healthRect, healthColor);
            }
            
            // Draw border
            Rectangle borderRect = new Rectangle(
                (int)barPosition.X,
                (int)barPosition.Y,
                barWidth,
                barHeight
            );
            DrawRectangleOutline(spriteBatch, borderRect, Color.White, borderThickness);
            
            // Draw health text (above the bar)
            if (_uiFont != null)
            {
                string healthText = $"HP: {player.CurrentHealth:F0}/{player.MaxHealth:F0}";
                Vector2 textSize = _uiFont.MeasureString(healthText);
                Vector2 textPosition = new Vector2(
                    barPosition.X + (barWidth - textSize.X) / 2.0f,
                    barPosition.Y - textSize.Y - 5
                );
                spriteBatch.DrawString(_uiFont, healthText, textPosition, Color.White);
            }
        }
        
        /// <summary>
        /// Draw enemy health bar in world space (above enemy)
        /// </summary>
        public void DrawEnemyHealthBar(SpriteBatch spriteBatch, Enemy enemy)
        {
            const float barWidth = 60.0f;
            const float barHeight = 6.0f;
            const float borderThickness = 1.0f;
            const float barOffsetY = -40.0f; // Position above enemy (in world space)
            
            // Position health bar above enemy in world space (so it moves with the enemy)
            Vector2 barPosition = new Vector2(
                enemy.Position.X - barWidth / 2.0f,
                enemy.Position.Y + barOffsetY
            );
            
            // Create textures if needed
            if (_healthBarForegroundTexture == null)
            {
                _healthBarForegroundTexture = new Texture2D(_graphicsDevice, 1, 1);
                _healthBarForegroundTexture.SetData(new[] { Color.White });
            }
            
            // Draw background (dark red)
            spriteBatch.Draw(
                _healthBarForegroundTexture,
                barPosition,
                null,
                new Color(60, 0, 0, 200),
                0f,
                Vector2.Zero,
                new Vector2(barWidth, barHeight),
                SpriteEffects.None,
                0f
            );
            
            // Calculate health percentage
            float healthPercent = enemy.MaxHealth > 0 ? enemy.CurrentHealth / enemy.MaxHealth : 0f;
            healthPercent = MathHelper.Clamp(healthPercent, 0f, 1f);
            
            // Draw health bar (red)
            float healthBarWidth = (barWidth - borderThickness * 2) * healthPercent;
            if (healthBarWidth > 0)
            {
                Color healthColor = new Color(200, 0, 0, 255); // Red
                Vector2 healthPos = barPosition + new Vector2(borderThickness, borderThickness);
                spriteBatch.Draw(
                    _healthBarForegroundTexture,
                    healthPos,
                    null,
                    healthColor,
                    0f,
                    Vector2.Zero,
                    new Vector2(healthBarWidth, barHeight - borderThickness * 2),
                    SpriteEffects.None,
                    0f
                );
            }
            
            // Draw border (4 lines)
            float borderAlpha = 0.8f;
            byte alphaByte = (byte)(255 * borderAlpha);
            Color semiWhite = new Color((byte)255, (byte)255, (byte)255, alphaByte);
            
            // Top
            spriteBatch.Draw(
                _healthBarForegroundTexture,
                barPosition,
                null,
                semiWhite,
                0f,
                Vector2.Zero,
                new Vector2(barWidth, borderThickness),
                SpriteEffects.None,
                0f
            );
            
            // Bottom
            spriteBatch.Draw(
                _healthBarForegroundTexture,
                barPosition + new Vector2(0, barHeight - borderThickness),
                null,
                semiWhite,
                0f,
                Vector2.Zero,
                new Vector2(barWidth, borderThickness),
                SpriteEffects.None,
                0f
            );
            
            // Left
            spriteBatch.Draw(
                _healthBarForegroundTexture,
                barPosition,
                null,
                semiWhite,
                0f,
                Vector2.Zero,
                new Vector2(borderThickness, barHeight),
                SpriteEffects.None,
                0f
            );
            
            // Right
            spriteBatch.Draw(
                _healthBarForegroundTexture,
                barPosition + new Vector2(barWidth - borderThickness, 0),
                null,
                semiWhite,
                0f,
                Vector2.Zero,
                new Vector2(borderThickness, barHeight),
                SpriteEffects.None,
                0f
            );
        }
        
        private void DrawRectangleOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            if (_healthBarForegroundTexture == null)
            {
                _healthBarForegroundTexture = new Texture2D(_graphicsDevice, 1, 1);
                _healthBarForegroundTexture.SetData(new[] { Color.White });
            }
            
            // Top
            spriteBatch.Draw(_healthBarForegroundTexture, 
                new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            
            // Bottom
            spriteBatch.Draw(_healthBarForegroundTexture, 
                new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
            
            // Left
            spriteBatch.Draw(_healthBarForegroundTexture, 
                new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            
            // Right
            spriteBatch.Draw(_healthBarForegroundTexture, 
                new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
        }
    }
}



