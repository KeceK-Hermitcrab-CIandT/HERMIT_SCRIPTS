//Only works on the new project
#if UNITY_2022_1_OR_NEWER && UNITY_EDITOR

using Prisms.Layout;
using Shapes;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.PropertyVariants;

namespace HERMIT_SCRIPTS
{
	public class AddTextBlockUtility : EditorWindow
	{

		private static List<StringTableCollection> cachedStringTables = null;

		[MenuItem("Tools/Add TextBlock Component")]
		public static void AddTextBlockToSelectedObjects()
		{
			GameObject[] selectedObjects = Selection.gameObjects;

			if (selectedObjects.Length == 0)
			{
				Debug.LogWarning("No GameObjects selected. Please select one or more GameObjects to add TextBlock component.");
				return;
			}

			// Load string tables once and cache them for the entire operation
			cachedStringTables = GetAllStringTables();
			Debug.Log($"Loaded {cachedStringTables.Count} StringTableCollection(s) for processing {selectedObjects.Length} GameObject(s)");

			try
			{
				foreach (GameObject go in selectedObjects)
				{
					AddTextBlockComponent(go);
				}
			}
			finally
			{
				// Clear cache after operation is complete
				cachedStringTables = null;
			}
		}

		private static void AddTextBlockComponent(GameObject gameObject)
		{
			// Get or add TMP_Text component
			TMP_Text tmpText = gameObject.GetComponent<TMP_Text>();
			if (tmpText == null)
			{
				Debug.LogWarning($"GameObject '{gameObject.name}' doesn't have a TMP_Text component. Skipping.");
				return;
			}

			RemoveGameObjectLocalizer(gameObject);

			// Check if TextBlock component already exists
			TextBlock existingTextBlock = gameObject.GetComponent<TextBlock>();
			if (existingTextBlock != null)
			{
				Debug.Log($"TextBlock component already exists on GameObject '{gameObject.name}'.");
				SetupExistingBlock(existingTextBlock, tmpText);
				return;
			}

			// Add TextBlock component
			TextBlock textBlock = gameObject.AddComponent<TextBlock>();
			Debug.Log($"Added TextBlock component to '{gameObject.name}'");

			SetupNewTextBlock(textBlock, tmpText);

			// Mark the object as dirty for undo/redo and saving
			EditorUtility.SetDirty(gameObject);
		}
		private static void SetupExistingBlock(TextBlock textBlock, TMP_Text tmpText)
		{
			Label textBlockLabel = textBlock.gameObject.GetComponent<Label>();
			if (textBlockLabel != null)
			{
				//Sets label color to rect color
				Rectangle rectangle = textBlock.gameObject.GetComponentInChildren<Rectangle>();
				textBlockLabel.BackgroundColor = rectangle.Color;
			}

			// Set custom text settings to true
			SetCustomTextSettings(textBlock, true);

			// Set the text color from TMP_Text
			SetTextColor(textBlock, tmpText);

			// Search for localization and set if found
			SearchAndSetLocalization(textBlock, tmpText);

			textBlock.UpdateTextBlock();
			textBlockLabel?.UpdateLabel();
		}

		private static void SetupNewTextBlock(TextBlock textBlock, TMP_Text tmpText)
		{
			// Set the TPM_Text and text from TMP_Text
			SetRectAndTMP(textBlock, tmpText);

			// Set custom text settings to true
			SetCustomTextSettings(textBlock, true);

			// Set the text color from TMP_Text
			SetTextColor(textBlock, tmpText);

			// Search for localization and set if found
			SearchAndSetLocalization(textBlock, tmpText);

			textBlock.UpdateTextBlock();
		}

		public static void RemoveGameObjectLocalizer(GameObject targetGameObject)
		{
			GameObjectLocalizer localizerComponent = targetGameObject.GetComponent<GameObjectLocalizer>();

			if (localizerComponent != null)
			{
				Undo.DestroyObjectImmediate(localizerComponent);
				Debug.Log($"Removed GameObjectLocalizer from '{targetGameObject.name}'.");
			}
		}

		private static void SetTextColor(TextBlock textBlock, TMP_Text tmpText)
		{
			textBlock.SetTextColor(tmpText.color);
		}

