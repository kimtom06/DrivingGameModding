using System;
using System.Collections.Generic;
using GLTFast;
using GLTFast.Logging;
using GLTFast.Materials;
using Material = UnityEngine.Material;
using MaterialBase = GLTFast.Schema.MaterialBase;
using TextureInfoBase = GLTFast.Schema.TextureInfoBase;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MobileModSystem
{
    /// <summary>
    /// glTFast IMaterialGenerator: directly outputs Built-in Standard materials.
    /// As a result glTF's heavyweight PBR shaders are never selected by this importer.
    /// A generator instance is used for ONE GLB load and released with that model.
    /// </summary>
    public sealed class RuntimeStandardMaterialGenerator : IMaterialGenerator, IDisposable
    {
        private readonly Shader shader;
        private readonly bool repackMetallic;
        private readonly bool repackAo;
        private readonly Dictionary<string, Texture2D> packedTextures = new Dictionary<string, Texture2D>();
        private Material defaultMaterial;
        private bool disposed;

        public RuntimeStandardMaterialGenerator(Shader shader, bool repackMetallic = true, bool repackAo = true)
        {
            RuntimeStandardMaterialConverter.ValidateShader(shader);
            this.shader = shader;
            this.repackMetallic = repackMetallic;
            this.repackAo = repackAo;
        }

        public void SetLogger(ICodeLogger logger) { }

        public Material GetDefaultMaterial(bool pointsSupport = false)
        {
            if (defaultMaterial == null)
                defaultMaterial = new Material(shader) { name = "GLB Default Standard" };
            return defaultMaterial;
        }

        public Material GenerateMaterial(MaterialBase gltfMaterial, IGltfReadable gltf, bool pointsSupport = false)
        {
            if (disposed) throw new ObjectDisposedException(nameof(RuntimeStandardMaterialGenerator));
            return RuntimeStandardMaterialConverter.GenerateDirect(
                gltfMaterial, gltf, shader, repackMetallic, repackAo,
                (tex, m, r, mergeAo) => GetPacked(tex, mergeAo ? 3 : 0, m, r),
                (tex, m, gloss, mergeAo) => GetPacked(tex, mergeAo ? 4 : 1, m, gloss),
                tex => GetPacked(tex, 2, 1f, 1f));
        }

        private Texture2D GetPacked(Texture texture, int kind, float a, float b)
        {
            // A mod can reuse one image on multiple materials but apply different
            // factors. Include the factors in the cache key so they don't collide.
            string key = texture.GetInstanceID() + ":" + kind + ":" + a.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                         ":" + b.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (!packedTextures.TryGetValue(key, out var result))
            {
                result = RuntimeStandardMaterialConverter.PackMap(texture, kind, a, b);
                packedTextures.Add(key, result);
            }
            return result;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var map in packedTextures.Values)
                if (map != null) Object.Destroy(map);
            packedTextures.Clear();
            if (defaultMaterial != null) Object.Destroy(defaultMaterial);
            defaultMaterial = null;
        }
    }
}
