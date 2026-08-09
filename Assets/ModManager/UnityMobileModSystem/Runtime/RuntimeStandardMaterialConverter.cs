using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MobileModSystem
{
    /// <summary>
    /// Converts materials created by glTFast to Unity's Built-in Render Pipeline Standard shader.
    /// This is intended for Built-in Render Pipeline projects only.
    /// </summary>
    public static class RuntimeStandardMaterialConverter
    {
        private static readonly string[] BaseTextureProperties =
        {
            "baseColorTexture", "_BaseColorTexture", "diffuseTexture", "_BaseMap", "_MainTex"
        };

        private static readonly string[] BaseColorProperties =
        {
            "baseColorFactor", "_BaseColorFactor", "diffuseFactor", "_BaseColor", "_Color"
        };

        private static readonly string[] MetallicProperties =
        {
            "metallicFactor", "_MetallicFactor", "_Metallic"
        };

        private static readonly string[] RoughnessProperties =
        {
            "roughnessFactor", "_RoughnessFactor", "_Roughness"
        };

        private static readonly string[] SmoothnessProperties =
        {
            "glossinessFactor", "_GlossinessFactor", "_Smoothness", "_Glossiness"
        };

        private static readonly string[] MetallicRoughnessTextureProperties =
        {
            "metallicRoughnessTexture", "_MetallicRoughnessTexture", "_MetallicGlossMap"
        };

        private static readonly string[] NormalTextureProperties =
        {
            "normalTexture", "_NormalTexture", "_NormalMap", "_BumpMap"
        };

        private static readonly string[] NormalScaleProperties =
        {
            "normalTexture_scale", "_NormalScale", "_BumpScale"
        };

        private static readonly string[] OcclusionTextureProperties =
        {
            "occlusionTexture", "_OcclusionTexture", "_OcclusionMap"
        };

        private static readonly string[] OcclusionStrengthProperties =
        {
            "occlusionTexture_strength", "_OcclusionStrength"
        };

        private static readonly string[] EmissionTextureProperties =
        {
            "emissiveTexture", "_EmissiveTexture", "_EmissionMap"
        };

        private static readonly string[] EmissionColorProperties =
        {
            "emissiveFactor", "_EmissiveFactor", "_EmissionColor"
        };

        private static readonly string[] EmissionStrengthProperties =
        {
            "emissiveStrength",
            "_EmissiveStrength",
            "emissionStrength",
            "_EmissionStrength"
        };

        private const float EmissionEpsilon = 0.0001f;
        private const float MinimumEmissionExposure = 1f;

        private static readonly string[] AlphaCutoffProperties =
        {
            "alphaCutoff", "_AlphaCutoff", "_Cutoff"
        };

        private static readonly string[] CullProperties =
        {
            "_CullMode", "_Cull"
        };

        public static int ConvertHierarchy(
            GameObject hierarchyRoot,
            Shader standardShader,
            bool repackMetallicRoughnessTexture = true,
            bool repackOcclusionTexture = true)
        {
            if (hierarchyRoot == null)
                throw new ArgumentNullException(nameof(hierarchyRoot));

            if (standardShader == null)
                throw new ArgumentNullException(nameof(standardShader));

            if (!standardShader.isSupported)
            {
                throw new NotSupportedException(
                    "The assigned Standard shader is not supported by the active render pipeline. " +
                    "Unity's Standard shader can only be used with the Built-in Render Pipeline.");
            }

            Dictionary<Material, Material> convertedMaterials =
                new Dictionary<Material, Material>();

            Dictionary<Texture, Texture2D> packedMetallicMaps =
                new Dictionary<Texture, Texture2D>();

            Dictionary<Texture, Texture2D> packedOcclusionMaps =
                new Dictionary<Texture, Texture2D>();

            Renderer[] renderers = hierarchyRoot.GetComponentsInChildren<Renderer>(true);
            int convertedCount = 0;

            foreach (Renderer targetRenderer in renderers)
            {
                Material[] sourceMaterials = targetRenderer.sharedMaterials;
                if (sourceMaterials == null || sourceMaterials.Length == 0)
                    continue;

                Material[] destinationMaterials = new Material[sourceMaterials.Length];
                bool changed = false;

                for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
                {
                    Material source = sourceMaterials[materialIndex];
                    if (source == null)
                    {
                        destinationMaterials[materialIndex] = null;
                        continue;
                    }

                    if (source.shader == standardShader)
                    {
                        ForceTextureTilingOne(source);
                        destinationMaterials[materialIndex] = source;
                        continue;
                    }

                    if (!convertedMaterials.TryGetValue(source, out Material converted))
                    {
                        converted = ConvertMaterial(
                            source,
                            standardShader,
                            repackMetallicRoughnessTexture,
                            repackOcclusionTexture,
                            packedMetallicMaps,
                            packedOcclusionMaps);

                        convertedMaterials.Add(source, converted);
                        convertedCount++;
                    }

                    destinationMaterials[materialIndex] = converted;
                    changed = true;
                }

                if (changed)
                    targetRenderer.sharedMaterials = destinationMaterials;
            }

            return convertedCount;
        }

        private static Material ConvertMaterial(
            Material source,
            Shader standardShader,
            bool repackMetallicRoughnessTexture,
            bool repackOcclusionTexture,
            Dictionary<Texture, Texture2D> packedMetallicMaps,
            Dictionary<Texture, Texture2D> packedOcclusionMaps)
        {
            Material destination = new Material(standardShader)
            {
                name = source.name + "_Standard"
            };

            Texture baseTexture = FindTexture(source, BaseTextureProperties, out string baseTextureProperty);
            if (baseTexture != null)
            {
                destination.SetTexture("_MainTex", baseTexture);
                CopyTextureTransform(source, baseTextureProperty, destination, "_MainTex");
            }

            Color baseColor = FindColor(source, BaseColorProperties, Color.white);
            destination.SetColor("_Color", baseColor);

            float metallic = Mathf.Clamp01(FindFloat(source, MetallicProperties, 0f));
            destination.SetFloat("_Metallic", metallic);

            float smoothness;
            if (TryFindFloat(source, SmoothnessProperties, out float sourceSmoothness))
            {
                smoothness = Mathf.Clamp01(sourceSmoothness);
            }
            else
            {
                float roughness = Mathf.Clamp01(FindFloat(source, RoughnessProperties, 1f));
                smoothness = 1f - roughness;
            }

            destination.SetFloat("_Glossiness", smoothness);

            Texture metallicRoughness = FindTexture(
                source,
                MetallicRoughnessTextureProperties,
                out string metallicRoughnessProperty);

            if (metallicRoughness != null && repackMetallicRoughnessTexture)
            {
                if (!packedMetallicMaps.TryGetValue(metallicRoughness, out Texture2D packedMetallic))
                {
                    packedMetallic = CreateStandardMetallicGlossMap(metallicRoughness);
                    packedMetallicMaps.Add(metallicRoughness, packedMetallic);
                }

                destination.SetTexture("_MetallicGlossMap", packedMetallic);
                destination.EnableKeyword("_METALLICGLOSSMAP");
                CopyTextureTransform(
                    source,
                    metallicRoughnessProperty,
                    destination,
                    "_MetallicGlossMap");
            }

            Texture normalTexture = FindTexture(source, NormalTextureProperties, out string normalProperty);
            if (normalTexture != null)
            {
                destination.SetTexture("_BumpMap", normalTexture);
                destination.SetFloat(
                    "_BumpScale",
                    FindFloat(source, NormalScaleProperties, 1f));
                destination.EnableKeyword("_NORMALMAP");
                CopyTextureTransform(source, normalProperty, destination, "_BumpMap");
            }

            Texture occlusionTexture = FindTexture(
                source,
                OcclusionTextureProperties,
                out string occlusionProperty);

            if (occlusionTexture != null)
            {
                Texture standardOcclusionTexture = occlusionTexture;

                if (repackOcclusionTexture)
                {
                    if (!packedOcclusionMaps.TryGetValue(occlusionTexture, out Texture2D packedOcclusion))
                    {
                        packedOcclusion = CreateStandardOcclusionMap(occlusionTexture);
                        packedOcclusionMaps.Add(occlusionTexture, packedOcclusion);
                    }

                    standardOcclusionTexture = packedOcclusion;
                }

                destination.SetTexture("_OcclusionMap", standardOcclusionTexture);
                destination.SetFloat(
                    "_OcclusionStrength",
                    Mathf.Clamp01(FindFloat(source, OcclusionStrengthProperties, 1f)));
                CopyTextureTransform(source, occlusionProperty, destination, "_OcclusionMap");
            }

            Texture emissionTexture = FindTexture(
                source,
                EmissionTextureProperties,
                out string emissionProperty);

            bool hasEmissionColor = TryFindColor(
                source,
                EmissionColorProperties,
                out Color emissionColor);

            bool hasEmission =
                emissionTexture != null ||
                (hasEmissionColor && GetMaximumRgb(emissionColor) > EmissionEpsilon);

            if (hasEmission)
            {
                if (emissionTexture != null)
                {
                    destination.SetTexture("_EmissionMap", emissionTexture);
                    CopyTextureTransform(source, emissionProperty, destination, "_EmissionMap");
                }

                if (!hasEmissionColor || GetMaximumRgb(emissionColor) <= EmissionEpsilon)
                    emissionColor = Color.white;

                float emissionStrength = Mathf.Max(
                    0f,
                    FindFloat(source, EmissionStrengthProperties, 1f));

                if (emissionTexture != null && emissionStrength <= EmissionEpsilon)
                    emissionStrength = 1f;

                emissionColor.r *= emissionStrength;
                emissionColor.g *= emissionStrength;
                emissionColor.b *= emissionStrength;
                emissionColor.a = 1f;

                // HDR intensity 0 means 1x brightness. Force at least +1 stop
                // so Unity's Inspector displays a positive intensity value.
                float minimumLinearIntensity =
                    Mathf.Pow(2f, MinimumEmissionExposure);

                float maximumRgb = GetMaximumRgb(emissionColor);
                if (maximumRgb < minimumLinearIntensity)
                {
                    float multiplier =
                        minimumLinearIntensity /
                        Mathf.Max(maximumRgb, EmissionEpsilon);

                    emissionColor.r *= multiplier;
                    emissionColor.g *= multiplier;
                    emissionColor.b *= multiplier;
                }

                destination.SetColor("_EmissionColor", emissionColor);
                destination.EnableKeyword("_EMISSION");
                destination.globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                destination.SetColor("_EmissionColor", Color.black);
                destination.DisableKeyword("_EMISSION");
                destination.globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            ApplyCullMode(source, destination);
            ApplySurfaceMode(source, destination, baseColor);
            ForceTextureTilingOne(destination);

            return destination;
        }

        private static void ApplyCullMode(Material source, Material destination)
        {
            bool doubleSided =
                source.IsKeywordEnabled("_DOUBLESIDED_ON") ||
                source.IsKeywordEnabled("_DOUBLE_SIDED_ON");

            if (TryFindFloat(source, CullProperties, out float cullMode))
                doubleSided |= Mathf.RoundToInt(cullMode) == (int)CullMode.Off;

            if (destination.HasProperty("_Cull"))
            {
                destination.SetInt(
                    "_Cull",
                    doubleSided ? (int)CullMode.Off : (int)CullMode.Back);
            }
        }

        private static void ApplySurfaceMode(Material source, Material destination, Color baseColor)
        {
            bool alphaTest =
                source.IsKeywordEnabled("_ALPHATEST_ON") ||
                source.renderQueue == (int)RenderQueue.AlphaTest;

            bool transparent =
                source.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") ||
                source.IsKeywordEnabled("_ALPHABLEND_ON") ||
                source.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON") ||
                source.renderQueue >= (int)RenderQueue.Transparent ||
                baseColor.a < 0.999f;

            if (alphaTest)
            {
                destination.SetFloat("_Mode", 1f);
                destination.SetInt("_SrcBlend", (int)BlendMode.One);
                destination.SetInt("_DstBlend", (int)BlendMode.Zero);
                destination.SetInt("_ZWrite", 1);
                destination.EnableKeyword("_ALPHATEST_ON");
                destination.DisableKeyword("_ALPHABLEND_ON");
                destination.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                destination.SetFloat(
                    "_Cutoff",
                    Mathf.Clamp01(FindFloat(source, AlphaCutoffProperties, 0.5f)));
                destination.renderQueue = (int)RenderQueue.AlphaTest;
                return;
            }

            if (transparent)
            {
                destination.SetFloat("_Mode", 2f);
                destination.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                destination.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                destination.SetInt("_ZWrite", 0);
                destination.DisableKeyword("_ALPHATEST_ON");
                destination.EnableKeyword("_ALPHABLEND_ON");
                destination.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                destination.renderQueue = (int)RenderQueue.Transparent;
                return;
            }

            destination.SetFloat("_Mode", 0f);
            destination.SetInt("_SrcBlend", (int)BlendMode.One);
            destination.SetInt("_DstBlend", (int)BlendMode.Zero);
            destination.SetInt("_ZWrite", 1);
            destination.DisableKeyword("_ALPHATEST_ON");
            destination.DisableKeyword("_ALPHABLEND_ON");
            destination.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            destination.renderQueue = -1;
        }

        private static Texture2D CreateStandardMetallicGlossMap(Texture source)
        {
            Texture2D readable = CreateReadableCopy(source, true);
            Color32[] pixels = readable.GetPixels32();

            for (int index = 0; index < pixels.Length; index++)
            {
                Color32 sourcePixel = pixels[index];
                byte metallic = sourcePixel.b;
                byte smoothness = (byte)(255 - sourcePixel.g);
                pixels[index] = new Color32(metallic, 0, 0, smoothness);
            }

            Texture2D result = new Texture2D(
                readable.width,
                readable.height,
                TextureFormat.RGBA32,
                true,
                true)
            {
                name = source.name + "_StandardMetallicGloss"
            };

            result.SetPixels32(pixels);
            result.Apply(true, false);
            UnityEngine.Object.Destroy(readable);
            return result;
        }

        private static Texture2D CreateStandardOcclusionMap(Texture source)
        {
            Texture2D readable = CreateReadableCopy(source, true);
            Color32[] pixels = readable.GetPixels32();

            for (int index = 0; index < pixels.Length; index++)
            {
                byte occlusion = pixels[index].r;
                pixels[index] = new Color32(255, occlusion, 255, 255);
            }

            Texture2D result = new Texture2D(
                readable.width,
                readable.height,
                TextureFormat.RGBA32,
                true,
                true)
            {
                name = source.name + "_StandardOcclusion"
            };

            result.SetPixels32(pixels);
            result.Apply(true, false);
            UnityEngine.Object.Destroy(readable);
            return result;
        }

        private static Texture2D CreateReadableCopy(Texture source, bool linear)
        {
            int width = Mathf.Max(1, source.width);
            int height = Mathf.Max(1, source.height);

            RenderTexture temporary = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);

            RenderTexture previous = RenderTexture.active;

            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;

                Texture2D readable = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false,
                    linear);

                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                readable.Apply(false, false);
                return readable;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static Texture FindTexture(Material material, string[] properties, out string foundProperty)
        {
            foreach (string property in properties)
            {
                if (!material.HasProperty(property))
                    continue;

                Texture texture = material.GetTexture(property);
                if (texture != null)
                {
                    foundProperty = property;
                    return texture;
                }
            }

            foundProperty = null;
            return null;
        }

        private static bool TryFindColor(
            Material material,
            string[] properties,
            out Color value)
        {
            foreach (string property in properties)
            {
                if (!material.HasProperty(property))
                    continue;

                value = material.GetColor(property);
                return true;
            }

            value = Color.black;
            return false;
        }

        private static float GetMaximumRgb(Color color)
        {
            return Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        }

        private static Color FindColor(Material material, string[] properties, Color fallback)
        {
            foreach (string property in properties)
            {
                if (material.HasProperty(property))
                    return material.GetColor(property);
            }

            return fallback;
        }

        private static float FindFloat(Material material, string[] properties, float fallback)
        {
            return TryFindFloat(material, properties, out float value)
                ? value
                : fallback;
        }

        private static bool TryFindFloat(Material material, string[] properties, out float value)
        {
            foreach (string property in properties)
            {
                if (!material.HasProperty(property))
                    continue;

                value = material.GetFloat(property);
                return true;
            }

            value = 0f;
            return false;
        }

        private static void CopyTextureTransform(
            Material source,
            string sourceProperty,
            Material destination,
            string destinationProperty)
        {
            if (destination == null || string.IsNullOrEmpty(destinationProperty))
                return;

            // Imported materials must always use tiling X = 1 and Y = 1.
            destination.SetTextureScale(destinationProperty, Vector2.one);

            // Preserve the glTF texture offset when the source property exists.
            if (!string.IsNullOrEmpty(sourceProperty) && source.HasProperty(sourceProperty))
            {
                destination.SetTextureOffset(
                    destinationProperty,
                    source.GetTextureOffset(sourceProperty));
            }
            else
            {
                destination.SetTextureOffset(destinationProperty, Vector2.zero);
            }
        }

        private static void ForceTextureTilingOne(Material material)
        {
            if (material == null)
                return;

            string[] textureProperties =
            {
                "_MainTex",
                "_MetallicGlossMap",
                "_BumpMap",
                "_OcclusionMap",
                "_EmissionMap",
                "_DetailMask",
                "_DetailAlbedoMap",
                "_DetailNormalMap"
            };

            foreach (string property in textureProperties)
            {
                if (!material.HasProperty(property))
                    continue;

                material.SetTextureScale(property, Vector2.one);
            }
        }
    }
}
