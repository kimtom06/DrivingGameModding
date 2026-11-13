using System;

using System.IO;
using System.IO.Compression;

using System.Threading.Tasks;

using UnityEngine;

using UnityEngine.Events;

using UnityEngine.SceneManagement;



namespace MobileModSystem

{

    [DefaultExecutionOrder(-1000)]

    public sealed class MobileModController : MonoBehaviour

    {

        public static MobileModController Instance { get; private set; }

        public static bool HasInstance => Instance != null;



        [Header("Singleton / Scene References")]

        [Tooltip("씬 전환 후 MobileModSceneReferences가 없을 때 ImportedMods 오브젝트를 자동 생성합니다.")]

        public bool createFallbackImportedModsParent = true;



        [Tooltip("MobileModSceneReferences가 없는 씬에서 생성할 기본 부모 이름입니다.")]

        public string fallbackImportedModsParentName = "ImportedMods";



        [Tooltip("모드 임포트 시 MobileModSceneReferences와 명시적으로 지정된 importedModsParent가 나타날 때까지 기다립니다.")]

        public bool waitForSceneReferencesBeforeImport = true;



        [Tooltip("0이면 제한 없이 기다립니다. 0보다 크면 지정한 초가 지난 뒤 임포트를 실패 처리합니다.")]

        [Min(0f)]

        public float sceneReferencesWaitTimeoutSeconds = 0f;



        [Header("References")]

        public Transform buildRoot;

        public Transform importedModsParent;

        public Renderer selectedTextureRenderer;



        [Tooltip("오디오를 저장할 메인 모델 루트입니다. 모델을 불러오면 자동으로 최신 모델로 설정됩니다.")]

        public GameObject mainModelRoot;



        [Tooltip("메인 모델 루트 아래에 자동 생성할 오디오 저장소 이름입니다.")]

        public string audioStorageName = "AudioStorage";



        [Header("Edit Existing Mod")]

        [Tooltip("현재 편집 대상으로 열린 기존 모드의 루트입니다. 편집 모드에서는 이 Transform이 buildRoot로 사용됩니다.")]

        public GameObject currentEditableModRoot;



        [Tooltip("새 기존 모드를 편집용으로 열 때 이전 편집 루트를 자동 삭제합니다.")]

        public bool destroyPreviousEditableMod = true;



        [Tooltip("씬에 편집 작업공간이 없을 때 생성할 부모 이름입니다.")]

        public string fallbackEditWorkspaceName = "ModEditWorkspace";



        [Header("Export Info")]

        public string modName = "MyMod";

        public string author = "Player";

        public string texturePropertyName = "_BaseMap";

        public int textureMaterialIndex;



        [Header("Default Settings Text")]

        [Tooltip("프로젝트에 포함한 기본 .txt 파일입니다. 지정하면 아래 문자열보다 우선합니다.")]

        public TextAsset defaultSettingsFile;



        [Tooltip("defaultSettingsFile을 지정하지 않았을 때 사용할 기본 템플릿입니다. 둘 다 비우면 내장 템플릿을 사용합니다.")]

        [TextArea(10, 30)]

        public string defaultSettingsText;



        [Header("Runtime Imported Mod")]

        [Tooltip("Play Mode에서 .sdgmod 모드는 항상 하나만 유지합니다. 새 모드가 정상적으로 로드되면 기존 모드를 제거합니다.")]

        public bool keepOnlyOneImportedMod = true;



        [Tooltip("현재 런타임으로 불러온 모드 루트입니다. 새 모드가 성공적으로 로드되면 자동으로 교체됩니다.")]

        public GameObject currentImportedModRoot;



        [Header("Recent Mod Cache")]

        [Tooltip("성공적으로 불러온 마지막 .sdgmod 파일을 앱 내부 저장소에 보관합니다.")]

        public bool saveLastImportedMod = true;

        [Header("Saved Mod Package Cleanup")]
        [Tooltip("앱 시작 시 내부 MobileModSystem 폴더에서 파일명에 last_loaded가 없는 모든 .sdgmod를 삭제합니다.")]
        public bool deleteOldModFilesOnStart = true;

        [Tooltip("새 모드를 정상적으로 불러온 뒤 앱 내부 MobileModSystem 폴더에 저장된 동일 modId의 오래된 .sdgmod 파일을 삭제합니다. 선택한 원본 및 현재 RecentMod 파일은 유지합니다.")]
        public bool deleteOlderStoredPackagesWithSameModId = true;



        [Tooltip("앱 내부 최근 모드 파일 이름입니다. 일반적으로 변경할 필요가 없습니다.")]

        public string recentModFileName = "last_loaded.sdgmod";



        [Header("Events")]

        public UnityEvent<string> onStatus;

        public UnityEvent<GameObject> onObjectCreated;

        public UnityEvent<string> onSettingsTextChanged;

        public UnityEvent<string> onImportedSettingsText;

        public UnityEvent<RuntimeModTextConfig> onImportedSettings;

        public UnityEvent<bool> onRecentModAvailabilityChanged;



        [Tooltip("PickAndImportModPackage가 성공적으로 완료된 뒤 불러온 모드 루트를 전달합니다.")]

        public UnityEvent<GameObject> onModPackageImportCompleted;



        [Tooltip("LoadRecentModPackage가 성공적으로 완료된 뒤 불러온 모드 루트를 전달합니다.")]

        public UnityEvent<GameObject> onRecentModPackageLoadCompleted;



        /// <summary>

        /// LoadRecentModPackage가 성공적으로 완료될 때 호출되는 C# 이벤트입니다.

        /// Unity UI Button에서 매개변수 없는 LoadRecentModPackage()를 호출한 경우에도 실행됩니다.

        /// </summary>

        public event Action<GameObject> RecentModPackageLoadCompleted;



        [Tooltip("일반 모드 또는 최근 모드가 성공적으로 불러와질 때마다 호출됩니다.")]

        public UnityEvent<GameObject> onAnyModImportCompleted;



        public UnityEvent<GameObject> onEditableModOpened;

        public UnityEvent<bool> onEditModeChanged;



        [SerializeField] private RuntimeModAssetImporter assetImporter;

        [SerializeField] private ModPackageExporter packageExporter;

        [SerializeField] private ModPackageImporter packageImporter;



        private Transform sceneBuildRoot;

        private Transform sceneEditWorkspaceParent;

#if !MOD_INGAME
        // Editor-only lock: do not let two editing imports replace each other concurrently.
        private bool editorImportInProgress;
#endif

        private void Awake()

        {

            if (Instance != null && Instance != this)

            {

                Destroy(gameObject);

                return;

            }



            Instance = this;



            // DontDestroyOnLoad는 루트 오브젝트에만 적용되므로 부모에서 분리합니다.

            if (transform.parent != null)

                transform.SetParent(null, true);



            DontDestroyOnLoad(gameObject);

            CleanupOldStoredModFilesOnStartup();

            InitializeRequiredComponents();

            SceneManager.sceneLoaded += HandleSceneLoaded;

            RefreshSceneReferences();

        }



        private void Start()

        {

            InitializeBuildConfigIfAvailable();

            NotifyRecentModAvailability();

        }



        private void OnDestroy()

        {

            if (Instance != this)

                return;



            SceneManager.sceneLoaded -= HandleSceneLoaded;

            Instance = null;

        }



        private void InitializeRequiredComponents()

