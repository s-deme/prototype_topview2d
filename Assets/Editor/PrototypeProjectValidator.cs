#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VerdantBlade.EditorTools
{
    /// <summary>Fast pre-play check available from Unity's Verdant Blade menu.</summary>
    public static class PrototypeProjectValidator
    {
        private const string PrototypeScenePath = "Assets/Scenes/Prototype.unity";

        [MenuItem("Verdant Blade/Validate Prototype Setup")]
        public static void ValidateProject()
        {
            var issues = 0;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PrototypeScenePath) == null)
            {
                Debug.LogError("[Verdant Blade] Missing scene: " + PrototypeScenePath);
                issues++;
            }

            var bootstrapScripts = AssetDatabase.FindAssets("GameBootstrap t:MonoScript");
            if (bootstrapScripts.Length == 0)
            {
                Debug.LogError("[Verdant Blade] GameBootstrap.cs could not be found.");
                issues++;
            }

            var sceneIsInBuild = System.Array.Exists(EditorBuildSettings.scenes, scene => scene.enabled && scene.path == PrototypeScenePath);
            if (!sceneIsInBuild)
            {
                Debug.LogWarning("[Verdant Blade] Prototype scene is not enabled in Build Settings.");
                issues++;
            }

            if (issues == 0)
            {
                Debug.Log("[Verdant Blade] Validation passed. Open Prototype.unity and press Play.");
            }
            else
            {
                Debug.LogWarning("[Verdant Blade] Validation finished with " + issues + " issue(s). See messages above.");
            }
        }
    }
}
#endif
