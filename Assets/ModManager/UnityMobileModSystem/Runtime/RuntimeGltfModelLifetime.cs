using GLTFast;
using System.Collections.Generic;
using UnityEngine;

namespace MobileModSystem
{
    /// <summary>
    /// Keeps glTFast mesh/texture/material resources alive while the instantiated
    /// hierarchy exists. Disposes them when that hierarchy is replaced or destroyed.
    /// DO NOT dispose the GltfImport immediately after InstantiateMainSceneAsync:
    /// doing so can invalidate meshes and textures still used by the model.
    /// </summary>
    public sealed class RuntimeGltfModelLifetime : MonoBehaviour
    {
        private GltfImport import;
        private RuntimeStandardMaterialGenerator generator;

        public void TakeOwnership(GltfImport loadedImport, RuntimeStandardMaterialGenerator loadedGenerator)
        {
            import = loadedImport;
            generator = loadedGenerator;
        }

        public void Release()
        {
            if (import != null)
            {
                import.Dispose();
                import = null;
            }
            if (generator != null)
            {
                generator.Dispose();
                generator = null;
            }
        }

        private void OnDestroy() { Release(); }
    }

    /// <summary>Owns a PNG/JPEG loaded separately via ImportTextureAsync.</summary>
    public sealed class RuntimeImportedTextureLifetime : MonoBehaviour
    {
        public Texture2D Texture { get; set; }
        private void OnDestroy()
        {
            if (Texture != null)
            {
                Destroy(Texture);
                Texture = null;
            }
        }
    }

    /// <summary>Owns materials/textures produced by the legacy post-import converter.</summary>
    public sealed class RuntimeConvertedMaterialLifetime : MonoBehaviour
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        public void Own(UnityEngine.Object resource)
        {
            if (resource != null && !owned.Contains(resource)) owned.Add(resource);
        }

        private void OnDestroy()
        {
            foreach (var resource in owned)
                if (resource != null) Destroy(resource);
            owned.Clear();
        }
    }

}
