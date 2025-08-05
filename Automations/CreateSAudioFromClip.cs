//Only works on the new project
#if UNITY_2022_1_OR_NEWER && UNITY_EDITOR

using Prisms.Audio;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HERMIT_SCRIPTS
{
	public class CreateSAudioFromClip
	{
		[MenuItem("Assets/Create SAudio from AudioClip", true)]
		private static bool ValidateCreateSAudio()
		{
			// Only show the menu item if the selected object is an AudioClip  
			Object[] selection = Selection.objects;
			return selection.All((obj) => obj.GetType() == typeof(AudioClip));
		}

		[MenuItem("Assets/Create SAudio from AudioClip", false, 100)]
		private static void CreateSAudio()
		{
			AudioClip[] selectedClip = Selection.objects.OfType<AudioClip>().ToArray();

			if (selectedClip == null)
			{
				Debug.LogError("Selected object is not an AudioClip!");
				return;
			}

			// Get the path of the selected audio clip  
			string assetPath = AssetDatabase.GetAssetPath(selectedClip[0]);
			string directory = Path.GetDirectoryName(assetPath);
			string fileName = Path.GetFileNameWithoutExtension(assetPath);

			// Create the SAudio asset path  
			string sAudioPath = Path.Combine(directory, fileName + ".asset");

			// Make sure the path is unique (in case an asset with the same name already exists)  
			sAudioPath = AssetDatabase.GenerateUniqueAssetPath(sAudioPath);

			// Create the SAudio scriptable object  
			SAudio sAudio = ScriptableObject.CreateInstance<SAudio>();
			AudioClipType clipType = selectedClip.Length > 1 ? AudioClipType.Random : AudioClipType.Single;
			sAudio.SetData(clipType, false, MixerGroupType.SFX);
			sAudio.SetClips(selectedClip);

			// Save the asset  
			AssetDatabase.CreateAsset(sAudio, sAudioPath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			// Select the newly created asset  
			Selection.activeObject = sAudio;
			EditorGUIUtility.PingObject(sAudio);

			Debug.Log($"Created SAudio asset with type Random: {sAudioPath}");
		}
	}
}

#endif