        {

            if (assetImporter == null)

                assetImporter = GetComponent<RuntimeModAssetImporter>();

            if (packageExporter == null)

                packageExporter = GetComponent<ModPackageExporter>();

            if (packageImporter == null)

                packageImporter = GetComponent<ModPackageImporter>();



            if (assetImporter == null)

                assetImporter = gameObject.AddComponent<RuntimeModAssetImporter>();

            if (packageExporter == null)

                packageExporter = gameObject.AddComponent<ModPackageExporter>();

            if (packageImporter == null)

                packageImporter = gameObject.AddComponent<ModPackageImporter>();

        }



        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)

        {

            ResolveSceneReferences(scene);

            InitializeBuildConfigIfAvailable();

            NotifyRecentModAvailability();

        }



        public void RefreshSceneReferences()

        {

            ResolveSceneReferences(SceneManager.GetActiveScene());

        }



        private void ResolveSceneReferences(Scene scene)

        {

            MobileModSceneReferences sceneReferences = FindSceneReferences(scene);



            if (sceneReferences != null)

            {

                importedModsParent = sceneReferences.ResolveImportedModsParent();

                sceneBuildRoot = sceneReferences.ResolveBuildRoot();

                sceneEditWorkspaceParent = sceneReferences.ResolveEditWorkspaceParent();

            }

            else

            {

                if (!IsUsableSceneTransform(importedModsParent, scene))

                    importedModsParent = null;



                if (!IsUsableSceneTransform(sceneBuildRoot, scene))

                    sceneBuildRoot = null;



                if (!IsUsableSceneTransform(sceneEditWorkspaceParent, scene))

                    sceneEditWorkspaceParent = null;

            }



            if (IsUsableSceneObject(currentEditableModRoot, scene))

            {

                buildRoot = currentEditableModRoot.transform;

            }

            else

            {

                currentEditableModRoot = null;

                buildRoot = sceneBuildRoot;

                mainModelRoot = null;

                onEditModeChanged?.Invoke(false);

            }



            // strict wait 모드에서는 fallback 부모를 먼저 만들지 않습니다.

            // 실제 임포트는 MobileModSceneReferences.importedModsParent가 준비될 때까지 대기합니다.

            if (importedModsParent == null &&

                createFallbackImportedModsParent &&

                !waitForSceneReferencesBeforeImport)

            {

                importedModsParent = CreateFallbackImportedModsParent(scene);

            }

        }



        private static MobileModSceneReferences FindSceneReferences(Scene scene)

        {

            MobileModSceneReferences[] allReferences =

                Resources.FindObjectsOfTypeAll<MobileModSceneReferences>();



            foreach (MobileModSceneReferences candidate in allReferences)

            {

                if (candidate == null)

                    continue;



                Scene candidateScene = candidate.gameObject.scene;

                if (!candidateScene.IsValid() || !candidateScene.isLoaded)

                    continue;



                if (candidateScene == scene)

                    return candidate;

            }



            return null;

        }



        private Transform CreateFallbackImportedModsParent(Scene scene)

        {

            string objectName = string.IsNullOrWhiteSpace(fallbackImportedModsParentName)

                ? "ImportedMods"

                : fallbackImportedModsParentName.Trim();



            GameObject fallback = new GameObject(objectName);



            if (scene.IsValid() && scene.isLoaded)

                SceneManager.MoveGameObjectToScene(fallback, scene);



            return fallback.transform;

        }



        private static bool IsUsableSceneTransform(Transform target, Scene expectedScene)

        {

            if (target == null)

                return false;



            Scene targetScene = target.gameObject.scene;

            return targetScene.IsValid() &&

                   targetScene.isLoaded &&

                   targetScene == expectedScene;

        }





        private static bool IsUsableSceneObject(GameObject target, Scene expectedScene)

        {

            return target != null &&

                   IsUsableSceneTransform(target.transform, expectedScene);

        }



        private void InitializeBuildConfigIfAvailable()

        {

            if (buildRoot == null)

                return;



            RuntimeModTextConfig config = GetOrCreateBuildConfig();

            config.EnsureDefault(modName, author, GetDefaultSettingsTemplate());

            onSettingsTextChanged?.Invoke(config.TextContent);

        }



        // UI Button에 연결: GLB 선택 후 buildRoot 아래에 생성

