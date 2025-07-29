#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Sirenix.Utilities;
using TexDrawLib;

namespace HERMIT_SCRIPTS
{
    public class TEXDraw3DConverterTool : EditorWindow
    {
        private List<GameObject> texObjects = new List<GameObject>();
        private TMP_FontAsset targetFontAsset;
        private bool removeTEXDraw = true;
        private bool useAutoSize = false;
        private Vector2 scroll;
        private GameObject manualObjectToAdd;

        [MenuItem("Hermitcrab/Converter TEXDraw3D para TMP (Avançado)")]
        public static void ShowWindow()
        {
            GetWindow<TEXDraw3DConverterTool>("TEXDraw3D → TMP");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Conversor TEXDraw3D para TextMeshPro", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            targetFontAsset =
                (TMP_FontAsset)EditorGUILayout.ObjectField("Font Asset (TMP)", targetFontAsset, typeof(TMP_FontAsset),
                    false);
            removeTEXDraw = EditorGUILayout.Toggle("Excluir TEXDraw3D?", removeTEXDraw);
            useAutoSize = EditorGUILayout.Toggle("Usar AutoSize no TMP?", useAutoSize);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Adicionar manualmente GameObject com TEXDraw3D:");
            EditorGUILayout.BeginHorizontal();
            manualObjectToAdd = (GameObject)EditorGUILayout.ObjectField(manualObjectToAdd, typeof(GameObject), true);
            if (GUILayout.Button("Adicionar", GUILayout.Width(80)))
            {
                if (manualObjectToAdd != null && manualObjectToAdd.GetComponent("TEXDraw3D") != null &&
                    !texObjects.Contains(manualObjectToAdd))
                {
                    texObjects.Add(manualObjectToAdd);
                    manualObjectToAdd = null;
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            if (GUILayout.Button("Adicionar todos da cena"))
            {
                AddAllFromScene();
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(200));
            for (int i = 0; i < texObjects.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                texObjects[i] = (GameObject)EditorGUILayout.ObjectField(texObjects[i], typeof(GameObject), true);
                if (GUILayout.Button("Remover", GUILayout.Width(70)))
                {
                    texObjects.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Converter selecionados"))
            {
                ConvertList();
            }

            if (GUILayout.Button("Limpar lista"))
            {
                texObjects.Clear();
            }

            if (GUILayout.Button("Remover todos TEXDraw3D da lista"))
            {
                RemoveAllTexDraw3D();
            }
        }

        private void RemoveAllTexDraw3D()
        {
            if (texObjects.IsNullOrEmpty())
            {
                Debug.LogWarning("A lista de objetos TEXDraw3D está vazia.");
                return;
            }

            foreach (var obj in texObjects)
            {
                if (obj == null) continue;

                Component texDraw = obj.GetComponent("TEXDraw3D");
                if (texDraw != null)
                    DestroyImmediate(texDraw);
            }

            texObjects.Clear();
            Debug.Log("Todos os componentes TEXDraw3D da lista foram removidos.");
        }

        private void AddAllFromScene()
        {
            var allObjects = FindObjectsOfType<Transform>(true);
            foreach (var t in allObjects)
            {
                var comp = t.GetComponent("TEXDraw3D");
                if (comp != null && !texObjects.Contains(t.gameObject))
                {
                    texObjects.Add(t.gameObject);
                }
            }

            Debug.Log($"Adicionados {texObjects.Count} objetos com TEXDraw3D à lista.");
        }

        private void ConvertList()
        {
            if (targetFontAsset == null)
            {
                Debug.LogWarning("Você precisa selecionar um TMP Font Asset antes de converter.");
                return;
            }

            int converted = 0;

            foreach (var obj in texObjects)
            {
                if (obj == null) continue;

                Component texDraw = obj.GetComponent("TEXDraw3D");
                if (texDraw == null) continue;

                string originalText = GetTextFromTEXDraw(texDraw);

                if (removeTEXDraw)
                {
                    DestroyImmediate(texDraw);
                }
                else if (texDraw is MonoBehaviour mono)
                {
                    mono.enabled = false;
                }

                TextMeshPro tmp = obj.GetComponent<TextMeshPro>();
                if (tmp == null)
                {
                    tmp = obj.AddComponent<TextMeshPro>();
                }

                tmp.richText = true;
                tmp.enableWordWrapping = false;
                tmp.text = ConvertTexToRichText(originalText);
                tmp.font = targetFontAsset;
                tmp.fontMaterial = targetFontAsset.material;
                tmp.enableAutoSizing = useAutoSize;
                tmp.fontSize = tmp.fontSize * (texDraw as TEXDraw3D).size / 3f;
                tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
                tmp.verticalAlignment = VerticalAlignmentOptions.Middle;

                converted++;
            }

            Debug.Log($"Conversão finalizada: {converted} objetos convertidos.");
        }

        private string GetTextFromTEXDraw(Component texDraw)
        {
            var textProp = texDraw.GetType().GetProperty("text");
            if (textProp != null)
            {
                return textProp.GetValue(texDraw) as string;
            }

            return string.Empty;
        }

        private string ConvertTexToRichText(string tex)
        {
            if (string.IsNullOrEmpty(tex))
                return "";

            // === Greek letters ===
            tex = Regex.Replace(tex, @"\\Delta", "Δ");
            tex = Regex.Replace(tex, @"\\alpha", "α");
            tex = Regex.Replace(tex, @"\\beta", "β");
            tex = Regex.Replace(tex, @"\\gamma", "γ");

            // === Bold, italic, underline, strikethrough ===
            tex = Regex.Replace(tex, @"\\textbf\{(.+?)\}", "<b>$1</b>");
            tex = Regex.Replace(tex, @"\\textit\{(.+?)\}", "<i>$1</i>");
            tex = Regex.Replace(tex, @"\\underline\{(.+?)\}", "<u>$1</u>");
            tex = Regex.Replace(tex, @"\\sout\{(.+?)\}", "<s>$1</s>");

            // === Subscript and superscript ===
            tex = Regex.Replace(tex, @"_([^\s\^_{}]+)", "<sub>$1</sub>");
            tex = Regex.Replace(tex, @"\^([^\s\^_{}]+)", "<sup>$1</sup>");

            // === Color {hex}{content} ===
            tex = Regex.Replace(tex,
                @"\\color\{#?([A-Fa-f0-9]{6,8})\}\{(.+?)\}",
                "<color=#$1>$2</color>");

            // === Font size {size}{content} ===
            tex = Regex.Replace(tex,
                @"\\fontsize\{(\d+)\}\{(.+?)\}",
                "<size=$1>$2</size>");

            // === Clean up stray braces ===
            tex = tex.Replace("{", "").Replace("}", "");

            // === Legacy color fallback ===
            if (tex.Contains("<color=") && !tex.Contains("</color>"))
                tex += "</color>";

            return tex;
        }
    }
}
#endif
