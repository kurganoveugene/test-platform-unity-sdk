using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TestPlatform.SDK
{
    /// <summary>
    /// Finds UI elements in the scene based on various selection strategies.
    /// </summary>
    public class ElementLocator
    {
        /// <summary>
        /// Find a single element matching the selector.
        /// </summary>
        public GameObject Find(ElementSelector selector)
        {
            var elements = FindAll(selector);

            if (elements.Count == 0)
            {
                return null;
            }

            var index = selector.Index >= 0 ? selector.Index : 0;
            if (index >= elements.Count)
            {
                return null;
            }

            return elements[index];
        }

        /// <summary>
        /// Find all elements matching the selector.
        /// </summary>
        public List<GameObject> FindAll(ElementSelector selector)
        {
            if (selector == null || string.IsNullOrEmpty(selector.Strategy))
            {
                return new List<GameObject>();
            }

            switch (selector.Strategy.ToLower())
            {
                case "name":
                    return FindByName(selector.Value);
                case "tag":
                    return FindByTag(selector.Value);
                case "path":
                    return FindByPath(selector.Value);
                case "text":
                    return FindByText(selector.Value);
                case "component":
                    return FindByComponent(selector.Value);
                default:
                    Debug.LogWarning($"[TestPlatform] Unknown selector strategy: {selector.Strategy}");
                    return new List<GameObject>();
            }
        }

        /// <summary>
        /// Check if an element exists and is active.
        /// </summary>
        public bool Exists(ElementSelector selector)
        {
            var element = Find(selector);
            return element != null && element.activeInHierarchy;
        }

        /// <summary>
        /// Check if an element is visible (active and has a renderer or UI component).
        /// </summary>
        public bool IsVisible(ElementSelector selector)
        {
            var element = Find(selector);
            if (element == null || !element.activeInHierarchy)
            {
                return false;
            }

            // Check for UI visibility
            var canvasGroup = element.GetComponentInParent<CanvasGroup>();
            if (canvasGroup != null && canvasGroup.alpha <= 0)
            {
                return false;
            }

            // Check if it has any visual component
            return element.GetComponent<Renderer>() != null ||
                   element.GetComponent<Graphic>() != null;
        }

        /// <summary>
        /// Get a property value from an element.
        /// </summary>
        public string GetProperty(ElementSelector selector, string propertyName)
        {
            var element = Find(selector);
            if (element == null)
            {
                return null;
            }

            switch (propertyName.ToLower())
            {
                case "text":
                    return GetText(element);
                case "name":
                    return element.name;
                case "tag":
                    return element.tag;
                case "active":
                    return element.activeInHierarchy.ToString();
                case "visible":
                    return IsVisible(selector).ToString();
                case "position":
                    return element.transform.position.ToString();
                case "localposition":
                    return element.transform.localPosition.ToString();
                case "rotation":
                    return element.transform.eulerAngles.ToString();
                case "scale":
                    return element.transform.localScale.ToString();
                case "interactable":
                    var selectable = element.GetComponent<Selectable>();
                    return (selectable != null && selectable.interactable).ToString();
                default:
                    // Try to get component property via reflection
                    return GetComponentProperty(element, propertyName);
            }
        }

        /// <summary>
        /// Get the screen position of an element's center.
        /// </summary>
        public Vector2? GetScreenPosition(ElementSelector selector)
        {
            var element = Find(selector);
            if (element == null)
            {
                return null;
            }

            // For UI elements - use RectTransformUtility for accurate conversion
            var rectTransform = element.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                var canvas = element.GetComponentInParent<Canvas>();
                Camera cam = canvas?.worldCamera;

                // If no world camera assigned, use main camera for non-overlay canvases
                if (cam == null && canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    cam = Camera.main;
                }

                // Convert rect center to screen point
                Vector3 worldCenter = rectTransform.TransformPoint(rectTransform.rect.center);

                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    // Overlay: position is already in screen space
                    return new Vector2(worldCenter.x, worldCenter.y);
                }
                else if (cam != null)
                {
                    // Camera-based canvas or world space
                    Vector3 screenPos = cam.WorldToScreenPoint(worldCenter);
                    return new Vector2(screenPos.x, screenPos.y);
                }
                else
                {
                    // Fallback for overlay without explicit check
                    return new Vector2(worldCenter.x, worldCenter.y);
                }
            }

            // For 3D objects
            if (Camera.main != null)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(element.transform.position);
                return new Vector2(screenPos.x, screenPos.y);
            }

            return null;
        }

        private List<GameObject> FindByName(string name)
        {
            var results = new List<GameObject>();

            // Find all GameObjects including inactive ones
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (var obj in allObjects)
            {
                // Skip objects that aren't in the scene
                if (obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave)
                    continue;
                if (obj.scene.name == null)
                    continue;

                if (obj.name == name || obj.name.Contains(name))
                {
                    results.Add(obj);
                }
            }

            return results.OrderBy(o => o.name == name ? 0 : 1).ToList();
        }

        private List<GameObject> FindByTag(string tag)
        {
            try
            {
                return GameObject.FindGameObjectsWithTag(tag).ToList();
            }
            catch (UnityException)
            {
                Debug.LogWarning($"[TestPlatform] Tag not found: {tag}");
                return new List<GameObject>();
            }
        }

        private List<GameObject> FindByPath(string path)
        {
            var results = new List<GameObject>();
            var obj = GameObject.Find(path);

            if (obj != null)
            {
                results.Add(obj);
            }

            return results;
        }

        private List<GameObject> FindByText(string text)
        {
            var results = new List<GameObject>();

            // Search in TMP Text components
            var tmpTexts = UnityEngine.Object.FindObjectsOfType<TMP_Text>(true);
            foreach (var tmp in tmpTexts)
            {
                if (tmp.text != null && (tmp.text == text || tmp.text.Contains(text)))
                {
                    results.Add(tmp.gameObject);
                }
            }

            // Search in legacy Text components
            var texts = UnityEngine.Object.FindObjectsOfType<Text>(true);
            foreach (var t in texts)
            {
                if (t.text != null && (t.text == text || t.text.Contains(text)))
                {
                    results.Add(t.gameObject);
                }
            }

            return results.Distinct().OrderBy(o => GetText(o) == text ? 0 : 1).ToList();
        }

        private List<GameObject> FindByComponent(string componentName)
        {
            var results = new List<GameObject>();

            // Find the component type
            var type = FindType(componentName);
            if (type == null)
            {
                Debug.LogWarning($"[TestPlatform] Component type not found: {componentName}");
                return results;
            }

            var components = UnityEngine.Object.FindObjectsOfType(type, true);
            foreach (var component in components)
            {
                if (component is Component c)
                {
                    results.Add(c.gameObject);
                }
            }

            return results;
        }

        private Type FindType(string typeName)
        {
            // Check common Unity types first
            var type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null) return type;

            type = Type.GetType($"UnityEngine.UI.{typeName}, UnityEngine.UI");
            if (type != null) return type;

            type = Type.GetType($"TMPro.{typeName}, Unity.TextMeshPro");
            if (type != null) return type;

            // Search all assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;

                // Try with namespace variations
                foreach (var t in assembly.GetTypes())
                {
                    if (t.Name == typeName)
                    {
                        return t;
                    }
                }
            }

            return null;
        }

        private string GetText(GameObject obj)
        {
            // Try TMP first
            var tmp = obj.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                return tmp.text;
            }

            // Try legacy Text
            var text = obj.GetComponent<Text>();
            if (text != null)
            {
                return text.text;
            }

            // Try InputField
            var inputField = obj.GetComponent<TMP_InputField>();
            if (inputField != null)
            {
                return inputField.text;
            }

            var legacyInput = obj.GetComponent<InputField>();
            if (legacyInput != null)
            {
                return legacyInput.text;
            }

            return null;
        }

        private string GetComponentProperty(GameObject obj, string propertyPath)
        {
            // Format: ComponentName.PropertyName
            var parts = propertyPath.Split('.');
            if (parts.Length < 2)
            {
                return null;
            }

            var componentName = parts[0];
            var propertyName = parts[1];

            var type = FindType(componentName);
            if (type == null)
            {
                return null;
            }

            var component = obj.GetComponent(type);
            if (component == null)
            {
                return null;
            }

            var property = type.GetProperty(propertyName);
            if (property != null)
            {
                var value = property.GetValue(component);
                return value?.ToString();
            }

            var field = type.GetField(propertyName);
            if (field != null)
            {
                var value = field.GetValue(component);
                return value?.ToString();
            }

            return null;
        }
    }
}
