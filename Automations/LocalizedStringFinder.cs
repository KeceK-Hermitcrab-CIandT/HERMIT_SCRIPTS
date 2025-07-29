#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using UnityEngine.Localization.PropertyVariants;
#if UNITY_2022_3_OR_NEWER
using Prisms.Layout;
using UnityEngine.Localization.Components;
#endif

namespace HERMIT_SCRIPTS
{
    public class LocalizedStringFinder : EditorWindow
    {
        [MenuItem("Hermitcrab/Localized Strings Finder")]
        public static void Open() => GetWindow<LocalizedStringFinder>("Localized Strings Finder");

        SerializedObject so;
        SerializedProperty parentProp;
        TreeViewState treeState;
        LocalizedChildTree tree;
        Dictionary<int, GameObject> map = new();

        [SerializeField] GameObject parent;
        private Editor componentEditor;

        void OnEnable()
        {
            so = new SerializedObject(this);
            parentProp = so.FindProperty(nameof(parent));
            if (treeState == null) treeState = new TreeViewState();
            tree = new LocalizedChildTree(treeState, map, this);
        }

        public void OnComponentSelected(Component comp)
        {
            componentEditor = comp != null
                ? Editor.CreateEditor(comp)
                : null;
            Repaint();
        }

        void OnGUI()
        {
            so.Update();
            EditorGUILayout.PropertyField(parentProp);
            so.ApplyModifiedProperties();

            if (GUILayout.Button("Scan"))
                ReloadTree();

            Rect rect = GUILayoutUtility.GetRect(0, 100000, 0, 100000);
            tree.OnGUI(rect);

            if (componentEditor != null)
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField("Component Preview", EditorStyles.boldLabel);
                componentEditor.OnInspectorGUI();
            }
        }

        void ReloadTree()
        {
            map.Clear();
            tree.SetRoot(parent);
            tree.Reload();
        }
    }

    class LocalizedChildTree : TreeView
    {
        Dictionary<int, GameObject> map;
        GameObject rootGO;
        int nextId = 1;
        private LocalizedStringFinder window;

        public LocalizedChildTree(TreeViewState state, Dictionary<int, GameObject> map, LocalizedStringFinder window)
            : base(state)
        {
            this.map = map;
            this.window = window;
            showBorder = true;
        }

        public void SetRoot(GameObject root)
        {
            rootGO = root;
        }

        protected override TreeViewItem BuildRoot()
        {
            nextId = 1; // reset counter
            var root = new TreeViewItem { id = 0, depth = -1 };

            if (rootGO != null)
            {
                var parentItem = new TreeViewItem { id = nextId++, depth = 0, displayName = rootGO.name };
                root.AddChild(parentItem);
                AddChildrenRecursive(rootGO, parentItem, 1);
            }

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        void AddChildrenRecursive(GameObject go, TreeViewItem parentItem, int depth)
        {
            foreach (Transform t in go.transform)
            {
                GameObject child = t.gameObject;

                bool hasLocalizer = child.GetComponent<GameObjectLocalizer>() != null;
                bool useTextBlock = false;
#if UNITY_2022_3_OR_NEWER
            var tb = child.GetComponent<TextBlock>();
            useTextBlock = tb != null && tb.UseLocalizedString && tb.LocalizedString != null;
#endif

                if (hasLocalizer || useTextBlock)
                {
                    string nameDisplay = child.name;
#if UNITY_2022_3_OR_NEWER
                if (useTextBlock)
                    nameDisplay += $" [{tb.LocalizedString.TableEntryReference.Key}]";
#endif
                    var item = new TreeViewItem
                    {
                        id = nextId++,
                        depth = depth,
                        displayName = nameDisplay
                    };
                    parentItem.AddChild(item);
                    map[item.id] = child;
                }

                AddChildrenRecursive(child, parentItem, depth + 1);
            }
        }

        protected override void SingleClickedItem(int id)
        {
            if (map.TryGetValue(id, out var go))
            {
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);

                Component comp = GetRelevantComponent(go);
                window.OnComponentSelected(comp);
            }
        }

        private Component GetRelevantComponent(GameObject go)
        {
#if UNITY_2022_3_OR_NEWER
        var tb = go.GetComponent<TextBlock>();
        if (tb != null && tb.UseLocalizedString && tb.LocalizedString != null)
            return tb;
#endif
            return go.GetComponent<GameObjectLocalizer>();
        }
    }
}
#endif