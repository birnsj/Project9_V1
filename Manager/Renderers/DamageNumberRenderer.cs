using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Project9
{
    /// <summary>
    /// Manages and renders damage numbers
    /// </summary>
    public class DamageNumberRenderer
    {
        private SpriteFont? _uiFont;
        
        // Damage numbers (using array for better performance, no allocations)
        private DamageNumber[] _damageNumbers = new DamageNumber[GameConfig.MaxDamageNumbers];
        private int _damageNumberCount = 0;
        
        public DamageNumberRenderer(SpriteFont? uiFont)
        {
            _uiFont = uiFont;
        }
        
        /// <summary>
        /// Add a damage number to display
        /// </summary>
        public void ShowDamageNumber(Vector2 worldPosition, float damage)
        {
            // Use array instead of List to avoid allocations
            if (_damageNumberCount < _damageNumbers.Length)
            {
                _damageNumbers[_damageNumberCount] = new DamageNumber(worldPosition, damage);
                _damageNumberCount++;
            }
            // If array is full, ignore new damage numbers (or overwrite oldest)
        }
        
        /// <summary>
        /// Update all damage numbers
        /// </summary>
        public void UpdateDamageNumbers(float deltaTime)
        {
            // Update in reverse order so we can remove expired ones efficiently
            for (int i = _damageNumberCount - 1; i >= 0; i--)
            {
                _damageNumbers[i].Update(deltaTime);
                if (_damageNumbers[i].IsExpired)
                {
                    // Swap with last element and decrement count (more efficient than shifting)
                    if (i < _damageNumberCount - 1)
                    {
                        _damageNumbers[i] = _damageNumbers[_damageNumberCount - 1];
                    }
                    _damageNumberCount--;
                }
            }
        }
        
        /// <summary>
        /// Draw all damage numbers in world space
        /// </summary>
        public void DrawDamageNumbers(SpriteBatch spriteBatch)
        {
            if (_uiFont == null)
                return;
            
            if (_damageNumberCount == 0)
                return;
            
            // Iterate only over active damage numbers
            for (int i = 0; i < _damageNumberCount; i++)
            {
                var damageNumber = _damageNumbers[i];
                // Position in world space (above entity, above health bar)
                Vector2 worldPos = damageNumber.Position + new Vector2(0, GameConfig.DamageNumberOffsetY);
                
                string damageText = $"-{damageNumber.Damage:F0}";
                Vector2 textSize = _uiFont.MeasureString(damageText);
                Vector2 textPos = worldPos - new Vector2(textSize.X / 2.0f, textSize.Y / 2.0f);
                
                // Draw with alpha fade
                float alpha = damageNumber.Alpha;
                if (alpha <= 0.01f)
                    continue; // Skip if invisible
                    
                byte alphaByte = (byte)(255 * alpha);
                byte shadowAlpha = (byte)(128 * alpha);
                Color textColor = new Color((byte)255, (byte)100, (byte)100, alphaByte); // Red damage text
                
                // Draw shadow for better visibility (small offset in world space)
                spriteBatch.DrawString(_uiFont, damageText, textPos + new Vector2(2, 2), new Color((byte)0, (byte)0, (byte)0, shadowAlpha));
                spriteBatch.DrawString(_uiFont, damageText, textPos, textColor);
            }
        }
    }
}



