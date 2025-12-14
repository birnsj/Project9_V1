using System;
using System.Windows.Forms;

namespace Project9.Editor
{
    /// <summary>
    /// Helper class to enable arrow key adjustment for numeric properties in PropertyGrid
    /// </summary>
    public class PropertyGridDragHandler
    {
        private PropertyGrid _propertyGrid;
        private Action? _onValueChanged;

        public PropertyGridDragHandler(PropertyGrid propertyGrid, Action? onValueChanged = null)
        {
            _propertyGrid = propertyGrid;
            _onValueChanged = onValueChanged;
            
            // Make sure PropertyGrid can receive focus and keyboard input
            _propertyGrid.TabStop = true;
            
            // Hook into PropertyGrid's key events
            HookPropertyGridEvents();
        }

        private void HookPropertyGridEvents()
        {
            // Hook into key events for arrow key adjustment
            _propertyGrid.KeyDown += PropertyGrid_KeyDown;
            _propertyGrid.PreviewKeyDown += PropertyGrid_PreviewKeyDown;
        }
        
        private void PropertyGrid_PreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
        {
            // Allow arrow keys to be processed
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
            {
                e.IsInputKey = true;
            }
        }
        
        private void PropertyGrid_KeyDown(object? sender, KeyEventArgs e)
        {
            // Only handle arrow keys when a numeric property is selected
            if (e.KeyCode != Keys.Left && e.KeyCode != Keys.Right)
                return;
            
            var selectedProperty = _propertyGrid.SelectedGridItem;
            if (selectedProperty == null || !IsNumericProperty(selectedProperty))
                return;
            
            // Get current value
            object? currentValue = selectedProperty.Value;
            if (currentValue == null)
                return;
            
            float currentFloatValue = 0;
            if (currentValue is float floatVal)
                currentFloatValue = floatVal;
            else if (currentValue is int intVal)
                currentFloatValue = intVal;
            else if (currentValue is double doubleVal)
                currentFloatValue = (float)doubleVal;
            else
                return; // Unsupported type
            
            // Determine step size based on property type
            float stepSize = GetStepSize(selectedProperty);
            
            // Adjust value based on arrow key direction
            float newValue = currentFloatValue;
            if (e.KeyCode == Keys.Left)
            {
                newValue -= stepSize;
            }
            else if (e.KeyCode == Keys.Right)
            {
                newValue += stepSize;
            }
            
            // Clamp to reasonable bounds
            newValue = ClampValue(newValue, selectedProperty);
            
            // Update the property value
            try
            {
                var propertyDescriptor = selectedProperty.PropertyDescriptor;
                if (propertyDescriptor != null && !propertyDescriptor.IsReadOnly)
                {
                    object? newValueObj = null;
                    if (propertyDescriptor.PropertyType == typeof(float))
                    {
                        newValueObj = newValue;
                    }
                    else if (propertyDescriptor.PropertyType == typeof(int))
                    {
                        newValueObj = (int)Math.Round(newValue);
                    }
                    else if (propertyDescriptor.PropertyType == typeof(double))
                    {
                        newValueObj = (double)newValue;
                    }
                    else if (propertyDescriptor.PropertyType == typeof(float?))
                    {
                        newValueObj = newValue;
                    }
                    else if (propertyDescriptor.PropertyType == typeof(int?))
                    {
                        newValueObj = (int)Math.Round(newValue);
                    }
                    else if (propertyDescriptor.PropertyType == typeof(double?))
                    {
                        newValueObj = (double)newValue;
                    }
                    
                    if (newValueObj != null)
                    {
                        // Set the value on the underlying object
                        propertyDescriptor.SetValue(_propertyGrid.SelectedObject, newValueObj);
                        
                        // Refresh the property grid to show the updated value
                        _propertyGrid.Refresh();
                        
                        // Notify that value changed
                        _onValueChanged?.Invoke();
                        
                        // Mark as handled to prevent default behavior
                        e.Handled = true;
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
        }
        
        private bool IsNumericProperty(GridItem? property)
        {
            if (property == null || property.Value == null)
                return false;

            Type valueType = property.Value.GetType();
            return valueType == typeof(float) || 
                   valueType == typeof(int) || 
                   valueType == typeof(double) ||
                   valueType == typeof(float?) ||
                   valueType == typeof(int?) ||
                   valueType == typeof(double?);
        }
        
        private float GetStepSize(GridItem property)
        {
            // Determine step size based on property name/type
            string propertyName = property.Label?.ToLower() ?? "";
            
            // Position properties need larger steps
            if (propertyName == "x" || propertyName == "y")
                return 10.0f;
            
            // Angle/rotation properties
            if (propertyName.Contains("angle") || propertyName.Contains("rotation"))
                return 5.0f; // 5 degrees
            
            // Small values (0-1 range)
            if (propertyName.Contains("multiplier") || propertyName.Contains("threshold"))
                return 0.1f;
            
            // Color components
            if (propertyName.Contains("color") && (propertyName.Contains("r") || 
                propertyName.Contains("g") || propertyName.Contains("b")))
                return 5.0f;
            
            // Default step size
            return 1.0f;
        }

        private float ClampValue(float value, GridItem property)
        {
            // Apply reasonable bounds based on property type
            string propertyName = property.Label?.ToLower() ?? "";
            
            // Color components (0-255)
            if (propertyName.Contains("color") && (propertyName.Contains("r") || 
                propertyName.Contains("g") || propertyName.Contains("b")))
            {
                return Math.Clamp(value, 0, 255);
            }
            
            // Angles (0-360 degrees, but we store in radians)
            if (propertyName.Contains("angle"))
            {
                // Don't clamp angles, allow full range
                return value;
            }
            
            // Multipliers/thresholds (typically 0-1 or small positive)
            if (propertyName.Contains("multiplier") || propertyName.Contains("threshold"))
            {
                return Math.Max(0, value);
            }
            
            // No clamping for most values
            return value;
        }
    }
}
