using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Project9
{
    /// <summary>
    /// Renders UI elements in screen space (death screen, version, sneak indicator, alarm, etc.)
    /// </summary>
    public class UIRenderer
    {
        private GraphicsDevice _graphicsDevice;
        private SpriteFont? _uiFont;
        private Texture2D? _whiteTexture;
        
        public UIRenderer(GraphicsDevice graphicsDevice, SpriteFont? uiFont)
        {
            _graphicsDevice = graphicsDevice;
            _uiFont = uiFont;
        }
        
        /// <summary>
        /// Draw death screen overlay
        /// </summary>
        public void DrawDeathScreen(SpriteBatch spriteBatch, Player player)
        {
            if (_uiFont == null)
                return;
            
            // Create white texture if needed
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(_graphicsDevice, 1, 1);
                _whiteTexture.SetData(new[] { Color.White });
            }
            
            // Draw semi-transparent overlay
            Rectangle overlayRect = new Rectangle(0, 0, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);
            spriteBatch.Draw(_whiteTexture, overlayRect, new Color(0, 0, 0, 180)); // Dark overlay
            
            // "You are Dead" text
            string deathText = "You are Dead";
            Vector2 deathTextSize = _uiFont.MeasureString(deathText);
            Vector2 deathTextPos = new Vector2(
                _graphicsDevice.Viewport.Width / 2.0f - deathTextSize.X / 2.0f,
                _graphicsDevice.Viewport.Height / 2.0f - 80.0f
            );
            spriteBatch.DrawString(_uiFont, deathText, deathTextPos, Color.Red);
            
            // Countdown text
            int countdownSeconds = (int)System.Math.Ceiling(player.RespawnTimer);
            if (countdownSeconds < 0) countdownSeconds = 0;
            string countdownText = $"Respawning in {countdownSeconds}...";
            Vector2 countdownTextSize = _uiFont.MeasureString(countdownText);
            Vector2 countdownTextPos = new Vector2(
                _graphicsDevice.Viewport.Width / 2.0f - countdownTextSize.X / 2.0f,
                _graphicsDevice.Viewport.Height / 2.0f - 20.0f
            );
            spriteBatch.DrawString(_uiFont, countdownText, countdownTextPos, Color.White);
            
            // "Press Space to Respawn" message
            if (player.IsRespawning)
            {
                string respawnHintText = "Press Space to Respawn";
                Vector2 respawnHintTextSize = _uiFont.MeasureString(respawnHintText);
                Vector2 respawnHintTextPos = new Vector2(
                    _graphicsDevice.Viewport.Width / 2.0f - respawnHintTextSize.X / 2.0f,
                    _graphicsDevice.Viewport.Height / 2.0f + 20.0f
                );
                spriteBatch.DrawString(_uiFont, respawnHintText, respawnHintTextPos, Color.Yellow);
            }
        }
        
        /// <summary>
        /// Draw version number in bottom right corner
        /// </summary>
        public void DrawVersion(SpriteBatch spriteBatch)
        {
            if (_uiFont == null)
                return;
            
            string versionText = "V002";
            Vector2 textSize = _uiFont.MeasureString(versionText);
            Vector2 position = new Vector2(
                _graphicsDevice.Viewport.Width - textSize.X - 10,
                _graphicsDevice.Viewport.Height - textSize.Y - 10
            );
            spriteBatch.DrawString(_uiFont, versionText, position, Color.White);
        }
        
        /// <summary>
        /// Draw sneak indicator if player is sneaking
        /// </summary>
        public void DrawSneakIndicator(SpriteBatch spriteBatch, bool isSneaking, bool isAlive)
        {
            if (_uiFont == null || !isSneaking || !isAlive)
                return;
            
            string sneakText = "SNEAK";
            Vector2 sneakTextSize = _uiFont.MeasureString(sneakText);
            Vector2 sneakPosition = new Vector2(
                _graphicsDevice.Viewport.Width / 2.0f - sneakTextSize.X / 2.0f,
                50.0f
            );
            spriteBatch.DrawString(_uiFont, sneakText, sneakPosition, Color.Purple);
        }
        
        /// <summary>
        /// Draw alarm countdown if alarm is active
        /// </summary>
        public void DrawAlarmCountdown(SpriteBatch spriteBatch, bool alarmActive, float alarmTimer)
        {
            if (_uiFont == null || !alarmActive)
                return;
            
            int secondsRemaining = (int)System.Math.Ceiling(alarmTimer);
            string alarmText = $"ALARM: {secondsRemaining}";
            Vector2 alarmTextSize = _uiFont.MeasureString(alarmText);
            Vector2 alarmPosition = new Vector2(
                _graphicsDevice.Viewport.Width / 2.0f - alarmTextSize.X / 2.0f,
                100.0f
            );
            
            // Flash red when time is running out
            Color alarmColor = secondsRemaining <= 10 ? Color.Red : Color.OrangeRed;
            
            // Draw shadow for better visibility
            spriteBatch.DrawString(_uiFont, alarmText, alarmPosition + new Vector2(2, 2), Color.Black);
            spriteBatch.DrawString(_uiFont, alarmText, alarmPosition, alarmColor);
        }
    }
}



