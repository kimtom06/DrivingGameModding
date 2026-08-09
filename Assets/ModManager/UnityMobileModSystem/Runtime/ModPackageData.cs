using System;
using System.Collections.Generic;
using UnityEngine;

namespace MobileModSystem
{
    public static class ModPackageConstants
    {
        public const string Magic = "SDGMOD";
        public const int CurrentVersion = 3;
        public const int MinimumSupportedVersion = 1;
        public const string Extension = ".sdgmod";
        public const string ManifestFileName = "manifest.json";
    }

    public enum ModAssetType
    {
        ModelGlb,
        Texture,
        Audio,
        ConfigText
    }

    [Serializable]
    public sealed class ModPackageManifest
    {
        public string magic = ModPackageConstants.Magic;
        public int formatVersion = ModPackageConstants.CurrentVersion;
        public string modId;
        public string displayName;
        public string author;
        public string createdUtc;

        // v2: 사용자가 편집할 수 있는 key=value 설정 텍스트 에셋입니다.
        public string settingsAssetId;
        public List<ModAssetRecord> assets = new List<ModAssetRecord>();
        public List<ModNodeRecord> nodes = new List<ModNodeRecord>();
    }

    [Serializable]
    public sealed class ModAssetRecord
    {
        public string assetId;
        public ModAssetType assetType;
        public string relativePath;
        public string originalFileName;
    }

    [Serializable]
    public sealed class ModNodeRecord
    {
        public string id;
        public string parentId;
        public string name;
        public bool activeSelf;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;

        public string modelAssetId;
        public List<ModTextureRecord> textures = new List<ModTextureRecord>();

        // v3: 하나의 ModNode에 여러 AudioSource를 저장합니다.
        public List<ModAudioRecord> audios = new List<ModAudioRecord>();

        // v1/v2 호환용 필드입니다. 새 모드팩에는 audios를 사용합니다.
        public bool hasAudio;
        public ModAudioRecord audio;
    }

    [Serializable]
    public sealed class ModTextureRecord
    {
        public string assetId;
        public string rendererPath;
        public int materialIndex;
        public string propertyName;
    }

    [Serializable]
    public sealed class ModAudioRecord
    {
        public string assetId;
        public bool playOnAwake;
        public bool loop;
        public float volume = 1f;
        public float spatialBlend;
        public float minDistance = 1f;
        public float maxDistance = 500f;
    }
}