        public void PickAndImportModel()
        {
            if (CrossPlatformFileDialog.IsBusy)
                return;

            CrossPlatformFileDialog.PickFile(
                "Open GLB Model",
                "glTF Binary",
                GetModelExtensions(),
                GetModelFileTypes(),
                async path =>
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

#if !MOD_INGAME
                if (editorImportInProgress)
                {
                    SetStatus("다른 모드를 불러오는 중입니다.");
                    return;
                }
                editorImportInProgress = true;
#endif
                try
                {
                    Transform importBuildRoot = buildRoot;
                    GameObject modelToReplace = mainModelRoot;

#if !MOD_INGAME
                    // A GLB chosen while editing an existing .sdgmod starts a NEW editing
                    // target. Never import the GLB underneath the old package root:
                    // destroying that root would also destroy the newly imported model.
                    GameObject previousEditableRoot = currentEditableModRoot;
                    if (previousEditableRoot != null)
                    {
                        importBuildRoot = ResolveModelBuildRootForEditing();
                        modelToReplace = null;
                    }
#endif
                    if (importBuildRoot == null)
                        throw new InvalidOperationException("buildRoot가 지정되지 않았습니다.");

                    SetStatus("3D 모델 교체 중...");

                    GameObject created = await assetImporter.ImportOrReplaceSingleGlbAsync(
                        path,
                        importBuildRoot,
                        modelToReplace,
                        true);

                    if (created == null)
                        throw new InvalidOperationException("모델을 가져왔지만 생성된 루트가 없습니다.");

#if !MOD_INGAME
                    // Replace only AFTER the new GLB has loaded successfully.
                    if (previousEditableRoot != null)
                    {
                        previousEditableRoot.SetActive(false);
                        Destroy(previousEditableRoot);
                        currentEditableModRoot = null;
                        buildRoot = importBuildRoot;
                        onEditModeChanged?.Invoke(false);
                    }

                    // Remove any older standalone GLBs in the editor build root.
                    RemovePreviousEditingModels(importBuildRoot, created);
                    selectedTextureRenderer = null;
#endif
                    mainModelRoot = created;
                    GetOrCreateAudioStorage(created);

#if !MOD_INGAME
                    // Run after GLB import AND editor workspace replacement are finished.
                    ExtractLoadedEditingElements(created, importBuildRoot);
#endif
                    onObjectCreated?.Invoke(created);
                    SetStatus("3D 모델 교체 완료: " + created.name);
                }
                catch (Exception exception)
                {
                    ReportError(exception);
                }
#if !MOD_INGAME
                finally
                {
                    editorImportInProgress = false;
                }
#endif
            });
        }


        // UI Button에 연결: 현재 selectedTextureRenderer에 텍스처 적용

        public void PickAndApplyTexture()

        {

            if (selectedTextureRenderer == null)

            {

                SetStatus("selectedTextureRenderer가 지정되지 않았습니다.");

                return;

            }



            GameObject ownerNode = FindOwningModNode(selectedTextureRenderer.transform);

            if (ownerNode == null)

            {

                SetStatus("Renderer의 상위에 ModNode가 없습니다.");

                return;

            }



            if (CrossPlatformFileDialog.IsBusy)

                return;



            CrossPlatformFileDialog.PickFile(

                "Open Texture",

                "Image Files",

                GetTextureExtensions(),

                GetTextureFileTypes(),

                async path =>

            {

                if (string.IsNullOrWhiteSpace(path))

                    return;



                try

                {

                    SetStatus("텍스처 적용 중...");

                    await assetImporter.ImportTextureAsync(

                        path,

                        selectedTextureRenderer,

                        ownerNode,

                        texturePropertyName,

                        textureMaterialIndex);

                    SetStatus("텍스처 적용 완료");

                }

                catch (Exception exception)

                {

                    ReportError(exception);

                }

            });

        }



        // UI Button에 연결: 메인 모델 루트/AudioStorage에 오디오를 계속 추가합니다.

        public void PickAndApplyAudio()

        {

            if (CrossPlatformFileDialog.IsBusy)

            {

                SetStatus("파일 선택기가 이미 열려 있습니다.");

                return;

            }



            GameObject modelRoot = ResolveMainModelRoot();

            if (modelRoot == null)

            {

                SetStatus("먼저 GLB 모델을 불러오거나 mainModelRoot를 지정하세요.");

                return;

            }



            SetStatus("오디오 파일을 선택하세요.");



            CrossPlatformFileDialog.PickFile(

                "Open Audio",

                "Audio Files",

                GetAudioExtensions(),

                GetAudioFileTypes(),

                async path =>

            {

                if (string.IsNullOrWhiteSpace(path))

                {

                    SetStatus("오디오 불러오기를 취소했습니다.");

                    return;

                }



                try

                {

                    if (!File.Exists(path))

                        throw new FileNotFoundException("선택한 오디오 파일을 찾을 수 없습니다.", path);



                    if (!IsSupportedAudioFile(path))

                    {

                        SetStatus("지원하지 않는 오디오 형식입니다. WAV, MP3, OGG, AIFF만 선택할 수 있습니다.");

                        return;

                    }



                    GameObject audioStorage = GetOrCreateAudioStorage(modelRoot);



                    SetStatus("사운드 불러오는 중: " + Path.GetFileName(path));

                    AudioSource createdSource = await assetImporter.ImportAudioSourceAsync(

                        path,

                        audioStorage);



                    SetStatus(

                        "사운드 추가 완료: " + Path.GetFileName(path) +

                        " (총 " + audioStorage.GetComponents<AudioSource>().Length + "개)");

                }

                catch (Exception exception)

                {

                    ReportError(exception);

                }

            });

        }



        /// <summary>

        /// InputField/TMP_InputField의 OnValueChanged(string)에 연결할 수 있습니다.

        /// 입력한 전체 내용이 다음 .sdgmod 내보내기에 포함됩니다.

        /// </summary>

        public void SetCurrentSettingsText(string text)

        {

            try

            {

                RuntimeModTextConfig config = GetOrCreateBuildConfig();

                config.SetText(text);

                onSettingsTextChanged?.Invoke(config.TextContent);

            }

            catch (Exception exception)

            {

                ReportError(exception);

            }

        }



        public string GetCurrentSettingsText()

        {

            RuntimeModTextConfig config = GetOrCreateBuildConfig();

            config.EnsureDefault(modName, author, GetDefaultSettingsTemplate());

            return config.TextContent;

        }



        // UI Button에 연결하면 현재 설정 텍스트를 onSettingsTextChanged로 다시 전달합니다.

        public void RequestCurrentSettingsText()

        {

            try

            {

                onSettingsTextChanged?.Invoke(GetCurrentSettingsText());

            }

            catch (Exception exception)

            {

                ReportError(exception);

            }

        }



        // UI Button에 연결: 설정 텍스트를 기본값으로 초기화합니다.

        public void ResetCurrentSettingsText()

        {

            try

            {

                RuntimeModTextConfig config = GetOrCreateBuildConfig();

                config.ResetToDefault(modName, author, GetDefaultSettingsTemplate());

                onSettingsTextChanged?.Invoke(config.TextContent);

                SetStatus("모드 설정 텍스트를 기본값으로 초기화했습니다.");

            }

            catch (Exception exception)

            {

                ReportError(exception);

            }

        }



        // UI Button에 연결: 외부에서 수정한 .txt 파일을 현재 제작 중인 모드에 적용합니다.

        public void PickAndImportSettingsText()

        {

            if (CrossPlatformFileDialog.IsBusy)

                return;



            CrossPlatformFileDialog.PickFile(

                "Open Settings Text",

                "Text Files",

                GetTextExtensions(),

                GetTextFileTypes(),

                path =>

            {

                if (string.IsNullOrWhiteSpace(path))

                    return;



                try

                {

                    RuntimeModTextConfig config = GetOrCreateBuildConfig();

                    config.LoadFromFile(path, true);

                    onSettingsTextChanged?.Invoke(config.TextContent);

                    SetStatus("모드 설정 텍스트 불러오기 완료");

                }

                catch (Exception exception)

                {

                    ReportError(exception);

                }

            });

        }



        // UI Button에 연결: 현재 설정 텍스트만 별도 .txt로 내보냅니다.

        public void ExportCurrentSettingsText()

        {

            try

            {

                RuntimeModTextConfig config = GetOrCreateBuildConfig();

                config.EnsureDefault(modName, author, GetDefaultSettingsTemplate());



                string fileName = ModPathUtility.MakeSafeFileName(modName, "mod") + "_settings.txt";

                string path = config.SaveToWorkspace(fileName);



                CrossPlatformFileDialog.ExportFile(

                    path,

                    "Export Settings Text",

                    fileName,

                    "txt",

                    (success, destination) =>

                    {

                        SetStatus(success

                            ? "설정 텍스트 내보내기 완료: " + destination

                            : "설정 텍스트 내보내기 취소");

                    });

            }

            catch (Exception exception)

            {

                ReportError(exception);

            }

        }



        public void SetCurrentSettingValue(string key, string value)

        {

            try

            {

                RuntimeModTextConfig config = GetOrCreateBuildConfig();

                config.EnsureDefault(modName, author, GetDefaultSettingsTemplate());

                config.SetValue(key, value);

                onSettingsTextChanged?.Invoke(config.TextContent);

            }

            catch (Exception exception)

            {

                ReportError(exception);

            }

        }



        // UI Button에 연결: buildRoot 게임오브젝트를 .sdgmod로 내보냄

        public async void ExportCurrentMod()

        {

            if (buildRoot == null)

            {

                SetStatus("buildRoot가 지정되지 않았습니다.");

                return;

            }



            try

            {

                RuntimeModTextConfig config = GetOrCreateBuildConfig();

                config.EnsureDefault(modName, author, GetDefaultSettingsTemplate());

                SyncExportInfoFromConfig(config);



                SetStatus("모드 패키지 생성 중...");

                string packagePath = await packageExporter.ExportAsync(

                    buildRoot.gameObject,

                    modName,

                    author);



                CrossPlatformFileDialog.ExportFile(

                    packagePath,

                    "Export Mod Package",

                    Path.GetFileName(packagePath),

                    "sdgmod",

                    (success, destination) =>

                    {

                        SetStatus(success

                            ? "모드 내보내기 완료: " + destination

                            : "모드 내보내기 취소");

                    });

            }

            catch (Exception exception)

            {

                ReportError(exception);

            }

        }



        /// <summary>

        /// UI Button에 연결: 기존 .sdgmod를 선택해 편집 작업으로 엽니다.

        /// 일반 게임 임포트와 달리 불러온 모드 루트가 현재 buildRoot가 됩니다.

        /// </summary>

        public void PickAndOpenModForEditing()

        {

#if UNITY_EDITOR

            // Unity Editor에서는 OS에 등록되지 않은 .sdgmod 확장자가 회색 처리될 수 있으므로

            // CrossPlatformFileDialog를 우회하고 필터 없는 Unity Editor 파일 선택기를 사용합니다.

            string path = UnityEditor.EditorUtility.OpenFilePanel(

                "Open Mod For Editing",

                "",

                "");



            if (string.IsNullOrWhiteSpace(path))

                return;



            OpenSelectedModForEditing(path);

#else

            if (CrossPlatformFileDialog.IsBusy)

                return;



            CrossPlatformFileDialog.PickFile(

                "Open Mod For Editing",

                "SDG Mod Package",

                GetModExtensions(),

                GetModFileTypes(),

                path =>

            {

                if (string.IsNullOrWhiteSpace(path))

                    return;



                OpenSelectedModForEditing(path);

            });

#endif

        }



        private async void OpenSelectedModForEditing(string path)

        {

            if (!IsSdgModFile(path))

            {

                SetStatus("선택한 파일은 .sdgmod 파일이 아닙니다.");

                return;

            }



            string candidatePath = null;



            try

            {

                candidatePath = saveLastImportedMod

                    ? CreateRecentModCandidate(path)

                    : path;



                GameObject editable = await OpenModForEditingFromPathAsync(candidatePath);



                if (saveLastImportedMod)

                    CommitRecentModCandidate(path);

                CleanupOlderStoredPackagesForImportedMod(editable, path);



                SetStatus("모드 편집 열기 완료: " + editable.name);

            }

            catch (Exception exception)

            {

                DeleteRecentModCandidate();

                ReportError(exception);

            }

        }



        /// <summary>

        /// UI Button에 연결: 최근 모드를 편집 대상으로 엽니다.

        /// </summary>

        public async void OpenRecentModForEditing()

        {

            string recentPath = GetRecentModPath();



            if (!File.Exists(recentPath))

            {

                NotifyRecentModAvailability();

                SetStatus("저장된 최근 모드가 없습니다.");

                return;

            }



            try

            {

                GameObject editable = await OpenModForEditingFromPathAsync(recentPath);

                SetStatus("최근 모드 편집 열기 완료: " + editable.name);

            }

            catch (Exception exception)

            {

                ReportError(exception);

            }

        }



        /// <summary>

        /// 경로의 모드를 편집 작업으로 엽니다. 외부 코드에서도 await하여 사용할 수 있습니다.

        /// </summary>

        public async Task<GameObject> OpenModForEditingFromPathAsync(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                throw new FileNotFoundException("편집할 모드 파일을 찾을 수 없습니다.", packagePath);

#if !MOD_INGAME
            if (editorImportInProgress)
                throw new InvalidOperationException("다른 모드를 불러오는 중입니다.");
            editorImportInProgress = true;
            try
            {
#endif
                SetStatus("기존 모드를 편집용으로 불러오는 중...");

                Transform workspaceParent = ResolveEditWorkspaceParentForImport();
                GameObject previousEditableRoot = currentEditableModRoot;

#if MOD_INGAME
                if (previousEditableRoot != null && !destroyPreviousEditableMod)
                {
                    throw new InvalidOperationException(
                        "이미 편집 중인 모드가 있습니다. CloseCurrentModEditing()을 먼저 호출하거나 " +
                        "destroyPreviousEditableMod를 활성화하세요.");
                }
#endif
                // Keep the previous edit target if the new package fails to load.
                GameObject imported = await packageImporter.ImportAsync(
                    packagePath,
                    workspaceParent);

                if (imported == null)
                    throw new InvalidOperationException("편집할 모드의 루트를 생성하지 못했습니다.");

                if (previousEditableRoot != null && previousEditableRoot != imported)
                {
                    previousEditableRoot.SetActive(false);
                    Destroy(previousEditableRoot);
                }

#if !MOD_INGAME
                // Switching from a directly imported GLB to an existing package:
                // remove all old model bindings from the editor workspace, not the
                // freshly imported package's own models.
                if (sceneBuildRoot != null)
                    RemovePreviousEditingModels(sceneBuildRoot, imported);
                if (workspaceParent != sceneBuildRoot)
                    RemovePreviousEditingModels(workspaceParent, imported);
                selectedTextureRenderer = null;
#endif

                currentEditableModRoot = imported;
                buildRoot = imported.transform;

                RuntimeModelBinding importedModel =
                    imported.GetComponentInChildren<RuntimeModelBinding>(true);
                mainModelRoot = importedModel != null
                    ? importedModel.gameObject
                    : null;

#if !MOD_INGAME
                // Extract after the new package and model hierarchy are loaded,
                // before notifying listeners about the newly opened editing root.
                ExtractLoadedEditingElements(imported, workspaceParent);
#endif

                RuntimeModPackageIdentity identity =
                    imported.GetComponent<RuntimeModPackageIdentity>();
                if (identity != null)
                {
                    if (!string.IsNullOrWhiteSpace(identity.DisplayName))
                        modName = identity.DisplayName;

                    author = identity.Author;
                }

                RuntimeModTextConfig importedConfig =
                    imported.GetComponent<RuntimeModTextConfig>();
                if (importedConfig != null)
                {
                    if (importedConfig.TryGetString("mod.name", out string configuredName) &&
                        !string.IsNullOrWhiteSpace(configuredName))
                    {
                        modName = configuredName;
                    }

                    if (importedConfig.TryGetString("mod.author", out string configuredAuthor))
                        author = configuredAuthor;

                    onImportedSettings?.Invoke(importedConfig);
                    onImportedSettingsText?.Invoke(importedConfig.TextContent);
                    onSettingsTextChanged?.Invoke(importedConfig.TextContent);
                }

                onObjectCreated?.Invoke(imported);
                onEditableModOpened?.Invoke(imported);
                onEditModeChanged?.Invoke(true);
                return imported;
#if !MOD_INGAME
            }
            finally
            {
                editorImportInProgress = false;
            }
#endif
        }


        public bool IsEditingExistingMod()

        {

            return currentEditableModRoot != null &&

                   buildRoot == currentEditableModRoot.transform;

        }



        /// <summary>

        /// UI Button에 연결: 현재 편집 작업을 닫고 씬의 원래 buildRoot로 돌아갑니다.

        /// </summary>

        public void CloseCurrentModEditing()

        {

            try

            {

                if (currentEditableModRoot != null)

                {

                    Destroy(currentEditableModRoot);

                    currentEditableModRoot = null;

                }



                buildRoot = IsUsableSceneTransform(

                    sceneBuildRoot,

                    SceneManager.GetActiveScene())

                    ? sceneBuildRoot

                    : null;



                mainModelRoot = null;

                selectedTextureRenderer = null;

                onEditModeChanged?.Invoke(false);



                InitializeBuildConfigIfAvailable();

                SetStatus("모드 편집 작업을 닫았습니다.");

            }

            catch (Exception exception)

            {

                ReportError(exception);

            }

        }



        // UI Button에 연결: .sdgmod 선택 후 importedModsParent 아래에 생성

        public void PickAndImportModPackage()

        {

            PickAndImportModPackage(null);

        }



        /// <summary>

        /// .sdgmod 파일 선택 후 모드를 불러옵니다.

        /// completedCallback은 모드가 정상적으로 생성된 경우에만 호출됩니다.

        /// </summary>

        public void PickAndImportModPackage(Action<GameObject> completedCallback)

        {

#if UNITY_EDITOR

            // Unity Editor에서는 확장자 필터를 사용하지 않습니다.

            // 따라서 .sdgmod를 포함한 모든 파일을 선택할 수 있습니다.

            string path = UnityEditor.EditorUtility.OpenFilePanel(

                "Open Mod Package",

                "",

                "");



            if (string.IsNullOrWhiteSpace(path))

                return;



            ImportSelectedModPackage(path, completedCallback);

#else

            if (CrossPlatformFileDialog.IsBusy)

                return;



            CrossPlatformFileDialog.PickFile(

                "Open Mod Package",

                "SDG Mod Package",

                GetModExtensions(),

                GetModFileTypes(),

                path =>

            {

                if (string.IsNullOrWhiteSpace(path))

                    return;



                ImportSelectedModPackage(path, completedCallback);

            });

#endif

        }



        private async void ImportSelectedModPackage(

            string path,

            Action<GameObject> completedCallback)

        {

            if (!IsSdgModFile(path))

            {

                SetStatus("선택한 파일은 .sdgmod 파일이 아닙니다.");

                return;

            }



            string candidatePath = null;



            try

            {

                // 네이티브 파일 선택기의 임시 경로가 사라지기 전에 앱 내부에 복사합니다.

                // 새 모드가 정상적으로 임포트된 뒤에만 기존 최근 모드를 교체합니다.

                candidatePath = saveLastImportedMod

                    ? CreateRecentModCandidate(path)

                    : path;



                GameObject imported = await ImportModFromPathAsync(candidatePath);



                if (saveLastImportedMod)

                    CommitRecentModCandidate(path);

                CleanupOlderStoredPackagesForImportedMod(imported, path);



                SetStatus("모드 불러오기 완료: " + imported.name);

                InvokeImportCompleted(

                    onModPackageImportCompleted,

                    imported,

                    completedCallback,

                    nameof(onModPackageImportCompleted));

            }

            catch (Exception exception)

            {

                DeleteRecentModCandidate();

                ReportError(exception);

            }

        }



        // UI Button에 연결: 파일 선택창 없이 앱 내부에 저장된 마지막 모드를 불러옵니다.

        public void LoadRecentModPackage()

        {

            LoadRecentModPackage(null);

        }



        /// <summary>

        /// 앱 내부에 저장된 최근 모드를 불러옵니다.

        /// completedCallback은 모드가 정상적으로 생성된 경우에만 호출됩니다.

        /// </summary>

        public async void LoadRecentModPackage(Action<GameObject> completedCallback)

        {

            string recentPath = GetRecentModPath();



            if (!File.Exists(recentPath))

            {

                NotifyRecentModAvailability();

                SetStatus("저장된 최근 모드가 없습니다.");

                return;

            }



            try

            {

                GameObject imported = await ImportModFromPathAsync(recentPath);

                SetStatus("최근 모드 불러오기 완료: " + imported.name);

                InvokeImportCompleted(

                    onRecentModPackageLoadCompleted,

                    imported,

                    completedCallback,

                    nameof(onRecentModPackageLoadCompleted));



                InvokeRecentModCSharpEvent(imported);

            }

            catch (Exception exception)

            {

                ReportError(exception);

            }

        }



        public bool HasRecentMod()

        {

            return File.Exists(GetRecentModPath());

        }



        public string GetRecentModOriginalFileName()

        {

            string metadataPath = GetRecentModNamePath();



            if (!File.Exists(metadataPath))

                return HasRecentMod() ? Path.GetFileName(GetRecentModPath()) : string.Empty;



            try

            {

                return File.ReadAllText(metadataPath).Trim();

            }

            catch

            {

                return Path.GetFileName(GetRecentModPath());

            }

        }



        // 필요하면 삭제 버튼에 연결할 수 있습니다.

        public void ClearRecentModPackage()

        {

            try

            {

                DeleteFileIfExists(GetRecentModPath());

                DeleteFileIfExists(GetRecentModNamePath());

                DeleteRecentModCandidate();

                NotifyRecentModAvailability();

                SetStatus("최근 모드 저장 데이터를 삭제했습니다.");

            }

            catch (Exception exception)

            {

                ReportError(exception);

            }

        }



        private void InvokeRecentModCSharpEvent(GameObject imported)

        {

            try

            {

                RecentModPackageLoadCompleted?.Invoke(imported);

            }

            catch (Exception callbackException)

            {

                Debug.LogError(

                    "RecentModPackageLoadCompleted C# callback 실행 중 오류가 발생했습니다.",

                    this);

                Debug.LogException(callbackException, this);

            }

        }



        private void InvokeImportCompleted(

            UnityEvent<GameObject> specificEvent,

            GameObject imported,

            Action<GameObject> completedCallback,

            string specificEventName)

        {

            // 콜백 코드에서 예외가 발생해도 이미 완료된 모드 임포트를 실패로 처리하지 않습니다.

            try

            {

                specificEvent?.Invoke(imported);

            }

            catch (Exception callbackException)

            {

                Debug.LogError($"{specificEventName} 리스너 실행 중 오류가 발생했습니다.", this);

                Debug.LogException(callbackException, this);

            }



            try

            {

                onAnyModImportCompleted?.Invoke(imported);

            }

            catch (Exception callbackException)

            {

                Debug.LogError("onAnyModImportCompleted 리스너 실행 중 오류가 발생했습니다.", this);

                Debug.LogException(callbackException, this);

            }



            try

            {

                completedCallback?.Invoke(imported);

            }

            catch (Exception callbackException)

            {

                Debug.LogError("모드 임포트 완료 C# 콜백 실행 중 오류가 발생했습니다.", this);

                Debug.LogException(callbackException, this);

            }

        }



