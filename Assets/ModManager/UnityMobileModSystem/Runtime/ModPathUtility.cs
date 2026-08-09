using System;
using System.IO;
using UnityEngine;

namespace MobileModSystem
{
    public static class ModPathUtility
    {
        public static string WorkspaceRoot
        {
            get
            {
                string path = Path.Combine(Application.persistentDataPath, "ModWorkspace");
                Directory.CreateDirectory(path);
                return path;
            }
        }

        public static string CopyIntoWorkspace(string sourcePath, string category)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("모드 에셋 파일을 찾을 수 없습니다.", sourcePath);

            string directory = Path.Combine(WorkspaceRoot, category);
            Directory.CreateDirectory(directory);

            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            string destination = Path.Combine(directory, Guid.NewGuid().ToString("N") + extension);
            File.Copy(sourcePath, destination, true);
            return destination;
        }

        // 이름이 중복되어도 복원되도록 Transform 이름 대신 sibling index 경로를 저장합니다.
        // 예: "0/2/1"
        public static string GetRelativeTransformPath(Transform root, Transform target)
        {
            if (root == null || target == null)
                return string.Empty;

            if (root == target)
                return string.Empty;

            System.Collections.Generic.List<int> indices =
                new System.Collections.Generic.List<int>();
            Transform current = target;

            while (current != null && current != root)
            {
                indices.Add(current.GetSiblingIndex());
                current = current.parent;
            }

            if (current != root)
                return string.Empty;

            indices.Reverse();
            return string.Join("/", indices);
        }

        public static Transform FindRelativeTransform(Transform root, string relativePath)
        {
            if (root == null)
                return null;

            if (string.IsNullOrEmpty(relativePath))
                return root;

            Transform current = root;
            string[] parts = relativePath.Split('/');

            foreach (string part in parts)
            {
                if (!int.TryParse(part, out int childIndex) ||
                    childIndex < 0 || childIndex >= current.childCount)
                    return null;

                current = current.GetChild(childIndex);
            }

            return current;
        }

        public static string MakeSafeFileName(string value, string fallback)
        {
            string name = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }

        public static Uri ToFileUri(string absolutePath)
        {
            return new Uri(Path.GetFullPath(absolutePath));
        }
    }
}
