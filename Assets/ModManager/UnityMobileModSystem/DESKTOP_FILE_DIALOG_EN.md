# Windows and macOS file dialogs

The mod system uses:

```text
Android / iOS       NativeFilePicker
Unity Editor         UnityEditor.EditorUtility
Windows standalone  UnitySimpleFileBrowser or UnityStandaloneFileBrowser
macOS standalone    UnitySimpleFileBrowser or UnityStandaloneFileBrowser
```

## Recommended: UnitySimpleFileBrowser

For modern Unity versions and Apple Silicon Macs, install the maintained uGUI-based browser through Package Manager using:

```text
https://github.com/yasirkula/UnitySimpleFileBrowser.git
```

It displays an in-game file browser rather than the operating system's native Finder/File Explorer panel.

## Optional native dialog: UnityStandaloneFileBrowser

To use a native operating-system dialog, import `StandaloneFileBrowser.unitypackage` from:

```text
https://github.com/gkngkc/UnityStandaloneFileBrowser
```

The upstream 1.2 release is old, so verify its native plugin binaries with Apple Silicon and recent Unity versions.

## Add or replace

```text
Runtime/MobileModController.cs
Runtime/CrossPlatformFileDialog.cs
Runtime/link.xml
```

Both optional desktop plugins are detected through reflection, so the mod-system scripts compile before either plugin is installed. A standalone desktop player logs an error and cancels file selection when neither plugin exists.

## Covered functions

```text
PickAndImportModel
PickAndApplyTexture
PickAndApplyAudio
PickAndImportSettingsText
PickAndImportModPackage
PickAndOpenModForEditing
ExportCurrentSettingsText
ExportCurrentMod
```

UnitySimpleFileBrowser is preferred when both plugins are installed.