#if !MOD_INGAME
        /// <summary>
        /// Finds the editor's original build root. A newly imported GLB must never
        /// be parented to the currently edited .sdgmod, which is about to be removed.
        /// </summary>
        private Transform ResolveModelBuildRootForEditing()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (IsUsableSceneTransform(sceneBuildRoot, scene) &&
                (currentEditableModRoot == null ||
                 !sceneBuildRoot.IsChildOf(currentEditableModRoot.transform)))
            {
                return sceneBuildRoot;
            }

            // If no scene buildRoot exists, make a standalone one in the workspace.
            Transform workspace = ResolveEditWorkspaceParentForImport();
            GameObject freshBuildRoot = new GameObject("ModBuildRoot");
            freshBuildRoot.transform.SetParent(workspace, false);
            freshBuildRoot.AddComponent<ModNode>();
            sceneBuildRoot = freshBuildRoot.transform;
            return sceneBuildRoot;
        }

        /// <summary>
        /// Remove old GLB model roots, but never delete the newly imported GLB,
        /// the newly opened .sdgmod, or any of their parent objects.
        /// </summary>
        private void RemovePreviousEditingModels(Transform workspace, GameObject keepRoot)
        {
            if (workspace == null || keepRoot == null)
                return;

            RuntimeModelBinding[] bindings =
                workspace.GetComponentsInChildren<RuntimeModelBinding>(true);

            foreach (RuntimeModelBinding binding in bindings)
            {
                if (binding == null)
                    continue;

                Transform candidate = binding.transform;
                Transform keep = keepRoot.transform;

                // The new model may itself contain many RuntimeModelBindings.
                // Also protect parents if the importer reused its existing node.
                if (candidate == keep || candidate.IsChildOf(keep) || keep.IsChildOf(candidate))
                    continue;

                // Never remove the workspace container itself.
                if (candidate == workspace)
                    continue;

                candidate.gameObject.SetActive(false);
                Destroy(candidate.gameObject);
            }
        }

        /// <summary>
        /// The imported root (and any nested model roots) can contain an
        /// ExtractElements component. Also support one attached directly to the
        /// editor build/workspace root. Each component runs once per new import.
        /// </summary>
        private void ExtractLoadedEditingElements(GameObject loadedRoot, Transform workspaceRoot)
        {
            if (loadedRoot == null)
                return;

            int extracted = 0;
            ExtractElements[] extractors =
                loadedRoot.GetComponentsInChildren<ExtractElements>(true);

            foreach (ExtractElements extractor in extractors)
            {
                if (extractor == null)
                    continue;
                try
                {
                    extractor.ExtractFromModel();
                    extracted++;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, extractor);
                }
            }

            if (workspaceRoot != null && workspaceRoot.gameObject != loadedRoot &&
                !workspaceRoot.IsChildOf(loadedRoot.transform))
            {
                ExtractElements[] onWorkspace = workspaceRoot.GetComponents<ExtractElements>();
                foreach (ExtractElements extractor in onWorkspace)
                {
                    if (extractor == null)
                        continue;
                    try
                    {
                        extractor.ExtractFromModel();
                        extracted++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex, extractor);
                    }
                }
            }

            if (extracted == 0)
                Debug.LogWarning("[MobileMod] ExtractElements was not found on the loaded editor roots: " + loadedRoot.name, loadedRoot);
        }
