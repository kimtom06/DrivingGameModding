#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MobileModSystem.Editor
{
    /// <summary>
    /// Automatically includes Built-in Standard in the player by keeping a tiny
    /// reference material under Resources. Does not change GraphicsSettings or
    /// strip other shaders automatically (those might be used by other systems).
    /// </summary>
    [InitializeOnLoad]
    public static class ModShaderRetentionSetup
    {
        private const string MaterialPath = "Assets/Resources/MobileModSystem/StandardShaderKeep.mat";

        static ModShaderRetentionSetup()
        {
            EditorApplication.delayCall += EnsureReferenceMaterial;
        }

        [MenuItem("Tools/Mobile Mod System/Ensure Standard Shader Is In Build")]
        public static void EnsureReferenceMaterial()
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogWarning("MobileModSystem: Built-in Standard shader not found. This setup is for Built-in RP only.");
                return;
            }
            var reference = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (reference != null)
            {
                if (reference.shader != shader)
                {
                    reference.shader = shader;
                    EditorUtility.SetDirty(reference);
                    AssetDatabase.SaveAssets();
                }
                return;
            }
            Directory.CreateDirectory("Assets/Resources/MobileModSystem");
            AssetDatabase.Refresh();
            reference = new Material(shader) { name = "StandardShaderKeep" };
            AssetDatabase.CreateAsset(reference, MaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log("MobileModSystem: Included Built-in Standard via " + MaterialPath);
        }
    }
}
#endif
