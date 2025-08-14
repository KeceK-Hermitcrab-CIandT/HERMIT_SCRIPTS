#if UNITY_2022_1_OR_NEWER && UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using AxonGenesis;

namespace HERMIT_SCRIPTS
{
	public class AnimationCopier : EditorWindow
	{
		private GameObject sourcePrefab;
		private GameObject targetPrefab;
		private bool copyTimeflowSettings = true;
		private bool preserveExistingComponents = true;
		private Vector2 scrollPosition;

		// REFACTOR: A private field to cache the preview results for efficiency.
		private List<string> _objectsToAddPreview;

		[MenuItem("Tools/Animation Copier")]
		public static void ShowWindow()
		{
			GetWindow<AnimationCopier>("Animation Copier");
		}

		private void OnGUI()
		{
			GUILayout.Label("Prefab Structure & Animation Copier", EditorStyles.boldLabel);
			GUILayout.Space(10);

			// Source and target prefab fields
			sourcePrefab = (GameObject)EditorGUILayout.ObjectField("Source Prefab (with changes)", sourcePrefab, typeof(GameObject), false);
			targetPrefab = (GameObject)EditorGUILayout.ObjectField("Target Prefab (clean)", targetPrefab, typeof(GameObject), false);

			GUILayout.Space(10);

			// Options
			copyTimeflowSettings = EditorGUILayout.Toggle("Copy Timeflow Settings", copyTimeflowSettings);
			preserveExistingComponents = EditorGUILayout.Toggle(new GUIContent("Preserve Existing Components", "If checked, components on the target prefab will not be overwritten. New components will still be added."), preserveExistingComponents);

			GUILayout.Space(10);

			// Validation and actions
			if (sourcePrefab != null && targetPrefab != null)
			{
				if (GUILayout.Button("Preview Changes", GUILayout.Height(30)))
				{
					PreviewChanges();
				}

				GUILayout.Space(5);

				// The copy button is only enabled if a preview has been generated.
				GUI.enabled = _objectsToAddPreview != null;
				if (GUILayout.Button("Copy Structure and Components", GUILayout.Height(40)))
				{
					CopyPrefabStructure();
					// Clear the preview after copying to force a new preview for the next operation.
					_objectsToAddPreview = null;
				}
				GUI.enabled = true;
			}
			else
			{
				EditorGUILayout.HelpBox("Please assign both source and target prefabs.", MessageType.Info);
			}

			// Display preview information from the cached list.
			DisplayPreviewInfo();
		}

		private void PreviewChanges()
		{
			if (!ValidatePrefabs()) return;

			var sourceObjects = GetAllChildObjects(sourcePrefab);
			var targetObjects = GetAllChildObjects(targetPrefab);

			var sourceNames = new HashSet<string>(sourceObjects.Select(obj => GetRelativePath(sourcePrefab.transform, obj.transform)));
			var targetNames = new HashSet<string>(targetObjects.Select(obj => GetRelativePath(targetPrefab.transform, obj.transform)));

			_objectsToAddPreview = sourceNames.Except(targetNames).ToList();

			Debug.Log("Preview generated. See the Animation Copier window for details.");
		}

		private void CopyPrefabStructure()
		{
			if (!ValidatePrefabs()) return;

			string targetPath = AssetDatabase.GetAssetPath(targetPrefab);
			PrefabStage prefabStage = null;

			try
			{
				EditorUtility.DisplayProgressBar("Copying Prefab", "Opening prefab stage...", 0.1f);
				prefabStage = PrefabStageUtility.OpenPrefab(targetPath);

				if (prefabStage == null || prefabStage.prefabContentsRoot == null)
				{
					Debug.LogError("Failed to open target prefab in prefab mode.");
					return;
				}

				GameObject targetRoot = prefabStage.prefabContentsRoot;

				EditorUtility.DisplayProgressBar("Copying Prefab", "Copying hierarchy and components...", 0.4f);
				CopyGameObjectHierarchy(sourcePrefab, targetRoot);

				if (copyTimeflowSettings)
				{
					CopyTimeflowComponent(sourcePrefab, targetRoot);
				}

				EditorUtility.DisplayProgressBar("Copying Prefab", "Saving changes...", 0.9f);

				// CORRECTION: Reverted to the correct saving method for prefabs.
				// This takes the modified GameObject root and saves it over the original prefab asset.
				PrefabUtility.SaveAsPrefabAsset(targetRoot, targetPath);

				Debug.Log("Prefab structure copied successfully!");
			}
			catch (System.Exception e)
			{
				Debug.LogError($"Error copying prefab structure: {e.Message}\n{e.StackTrace}");
			}
			finally
			{
				// Ensures the progress bar is always cleared, even if an error occurs.
				EditorUtility.ClearProgressBar();

				// It's also good practice to explicitly close the stage when you are done.
				if (prefabStage != null)
				{
					StageUtility.GoToMainStage();
				}
			}
		}

