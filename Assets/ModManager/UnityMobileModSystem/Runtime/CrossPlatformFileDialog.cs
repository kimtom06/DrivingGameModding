using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MobileModSystem
{
    /// <summary>
    /// Cross-platform file dialog used by the mod system.
    ///
    /// Android/iOS: NativeFilePicker
    /// Unity Editor: EditorUtility
    /// Windows/macOS/Linux standalone:
    ///   1) UnitySimpleFileBrowser, when installed
    ///   2) UnityStandaloneFileBrowser (SFB), as a fallback
    ///
    /// Desktop plugins are accessed through reflection, so this script compiles
    /// even before either optional desktop plugin is installed.
    /// </summary>
    public static class CrossPlatformFileDialog
    {
        public static bool IsBusy { get; private set; }

        public static bool IsDesktopStandalone
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
                return !Application.isEditor;
#else
                return false;
#endif
            }
        }

        public static bool IsSimpleFileBrowserAvailable =>
            FindType("SimpleFileBrowser.FileBrowser") != null;

        public static bool IsStandaloneFileBrowserAvailable =>
            FindType("SFB.StandaloneFileBrowser") != null &&
            FindType("SFB.ExtensionFilter") != null;

        public static bool IsDesktopPluginAvailable =>
            IsSimpleFileBrowserAvailable ||
            IsStandaloneFileBrowserAvailable;

        public static void PickFile(
            string title,
            string filterName,
            string[] extensions,
            string[] mobileFileTypes,
            Action<string> completed)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[MobileMod] A file dialog is already open.");
                return;
            }

#if UNITY_ANDROID || UNITY_IOS
            if (NativeFilePicker.IsFilePickerBusy())
            {
                Debug.LogWarning("[MobileMod] NativeFilePicker is already busy.");
                return;
            }
#endif

            IsBusy = true;

#if UNITY_ANDROID || UNITY_IOS
            try
            {
                Action<string> mobileCallback = path => FinishPick(path, completed);

                if (mobileFileTypes != null && mobileFileTypes.Length > 0)
                    NativeFilePicker.PickFile(mobileCallback, mobileFileTypes);
                else
                    NativeFilePicker.PickFile(mobileCallback);
            }
            catch (Exception exception)
            {
                Fail(exception, completed);
            }
#elif UNITY_EDITOR
            try
            {
                string directory = GetInitialDirectory();
                string[] editorFilters = BuildEditorFilters(filterName, extensions);

                string path = editorFilters.Length > 0
                    ? EditorUtility.OpenFilePanelWithFilters(title, directory, editorFilters)
                    : EditorUtility.OpenFilePanel(title, directory, string.Empty);

                FinishPick(path, completed);
            }
            catch (Exception exception)
            {
                Fail(exception, completed);
            }
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            try
            {
                if (!TryOpenDesktopFilePanel(title, filterName, extensions, completed))
                {
                    IsBusy = false;
                    Debug.LogError(
                        "[MobileMod] No desktop file-browser plugin was found. " +
                        "Install UnitySimpleFileBrowser or UnityStandaloneFileBrowser.");
                    completed?.Invoke(null);
                }
            }
            catch (Exception exception)
            {
                Fail(exception, completed);
            }
#else
            IsBusy = false;
            Debug.LogError("[MobileMod] File selection is not supported on this platform.");
            completed?.Invoke(null);
#endif
        }

        /// <summary>
        /// Shows a save dialog and copies sourcePath to the selected destination.
        /// </summary>
        public static void ExportFile(
            string sourcePath,
            string title,
            string defaultFileName,
            string extension,
            Action<bool, string> completed)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[MobileMod] A file dialog is already open.");
                completed?.Invoke(false, null);
                return;
            }

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                Debug.LogError("[MobileMod] Export source file does not exist: " + sourcePath);
                completed?.Invoke(false, null);
                return;
            }