#endif

        private async Task<GameObject> ImportModFromPathAsync(string packagePath)

        {

            SetStatus("모드 불러오는 중...");



            Transform importParent = await ResolveImportedModsParentForImportAsync();



            // IMPORTANT:

            // 새 모드를 먼저 완전히 생성합니다. ImportAsync가 실패하면 예외가 발생하고

            // 기존 모드는 건드리지 않으므로 화면에 남아 있게 됩니다.

            GameObject imported = await packageImporter.ImportAsync(

                packagePath,

                importParent);



            if (imported == null)

                throw new InvalidOperationException("모드 패키지를 불러왔지만 생성된 루트 오브젝트가 없습니다.");



            // 새 모드가 완전히 성공한 뒤에만 이전 런타임 모드를 제거합니다.

            // Destroy는 프레임 끝에 처리되므로 SetActive(false)를 먼저 호출하여

            // 같은 프레임에 이전 모델이 화면에 남는 현상도 막습니다.

            if (keepOnlyOneImportedMod)

                RemovePreviousImportedMods(importParent, imported);



            currentImportedModRoot = imported;



            // 이전 모드를 가리킬 수 있는 선택 참조는 새 모드로 교체합니다.

            selectedTextureRenderer = null;



            RuntimeModelBinding importedModel =

                imported.GetComponentInChildren<RuntimeModelBinding>(true);



            mainModelRoot = importedModel != null

                ? importedModel.gameObject

                : null;



            RuntimeModTextConfig importedConfig =

                imported.GetComponent<RuntimeModTextConfig>();

            if (importedConfig != null)

            {

                onImportedSettings?.Invoke(importedConfig);

                onImportedSettingsText?.Invoke(importedConfig.TextContent);

            }



            onObjectCreated?.Invoke(imported);

            return imported;

        }



        /// <summary>

        /// importedModsParent 아래에서 새로 불러온 모드를 제외한 기존 모드 루트를 제거합니다.

        /// RuntimeModPackageIdentity가 붙은 루트만 제거하므로 importedModsParent의 다른 씬 오브젝트는 건드리지 않습니다.

        /// </summary>

        private void RemovePreviousImportedMods(Transform importParent, GameObject keepRoot)

        {

            if (importParent == null || keepRoot == null)

                return;



            // 1) 현재 추적 중인 기존 모드를 우선 제거합니다.

            if (currentImportedModRoot != null &&

                currentImportedModRoot != keepRoot &&

                currentImportedModRoot.transform.IsChildOf(importParent))

            {

                currentImportedModRoot.SetActive(false);

                Destroy(currentImportedModRoot);

            }



            // 2) 이전 버전에서 여러 모드가 이미 누적된 Play Session도 정리합니다.

            // ImportAsync가 생성한 실제 모드 루트에는 RuntimeModPackageIdentity가 있습니다.

            RuntimeModPackageIdentity[] identities =

                importParent.GetComponentsInChildren<RuntimeModPackageIdentity>(true);



            foreach (RuntimeModPackageIdentity identity in identities)

            {

                if (identity == null)

                    continue;



                GameObject oldRoot = identity.gameObject;



                if (oldRoot == keepRoot)

                    continue;



                // 다른 nested object를 실수로 삭제하지 않도록 importParent의 직계 자식인

                // 패키지 루트만 대상으로 합니다.

                if (oldRoot.transform.parent != importParent)

                    continue;



                oldRoot.SetActive(false);

                Destroy(oldRoot);

            }

        }





        private async Task<Transform> ResolveImportedModsParentForImportAsync()

        {

            if (!waitForSceneReferencesBeforeImport)

            {

                Scene activeScene = SceneManager.GetActiveScene();



                if (!IsUsableSceneTransform(importedModsParent, activeScene))

                    ResolveSceneReferences(activeScene);



                if (IsUsableSceneTransform(importedModsParent, activeScene))

                    return importedModsParent;



                if (createFallbackImportedModsParent)

                {

                    importedModsParent = CreateFallbackImportedModsParent(activeScene);

                    return importedModsParent;

                }



                throw new InvalidOperationException(

                    "현재 씬에서 importedModsParent를 찾을 수 없습니다. " +

                    "MobileModSceneReferences를 씬 오브젝트에 추가하세요.");

            }



            float waitStartedAt = Time.realtimeSinceStartup;

            bool waitingStatusSent = false;



            while (Instance == this && this != null)

            {

                Scene activeScene = SceneManager.GetActiveScene();

                MobileModSceneReferences sceneReferences =

                    FindSceneReferences(activeScene);



                if (sceneReferences != null)

                {

                    // 사용자가 요청한 대로 ResolveImportedModsParent()의 fallback을 사용하지 않고,

                    // 반드시 Inspector/코드에서 명시적으로 지정한 importedModsParent만 사용합니다.

                    Transform resolvedParent = sceneReferences.importedModsParent;



                    if (IsUsableSceneTransform(resolvedParent, activeScene))

                    {

                        importedModsParent = resolvedParent;

                        sceneBuildRoot = sceneReferences.ResolveBuildRoot();

                        sceneEditWorkspaceParent =

                            sceneReferences.ResolveEditWorkspaceParent();



                        return importedModsParent;

                    }

                }



                if (!waitingStatusSent)

                {

                    SetStatus(

                        "MobileModSceneReferences.importedModsParent가 준비될 때까지 기다리는 중...");

                    waitingStatusSent = true;

                }



                if (sceneReferencesWaitTimeoutSeconds > 0f &&

                    Time.realtimeSinceStartup - waitStartedAt >=

                    sceneReferencesWaitTimeoutSeconds)

                {

                    throw new TimeoutException(

                        "MobileModSceneReferences 또는 importedModsParent가 제한 시간 안에 준비되지 않았습니다.");

                }



                // Unity 메인 스레드의 다음 프레임까지 기다립니다.

                await Task.Yield();

            }



            throw new InvalidOperationException(

                "모드 매니저가 제거되어 importedModsParent 대기가 취소되었습니다.");

        }



        private Transform ResolveEditWorkspaceParentForImport()

        {

            Scene activeScene = SceneManager.GetActiveScene();



            if (!IsUsableSceneTransform(sceneEditWorkspaceParent, activeScene))

                ResolveSceneReferences(activeScene);



            if (IsUsableSceneTransform(sceneEditWorkspaceParent, activeScene))

                return sceneEditWorkspaceParent;



            if (IsUsableSceneTransform(sceneBuildRoot, activeScene))

                return sceneBuildRoot;



            string objectName = string.IsNullOrWhiteSpace(fallbackEditWorkspaceName)

                ? "ModEditWorkspace"

                : fallbackEditWorkspaceName.Trim();



            GameObject fallback = new GameObject(objectName);

            if (activeScene.IsValid() && activeScene.isLoaded)

                SceneManager.MoveGameObjectToScene(fallback, activeScene);



            sceneEditWorkspaceParent = fallback.transform;

            return sceneEditWorkspaceParent;

        }



        private string CreateRecentModCandidate(string sourcePath)

        {

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))

                throw new FileNotFoundException("선택한 모드 파일을 찾을 수 없습니다.", sourcePath);



            Directory.CreateDirectory(GetRecentModDirectory());



            long maxPackageBytes = (long)packageImporter.maxPackageSizeMb * 1024L * 1024L;
                #if MOD_INGAME
            if (new FileInfo(sourcePath).Length > maxPackageBytes){

                if (AlamTextManager.instance)

                {

                    AlamTextManager.instance.EnqueueMessage(LanguageManager.GetString(262));

                }

                throw new InvalidDataException("모드 파일이 허용 용량을 초과했습니다.");

            }
        #endif
            string candidatePath = GetRecentModCandidatePath();

            DeleteFileIfExists(candidatePath);

            File.Copy(sourcePath, candidatePath, true);

            return candidatePath;

        }



        private void CommitRecentModCandidate(string originalSourcePath)

        {

            string candidatePath = GetRecentModCandidatePath();

            if (!File.Exists(candidatePath))

                throw new FileNotFoundException("최근 모드 임시 저장 파일이 없습니다.", candidatePath);



            string recentPath = GetRecentModPath();

            string backupPath = recentPath + ".backup";



            DeleteFileIfExists(backupPath);



            try

            {

                if (File.Exists(recentPath))

                    File.Move(recentPath, backupPath);



                File.Move(candidatePath, recentPath);

                DeleteFileIfExists(backupPath);

            }

            catch

            {

                DeleteFileIfExists(recentPath);



                if (File.Exists(backupPath))

                    File.Move(backupPath, recentPath);



                throw;

            }



            string originalName = Path.GetFileName(originalSourcePath);

            File.WriteAllText(

                GetRecentModNamePath(),

                string.IsNullOrWhiteSpace(originalName) ? recentModFileName : originalName);



            NotifyRecentModAvailability();

        }



        private void DeleteRecentModCandidate()

        {

            DeleteFileIfExists(GetRecentModCandidatePath());

        }



        /// <summary>
        /// 앱 실행 시 이전에 누적된 .sdgmod 패키지를 삭제합니다.
        /// 파일명에 "last_loaded"가 들어간 패키지와 현재 RecentMod 파일은 유지합니다.
        /// Application.persistentDataPath/MobileModSystem 내부만 검사합니다.
        /// </summary>
        private void CleanupOldStoredModFilesOnStartup()
        {
            if (!deleteOldModFilesOnStart)
                return;

            string storageRoot = Path.Combine(Application.persistentDataPath, "MobileModSystem");
            if (!Directory.Exists(storageRoot))
                return;

            int deletedCount = 0;
            string recentPath = GetFullPathOrEmpty(GetRecentModPath());

            try
            {
                foreach (string filePath in Directory.EnumerateFiles(
                    storageRoot, "*", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(filePath);

                    // 파일 이름만 검사하여 폴더에 last_loaded가 들어가더라도
                    // 오래된 패키지 파일은 삭제합니다. 대소문자는 구분하지 않습니다.
                    if (!string.Equals(Path.GetExtension(name), ".sdgmod", StringComparison.OrdinalIgnoreCase) ||
                        name.IndexOf("last_loaded", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        string.Equals(GetFullPathOrEmpty(filePath), recentPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        File.Delete(filePath);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[MobileMod] 오래된 모드 파일 삭제 실패: " + filePath + " / " + ex.Message, this);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MobileMod] 시작 시 모드 파일 검색 실패: " + ex.Message, this);
            }

            if (deletedCount > 0)
                Debug.Log("[MobileMod] 시작 시 오래된 .sdgmod 파일 " + deletedCount + "개 삭제 완료.", this);
        }

        /// <summary>
        /// 동일 modId 패키지가 앱 내부 MobileModSystem 경로에 여러 개 존재할 때
        /// 성공적으로 불러온 최신 모드와 동일한 modId의 오래된 사본만 제거합니다.
        /// 사용자 파일 선택기의 원본(.sdgmod)은 절대 삭제하지 않습니다.
        /// GLB/텍스처/오디오 작업 파일은 별도의 참조가 있을 수 있으므로 건드리지 않습니다.
        /// </summary>
        private void CleanupOlderStoredPackagesForImportedMod(GameObject imported, string selectedSourcePath)
        {
            if (!deleteOlderStoredPackagesWithSameModId || imported == null)
                return;

            RuntimeModPackageIdentity identity = imported.GetComponent<RuntimeModPackageIdentity>();
            if (identity == null || string.IsNullOrWhiteSpace(identity.ModId))
                return;

            string storageRoot = Path.Combine(Application.persistentDataPath, "MobileModSystem");
            if (!Directory.Exists(storageRoot))
                return;

            string selectedFullPath = GetFullPathOrEmpty(selectedSourcePath);
            string recentFullPath = GetFullPathOrEmpty(GetRecentModPath());
            string pendingFullPath = GetFullPathOrEmpty(GetRecentModCandidatePath());
            int deletedCount = 0;

            try
            {
                foreach (string storedPath in Directory.EnumerateFiles(
                    storageRoot, "*.sdgmod", SearchOption.AllDirectories))
                {
                    string fullPath = GetFullPathOrEmpty(storedPath);
                    if (string.Equals(fullPath, selectedFullPath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(fullPath, recentFullPath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(fullPath, pendingFullPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        // 파일명이 달라도 manifest.json의 modId가 일치하면 같은 모드입니다.
                        string storedModId = TryReadModIdFromPackage(storedPath);
                        if (!string.IsNullOrWhiteSpace(storedModId) &&
                            string.Equals(storedModId, identity.ModId, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Delete(storedPath);
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[MobileMod] 중복 패키지 검사/삭제 실패: " + storedPath + " / " + ex.Message, this);
                    }
                }
            }
            catch (Exception ex)
            {
                // 정리 실패는 이미 완료된 모드 임포트 성공 상태에 영향을 주지 않습니다.
                Debug.LogWarning("[MobileMod] 저장된 모드 파일 검색 중 오류: " + ex.Message, this);
            }

            if (deletedCount > 0)
                Debug.Log("[MobileMod] 동일 modId의 오래된 .sdgmod 파일 " + deletedCount + "개 삭제 완료.", this);
        }

        private static string TryReadModIdFromPackage(string packagePath)
        {
            using (FileStream file = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive archive = new ZipArchive(file, ZipArchiveMode.Read))
            {
                ZipArchiveEntry manifestEntry = archive.GetEntry(ModPackageConstants.ManifestFileName);
                if (manifestEntry == null || manifestEntry.Length > 128L * 1024L)
                    return null;

                using (StreamReader reader = new StreamReader(manifestEntry.Open()))
                {
                    ModPackageManifest manifest = JsonUtility.FromJson<ModPackageManifest>(reader.ReadToEnd());
                    return manifest != null ? manifest.modId : null;
                }
            }
        }

        private static string GetFullPathOrEmpty(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);
        }

        private string GetRecentModDirectory()

        {

            return Path.Combine(

                Application.persistentDataPath,

                "MobileModSystem",

                "RecentMod");

        }



        private string GetRecentModPath()

        {

            string fileName = string.IsNullOrWhiteSpace(recentModFileName)

                ? "last_loaded.sdgmod"

                : ModPathUtility.MakeSafeFileName(recentModFileName, "last_loaded.sdgmod");



            if (!fileName.EndsWith(".sdgmod", StringComparison.OrdinalIgnoreCase))

                fileName += ".sdgmod";



            return Path.Combine(GetRecentModDirectory(), fileName);

        }



        private string GetRecentModCandidatePath()

        {

            return Path.Combine(GetRecentModDirectory(), "pending.sdgmod");

        }



        private string GetRecentModNamePath()

        {

            return Path.Combine(GetRecentModDirectory(), "last_loaded_name.txt");

        }



        private void NotifyRecentModAvailability()

        {

            onRecentModAvailabilityChanged?.Invoke(HasRecentMod());

        }



        private static void DeleteFileIfExists(string path)

        {

            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))

                File.Delete(path);

        }



        // 빈 그룹을 런타임에서 만들 때 사용합니다.

        public GameObject CreateEmptyModNode(string nodeName, Transform parent = null)

        {

            GameObject node = new GameObject(string.IsNullOrWhiteSpace(nodeName) ? "New Mod Node" : nodeName);

            node.transform.SetParent(parent != null ? parent : buildRoot, false);

            node.AddComponent<ModNode>();

            onObjectCreated?.Invoke(node);

            return node;

        }



        public void SetMainModelRoot(GameObject modelRoot)

        {

            mainModelRoot = modelRoot;



            if (mainModelRoot != null)

                GetOrCreateAudioStorage(mainModelRoot);

        }



        public GameObject GetOrCreateAudioStorage(GameObject modelRoot)

        {

            if (modelRoot == null)

                throw new ArgumentNullException(nameof(modelRoot));



            string storageName = string.IsNullOrWhiteSpace(audioStorageName)

                ? "AudioStorage"

                : audioStorageName.Trim();



            Transform existing = modelRoot.transform.Find(storageName);

            GameObject storage;



            if (existing != null)

            {

                storage = existing.gameObject;

            }

            else

            {

                storage = new GameObject(storageName);

                storage.transform.SetParent(modelRoot.transform, false);

            }



            if (storage.GetComponent<ModNode>() == null)

                storage.AddComponent<ModNode>();



            return storage;

        }



        private GameObject ResolveMainModelRoot()

        {

            if (mainModelRoot != null)

                return mainModelRoot;



            if (buildRoot == null)

                return null;



            RuntimeModelBinding[] models =

                buildRoot.GetComponentsInChildren<RuntimeModelBinding>(true);



            if (models.Length == 0)

                return null;



            // 가장 최근에 추가된 모델을 기본 메인 모델로 사용합니다.

            mainModelRoot = models[models.Length - 1].gameObject;

            return mainModelRoot;

        }



        private void SyncExportInfoFromConfig(RuntimeModTextConfig config)

        {

            if (config == null)

                return;



            if (config.TryGetString("mod.name", out string configuredName) &&

                !string.IsNullOrWhiteSpace(configuredName))

            {

                modName = configuredName;

            }



            if (config.TryGetString("mod.author", out string configuredAuthor))

                author = configuredAuthor;

        }



        private string GetDefaultSettingsTemplate()

        {

            if (defaultSettingsFile != null)

                return defaultSettingsFile.text;



            return defaultSettingsText;

        }



        private RuntimeModTextConfig GetOrCreateBuildConfig()

        {

            if (buildRoot == null)

                throw new InvalidOperationException("buildRoot가 지정되지 않았습니다.");



            RuntimeModTextConfig config = buildRoot.GetComponent<RuntimeModTextConfig>();

            if (config == null)

                config = buildRoot.gameObject.AddComponent<RuntimeModTextConfig>();



            return config;

        }



        private static GameObject FindOwningModNode(Transform current)

        {

            while (current != null)

            {

                if (current.GetComponent<ModNode>() != null)

                    return current.gameObject;

                current = current.parent;

            }



            return null;

        }



        private void SetStatus(string message)

        {

            Debug.Log("[MobileMod] " + message);

            onStatus?.Invoke(message);

        }



        private void ReportError(Exception exception)

        {

            Debug.LogException(exception);

            SetStatus("오류: " + exception.Message);

        }



        private static bool IsSdgModFile(string path)

        {

            return !string.IsNullOrWhiteSpace(path) &&

                   string.Equals(

                       Path.GetExtension(path),

                       ".sdgmod",

                       StringComparison.OrdinalIgnoreCase);

        }



        private static string[] GetModelExtensions()

        {

            return new[] { "glb" };

        }



        private static string[] GetTextureExtensions()

        {

            return new[] { "png", "jpg", "jpeg" };

        }



        private static string[] GetAudioExtensions()

        {

            return new[] { "wav", "mp3", "ogg", "aif", "aiff" };

        }



        private static string[] GetTextExtensions()

        {

            return new[] { "txt" };

        }



        private static string[] GetModExtensions()

        {

            return new[] { "sdgmod" };

        }



        private static string[] GetModelFileTypes()

        {

#if UNITY_ANDROID

            return new[] { "model/gltf-binary", "application/octet-stream" };

#elif UNITY_IOS

            return new[] { NativeFilePicker.ConvertExtensionToFileType("glb") };

#else

            return Array.Empty<string>();

#endif

        }



        private static string[] GetTextureFileTypes()

        {

#if UNITY_ANDROID

            return new[] { "image/png", "image/jpeg" };

#elif UNITY_IOS

            return new[] { "public.png", "public.jpeg" };

#else

            return Array.Empty<string>();

#endif

        }





        private static string[] GetAudioFileTypes()

        {

#if UNITY_ANDROID

            return new[] { "audio/wav", "audio/x-wav", "audio/mpeg", "audio/ogg", "audio/aiff", "audio/*" };

#elif UNITY_IOS

            return new[] { "public.audio" };

#else

            return Array.Empty<string>();

#endif

        }



        private static bool IsSupportedAudioFile(string path)

        {

            string extension = Path.GetExtension(path).ToLowerInvariant();

            return extension == ".wav" ||

                   extension == ".mp3" ||

                   extension == ".ogg" ||

                   extension == ".aif" ||

                   extension == ".aiff";

        }



        private static string[] GetTextFileTypes()

        {

#if UNITY_ANDROID

            return new[] { "text/plain" };

#elif UNITY_IOS

            return new[] { "public.plain-text" };

#else

            return Array.Empty<string>();

#endif

        }



        private static string[] GetModFileTypes()

        {

            // .sdgmod is a custom extension. Some OS file pickers grey it out when

            // the type isn't registered with the operating system. Show all files

            // and validate the selected package ourselves instead.

            return Array.Empty<string>();

        }

    }

}