		private static void SetRectAndTMP(TextBlock textBlock, TMP_Text tmpText)
		{
			try
			{
				// Use reflection to set the private field _rectTransform
				var rectTransformField = typeof(TextBlock).GetField("_rectTransform",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

				if (rectTransformField != null)
				{
					rectTransformField.SetValue(textBlock, tmpText.rectTransform);
					Debug.Log($"Set _rectTransform to {tmpText.rectTransform} on TextBlock");
				}
				else
				{
					Debug.LogWarning("Could not find _rectTransform field in TextBlock component");
				}

				// Use reflection to set the private field _text
				var tmpTextSettingsField = typeof(TextBlock).GetField("_text",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

				if (tmpTextSettingsField != null)
				{
					tmpTextSettingsField.SetValue(textBlock, tmpText);
					Debug.Log($"Set _text to {tmpText} on TextBlock");
				}
				else
				{
					Debug.LogWarning("Could not find _text field in TextBlock component");
				}

			}
			catch (Exception e)
			{
				Debug.LogError($"Error setting custom text settings: {e.Message}");
			}
		}

		private static void SetCustomTextSettings(TextBlock textBlock, bool value)
		{
			try
			{
				// Use reflection to set the private field _useCustomTextSettings
				var customTextSettingsField = typeof(TextBlock).GetField("_useCustomTextSettings",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

				if (customTextSettingsField != null)
				{
					customTextSettingsField.SetValue(textBlock, value);
					Debug.Log($"Set _useCustomTextSettings to {value} on TextBlock");
				}
				else
				{
					Debug.LogWarning("Could not find _useCustomTextSettings field in TextBlock component");
				}
			}
			catch (System.Exception e)
			{
				Debug.LogError($"Error setting custom text settings: {e.Message}");
			}
		}

		private static void SearchAndSetLocalization(TextBlock textBlock, TMP_Text tmpText)
		{
			try
			{
				string currentText = tmpText.text;
				if (string.IsNullOrEmpty(currentText))
				{
					Debug.LogWarning("TMP_Text is empty, skipping localization search");
					return;
				}

				// Find the best matching localization key and table collection
				var matchResult = FindBestLocalizationMatch(currentText);

				if (String.IsNullOrEmpty(matchResult.Value))
				{
					Debug.LogWarning($"No localization match found for text: '{currentText}'");
				}
				else
				{
					SetLocalizedString(textBlock, matchResult.Key, matchResult.Value);
					SetUseLocalizedString(textBlock, true);
					Debug.Log($"Found localization match: '{matchResult.Value}' in table '{matchResult.Key}' for text: '{currentText}'");
				}
			}
			catch (System.Exception e)
			{
				Debug.LogError($"Error searching for localization: {e.Message}");
			}
		}

		private static KeyValuePair<string, string> FindBestLocalizationMatch(string targetText)
		{
			float bestSimilarity = 0.3f; // Minimum similarity threshold

			KeyValuePair<string, string> bestMatch = new("", "");
			foreach (var tableCollection in cachedStringTables)
			{
				// Get all locales for this table collection
				var tableEntries = tableCollection.StringTables;

				foreach (var table in tableEntries)
				{
					if (table?.Values == null) continue;

					foreach (var entry in table.Values)
					{
						if (entry?.Value == null) continue;

						float similarity = CalculateStringSimilarity(targetText, entry.Value);
						if (similarity > bestSimilarity)
						{
							bestSimilarity = similarity;
							bestMatch = new(table.TableCollectionName, entry.Key);
						}

						// Check for exact match
						if (entry.Value.Equals(targetText, StringComparison.OrdinalIgnoreCase))
						{
							return new(table.TableCollectionName, entry.Key);
						}
					}
				}
			}

			return bestMatch;
		}

		private static List<StringTableCollection> GetAllStringTables()
		{
			var stringTables = new List<StringTableCollection>();

			try
			{
				var stringTableCollections = LocalizationEditorSettings.GetStringTableCollections();

				foreach (var tableCollection in stringTableCollections)
				{
					stringTables.Add(tableCollection);
				}
				// Search for StringTableCollection assets in the project
				if (stringTables.Count < 1)
				{
					string[] guids = AssetDatabase.FindAssets("t:StringTableCollection");

					foreach (string guid in guids)
					{
						string path = AssetDatabase.GUIDToAssetPath(guid);
						StringTableCollection collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(path);
						if (collection != null)
							stringTables.Add(collection);
					}
				}

			}
			catch (Exception e)
			{
				Debug.LogError($"Error loading string tables: {e.Message}");
			}

			return stringTables;
		}

		private static float CalculateStringSimilarity(string source, string target)
		{
			if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
				return 0f;

			// Simple similarity calculation using Levenshtein distance
			int maxLength = Mathf.Max(source.Length, target.Length);
			if (maxLength == 0) return 1f;

			int distance = LevenshteinDistance(source.ToLower(), target.ToLower());
			return 1f - ((float)distance / maxLength);
		}

		private static int LevenshteinDistance(string source, string target)
		{
			if (source.Length == 0) return target.Length;
			if (target.Length == 0) return source.Length;

			int[,] matrix = new int[source.Length + 1, target.Length + 1];

			for (int i = 0; i <= source.Length; i++)
				matrix[i, 0] = i;
			for (int j = 0; j <= target.Length; j++)
				matrix[0, j] = j;

			for (int i = 1; i <= source.Length; i++)
			{
				for (int j = 1; j <= target.Length; j++)
				{
					int cost = (source[i - 1] == target[j - 1]) ? 0 : 1;
					matrix[i, j] = Mathf.Min(
							matrix[i - 1, j] + 1,      // deletion
							Mathf.Min(
								matrix[i, j - 1] + 1,  // insertion
								matrix[i - 1, j - 1] + cost)
							); // substitution
				}
			}

			return matrix[source.Length, target.Length];
		}

		private static void SetLocalizedString(TextBlock textBlock, string tableName, string localizationKey)
		{
			try
			{
				// Create a LocalizedString reference
				LocalizedString localizedString = new(tableName, localizationKey);
				textBlock.SetLocalizedString(localizedString);
				Debug.Log($"Set localizationString to table: '{tableName}', key: '{localizationKey}' on TextBlock");
			}
			catch (Exception e)
			{
				Debug.LogError($"Error setting localized string: {e.Message}");
			}
		}

		private static void SetUseLocalizedString(TextBlock textBlock, bool value)
		{
			try
			{
				// Use reflection to set the private field _useLocalizedString
				var useLocalizedStringField = typeof(TextBlock).GetField("_useLocalizedString",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

				if (useLocalizedStringField != null)
				{
					useLocalizedStringField.SetValue(textBlock, value);
					Debug.Log($"Set _useLocalizedString to {value} on TextBlock");
				}
				else
				{
					Debug.LogWarning("Could not find _useLocalizedString field in TextBlock component");
				}
			}
			catch (System.Exception e)
			{
				Debug.LogError($"Error setting use localized string: {e.Message}");
			}
		}

		[MenuItem("Tools/Add TextBlock Component", true)]
		public static bool ValidateAddTextBlockToSelectedObjects()
		{
			return Selection.gameObjects.Length > 0;
		}
	}
}
#endif