#if UNITY_ANDROID || UNITY_IOS
            if (NativeFilePicker.IsFilePickerBusy())
            {
                completed?.Invoke(false, null);
                return;
            }
#endif

            IsBusy = true;

#if UNITY_ANDROID || UNITY_IOS
            try
            {
                if (!NativeFilePicker.CanExportFiles())
                {
                    IsBusy = false;
                    completed?.Invoke(false, sourcePath);
                    return;
                }

                NativeFilePicker.ExportFile(sourcePath, success =>
                {
                    IsBusy = false;
                    completed?.Invoke(success, success ? sourcePath : null);
                });
            }
            catch (Exception exception)
            {
                FailExport(exception, completed);
            }
#elif UNITY_EDITOR
            try
            {
                string normalizedExtension = NormalizeExtension(extension);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(defaultFileName);
                string destination = EditorUtility.SaveFilePanel(
                    title,
                    GetInitialDirectory(),
                    fileNameWithoutExtension,
                    normalizedExtension);

                FinishDesktopExport(sourcePath, destination, normalizedExtension, completed);
            }
            catch (Exception exception)
            {
                FailExport(exception, completed);
            }
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            try
            {
                if (!TryOpenDesktopSavePanel(
                        sourcePath,
                        title,
                        defaultFileName,
                        extension,
                        completed))
                {
                    IsBusy = false;
                    Debug.LogError(
                        "[MobileMod] No desktop file-browser plugin was found. " +
                        "Install UnitySimpleFileBrowser or UnityStandaloneFileBrowser.");
                    completed?.Invoke(false, null);
                }
            }
            catch (Exception exception)
            {
                FailExport(exception, completed);
            }