		private void CopyGameObjectHierarchy(GameObject sourceRoot, GameObject targetRoot)
		{
			var sourceChildren = GetAllChildObjects(sourceRoot);
			var targetChildren = GetAllChildObjects(targetRoot);

			var targetMap = targetChildren.ToDictionary(child => GetRelativePath(targetRoot.transform, child.transform));

			// First pass: Create/identify all GameObjects
			foreach (var sourceChild in sourceChildren)
			{
				string relativePath = GetRelativePath(sourceRoot.transform, sourceChild.transform);

				if (!targetMap.TryGetValue(relativePath, out GameObject existingTargetChild))
				{
					// Object is new, create it.
					CreateNewGameObject(sourceChild, sourceRoot, targetRoot);
				}
			}

			// Build reference mapping after all GameObjects exist
			Dictionary<GameObject, GameObject> referenceMap = BuildReferenceMap(sourceRoot, targetRoot);

			// Second pass: Copy components with proper reference remapping
			foreach (var sourceChild in sourceChildren)
			{
				string relativePath = GetRelativePath(sourceRoot.transform, sourceChild.transform);

				if (targetMap.TryGetValue(relativePath, out GameObject existingTargetChild))
				{
					// Object exists, sync its components with reference remapping
					SyncComponentsWithReferenceMapping(sourceChild, existingTargetChild, referenceMap);
				}
				else
				{
					// Find the newly created object
					Transform targetTransform = FindChildByPath(targetRoot.transform, relativePath);
					if (targetTransform != null)
					{
						SyncComponentsWithReferenceMapping(sourceChild, targetTransform.gameObject, referenceMap);
					}
				}
			}
		}

		private Dictionary<GameObject, GameObject> BuildReferenceMap(GameObject sourceRoot, GameObject targetRoot)
		{
			var referenceMap = new Dictionary<GameObject, GameObject>();

			// Add root mapping
			referenceMap[sourceRoot] = targetRoot;

			// Add all children mappings
			var sourceChildren = GetAllChildObjects(sourceRoot);
			foreach (var sourceChild in sourceChildren)
			{
				string relativePath = GetRelativePath(sourceRoot.transform, sourceChild.transform);
				Transform targetTransform = FindChildByPath(targetRoot.transform, relativePath);
				if (targetTransform != null)
				{
					referenceMap[sourceChild] = targetTransform.gameObject;
				}
			}

			return referenceMap;
		}

		private void CreateNewGameObject(GameObject sourceObj, GameObject sourceRoot, GameObject targetRoot)
		{
			// Find parent in target hierarchy by path
			string parentPath = GetRelativePath(sourceRoot.transform, sourceObj.transform.parent);
			Transform targetParent = FindChildByPath(targetRoot.transform, parentPath);

			if (targetParent == null)
			{
				Debug.LogWarning($"Could not find parent path '{parentPath}' for '{sourceObj.name}'. Skipping.");
				return;
			}

			GameObject newObj = new(sourceObj.name);
			Undo.RegisterCreatedObjectUndo(newObj, "Create " + newObj.name);
			newObj.transform.SetParent(targetParent);

			// Copy transform values
			newObj.transform.localPosition = sourceObj.transform.localPosition;
			newObj.transform.localRotation = sourceObj.transform.localRotation;
			newObj.transform.localScale = sourceObj.transform.localScale;

			Debug.Log($"Created new GameObject: {GetRelativePath(targetRoot.transform, newObj.transform)}");
		}

