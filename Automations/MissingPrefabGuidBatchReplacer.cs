#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace HERMIT_SCRIPTS
{
    public class MissingPrefabGuidBatchReplacer : EditorWindow
    {
        private class MissingPrefabData
        {
            public GameObject missingObj;
            public string extractedGuid;
            public GameObject replacementPrefab;
        }
    
        private List<MissingPrefabData> missingPrefabs = new List<MissingPrefabData>();
        private Vector2 scrollPos;
    
        [MenuItem("Hermitcrab/Batch Replace Missing Prefabs GUID")]
        public static void ShowWindow()
        {
            GetWindow<MissingPrefabGuidBatchReplacer>("Batch Prefab GUID Replacer");
        }
    
        private void OnEnable()
        {
            ScanSceneForMissingPrefabs();
        }
    
        private void OnGUI()
        {
            if (GUILayout.Button("🔄 Rescan Scene"))
            {
                ScanSceneForMissingPrefabs();
            }
    
            if (missingPrefabs.Count == 0)
            {
                EditorGUILayout.HelpBox("No missing prefabs found in the scene!", MessageType.Info);
                return;
            }
    
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Missing Prefabs Found:", EditorStyles.boldLabel);
    
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
    
            for (int i = 0; i < missingPrefabs.Count; i++)
            {
                var data = missingPrefabs[i];
                EditorGUILayout.BeginHorizontal("box");
    
                // Missing object column
                EditorGUILayout.ObjectField(data.missingObj, typeof(GameObject), true, GUILayout.Width(200));
    
                // GUID label + Copy button
                EditorGUILayout.LabelField("GUID: " + data.extractedGuid, GUILayout.Width(260));
                if (GUILayout.Button("📋 Copy GUID", GUILayout.Width(90)))
                {
                    EditorGUIUtility.systemCopyBuffer = data.extractedGuid;
                    Debug.Log($"GUID {data.extractedGuid} copied to clipboard.");
                }
    
                // Replacement prefab field
                data.replacementPrefab = (GameObject)EditorGUILayout.ObjectField("Replacement Prefab", data.replacementPrefab, typeof(GameObject), false);
    
                EditorGUILayout.EndHorizontal();
            }
    
            EditorGUILayout.EndScrollView();
    
            EditorGUILayout.Space();
            if (GUILayout.Button("✅ Replace GUIDs"))
            {
                ReplaceAllGuids();
            }
        }
    
        private void ScanSceneForMissingPrefabs()
        {
            missingPrefabs.Clear();
            var allObjects = GameObject.FindObjectsOfType<GameObject>();
    
            foreach (var obj in allObjects)
            {
                if (obj.name.Contains("guid:"))
                {
                    Match match = Regex.Match(obj.name, @"guid:\s*([0-9a-f]{32})", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        missingPrefabs.Add(new MissingPrefabData
                        {
                            missingObj = obj,
                            extractedGuid = match.Groups[1].Value
                        });
                    }
                }
            }
            Repaint();
        }
    
        private void ReplaceAllGuids()
        {
            foreach (var data in missingPrefabs)
            {
                if (data.replacementPrefab == null) continue;
    
                string path = AssetDatabase.GetAssetPath(data.replacementPrefab);
                string metaPath = path + ".meta";
    
                if (!File.Exists(metaPath))
                {
                    Debug.LogError($"Meta file not found for {data.replacementPrefab.name}");
                    continue;
                }
    
                string metaContent = File.ReadAllText(metaPath);
                metaContent = Regex.Replace(metaContent, @"guid:\s*[0-9a-f]{32}", "guid: " + data.extractedGuid);
                File.WriteAllText(metaPath, metaContent);
    
                Debug.Log($"✅ GUID of prefab {data.replacementPrefab.name} has been replaced with {data.extractedGuid}");
            }
    
            AssetDatabase.Refresh();
        }
    }
}
#endif