#else
            IsBusy = false;
            completed?.Invoke(false, null);
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
        private static bool TryOpenDesktopFilePanel(
            string title,
            string filterName,
            string[] extensions,
            Action<string> completed)
        {
            // Prefer the maintained, architecture-independent uGUI browser.
            if (TryOpenSimpleFileBrowser(title, filterName, extensions, completed))
                return true;

            // Fall back to the native SFB plugin when available.
            return TryOpenSfbFilePanel(title, filterName, extensions, completed);
        }

        private static bool TryOpenDesktopSavePanel(
            string sourcePath,
            string title,
            string defaultFileName,
            string extension,
            Action<bool, string> completed)
        {
            if (TryOpenSimpleFileBrowserSave(
                    sourcePath,
                    title,
                    defaultFileName,
                    extension,
                    completed))
            {
                return true;
            }

            return TryOpenSfbSavePanel(
                sourcePath,
                title,
                defaultFileName,
                extension,
                completed);
        }

        private static bool TryOpenSimpleFileBrowser(
            string title,
            string filterName,
            string[] extensions,
            Action<string> completed)
        {
            Type browserType = FindType("SimpleFileBrowser.FileBrowser");
            if (browserType == null)
                return false;

            try
            {
                ConfigureSimpleFileBrowserFilters(browserType, filterName, extensions);

                Type pickModeType = browserType.GetNestedType(
                    "PickMode",
                    BindingFlags.Public | BindingFlags.NonPublic);
                Type successType = browserType.GetNestedType(
                    "OnSuccess",
                    BindingFlags.Public | BindingFlags.NonPublic);
                Type cancelType = browserType.GetNestedType(
                    "OnCancel",
                    BindingFlags.Public | BindingFlags.NonPublic);

                if (pickModeType == null || successType == null || cancelType == null)
                    return false;

                MethodInfo method = browserType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(candidate =>
                    {
                        if (candidate.Name != "ShowLoadDialog")
                            return false;

                        ParameterInfo[] parameters = candidate.GetParameters();
                        return parameters.Length == 8 &&
                               parameters[0].ParameterType == successType &&
                               parameters[1].ParameterType == cancelType &&
                               parameters[2].ParameterType == pickModeType;
                    });

                if (method == null)
                    return false;

                var bridge = new SimpleFileBrowserCallbackBridge(
                    paths => FinishPick(
                        paths != null && paths.Length > 0 ? paths[0] : null,
                        completed),
                    () => FinishPick(null, completed));

                Delegate success = Delegate.CreateDelegate(
                    successType,
                    bridge,
                    nameof(SimpleFileBrowserCallbackBridge.Success));
                Delegate cancel = Delegate.CreateDelegate(
                    cancelType,
                    bridge,
                    nameof(SimpleFileBrowserCallbackBridge.Cancel));

                object filePickMode = Enum.Parse(pickModeType, "Files");
                object result = method.Invoke(null, new object[]
                {
                    success,
                    cancel,
                    filePickMode,
                    false,
                    GetInitialDirectory(),
                    null,
                    title,
                    "Select"
                });

                if (result is bool shown && !shown)
                    return false;

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MobileMod] UnitySimpleFileBrowser couldn't open: " +
                    UnwrapReflectionException(exception).Message);
                return false;
            }
        }

        private static bool TryOpenSimpleFileBrowserSave(
            string sourcePath,
            string title,
            string defaultFileName,
            string extension,
            Action<bool, string> completed)
        {
            Type browserType = FindType("SimpleFileBrowser.FileBrowser");
            if (browserType == null)
                return false;

            try
            {
                ConfigureSimpleFileBrowserFilters(
                    browserType,
                    "Supported Files",
                    new[] { extension });

                Type pickModeType = browserType.GetNestedType(
                    "PickMode",
                    BindingFlags.Public | BindingFlags.NonPublic);
                Type successType = browserType.GetNestedType(
                    "OnSuccess",
                    BindingFlags.Public | BindingFlags.NonPublic);
                Type cancelType = browserType.GetNestedType(
                    "OnCancel",
                    BindingFlags.Public | BindingFlags.NonPublic);

                if (pickModeType == null || successType == null || cancelType == null)
                    return false;

                MethodInfo method = browserType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(candidate =>
                    {
                        if (candidate.Name != "ShowSaveDialog")
                            return false;

                        ParameterInfo[] parameters = candidate.GetParameters();
                        return parameters.Length == 8 &&
                               parameters[0].ParameterType == successType &&
                               parameters[1].ParameterType == cancelType &&
                               parameters[2].ParameterType == pickModeType;
                    });

                if (method == null)
                    return false;

                string normalizedExtension = NormalizeExtension(extension);
                string initialFileName = EnsureExtension(
                    Path.GetFileName(defaultFileName),
                    normalizedExtension);

                var bridge = new SimpleFileBrowserCallbackBridge(
                    paths => FinishDesktopExport(
                        sourcePath,
                        paths != null && paths.Length > 0 ? paths[0] : null,
                        normalizedExtension,
                        completed),
                    () => FinishDesktopExport(
                        sourcePath,
                        null,
                        normalizedExtension,
                        completed));

                Delegate success = Delegate.CreateDelegate(
                    successType,
                    bridge,
                    nameof(SimpleFileBrowserCallbackBridge.Success));
                Delegate cancel = Delegate.CreateDelegate(
                    cancelType,
                    bridge,
                    nameof(SimpleFileBrowserCallbackBridge.Cancel));

                object filePickMode = Enum.Parse(pickModeType, "Files");
                object result = method.Invoke(null, new object[]
                {
                    success,
                    cancel,
                    filePickMode,
                    false,
                    GetInitialDirectory(),
                    initialFileName,
                    title,
                    "Save"
                });

                if (result is bool shown && !shown)
                    return false;

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MobileMod] UnitySimpleFileBrowser couldn't open save dialog: " +
                    UnwrapReflectionException(exception).Message);
                return false;
            }
        }

        private static void ConfigureSimpleFileBrowserFilters(
            Type browserType,
            string filterName,
            string[] extensions)
        {
            Type filterType = browserType.GetNestedType(
                "Filter",
                BindingFlags.Public | BindingFlags.NonPublic);
            if (filterType == null)
                return;

            string[] normalizedExtensions = NormalizeExtensions(extensions)
                .Select(extension => "." + extension)
                .ToArray();

            if (normalizedExtensions.Length == 0)
                return;

            ConstructorInfo constructor = filterType.GetConstructor(
                new[] { typeof(string), typeof(string[]) });
            if (constructor == null)
                return;

            Array filters = Array.CreateInstance(filterType, 1);
            object filter = constructor.Invoke(new object[]
            {
                string.IsNullOrWhiteSpace(filterName) ? "Supported Files" : filterName,
                normalizedExtensions
            });
            filters.SetValue(filter, 0);

            MethodInfo setFilters = browserType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(candidate =>
                {
                    if (candidate.Name != "SetFilters")
                        return false;

                    ParameterInfo[] parameters = candidate.GetParameters();
                    return parameters.Length == 2 &&
                           parameters[0].ParameterType == typeof(bool) &&
                           parameters[1].ParameterType.IsArray &&
                           parameters[1].ParameterType.GetElementType() == filterType;
                });

            setFilters?.Invoke(null, new object[] { true, filters });
        }

        private static bool TryOpenSfbFilePanel(
            string title,
            string filterName,
            string[] extensions,
            Action<string> completed)
        {
            Type browserType = FindType("SFB.StandaloneFileBrowser");
            Type filterType = FindType("SFB.ExtensionFilter");

            if (browserType == null || filterType == null)
                return false;

            try
            {
                Array filters = BuildSfbFilters(filterType, filterName, extensions);

                MethodInfo method = browserType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(candidate =>
                    {
                        if (candidate.Name != "OpenFilePanelAsync")
                            return false;

                        ParameterInfo[] parameters = candidate.GetParameters();
                        return parameters.Length == 5 &&
                               parameters[0].ParameterType == typeof(string) &&
                               parameters[1].ParameterType == typeof(string) &&
                               parameters[2].ParameterType.IsArray &&
                               parameters[2].ParameterType.GetElementType() == filterType &&
                               parameters[3].ParameterType == typeof(bool) &&
                               parameters[4].ParameterType == typeof(Action<string[]>);
                    });

                if (method == null)
                    return false;

                Action<string[]> callback = paths =>
                {
                    string selectedPath = paths != null && paths.Length > 0
                        ? paths[0]
                        : null;

                    FinishPick(selectedPath, completed);
                };

                method.Invoke(null, new object[]
                {
                    title,
                    GetInitialDirectory(),
                    filters,
                    false,
                    callback
                });

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MobileMod] UnityStandaloneFileBrowser couldn't open: " +
                    UnwrapReflectionException(exception).Message);
                return false;
            }
        }

        private static bool TryOpenSfbSavePanel(
            string sourcePath,
            string title,
            string defaultFileName,
            string extension,
            Action<bool, string> completed)
        {
            Type browserType = FindType("SFB.StandaloneFileBrowser");
            if (browserType == null)
                return false;

            try
            {
                MethodInfo method = browserType.GetMethod(
                    "SaveFilePanelAsync",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(string),
                        typeof(Action<string>)
                    },
                    null);

                if (method == null)
                    return false;

                string normalizedExtension = NormalizeExtension(extension);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(defaultFileName);

                Action<string> callback = destination =>
                    FinishDesktopExport(
                        sourcePath,
                        destination,
                        normalizedExtension,
                        completed);

                method.Invoke(null, new object[]
                {
                    title,
                    GetInitialDirectory(),
                    fileNameWithoutExtension,
                    normalizedExtension,
                    callback
                });

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MobileMod] UnityStandaloneFileBrowser couldn't open save dialog: " +
                    UnwrapReflectionException(exception).Message);
                return false;
            }
        }

        private static Array BuildSfbFilters(
            Type filterType,
            string filterName,
            string[] extensions)
        {
            string[] normalizedExtensions = NormalizeExtensions(extensions);
            if (normalizedExtensions.Length == 0)
                return null;

            ConstructorInfo constructor = filterType.GetConstructor(
                new[] { typeof(string), typeof(string[]) });

            if (constructor == null)
                throw new MissingMethodException(
                    filterType.FullName,
                    ".ctor(string, string[])");

            Array result = Array.CreateInstance(filterType, 1);
            object filter = constructor.Invoke(new object[]
            {
                string.IsNullOrWhiteSpace(filterName) ? "Supported Files" : filterName,
                normalizedExtensions
            });

            result.SetValue(filter, 0);
            return result;
        }

        private sealed class SimpleFileBrowserCallbackBridge
        {
            private readonly Action<string[]> success;
            private readonly Action cancel;

            public SimpleFileBrowserCallbackBridge(
                Action<string[]> success,
                Action cancel)
            {
                this.success = success;
                this.cancel = cancel;
            }

            public void Success(string[] paths)
            {
                success?.Invoke(paths);
            }

            public void Cancel()
            {
                cancel?.Invoke();
            }
        }
