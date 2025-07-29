#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace HERMIT_SCRIPTS
{
    public class SceneScriptAnalyzer : EditorWindow
    {
        private Vector2 scrollPosition;
        private Dictionary<string, List<string>> scriptsByFolder = new Dictionary<string, List<string>>();
        private bool showFullPaths = false;
        private bool groupByFolder = true;

        [MenuItem("Hermitcrab/Scene Script Analyzer")]
        public static void ShowWindow()
        {
            GetWindow<SceneScriptAnalyzer>("Scene Script Analyzer");
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.Space();

            // Options
            EditorGUILayout.BeginHorizontal();
            showFullPaths = EditorGUILayout.Toggle("Show Full Paths", showFullPaths);
            groupByFolder = EditorGUILayout.Toggle("Group By Folder", groupByFolder);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (GUILayout.Button("Analyze Current Scene", GUILayout.Height(30)))
            {
                AnalyzeScene();
            }

            EditorGUILayout.Space();

            if (scriptsByFolder.Count > 0)
            {
                DisplayResults();
            }

            EditorGUILayout.EndVertical();
        }

        private void AnalyzeScene()
        {
            scriptsByFolder.Clear();

            // Get all GameObjects in the scene
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            HashSet<string> uniqueScripts = new HashSet<string>();

            foreach (GameObject obj in allObjects)
            {
                // Get all MonoBehaviour components
                MonoBehaviour[] scripts = obj.GetComponents<MonoBehaviour>();

                foreach (MonoBehaviour script in scripts)
                {
                    if (script == null) continue; // Skip missing scripts

                    // Get the script's type
                    System.Type scriptType = script.GetType();

                    // Find the script file path
                    string scriptPath = GetScriptPath(scriptType);

                    if (!string.IsNullOrEmpty(scriptPath))
                    {
                        uniqueScripts.Add(scriptPath);
                    }
                }
            }

            // Organize scripts by folder
            foreach (string scriptPath in uniqueScripts)
            {
                string folderPath = Path.GetDirectoryName(scriptPath);
                string fileName = Path.GetFileName(scriptPath);

                if (string.IsNullOrEmpty(folderPath))
                    folderPath = "Root";

                if (!scriptsByFolder.ContainsKey(folderPath))
                    scriptsByFolder[folderPath] = new List<string>();

                scriptsByFolder[folderPath].Add(fileName);
            }

            // Sort folders and scripts within each folder
            var sortedFolders = scriptsByFolder.Keys.OrderBy(k => k).ToList();
            var newDict = new Dictionary<string, List<string>>();

            foreach (string folder in sortedFolders)
            {
                newDict[folder] = scriptsByFolder[folder].OrderBy(s => s).ToList();
            }

            scriptsByFolder = newDict;

            Debug.Log(
                $"Scene analysis complete. Found {uniqueScripts.Count} unique scripts across {scriptsByFolder.Count} folders.");
        }

        private string GetScriptPath(System.Type scriptType)
        {
            // Try to find the script file using Unity's AssetDatabase
            string[] guids = AssetDatabase.FindAssets($"t:MonoScript {scriptType.Name}");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (monoScript != null && monoScript.GetClass() == scriptType)
                {
                    return path;
                }
            }

            return null;
        }

        private void DisplayResults()
        {
            EditorGUILayout.LabelField("Scene Scripts Analysis", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (groupByFolder)
            {
                DisplayGroupedByFolder();
            }
            else
            {
                DisplayFlattened();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            // Summary
            int totalScripts = scriptsByFolder.Values.Sum(list => list.Count);
            EditorGUILayout.LabelField($"Total Scripts: {totalScripts}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Total Folders: {scriptsByFolder.Count}", EditorStyles.miniLabel);
        }

        private void DisplayGroupedByFolder()
        {
            foreach (var kvp in scriptsByFolder)
            {
                string folderPath = kvp.Key;
                List<string> scripts = kvp.Value;

                // Folder header
                EditorGUILayout.BeginVertical("box");

                string displayPath = showFullPaths ? folderPath : GetShortFolderName(folderPath);
                EditorGUILayout.LabelField($"📁 {displayPath} ({scripts.Count} scripts)", EditorStyles.boldLabel);

                EditorGUILayout.Space(5);

                // Scripts in this folder
                foreach (string script in scripts)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("    📄", GUILayout.Width(30));
                    EditorGUILayout.LabelField(script);
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }

        private void DisplayFlattened()
        {
            EditorGUILayout.BeginVertical("box");

            foreach (var kvp in scriptsByFolder)
            {
                string folderPath = kvp.Key;
                List<string> scripts = kvp.Value;

                foreach (string script in scripts)
                {
                    EditorGUILayout.BeginHorizontal();

                    if (showFullPaths)
                    {
                        EditorGUILayout.LabelField($"{folderPath}/{script}");
                    }
                    else
                    {
                        EditorGUILayout.LabelField(script);
                        EditorGUILayout.LabelField($"({GetShortFolderName(folderPath)})", EditorStyles.miniLabel);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private string GetShortFolderName(string fullPath)
        {
            if (fullPath == "Root") return "Root";

            string[] parts = fullPath.Split('/');
            if (parts.Length > 2)
            {
                return ".../" + string.Join("/", parts.Skip(parts.Length - 2));
            }

            return fullPath;
        }
    }
}
#endif