		private void SyncComponents(GameObject source, GameObject target)
		{
			foreach (var sourceComp in source.GetComponents<Component>())
			{
				if (sourceComp is Transform) continue;

				System.Type compType = sourceComp.GetType();

				// Check if the target already has this component.
				var targetComp = target.GetComponent(compType);
				if (targetComp != null)
				{
					// If it exists, only overwrite if the user allows it.
					if (!preserveExistingComponents)
					{
						Undo.RecordObject(targetComp, "Overwrite Component");
						EditorUtility.CopySerialized(sourceComp, targetComp);
					}
				}
				else
				{
					// If it doesn't exist, add it and copy the values.
					Component newComp = Undo.AddComponent(target, compType);
					EditorUtility.CopySerialized(sourceComp, newComp);
				}
			}
		}

		private void SyncComponentsWithReferenceMapping(GameObject source, GameObject target, Dictionary<GameObject, GameObject> referenceMap)
		{
			foreach (var sourceComp in source.GetComponents<Component>())
			{
				if (sourceComp is Transform) continue;

				System.Type compType = sourceComp.GetType();

				// Check if the target already has this component.
				var targetComp = target.GetComponent(compType);
				if (targetComp != null)
				{
					// If it exists, only overwrite if the user allows it.
					if (!preserveExistingComponents)
					{
						Undo.RecordObject(targetComp, "Overwrite Component");
						CopyComponentWithReferenceMapping(sourceComp, targetComp, referenceMap);
					}
				}
				else
				{
					// If it doesn't exist, add it and copy the values.
					Component newComp = Undo.AddComponent(target, compType);
					CopyComponentWithReferenceMapping(sourceComp, newComp, referenceMap);
				}
			}
		}

		private void CopyComponentWithReferenceMapping(Component source, Component target, Dictionary<GameObject, GameObject> referenceMap)
		{
			// First copy normally
			EditorUtility.CopySerialized(source, target);

			// Then remap references using SerializedObject
			var serializedTarget = new SerializedObject(target);
			RemapReferences(serializedTarget, referenceMap);
			serializedTarget.ApplyModifiedProperties();
		}

		private void RemapReferences(SerializedObject serializedObject, Dictionary<GameObject, GameObject> referenceMap)
		{
			var property = serializedObject.GetIterator();
			bool enterChildren = true;

			while (property.Next(enterChildren))
			{
				enterChildren = true;

				if (property.propertyType == SerializedPropertyType.ObjectReference)
				{
					if (property.objectReferenceValue != null)
					{
						// Check if it's a GameObject reference that needs remapping
						if (property.objectReferenceValue is GameObject gameObjectRef)
						{
							if (referenceMap.TryGetValue(gameObjectRef, out GameObject mappedGameObject))
							{
								property.objectReferenceValue = mappedGameObject;
							}
						}
						// Check if it's a Component reference that needs remapping
						else if (property.objectReferenceValue is Component componentRef)
						{
							if (referenceMap.TryGetValue(componentRef.gameObject, out GameObject mappedGameObject))
							{
								// Find the equivalent component on the mapped GameObject
								var mappedComponent = mappedGameObject.GetComponent(componentRef.GetType());
								if (mappedComponent != null)
								{
									property.objectReferenceValue = mappedComponent;
								}
							}
						}
						// For Transform references, also remap
						else if (property.objectReferenceValue is Transform transformRef)
						{
							if (referenceMap.TryGetValue(transformRef.gameObject, out GameObject mappedGameObject))
							{
								property.objectReferenceValue = mappedGameObject.transform;
							}
						}
					}
				}
				// Don't enter children for these property types to avoid infinite loops
				else if (property.propertyType == SerializedPropertyType.String ||
						property.propertyType == SerializedPropertyType.Integer ||
						property.propertyType == SerializedPropertyType.Boolean ||
						property.propertyType == SerializedPropertyType.Float ||
						property.propertyType == SerializedPropertyType.Color ||
						property.propertyType == SerializedPropertyType.Vector2 ||
						property.propertyType == SerializedPropertyType.Vector3 ||
						property.propertyType == SerializedPropertyType.Vector4 ||
						property.propertyType == SerializedPropertyType.Quaternion ||
						property.propertyType == SerializedPropertyType.Enum)
				{
					enterChildren = false;
				}
			}
		}

