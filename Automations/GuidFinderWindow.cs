using UnityEditor;
using UnityEngine;

/// <summary>
    /// Window to find an asset by its GUID and parent folder path.
    /// </summary>
    public class GuidFinderWindow : EditorWindow
    {
        private string _guidInput = "";
        private string _parentFolderPath = "Assets/";
        private string _foundAssetPath = "";
        private Object _foundAsset = null;

        [MenuItem("Hermitcrab/GUID Finder")]
        public static void ShowWindow() => GetWindow<GuidFinderWindow>("GUID Finder");

        private void OnGUI()
        {
            GUILayout.Label("Find asset by GUID and folder", EditorStyles.boldLabel);
            _guidInput = EditorGUILayout.TextField("GUID:", _guidInput);
            _parentFolderPath = EditorGUILayout.TextField("Parent Folder Path (Assets/...): ", _parentFolderPath);

            if (GUILayout.Button("Find"))
            {
                FindByGuid();
            }

            if (!string.IsNullOrEmpty(_foundAssetPath))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Found Asset Path:", _foundAssetPath);
                EditorGUILayout.ObjectField("Asset:", _foundAsset, typeof(Object), false);
                if (_foundAsset != null)
                {
                    if(GUILayout.Button("Ping in Project"))
                    {
                        EditorGUIUtility.PingObject(_foundAsset);
                    }
                }
            }
        }
        
        private void FindByGuid()
        {
            _foundAssetPath = AssetDatabase.GUIDToAssetPath(_guidInput);
            if (string.IsNullOrEmpty(_foundAssetPath))
            {
                Debug.LogWarning($"No asset found with GUID: {_guidInput}");
                _foundAsset = null;
            }
            else if (!_foundAssetPath.StartsWith(_parentFolderPath)) {
                Debug.LogWarning($"Asset found, but does not start with \"{_parentFolderPath}\" – full path: {_foundAssetPath}");
                _foundAsset = AssetDatabase.LoadAssetAtPath<Object>(_foundAssetPath);
            }
            else {
                _foundAsset = AssetDatabase.LoadAssetAtPath<Object>(_foundAssetPath);
                Debug.Log($"Found asset: {_foundAssetPath} → {_foundAsset.name}", _foundAsset);
            }
        }
    }