#endif

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static void FinishPick(string path, Action<string> completed)
        {
            IsBusy = false;
            completed?.Invoke(string.IsNullOrWhiteSpace(path) ? null : path);
        }

        private static void FinishDesktopExport(
            string sourcePath,
            string destination,
            string extension,
            Action<bool, string> completed)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(destination))
                {
                    IsBusy = false;
                    completed?.Invoke(false, null);
                    return;
                }

                destination = EnsureExtension(destination, extension);

                string sourceFullPath = Path.GetFullPath(sourcePath);
                string destinationFullPath = Path.GetFullPath(destination);

                string directory = Path.GetDirectoryName(destinationFullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                if (!string.Equals(
                        sourceFullPath,
                        destinationFullPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(sourceFullPath, destinationFullPath, true);
                }

                IsBusy = false;
                completed?.Invoke(true, destinationFullPath);
            }
            catch (Exception exception)
            {
                FailExport(exception, completed);
            }
        }

        private static string[] BuildEditorFilters(string filterName, string[] extensions)
        {
            string[] normalizedExtensions = NormalizeExtensions(extensions);
            if (normalizedExtensions.Length == 0)
                return Array.Empty<string>();

            return new[]
            {
                string.IsNullOrWhiteSpace(filterName) ? "Supported Files" : filterName,
                string.Join(",", normalizedExtensions),
                "All Files",
                "*"
            };
        }

        private static string[] NormalizeExtensions(string[] extensions)
        {
            if (extensions == null)
                return Array.Empty<string>();

            return extensions
                .Where(extension => !string.IsNullOrWhiteSpace(extension))
                .Select(NormalizeExtension)
                .Where(extension => !string.IsNullOrWhiteSpace(extension))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string NormalizeExtension(string extension)
        {
            return (extension ?? string.Empty).Trim().TrimStart('.');
        }

        private static string EnsureExtension(string path, string extension)
        {
            string normalizedExtension = NormalizeExtension(extension);
            if (string.IsNullOrEmpty(normalizedExtension))
                return path;

            string expectedExtension = "." + normalizedExtension;
            return path.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase)
                ? path
                : path + expectedExtension;
        }

        private static string GetInitialDirectory()
        {
            string documents = Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);

            return Directory.Exists(documents)
                ? documents
                : Application.persistentDataPath;
        }

        private static Exception UnwrapReflectionException(Exception exception)
        {
            if (exception is TargetInvocationException invocation &&
                invocation.InnerException != null)
            {
                return invocation.InnerException;
            }

            return exception;
        }

        private static void Fail(Exception exception, Action<string> completed)
        {
            IsBusy = false;
            Debug.LogException(UnwrapReflectionException(exception));
            completed?.Invoke(null);
        }

        private static void FailExport(
            Exception exception,
            Action<bool, string> completed)
        {
            IsBusy = false;
            Debug.LogException(UnwrapReflectionException(exception));
            completed?.Invoke(false, null);
        }
    }
}