		private void CopyTimeflowComponent(GameObject source, GameObject target)
		{
			var sourceTimeflow = source.GetComponent<Timeflow>();
			var targetTimeflow = target.GetComponent<Timeflow>();

			if (sourceTimeflow != null)
			{
				if (targetTimeflow != null)
				{
					Undo.RecordObject(targetTimeflow, "Copy Timeflow Settings");

					// Build reference map for timeflow component
					Dictionary<GameObject, GameObject> referenceMap = BuildReferenceMap(source, target);
					CopyComponentWithReferenceMapping(sourceTimeflow, targetTimeflow, referenceMap);

					Debug.Log("Timeflow component settings updated with proper reference remapping.");
				}
				else
				{
					Debug.LogError("Timeflow component does not exist on the target prefab");
				}
			}
		}

		private void DisplayPreviewInfo()
		{
			if (sourcePrefab == null || targetPrefab == null) return;

			GUILayout.Space(10);
			GUILayout.Label("Preview Information:", EditorStyles.boldLabel);

			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, EditorStyles.helpBox, GUILayout.Height(200));

			if (_objectsToAddPreview == null)
			{
				EditorGUILayout.LabelField("Press 'Preview Changes' to see what will be copied.");
			}
			else
			{
				EditorGUILayout.LabelField($"New objects to be created: {_objectsToAddPreview.Count}");
				if (_objectsToAddPreview.Count > 0) GUILayout.Space(5);

				foreach (var objName in _objectsToAddPreview.Take(20)) // Show first 20
				{
					EditorGUILayout.LabelField($"  • {objName}");
				}

				if (_objectsToAddPreview.Count > 1000)
				{
					EditorGUILayout.LabelField($"  ... and {_objectsToAddPreview.Count - 1000} more.");
				}

				GUILayout.Space(10);
				if (preserveExistingComponents)
					EditorGUILayout.LabelField("Note: Existing GameObjects and components will NOT be modified.", EditorStyles.wordWrappedLabel);
				else
					EditorGUILayout.LabelField("Note: Existing GameObjects and components WILL be overwritten.", EditorStyles.wordWrappedLabel);
			}

			EditorGUILayout.EndScrollView();
		}

		#region Helper Methods
		private bool ValidatePrefabs()
		{
			if (sourcePrefab == null || targetPrefab == null)
			{
				EditorUtility.DisplayDialog("Error", "Both source and target prefabs must be assigned.", "OK");
				return false;
			}

			if (!PrefabUtility.IsPartOfPrefabAsset(sourcePrefab) || !PrefabUtility.IsPartOfPrefabAsset(targetPrefab))
			{
				EditorUtility.DisplayDialog("Error", "Both objects must be prefab assets from the Project view.", "OK");
				return false;
			}

			if (sourcePrefab == targetPrefab)
			{
				EditorUtility.DisplayDialog("Error", "Source and Target prefabs cannot be the same.", "OK");
				return false;
			}

			return true;
		}

		private List<GameObject> GetAllChildObjects(GameObject parent)
		{
			var result = new List<GameObject>();
			var queue = new Queue<Transform>();
			queue.Enqueue(parent.transform);
			while (queue.Count > 0)
			{
				Transform current = queue.Dequeue();
				foreach (Transform child in current)
				{
					result.Add(child.gameObject);
					queue.Enqueue(child);
				}
			}
			return result;
		}

		private string GetRelativePath(Transform root, Transform target)
		{
			if (target == root || target == null) return "";
			var path = new System.Text.StringBuilder();
			var current = target;
			while (current != null && current != root)
			{
				path.Insert(0, "/" + current.name);
				current = current.parent;
			}
			return path.ToString().TrimStart('/');
		}

		private Transform FindChildByPath(Transform root, string path)
		{
			return string.IsNullOrEmpty(path) ? root : root.Find(path);
		}
		#endregion
	}
}

#endif
