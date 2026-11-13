// UPDATED: 2026-07-31
// Added automatic extraction for Cam_FirstPerson, mirrors, FuelPoint, doors,
// multiple Exhaust/NumberPlate points, and vehicle mod settings.

using System;
using System.Collections.Generic;
using UnityEngine;
using MobileModSystem;

public class ExtractElements : MonoBehaviour
{
    #if MOD_INGAME
    private RCC_CarControllerV3 car;
    public ModLoader modLoader;
    #endif
    public GameObject Person_Position;
    [Header("자동차 색 재질")]
    public List <Material> PaintMat = new();
    public List <Material> CustomMat = new();

    [Header("검색할 모델 루트")]
    public GameObject RootObject;

    [Header("충돌체")]
    public List<GameObject> Colliders =
        new List<GameObject>();

    [Header("바퀴 메시")]
    public GameObject Wheel_FL;
    public GameObject Wheel_FR;
    public GameObject Wheel_RR;
    public GameObject Wheel_RL;

    [Header("생성된 WheelCollider")]
    public WheelCollider WheelCollider_FL;
    public WheelCollider WheelCollider_FR;
    public WheelCollider WheelCollider_RR;
    public WheelCollider WheelCollider_RL;

    [Header("WheelCollider 설정")]
    [Min(0.01f)]
    public float WheelRadiusMultiplier = 1f;

    [Min(0f)]
    public float WheelMass = 20f;

    [Min(0f)]
    public float SuspensionDistance = 0.2f;

    [Header("핸들")]
    public GameObject SteeringWheel;

    [Header("오브젝트 스폰, 이동 위치들")]
    public GameObject MirrorRight;
    public GameObject MirrorLeft;
    public GameObject MirrorBack;
    public List<GameObject> Exhaust =
        new List<GameObject>();
    public List<GameObject> Numberplate =
        new List<GameObject>();
    public GameObject FuelPoint;
    public GameObject FPSCamPoint;

    [Header("문 및 트렁크")]
    public GameObject Door_Left;
    public GameObject Door_Right;
    public GameObject Door_Left_Gul;
    public GameObject Door_Right_Gul;
    public GameObject Door_Fold;
    public GameObject Door_Slide;
    public GameObject BusDoor_Slide;
    public GameObject BusDoor_FoldRight;
    public GameObject BusDoor_FoldLeft;
    public GameObject BusDoor_InFoldRight;
    public GameObject BusDoor_InFoldLeft;
    public GameObject Trunk;

    [Header("조명")]
    public GameObject Light_Run;
    public GameObject Light_Brake;
    public GameObject Light_Head;
    public GameObject Light_Rev;
    public GameObject Light_Left;
    public GameObject Light_Right;

    [Header("무게중심")]
    public Transform COM;

    [Header("자동차 물리")]
    public Rigidbody rb;

    public RuntimeModTextConfig rt;

    private const string GeneratedWheelColliderPrefix =
        "Generated_WheelCollider_";

    private const string GeneratedWheelPivotPrefix =
        "Generated_WheelPivot_";

    [ContextMenu("Extract From Model")]
    public void ExtractFromModel()
    {
        print("startExtract");
        Exhaust.Clear();
        Colliders.Clear();
        Numberplate.Clear();
        PaintMat.Clear();
        CustomMat.Clear();
        #if MOD_INGAME
        car = gameObject.GetComponent<RCC_CarControllerV3>();
        #endif
        /*
         * 현재 모델을 먼저 저장합니다.
         * ClearExtractedReferences()에서 RootObject도 초기화되기 때문입니다.
         */
        GameObject previousRoot = RootObject;

        /*
         * 새롭게 추가된 RuntimeModTextConfig 검색
         */
        RuntimeModTextConfig selectedConfig =
            FindNewRuntimeConfig(previousRoot);

        GameObject nextRoot = null;

        if (selectedConfig != null)
        {
            nextRoot = selectedConfig.gameObject;
        }
        else if (previousRoot != null)
        {
            /*
             * 새로운 모델이 없다면 현재 모델을 다시 추출합니다.
             */
            selectedConfig =
                previousRoot.GetComponentInChildren
                    <RuntimeModTextConfig>(true);

            nextRoot = previousRoot;
        }

        if (selectedConfig == null || nextRoot == null)
        {
            Debug.LogError(
                "추출할 RuntimeModTextConfig 또는 모델 루트를 찾지 못했습니다.",
                this
            );

            return;
        }

        bool replacingPreviousRoot =
            previousRoot != null &&
            previousRoot != nextRoot;

        bool canDestroyPreviousRoot =
            replacingPreviousRoot &&
            !nextRoot.transform.IsChildOf(previousRoot.transform);

        /*
         * 이전 실행에서 만들어진 WheelCollider와 상태를 먼저 제거합니다.
         */
        CleanupPreviousExtraction(
            previousRoot,
            canDestroyPreviousRoot
        );

        /*
         * 이전 실행의 모든 변수 참조 초기화
         */
        ClearExtractedReferences();

        /*
         * 새로운 모델 정보 등록
         */
        #if MOD_INGAME
        PlayerCarDataManager.instance.AddModCarHashtoDictionary(car.gameObject.GetComponent<CarCustomizerComp>().CarInd,modLoader.GetModHash());
        #endif
        RootObject = nextRoot;
        rt = selectedConfig;

        rb = RootObject.GetComponentInParent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError(
                $"{RootObject.name}의 부모에서 Rigidbody를 찾지 못했습니다.",
                RootObject
            );

            return;
        }

        /*
         * 차량 무게 적용
         */
        float mass;

        if (rt.TryGetFloat(
                "car.physics.mass",
                out mass
            ))
        {
            rb.mass = Mathf.Clamp(mass,1000,30000);
        }
        else
        {
            rb.mass = 1200f;
        }

        /*
         * 설정 파일의 엔진, 오디오, 카메라 값을 차량에 적용합니다.
         * 재질은 아래 ExtractElementRecursive()에서 Renderer를 검색하며 적용됩니다.
         */
        ApplyVehicleSettings();


        /*
         * 1차: 정확한 이름이 일치하는 오브젝트를 먼저 등록합니다.
         * 2차: 아직 등록되지 않은 요소만 부분 이름으로 검색합니다.
         *
         * 예: Wheel_FL과 Wheel_FL_Mesh가 둘 다 존재하면
         * 정확히 Wheel_FL인 오브젝트가 항상 우선됩니다.
         */
        AssignExactNameMatchesRecursive(RootObject.transform);
        ExtractElementRecursive(RootObject.transform);

        /*
         * Exhaust 1, Exhaust 2 / NumberPlate 1, NumberPlate 2 순서로 정렬합니다.
         */
        SortIndexedObjectLists();

        /*
         * 바퀴 Renderer의 실제 중심에 Pivot 부모를 생성합니다.
         * 원본 모델의 피봇이 중앙에서 벗어나 있어도
         * Wheel_FL / FR / RL / RR 참조는 생성된 중심 Pivot을 사용합니다.
         */
        GenerateCenteredWheelPivots();

        /*
         * 중심 Pivot을 기준으로 WheelCollider 생성
         */
        GenerateWheelColliders();

        Debug.Log(
            $"모델 요소 추출 완료\n" +
            $"Root: {RootObject.name}\n" +
            $"Colliders: {Colliders.Count}\n" +
            $"Wheel FL: {GetObjectName(Wheel_FL)}\n" +
            $"Wheel FR: {GetObjectName(Wheel_FR)}\n" +
            $"Wheel RL: {GetObjectName(Wheel_RL)}\n" +
            $"Wheel RR: {GetObjectName(Wheel_RR)}\n" +
            $"WheelCollider FL: " +
            $"{GetWheelColliderName(WheelCollider_FL)}\n" +
            $"WheelCollider FR: " +
            $"{GetWheelColliderName(WheelCollider_FR)}\n" +
            $"WheelCollider RL: " +
            $"{GetWheelColliderName(WheelCollider_RL)}\n" +
            $"WheelCollider RR: " +
            $"{GetWheelColliderName(WheelCollider_RR)}\n" +
            $"First Person Camera: {GetObjectName(FPSCamPoint)}\n" +
            $"Mirror Left: {GetObjectName(MirrorLeft)}\n" +
            $"Mirror Right: {GetObjectName(MirrorRight)}\n" +
            $"Mirror Back: {GetObjectName(MirrorBack)}\n" +
            $"Fuel Point: {GetObjectName(FuelPoint)}\n" +
            $"Exhaust Count: {Exhaust.Count}\n" +
            $"NumberPlate Count: {Numberplate.Count}\n" +
            $"Door Left: {GetObjectName(Door_Left)}\n" +
            $"Door Right: {GetObjectName(Door_Right)}\n" +
            $"Door Fold: {GetObjectName(Door_Fold)}\n" +
            $"Door Slide: {GetObjectName(Door_Slide)}\n" +
            $"Trunk: {GetObjectName(Trunk)}",
            this
        );

        /*
         * 새로운 모델로 교체된 경우 기존 모델 삭제
         */
        if (canDestroyPreviousRoot)
        {
            previousRoot.SetActive(false);
            DestroyUnityObject(previousRoot);
        }
        else if (replacingPreviousRoot)
        {
            Debug.LogWarning(
                "새 RootObject가 이전 RootObject의 자식이므로 " +
                "이전 RootObject를 삭제하지 않았습니다.",
                previousRoot
            );
        }
        #if MOD_INGAME
        if (modLoader != null)
        {
            modLoader.Initialize();
        }
        else
        {
            Debug.LogWarning(
                "modLoader가 설정되지 않았습니다.",
                this
            );
        }
        #endif
    }

    private void ApplyVehicleSettings()
    {
        #if MOD_INGAME
        if (rt == null)
            return;

        if (car == null)
        {
            car = GetComponent<RCC_CarControllerV3>();

            if (car == null)
                car = GetComponentInParent<RCC_CarControllerV3>();

            if (car == null && rb != null)
                car = rb.GetComponent<RCC_CarControllerV3>();
        }

        if (car == null)
        {
            Debug.LogError(
                "RCC_CarControllerV3를 찾지 못해 차량 설정을 적용할 수 없습니다.",
                this
            );
            return;
        }

        ApplyEngineSettings();
        ApplyAudioSettings();
        ApplyCameraSettings();

        // 최대속도가 변경되었으면 RCC 기어 속도/토크 커브도 갱신합니다.
        if (car.gears != null && car.gears.Length > 0)
        {
            car.CreateGearCurves();
        }
        #endif
    }
    #if MOD_INGAME
    private void ApplyEngineSettings()
    {

        bool bval = false;
        if (rt.TryGetBool("car.engine.Electric", out bval)){
            car.IsItAnElectric = bval;
        }
        if (rt.TryGetBool("car.engine.reverseBeep", out bval)){
            car.op.revseBeep = bval;
        }
        if (rt.TryGetBool("car.engine.airBrake", out bval)){
            car.op.modEnableAirbrake = bval;
        }
        

        float value;

        if (rt.TryGetFloat("car.engine.maxSpeed", out value))
            car.maxspeed = value;

        if (rt.TryGetFloat("car.engine.engineTorque", out value))
            car.engineTorque = value;

        if (rt.TryGetFloat("car.engine.brakeTorque", out value))
            car.brakeTorque = value;

        if (rt.TryGetFloat("car.engine.downforce", out value))
            car.downForce = value;

        if (rt.TryGetFloat("car.engine.rpmLow", out value))
            car.minEngineRPM = value;

        if (rt.TryGetFloat("car.engine.rpmHigh", out value))
            car.maxEngineRPM = value;

        if (rt.TryGetFloat("car.engine.fuelTankCapacity", out value))
        {
            car.fuelTankCapacity = Mathf.Max(0f, value);
            car.fuelTank = Mathf.Clamp(
                car.fuelTank,
                0f,
                car.fuelTankCapacity
            );
        }

        bool boolValue;

        if (rt.TryGetBool("car.engine.backFire", out boolValue))
            car.useExhaustFlame = boolValue;
    }

    private void ApplyAudioSettings()
    {
        string fileName;
        AudioClip clip;
        float value;

        if (rt.TryGetString("car.audio.engineSound", out fileName))
        {
            clip = FindAudioClip(fileName);

            if (clip != null)
            {
                car.engineClipHigh = clip;
                car.engineClipMed = clip;
                car.engineClipLow = clip;

                SetExistingAudioSourceClip(
                    clip,
                    "Engine Sound High",
                    "Engine Sound Medium",
                    "Engine Sound Low"
                );
            }
            else
            {
                LogMissingAudio(fileName);
            }
        }

        if (rt.TryGetString("car.audio.idleRPM", out fileName))
        {
            clip = FindAudioClip(fileName);

            if (clip != null)
            {
                car.engineClipIdle = clip;
                SetExistingAudioSourceClip(clip, "Engine Sound Idle");
            }
            else
            {
                LogMissingAudio(fileName);
            }
        }

        if (rt.TryGetString("car.audio.engineStart", out fileName))
        {
            clip = FindAudioClip(fileName);

            if (clip != null)
            {
                car.engineStartClip = clip;
                SetExistingAudioSourceClip(clip, "Engine Start");
            }
            else
            {
                LogMissingAudio(fileName);
            }
        }

        if (rt.TryGetFloat("car.audio.idleEngineVolume", out value))
            car.idleEngineSoundVolume = Mathf.Clamp01(value);

        if (rt.TryGetFloat("car.audio.minEnginePitch", out value))
            car.minEngineSoundPitch = Mathf.Max(0f, value);

        if (rt.TryGetFloat("car.audio.maxEnginePitch", out value))
            car.maxEngineSoundPitch = Mathf.Max(0f, value);

        if (rt.TryGetFloat("car.audio.minEngineVolume", out value))
            car.minEngineSoundVolume = Mathf.Clamp01(value);

        if (rt.TryGetFloat("car.audio.maxEngineVolume", out value))
            car.maxEngineSoundVolume = Mathf.Clamp01(value);
    }

    private void ApplyCameraSettings()
    {
        float value;

        // 기존 설정 파일의 hieght 오타와 올바른 height를 모두 지원합니다.
        if (rt.TryGetFloat("car.camera.height", out value) ||
            rt.TryGetFloat("car.camera.hieght", out value))
        {
            car.TPSOffsetY = value;
        }

        if (rt.TryGetFloat("car.camera.distance", out value))
            car.TPSDistance = value;
    }
    #endif

    private AudioClip FindAudioClip(string configuredFileName)
    {
        if (string.IsNullOrWhiteSpace(configuredFileName))
            return null;

        string requestedName = NormalizeAssetName(configuredFileName);

        if (RootObject != null)
        {
            AudioSource[] sources =
                RootObject.GetComponentsInChildren<AudioSource>(true);

            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];

                if (source == null || source.clip == null)
                    continue;

                if (string.Equals(
                        NormalizeAssetName(source.clip.name),
                        requestedName,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    return source.clip;
                }
            }
        }

        // 모드 Importer가 생성해 현재 메모리에 로드한 오디오도 검색합니다.
        AudioClip[] loadedClips =
            Resources.FindObjectsOfTypeAll<AudioClip>();

        for (int i = 0; i < loadedClips.Length; i++)
        {
            AudioClip loadedClip = loadedClips[i];

            if (loadedClip == null)
                continue;

            if (string.Equals(
                    NormalizeAssetName(loadedClip.name),
                    requestedName,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return loadedClip;
            }
        }

        return null;
    }
    
      #if MOD_INGAME
    private void SetExistingAudioSourceClip(AudioClip clip, params string[] audioObjectNameParts)
    {
        if (clip == null || car == null)
            return;

        AudioSource[] sources =
            car.GetComponentsInChildren<AudioSource>(true);

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];

            if (source == null)
                continue;

            for (int j = 0; j < audioObjectNameParts.Length; j++)
            {
                if (source.gameObject.name.IndexOf(
                        audioObjectNameParts[j],
                        StringComparison.OrdinalIgnoreCase
                    ) < 0)
                {
                    continue;
                }

                source.clip = clip;
                break;
            }
        }
    }
#endif
    private string NormalizeAssetName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string result = value.Trim().Replace('\\', '/');

        int slashIndex = result.LastIndexOf('/');

        if (slashIndex >= 0 && slashIndex < result.Length - 1)
            result = result.Substring(slashIndex + 1);

        int extensionIndex = result.LastIndexOf('.');

        if (extensionIndex > 0)
            result = result.Substring(0, extensionIndex);

        result = result.Replace(" (Instance)", string.Empty);
        result = result.Replace("(Instance)", string.Empty);

        return result.Trim();
    }

    private void LogMissingAudio(string fileName)
    {
        Debug.LogWarning(
            $"설정에 지정된 AudioClip을 찾지 못했습니다: {fileName}",
            RootObject != null ? RootObject : gameObject
        );
    }

    private RuntimeModTextConfig FindNewRuntimeConfig(
        GameObject previousRoot
    )
    {
        RuntimeModTextConfig[] configs =
            gameObject.GetComponentsInChildren
                <RuntimeModTextConfig>(true);

        for (int i = 0; i < configs.Length; i++)
        {
            RuntimeModTextConfig config = configs[i];

            if (config == null)
                continue;

            if (previousRoot == null)
                return config;

            /*
             * 이전 모델과 이전 모델의 자식에 있는 Config는 제외합니다.
             */
            if (config.gameObject == previousRoot)
                continue;

            if (config.transform.IsChildOf(previousRoot.transform))
                continue;

            return config;
        }

        return null;
    }

    private void CleanupPreviousExtraction(
        GameObject previousRoot,
        bool previousRootWillBeDestroyed
    )
    {
        if (previousRoot == null)
            return;

        Transform cleanupRoot = previousRoot.transform;

        Rigidbody previousRigidbody =
            previousRoot.GetComponentInParent<Rigidbody>();

        if (previousRigidbody != null)
        {
            cleanupRoot = previousRigidbody.transform;
        }

        HashSet<GameObject> removedObjects =
            new HashSet<GameObject>();

        /*
         * 이 스크립트가 이전 실행에서 붙인 Marker 검색
         */
        ExtractGeneratedMarker[] markers =
            cleanupRoot.GetComponentsInChildren
                <ExtractGeneratedMarker>(true);

        for (int i = markers.Length - 1; i >= 0; i--)
        {
            ExtractGeneratedMarker marker = markers[i];

            if (marker == null)
                continue;

            if (marker.owner != this)
                continue;

            /*
             * 자동 생성된 Wheel Pivot인 경우
             *
             * Pivot을 삭제하기 전에 원래 바퀴 모델을 부모 밖으로 꺼내
             * 기존 계층으로 복구합니다. 그렇지 않으면 Pivot 삭제 시
             * 원래 바퀴 모델까지 같이 삭제될 수 있습니다.
             */
            if (marker.generatedWheelPivot)
            {
                Transform pivotTransform =
                    marker.transform;

                Transform pivotParent =
                    pivotTransform.parent;

                int pivotSiblingIndex =
                    pivotTransform.GetSiblingIndex();

                while (pivotTransform.childCount > 0)
                {
                    Transform child =
                        pivotTransform.GetChild(0);

                    child.SetParent(
                        pivotParent,
                        true
                    );

                    if (pivotParent != null)
                    {
                        int targetSiblingIndex =
                            Mathf.Clamp(
                                pivotSiblingIndex,
                                0,
                                pivotParent.childCount - 1
                            );

                        child.SetSiblingIndex(
                            targetSiblingIndex
                        );
                    }
                }

                GameObject pivotObject =
                    pivotTransform.gameObject;

                if (pivotObject != null &&
                    removedObjects.Add(pivotObject))
                {
                    pivotObject.SetActive(false);
                    DestroyUnityObject(pivotObject);
                }

                continue;
            }

            /*
             * WheelCollider처럼 GameObject 자체를 생성한 경우
             */
            if (marker.generatedGameObject)
            {
                GameObject generatedObject =
                    marker.gameObject;

                if (generatedObject != null &&
                    removedObjects.Add(generatedObject))
                {
                    generatedObject.SetActive(false);
                    DestroyUnityObject(generatedObject);
                }

                continue;
            }

            /*
             * 현재 모델을 다시 추출하는 경우,
             * 이전에 껐던 Renderer를 잠시 복원합니다.
             */
            if (!previousRootWillBeDestroyed &&
                marker.disabledRenderer)
            {
                Renderer rendererComponent =
                    marker.GetComponent<Renderer>();

                if (rendererComponent != null)
                {
                    rendererComponent.enabled = true;
                }

                marker.disabledRenderer = false;
            }

            /*
             * MeshCollider는 같은 모델을 다시 추출할 때 재사용합니다.
             * 새 모델로 교체될 때는 이전 RootObject와 함께 삭제됩니다.
             */
        }

        /*
         * 이전 버전 스크립트에서 생성되어 Marker가 없는
         * WheelCollider 오브젝트도 이름으로 찾아 제거합니다.
         */
        WheelCollider[] previousWheelColliders =
            cleanupRoot.GetComponentsInChildren<WheelCollider>(true);

        for (int i = previousWheelColliders.Length - 1;
             i >= 0;
             i--)
        {
            WheelCollider wheelCollider =
                previousWheelColliders[i];

            if (wheelCollider == null)
                continue;

            GameObject wheelColliderObject =
                wheelCollider.gameObject;

            if (!wheelColliderObject.name.StartsWith(
                    GeneratedWheelColliderPrefix,
                    StringComparison.Ordinal
                ))
            {
                continue;
            }

            if (!removedObjects.Add(wheelColliderObject))
                continue;

            wheelColliderObject.SetActive(false);
            DestroyUnityObject(wheelColliderObject);
        }
    }

    private void ClearExtractedReferences()
    {
        Colliders.Clear();

        Wheel_FL = null;
        Wheel_FR = null;
        Wheel_RR = null;
        Wheel_RL = null;

        WheelCollider_FL = null;
        WheelCollider_FR = null;
        WheelCollider_RR = null;
        WheelCollider_RL = null;

        SteeringWheel = null;

        MirrorRight = null;
        MirrorLeft = null;
        MirrorBack = null;
        Exhaust.Clear();
        Numberplate.Clear();
        FuelPoint = null;
        FPSCamPoint = null;

        Door_Left = null;
        Door_Right = null;
        BusDoor_FoldLeft = null;
        BusDoor_FoldRight = null;
        BusDoor_InFoldLeft = null;
        BusDoor_InFoldRight = null;
        Door_Fold = null;
        Door_Slide = null;
        Trunk = null;

        Light_Run = null;
        Light_Brake = null;
        Light_Head = null;
        Light_Rev = null;
        Light_Left = null;
        Light_Right = null;

        COM = null;

        CustomMat.Clear();
        PaintMat.Clear();

        rb = null;
        rt = null;
        RootObject = null;
    }

    /// <summary>
    /// 부분 문자열 검색보다 먼저 실행되는 정확한 이름 우선 검색입니다.
    /// NormalizeObjectName()을 사용하므로 대소문자와 공백, '_', '-' 차이는
    /// 무시하지만, 이름에 추가 문자가 붙은 오브젝트는 일치하지 않습니다.
    /// </summary>
    private void AssignExactNameMatchesRecursive(Transform target)
    {
        if (target == null)
            return;

        // 자동 생성된 WheelCollider는 모델 요소로 사용하지 않습니다.
        if (target.GetComponent<WheelCollider>() != null)
            return;

        if (target.name.StartsWith(
                GeneratedWheelColliderPrefix,
                StringComparison.Ordinal
            ) ||
            target.name.StartsWith(
                GeneratedWheelPivotPrefix,
                StringComparison.Ordinal
            ))
        {
            return;
        }

        string objectName = target.name;

        // 무게중심
        if (COM == null &&
            MatchesAnyObjectName(objectName, "COM"))
        {
            COM = target;
        }

        // 바퀴 메시
        if (Wheel_FL == null &&
            MatchesAnyObjectName(
                objectName,
                "Wheel_FL",
                "WheelFL",
                "Wheel_F_L",
                "FrontLeftWheel",
                "Front_Left_Wheel"
            ))
        {
            Wheel_FL = target.gameObject;
        }
        else if (Wheel_FR == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Wheel_FR",
                     "WheelFR",
                     "Wheel_F_R",
                     "FrontRightWheel",
                     "Front_Right_Wheel"
                 ))
        {
            Wheel_FR = target.gameObject;
        }
        else if (Wheel_RL == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Wheel_RL",
                     "WheelRL",
                     "Wheel_R_L",
                     "RearLeftWheel",
                     "Rear_Left_Wheel"
                 ))
        {
            Wheel_RL = target.gameObject;
        }
        else if (Wheel_RR == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Wheel_RR",
                     "WheelRR",
                     "Wheel_R_R",
                     "RearRightWheel",
                     "Rear_Right_Wheel"
                 ))
        {
            Wheel_RR = target.gameObject;
        }

        // 핸들
        if (SteeringWheel == null &&
            MatchesAnyObjectName(
                objectName,
                "SteeringWheel",
                "Steering_Wheel",
                "SteerWheel"
            ))
        {
            SteeringWheel = target.gameObject;
        }

        // 1인칭 카메라 위치
        if (FPSCamPoint == null &&
            MatchesAnyObjectName(
                objectName,
                "Cam_FirstPerson",
                "CamFirstPerson",
                "FirstPersonCamera",
                "FPSCamPoint"
            ))
        {
            FPSCamPoint = target.gameObject;
        }

        // 미러 위치
        if (MirrorLeft == null &&
            MatchesAnyObjectName(
                objectName,
                "Mirror_Left",
                "MirrorLeft"
            ))
        {
            MirrorLeft = target.gameObject;
        }
        else if (MirrorRight == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Mirror_Right",
                     "MirrorRight"
                 ))
        {
            MirrorRight = target.gameObject;
        }
        else if (MirrorBack == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Mirror_Back",
                     "MirrorBack",
                     "RearViewMirrorPoint"
                 ))
        {
            MirrorBack = target.gameObject;
        }

        // 주유구 위치
        if (FuelPoint == null &&
            MatchesAnyObjectName(
                objectName,
                "FuelPoint",
                "Fuel_Point",
                "FuelPort"
            ))
        {
            FuelPoint = target.gameObject;
        }

        // 문과 트렁크
        if (Door_Left == null &&
            MatchesAnyObjectName(
                objectName,
                "Door_Left",
                "DoorLeft"
            ))
        {
            Door_Left = target.gameObject;
        }
        else if (Door_Right == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Door_Right",
                     "DoorRight"
                 ))
        {
            Door_Right = target.gameObject;
        }
         if (Door_Left_Gul == null &&
            MatchesAnyObjectName(
                objectName,
                "Door_Gul_Left",
                "DoorGulLeft"
            ))
        {
            Door_Left_Gul = target.gameObject;
        }
        else if (Door_Right_Gul == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Door_Gul_Right",
                     "DoorGulRight"
                 ))
        {
            Door_Right_Gul = target.gameObject;
        }
        else if (Person_Position == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Person_Position",
                     "PersonPosition"
                 ))
        {
            Person_Position = target.gameObject;
        }
        else if (BusDoor_FoldRight == null && MatchesAnyObjectName( objectName,"BusDoor_FoldRight","BusDoorFoldRight" ))
        {
            BusDoor_FoldRight = target.gameObject;
        }
        else if (BusDoor_FoldLeft == null &&MatchesAnyObjectName(objectName,"BusDoor_FoldLeft","BusDoor_FoldLeft"))
        {
            BusDoor_FoldLeft = target.gameObject;
        }
        else if (BusDoor_InFoldRight == null && MatchesAnyObjectName( objectName,"BusDoor_InFoldRight","BusDoorInFoldRight" ))
        {
            BusDoor_InFoldRight = target.gameObject;
        }
        else if (BusDoor_InFoldLeft == null &&MatchesAnyObjectName(objectName,"BusDoor_InFoldLeft","BusDoor_InFoldLeft"))
        {
            BusDoor_InFoldLeft = target.gameObject;
        }
         else if (BusDoor_Slide == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "BusDoor_Slide",
                     "BusDoor_Slide",
                     "busdoor_slide"
                 ))
        {
            BusDoor_Slide = target.gameObject;
        }
        else if (Door_Slide == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Door_Slide",
                     "DoorSlide",
                     "SlidingDoor"
                 ))
        {
            Door_Slide = target.gameObject;
        }
        else if (Trunk == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Trunk",
                     "Door_Trunk",
                     "DoorTrunk",
                     "Boot"
                 ))
        {
            Trunk = target.gameObject;
        }

        // 조명
        if (Light_Run == null &&
            MatchesAnyObjectName(
                objectName,
                "Light_Run",
                "LightRun",
                "RunningLight",
                "DayLight",
                "DRL"
            ))
        {
            Light_Run = target.gameObject;
        }
        else if (Light_Brake == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Light_Brake",
                     "LightBrake",
                     "BrakeLight",
                     "StopLight"
                 ))
        {
            Light_Brake = target.gameObject;
        }
        else if (Light_Head == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Light_Head",
                     "LightHead",
                     "HeadLight",
                     "Headlamp"
                 ))
        {
            Light_Head = target.gameObject;
        }
        else if (Light_Rev == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Light_Rev",
                     "LightRev",
                     "ReverseLight",
                     "BackupLight"
                 ))
        {
            Light_Rev = target.gameObject;
        }
        else if (Light_Left == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Light_Left",
                     "LightLeft",
                     "LeftIndicator",
                     "LeftTurnSignal",
                     "Indicator_L"
                 ))
        {
            Light_Left = target.gameObject;
        }
        else if (Light_Right == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Light_Right",
                     "LightRight",
                     "RightIndicator",
                     "RightTurnSignal",
                     "Indicator_R"
                 ))
        {
            Light_Right = target.gameObject;
        }

        for (int i = 0; i < target.childCount; i++)
        {
            AssignExactNameMatchesRecursive(target.GetChild(i));
        }
    }

    private void ExtractElementRecursive(Transform target)
    {
        if (target == null)
            return;

        /*
         * 자동 생성된 WheelCollider는 검색하지 않습니다.
         */
        if (target.GetComponent<WheelCollider>() != null)
            return;

        if (target.name.StartsWith(
                GeneratedWheelColliderPrefix,
                StringComparison.Ordinal
            ) ||
            target.name.StartsWith(
                GeneratedWheelPivotPrefix,
                StringComparison.Ordinal
            ))
        {
            return;
        }

        string objectName = target.name;

        /*
         * 자동차 재질 검색
         */
        Renderer targetRenderer =
            target.GetComponent<Renderer>();

        if (targetRenderer != null)
        {
            ExtractMaterials(targetRenderer);
        }

        /*
         * 일반 충돌체 검색 및 MeshCollider 추가
         */
        if (ContainsAny(
                objectName,
                "Collider",
                "Collision"
            ))
        {
            SetupModelCollider(target);
        }

        /*
         * 무게중심
         */
        if (COM == null &&
            ContainsAny(
                objectName,
                "COM"
            ))
        {
            COM = target;
        }

        /*
         * 앞 왼쪽 바퀴
         */
        if (Wheel_FL == null &&
            ContainsAny(
                objectName,
                "Wheel_FL",
                "WheelFL",
                "Wheel_F_L",
                "FrontLeftWheel",
                "Front_Left_Wheel"
            ))
        {
            Wheel_FL = target.gameObject;
        }

        /*
         * 앞 오른쪽 바퀴
         */
        else if (Wheel_FR == null &&
                 ContainsAny(
                     objectName,
                     "Wheel_FR",
                     "WheelFR",
                     "Wheel_F_R",
                     "FrontRightWheel",
                     "Front_Right_Wheel"
                 ))
        {
            Wheel_FR = target.gameObject;
        }

        /*
         * 뒤 왼쪽 바퀴
         */
        else if (Wheel_RL == null &&
                 ContainsAny(
                     objectName,
                     "Wheel_RL",
                     "WheelRL",
                     "Wheel_R_L",
                     "RearLeftWheel",
                     "Rear_Left_Wheel"
                 ))
        {
            Wheel_RL = target.gameObject;
        }

        /*
         * 뒤 오른쪽 바퀴
         */
        else if (Wheel_RR == null &&
                 ContainsAny(
                     objectName,
                     "Wheel_RR",
                     "WheelRR",
                     "Wheel_R_R",
                     "RearRightWheel",
                     "Rear_Right_Wheel"
                 ))
        {
            Wheel_RR = target.gameObject;
        }

        /*
         * 핸들
         */
        if (SteeringWheel == null &&
            ContainsAny(
                objectName,
                "SteeringWheel",
                "Steering_Wheel",
                "SteerWheel"
            ))
        {
            SteeringWheel = target.gameObject;
        }

        /*
         * 1인칭 카메라 위치
         */
        if (FPSCamPoint == null &&
            MatchesAnyObjectName(
                objectName,
                "Cam_FirstPerson",
                "CamFirstPerson",
                "FirstPersonCamera",
                "FPSCamPoint"
            ))
        {
            FPSCamPoint = target.gameObject;
        }

        /*
         * 미러 카메라 위치
         */
        if (MirrorLeft == null &&
            MatchesAnyObjectName(
                objectName,
                "Mirror_Left",
                "MirrorLeft"
            ))
        {
            MirrorLeft = target.gameObject;
        }
        else if (MirrorRight == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Mirror_Right",
                     "MirrorRight"
                 ))
        {
            MirrorRight = target.gameObject;
        }
        else if (MirrorBack == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Mirror_Back",
                     "MirrorBack",
                     "RearViewMirrorPoint"
                 ))
        {
            MirrorBack = target.gameObject;
        }

        /*
         * 주유구 위치
         */
        if (FuelPoint == null &&
            MatchesAnyObjectName(
                objectName,
                "FuelPoint",
                "Fuel_Point",
                "FuelPort"
            ))
        {
            FuelPoint = target.gameObject;
        }

        /*
         * 문과 트렁크
         */
        if (Door_Left == null &&
            MatchesAnyObjectName(
                objectName,
                "Door_Left",
                "DoorLeft"
            ))
        {
            Door_Left = target.gameObject;
        }
        else if (Door_Right == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Door_Right",
                     "DoorRight"
                 ))
        {
            Door_Right = target.gameObject;
        }
        else if (Door_Fold == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Door_Fold",
                     "DoorFold",
                     "FoldingDoor"
                 ))
        {
            Door_Fold = target.gameObject;
        }
        else if (Door_Slide == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Door_Slide",
                     "DoorSlide",
                     "SlidingDoor"
                 ))
        {
            Door_Slide = target.gameObject;
        }
        else if (Trunk == null &&
                 MatchesAnyObjectName(
                     objectName,
                     "Trunk",
                     "Door_Trunk",
                     "DoorTrunk",
                     "Boot"
                 ))
        {
            Trunk = target.gameObject;
        }

        /*
         * 여러 개 등록 가능한 배기구와 번호판 위치
         *
         * 기본 이름, 숫자 suffix, 위치/용도 suffix를 모두 지원합니다.
         * 예:
         * Exhaust, Exhaust_1, Exhaust_Left, Exhaust_Right, Exhaust_Back
         * NumberPlate, NumberPlate_1, NumberPlate_Front, NumberPlate_Back
         *
         * NormalizeObjectName()에서 공백, '_', '-'를 제거하므로
         * NumberPlate_Back / NumberPlate-Back / NumberPlate Back도 모두 처리됩니다.
         */
        if (MatchesBaseOrVariantName(
                objectName,
                "Exhaust"
            ))
        {
            AddUniqueObject(Exhaust, target.gameObject);
        }

        if (MatchesBaseOrVariantName(
                objectName,
                "NumberPlate",
                "Number_Plate"
            ))
        {
            AddUniqueObject(Numberplate, target.gameObject);
        }

        /*
         * 주행등
         */
        if (Light_Run == null &&
            ContainsAny(
                objectName,
                "Light_Run",
                "LightRun",
                "RunningLight",
                "DayLight",
                "DRL"
            ))
        {
            Light_Run = target.gameObject;
        }

        /*
         * 브레이크등
         */
        else if (Light_Brake == null &&
                 ContainsAny(
                     objectName,
                     "Light_Brake",
                     "LightBrake",
                     "BrakeLight",
                     "StopLight"
                 ))
        {
            Light_Brake = target.gameObject;
        }

        /*
         * 전조등
         */
        else if (Light_Head == null &&
                 ContainsAny(
                     objectName,
                     "Light_Head",
                     "LightHead",
                     "HeadLight",
                     "Headlamp"
                 ))
        {
            Light_Head = target.gameObject;
        }

        /*
         * 후진등
         */
        else if (Light_Rev == null &&
                 ContainsAny(
                     objectName,
                     "Light_Rev",
                     "LightRev",
                     "ReverseLight",
                     "BackupLight"
                 ))
        {
            Light_Rev = target.gameObject;
        }

        /*
         * 왼쪽 방향지시등
         */
        else if (Light_Left == null &&
                 ContainsAny(
                     objectName,
                     "Light_Left",
                     "LightLeft",
                     "LeftIndicator",
                     "LeftTurnSignal",
                     "Indicator_L"
                 ))
        {
            Light_Left = target.gameObject;
        }

        /*
         * 오른쪽 방향지시등
         */
        else if (Light_Right == null &&
                 ContainsAny(
                     objectName,
                     "Light_Right",
                     "LightRight",
                     "RightIndicator",
                     "RightTurnSignal",
                     "Indicator_R"
                 ))
        {
            Light_Right = target.gameObject;
        }

        /*
         * 모든 자식 재귀 검색
         */
        for (int i = 0; i < target.childCount; i++)
        {
            ExtractElementRecursive(target.GetChild(i));
        }
    }

    private void ExtractMaterials(Renderer rendererComponent)
{
    if (rendererComponent == null || rt == null)
        return;

    Material[] rendererMaterials =
        rendererComponent.sharedMaterials;

    if (rendererMaterials == null ||
        rendererMaterials.Length == 0)
    {
        return;
    }

    /*
     * 차체 색상 변경 대상 재질
     *
     * 여러 이름 입력 예:
     * BodyPaint, HoodPaint, DoorPaint
     *
     * 이전 단일 Material 설정 키도 지원합니다.
     */
    AddConfiguredMaterials(
        PaintMat,
        rendererMaterials,
        "car.custom.colorMaterialList",
        "car.custom.bodyPaintMaterial",
        "car.custom.bodyPaintMaterail"
    );

    /*
     * 사용자 텍스처 변경 대상 재질
     *
     * 여러 이름 입력 예:
     * CustomBody, Decal, Display
     *
     * 이전 단일 Material 설정 키도 지원합니다.
     */
    AddConfiguredMaterials(
        CustomMat,
        rendererMaterials,
        "car.custom.customMaterialList",
        "car.custom.customTextureMaterial"
    );
}

private void AddConfiguredMaterials(
    List<Material> targetList,
    Material[] rendererMaterials,
    params string[] configKeys
)
{
    if (targetList == null ||
        rendererMaterials == null ||
        configKeys == null ||
        rt == null)
    {
        return;
    }

    HashSet<string> configuredMaterialNames =
        new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );

    /*
     * 여러 설정 키에서 Material 이름을 모두 읽습니다.
     */
    for (int keyIndex = 0;
         keyIndex < configKeys.Length;
         keyIndex++)
    {
        string configKey = configKeys[keyIndex];

        if (string.IsNullOrWhiteSpace(configKey))
            continue;

        string configuredValue;

        if (!rt.TryGetString(
                configKey,
                out configuredValue
            ))
        {
            continue;
        }

        if (string.IsNullOrWhiteSpace(configuredValue))
            continue;

        AddMaterialNamesFromConfig(
            configuredMaterialNames,
            configuredValue
        );
    }

    if (configuredMaterialNames.Count == 0)
        return;

    /*
     * 현재 Renderer가 사용하는 모든 Material을 확인합니다.
     */
    for (int materialIndex = 0;
         materialIndex < rendererMaterials.Length;
         materialIndex++)
    {
        Material material =
            rendererMaterials[materialIndex];

        if (material == null)
            continue;

        string normalizedMaterialName =
            NormalizeMaterialName(material.name);

        if (!configuredMaterialNames.Contains(
                normalizedMaterialName
            ))
        {
            continue;
        }

        /*
         * 같은 Material이 여러 Renderer에서 발견되어도
         * 리스트에는 한 번만 등록합니다.
         */
        if (!targetList.Contains(material))
        {
            targetList.Add(material);

            Debug.Log(
                $"Material 등록: {material.name}",
                rendererMaterials[materialIndex]
            );
        }
    }
}

private void AddMaterialNamesFromConfig(
    HashSet<string> targetNames,
    string configuredValue
)
{
    if (targetNames == null ||
        string.IsNullOrWhiteSpace(configuredValue))
    {
        return;
    }

    /*
     * 다음 구분자를 모두 지원합니다.
     *
     * Body, Hood, Door
     * Body; Hood; Door
     * Body | Hood | Door
     * 한 줄에 하나씩 입력
     *
     * JSON 배열과 비슷한 입력도 처리합니다.
     * ["Body", "Hood", "Door"]
     */
    char[] separators =
    {
        ',',
        ';',
        '|',
        '\n',
        '\r'
    };

    string[] materialNames =
        configuredValue.Split(
            separators,
            StringSplitOptions.RemoveEmptyEntries
        );

    for (int i = 0; i < materialNames.Length; i++)
    {
        string normalizedName =
            NormalizeMaterialName(materialNames[i]);

        if (string.IsNullOrWhiteSpace(normalizedName))
            continue;

        targetNames.Add(normalizedName);
    }
}

private string NormalizeMaterialName(string materialName)
{
    if (string.IsNullOrWhiteSpace(materialName))
        return string.Empty;

    string normalizedName = materialName.Trim();

    /*
     * 배열 또는 따옴표 형식 제거
     */
    normalizedName = normalizedName.Trim(
        '[',
        ']',
        '"',
        '\'',
        ' '
    );

    /*
     * 런타임 Material 인스턴스 이름 처리
     */
    normalizedName = normalizedName.Replace(
        " (Instance)",
        string.Empty
    );

    normalizedName = normalizedName.Replace(
        "(Instance)",
        string.Empty
    );

    return normalizedName.Trim();
}
    private void SetupModelCollider(Transform target)
    {
        ExtractGeneratedMarker marker =
            target.GetComponent<ExtractGeneratedMarker>();

        if (marker == null)
        {
            marker =
                target.gameObject.AddComponent
                    <ExtractGeneratedMarker>();
        }

        marker.owner = this;

        Renderer rendererComponent =
            target.GetComponent<Renderer>();

        if (rendererComponent != null &&
            rendererComponent.enabled)
        {
            rendererComponent.enabled = false;
            marker.disabledRenderer = true;
        }

        MeshCollider meshCollider =
            target.GetComponent<MeshCollider>();

        if (meshCollider == null)
        {
            Mesh colliderMesh = GetColliderMesh(target);

            if (colliderMesh != null)
            {
                meshCollider =
                    target.gameObject.AddComponent<MeshCollider>();

                meshCollider.sharedMesh = colliderMesh;
                meshCollider.gameObject.layer = LayerMask.NameToLayer("Car");
                marker.generatedMeshCollider = true;
            }
            else
            {
                Debug.LogWarning(
                    $"{target.name}에서 MeshFilter 또는 " +
                    "SkinnedMeshRenderer Mesh를 찾지 못했습니다.",
                    target
                );
            }
        }

        if (meshCollider != null)
        {
            meshCollider.convex = true;

            if (!Colliders.Contains(target.gameObject))
            {
                Colliders.Add(target.gameObject);
            }
        }
    }

    private Mesh GetColliderMesh(Transform target)
    {
        MeshFilter meshFilter =
            target.GetComponent<MeshFilter>();

        if (meshFilter != null &&
            meshFilter.sharedMesh != null)
        {
            return meshFilter.sharedMesh;
        }

        SkinnedMeshRenderer skinnedMeshRenderer =
            target.GetComponent<SkinnedMeshRenderer>();

        if (skinnedMeshRenderer != null &&
            skinnedMeshRenderer.sharedMesh != null)
        {
            return skinnedMeshRenderer.sharedMesh;
        }

        return null;
    }

    private void GenerateCenteredWheelPivots()
    {
        Wheel_FL = CreateCenteredWheelPivot(
            Wheel_FL,
            "FL"
        );

        Wheel_FR = CreateCenteredWheelPivot(
            Wheel_FR,
            "FR"
        );

        Wheel_RL = CreateCenteredWheelPivot(
            Wheel_RL,
            "RL"
        );

        Wheel_RR = CreateCenteredWheelPivot(
            Wheel_RR,
            "RR"
        );
    }

    private GameObject CreateCenteredWheelPivot(
        GameObject wheelMesh,
        string wheelName
    )
    {
        if (wheelMesh == null)
        {
            Debug.LogWarning(
                $"{wheelName} 바퀴가 없어 중심 Pivot을 생성할 수 없습니다.",
                this
            );

            return null;
        }

        if (rb == null)
        {
            Debug.LogWarning(
                $"{wheelName} 바퀴 Pivot 생성용 Rigidbody가 없습니다.",
                this
            );

            return wheelMesh;
        }

        /*
         * 이미 이 스크립트에서 생성한 중심 Pivot이라면
         * 중복 생성하지 않습니다.
         */
        ExtractGeneratedMarker existingMarker =
            wheelMesh.GetComponent<ExtractGeneratedMarker>();

        if (existingMarker != null &&
            existingMarker.owner == this &&
            existingMarker.generatedWheelPivot)
        {
            return wheelMesh;
        }

        /*
         * Rigidbody 로컬 좌표 기준으로 바퀴 Renderer 전체의
         * 실제 Bounds를 계산합니다.
         */
        Bounds wheelBounds;

        if (!TryGetRendererBoundsInLocalSpace(
                wheelMesh,
                rb.transform,
                out wheelBounds
            ))
        {
            Debug.LogWarning(
                $"{wheelMesh.name}에서 Renderer Bounds를 계산할 수 없어 " +
                "중심 Pivot을 생성하지 않았습니다.",
                wheelMesh
            );

            return wheelMesh;
        }

        Vector3 centerWorldPosition =
            rb.transform.TransformPoint(
                wheelBounds.center
            );

        Transform wheelTransform =
            wheelMesh.transform;

        Transform originalParent =
            wheelTransform.parent;

        int originalSiblingIndex =
            wheelTransform.GetSiblingIndex();

        /*
         * 새 Pivot이 기존 바퀴와 같은 축 방향을 가지게 합니다.
         * 이후 원본 바퀴를 worldPositionStays=true로 자식화하므로
         * 화면상의 위치/회전/크기는 바뀌지 않습니다.
         */
        Quaternion originalLocalRotation =
            wheelTransform.localRotation;

        Vector3 originalLocalScale =
            wheelTransform.localScale;

        GameObject pivotObject =
            new GameObject(
                GeneratedWheelPivotPrefix + wheelName
            );

        pivotObject.layer =
            wheelMesh.layer;

        Transform pivotTransform =
            pivotObject.transform;

        if (originalParent != null)
        {
            pivotTransform.SetParent(
                originalParent,
                false
            );

            pivotTransform.localPosition =
                originalParent.InverseTransformPoint(
                    centerWorldPosition
                );

            pivotTransform.localRotation =
                originalLocalRotation;

            pivotTransform.localScale =
                originalLocalScale;

            pivotTransform.SetSiblingIndex(
                originalSiblingIndex
            );
        }
        else
        {
            pivotTransform.position =
                centerWorldPosition;

            pivotTransform.rotation =
                wheelTransform.rotation;

            pivotTransform.localScale =
                wheelTransform.localScale;
        }

        ExtractGeneratedMarker marker =
            pivotObject.AddComponent<ExtractGeneratedMarker>();

        marker.owner = this;
        marker.generatedGameObject = true;
        marker.generatedWheelPivot = true;

        /*
         * 원본 바퀴의 World Transform을 유지한 채
         * 중심 Pivot 아래로 이동시킵니다.
         */
        wheelTransform.SetParent(
            pivotTransform,
            true
        );

        Debug.Log(
            $"{wheelName} Wheel Pivot 생성\n" +
            $"Original Wheel: {wheelMesh.name}\n" +
            $"Pivot: {pivotObject.name}\n" +
            $"Center World: {centerWorldPosition}\n" +
            $"Wheel Local Offset: {wheelTransform.localPosition}",
            pivotObject
        );

        return pivotObject;
    }

    private void GenerateWheelColliders()
{
    WheelCollider_FL = CreateWheelCollider(
        Wheel_FL,
        "FL"
    );

    WheelCollider_FR = CreateWheelCollider(
        Wheel_FR,
        "FR"
    );

    WheelCollider_RL = CreateWheelCollider(
        Wheel_RL,
        "RL"
    );

    WheelCollider_RR = CreateWheelCollider(
        Wheel_RR,
        "RR"
    );

    // Make left/right radius identical on each axle.
    SynchronizeWheelColliderRadii();
}

private void SynchronizeWheelColliderRadii()
{
    // FRONT AXLE
    if (WheelCollider_FL != null &&
        WheelCollider_FR != null)
    {
        // Use the larger radius so neither wheel collider
        // becomes smaller than its wheel mesh.
        float frontRadius = Mathf.Max(
            WheelCollider_FL.radius,
            WheelCollider_FR.radius
        );

        WheelCollider_FL.radius = frontRadius;
        WheelCollider_FR.radius = frontRadius;

        Debug.Log(
            $"Front WheelCollider radius synchronized: {frontRadius:F3}",
            this
        );
    }

    // REAR AXLE
    if (WheelCollider_RL != null &&
        WheelCollider_RR != null)
    {
        float rearRadius = Mathf.Max(
            WheelCollider_RL.radius,
            WheelCollider_RR.radius
        );

        WheelCollider_RL.radius = rearRadius;
        WheelCollider_RR.radius = rearRadius;

        Debug.Log(
            $"Rear WheelCollider radius synchronized: {rearRadius:F3}",
            this
        );
    }
}

    private WheelCollider CreateWheelCollider(
        GameObject wheelMesh,
        string wheelName
    )
    {
        if (wheelMesh == null)
        {
            Debug.LogWarning(
                $"{wheelName} 바퀴 메시를 찾지 못했습니다.",
                this
            );

            return null;
        }

        if (rb == null)
        {
            Debug.LogError(
                $"{wheelName} WheelCollider를 생성할 Rigidbody가 없습니다.",
                this
            );

            return null;
        }

        Transform colliderParent = rb.transform;

        Bounds wheelBounds;

        if (!TryGetRendererBoundsInLocalSpace(
                wheelMesh,
                colliderParent,
                out wheelBounds
            ))
        {
            Debug.LogWarning(
                $"{wheelMesh.name}에서 바퀴 Renderer를 찾지 못했습니다.",
                wheelMesh
            );

            return null;
        }

        GameObject wheelColliderObject =
            new GameObject(
                GeneratedWheelColliderPrefix + wheelName
            );

        wheelColliderObject.layer = wheelMesh.layer;

        wheelColliderObject.transform.SetParent(
            colliderParent,
            false
        );

        /*
         * 자동차 Rigidbody 기준 바퀴 메시의 실제 중심 위치
         */
        wheelColliderObject.transform.localPosition =
            wheelBounds.center;

        /*
         * WheelCollider는 차량 축과 같은 회전을 사용합니다.
         */
        wheelColliderObject.transform.localRotation =
            Quaternion.identity;

        wheelColliderObject.transform.localScale =
            Vector3.one;

        ExtractGeneratedMarker marker =
            wheelColliderObject.AddComponent
                <ExtractGeneratedMarker>();

        marker.owner = this;
        marker.generatedGameObject = true;

        WheelCollider wheelCollider =
            wheelColliderObject.AddComponent<WheelCollider>();

        /*
         * GameObject 자체를 바퀴 중심에 배치했으므로 center는 0
         */
        wheelCollider.center = Vector3.zero;

        /*
         * 일반 자동차 바퀴:
         * X축 = 바퀴 폭 방향
         * Y/Z축 = 바퀴 지름
         */
        float radiusFromY = wheelBounds.extents.y;
        float radiusFromZ = wheelBounds.extents.z;

        wheelCollider.radius =
            Mathf.Max(
                radiusFromY,
                radiusFromZ
            ) *
            WheelRadiusMultiplier+0.1f;

        wheelCollider.mass = WheelMass;
        rt.TryGetFloat("car.wheel.suspension.distance",out SuspensionDistance);
        if(SuspensionDistance == 0)
        {
            SuspensionDistance = 0.1f;
        }
        wheelCollider.suspensionDistance =
            SuspensionDistance;

        Debug.Log(
            $"{wheelName} WheelCollider 생성\n" +
            $"Mesh: {wheelMesh.name}\n" +
            $"Radius: {wheelCollider.radius:F3}\n" +
            $"Local Position: " +
            $"{wheelColliderObject.transform.localPosition}",
            wheelColliderObject
        );

        return wheelCollider;
    }

    private bool TryGetRendererBoundsInLocalSpace(
        GameObject targetObject,
        Transform referenceTransform,
        out Bounds localBounds
    )
    {
        Renderer[] renderers =
            targetObject.GetComponentsInChildren<Renderer>(true);

        localBounds = new Bounds();

        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererComponent = renderers[i];

            if (rendererComponent == null)
                continue;

            Bounds rendererLocalBounds;
            Transform rendererTransform =
                rendererComponent.transform;

            MeshRenderer meshRenderer =
                rendererComponent as MeshRenderer;

            SkinnedMeshRenderer skinnedMeshRenderer =
                rendererComponent as SkinnedMeshRenderer;

            if (meshRenderer != null)
            {
                MeshFilter meshFilter =
                    meshRenderer.GetComponent<MeshFilter>();

                if (meshFilter == null ||
                    meshFilter.sharedMesh == null)
                {
                    continue;
                }

                rendererLocalBounds =
                    meshFilter.sharedMesh.bounds;
            }
            else if (skinnedMeshRenderer != null)
            {
                if (skinnedMeshRenderer.sharedMesh == null)
                    continue;

                rendererLocalBounds =
                    skinnedMeshRenderer.localBounds;
            }
            else
            {
                /*
                 * MeshRenderer가 아닌 Renderer는
                 * 월드 Bounds 방식으로 처리합니다.
                 */
                EncapsulateWorldBounds(
                    rendererComponent.bounds,
                    referenceTransform,
                    ref localBounds,
                    ref hasBounds
                );

                continue;
            }

            EncapsulateLocalBounds(
                rendererLocalBounds,
                rendererTransform,
                referenceTransform,
                ref localBounds,
                ref hasBounds
            );
        }

        return hasBounds;
    }

    private void EncapsulateLocalBounds(
        Bounds sourceBounds,
        Transform sourceTransform,
        Transform referenceTransform,
        ref Bounds resultBounds,
        ref bool hasBounds
    )
    {
        Vector3 center = sourceBounds.center;
        Vector3 extents = sourceBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 sourceLocalPoint =
                        center +
                        Vector3.Scale(
                            extents,
                            new Vector3(x, y, z)
                        );

                    Vector3 worldPoint =
                        sourceTransform.TransformPoint(
                            sourceLocalPoint
                        );

                    Vector3 referenceLocalPoint =
                        referenceTransform.InverseTransformPoint(
                            worldPoint
                        );

                    EncapsulatePoint(
                        referenceLocalPoint,
                        ref resultBounds,
                        ref hasBounds
                    );
                }
            }
        }
    }

    private void EncapsulateWorldBounds(
        Bounds worldBounds,
        Transform referenceTransform,
        ref Bounds resultBounds,
        ref bool hasBounds
    )
    {
        Vector3 center = worldBounds.center;
        Vector3 extents = worldBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldPoint =
                        center +
                        Vector3.Scale(
                            extents,
                            new Vector3(x, y, z)
                        );

                    Vector3 referenceLocalPoint =
                        referenceTransform.InverseTransformPoint(
                            worldPoint
                        );

                    EncapsulatePoint(
                        referenceLocalPoint,
                        ref resultBounds,
                        ref hasBounds
                    );
                }
            }
        }
    }

    private void EncapsulatePoint(
        Vector3 point,
        ref Bounds bounds,
        ref bool hasBounds
    )
    {
        if (!hasBounds)
        {
            bounds = new Bounds(
                point,
                Vector3.zero
            );

            hasBounds = true;
        }
        else
        {
            bounds.Encapsulate(point);
        }
    }

    private bool MatchesAnyObjectName(
        string source,
        params string[] names
    )
    {
        if (string.IsNullOrWhiteSpace(source) || names == null)
            return false;

        string normalizedSource = NormalizeObjectName(source);

        for (int i = 0; i < names.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(names[i]))
                continue;

            if (string.Equals(
                    normalizedSource,
                    NormalizeObjectName(names[i]),
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 여러 개 존재할 수 있는 오브젝트를 기본 이름 기준으로 검색합니다.
    /// 기본 이름 뒤의 숫자뿐 아니라 Front, Back, Left, Right 등의
    /// 임의 suffix도 허용합니다.
    ///
    /// 예:
    /// Exhaust
    /// Exhaust_1
    /// Exhaust_Left
    /// Exhaust_Right_2
    /// NumberPlate_Back
    /// Number_Plate_Front
    /// </summary>
    private bool MatchesBaseOrVariantName(
        string source,
        params string[] baseNames
    )
    {
        if (string.IsNullOrWhiteSpace(source) ||
            baseNames == null ||
            baseNames.Length == 0)
        {
            return false;
        }

        string normalizedSource = NormalizeObjectName(source);

        for (int i = 0; i < baseNames.Length; i++)
        {
            string baseName = baseNames[i];

            if (string.IsNullOrWhiteSpace(baseName))
                continue;

            string normalizedBase = NormalizeObjectName(baseName);

            if (string.IsNullOrWhiteSpace(normalizedBase))
                continue;

            // 정확히 기본 이름과 일치
            if (string.Equals(
                    normalizedSource,
                    normalizedBase,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return true;
            }

            // 기본 이름으로 시작하면 suffix 종류와 관계없이 허용합니다.
            // 예: NumberPlate_Back, Exhaust_Left_1
            if (normalizedSource.StartsWith(
                    normalizedBase,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return true;
            }
        }

        return false;
    }

    private string NormalizeObjectName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Trim()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty);
    }

    private void AddUniqueObject(
        List<GameObject> targetList,
        GameObject targetObject
    )
    {
        if (targetList == null || targetObject == null)
            return;

        if (!targetList.Contains(targetObject))
            targetList.Add(targetObject);
    }

    private void SortIndexedObjectLists()
    {
        SortIndexedObjectList(Exhaust, "Exhaust");
        SortIndexedObjectList(Numberplate, "NumberPlate");
    }

    private void SortIndexedObjectList(
        List<GameObject> targetList,
        string baseName
    )
    {
        if (targetList == null || targetList.Count <= 1)
            return;

        targetList.Sort((first, second) =>
        {
            if (first == null && second == null)
                return 0;

            if (first == null)
                return 1;

            if (second == null)
                return -1;

            int firstIndex = GetObjectNameIndex(
                first.name,
                baseName
            );

            int secondIndex = GetObjectNameIndex(
                second.name,
                baseName
            );

            int indexComparison = firstIndex.CompareTo(secondIndex);

            if (indexComparison != 0)
                return indexComparison;

            return string.Compare(
                first.name,
                second.name,
                StringComparison.OrdinalIgnoreCase
            );
        });
    }

    private int GetObjectNameIndex(
        string objectName,
        string baseName
    )
    {
        string normalizedObjectName = NormalizeObjectName(objectName);
        string normalizedBaseName = NormalizeObjectName(baseName);

        if (!normalizedObjectName.StartsWith(
                normalizedBaseName,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return int.MaxValue;
        }

        string suffix = normalizedObjectName.Substring(
            normalizedBaseName.Length
        );

        // 숫자가 없는 기본 이름은 가장 앞에 둡니다.
        if (suffix.Length == 0)
            return 0;

        int parsedIndex;

        if (int.TryParse(suffix, out parsedIndex))
            return parsedIndex;

        return int.MaxValue;
    }

    private bool ContainsAny(
        string source,
        params string[] keywords
    )
    {
        if (string.IsNullOrEmpty(source))
            return false;

        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = keywords[i];

            if (string.IsNullOrEmpty(keyword))
                continue;

            if (source.IndexOf(
                    keyword,
                    StringComparison.OrdinalIgnoreCase
                ) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void DestroyUnityObject(
        UnityEngine.Object target
    )
    {
        if (target == null)
            return;

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private string GetObjectName(GameObject target)
    {
        return target != null
            ? target.name
            : "없음";
    }

    private string GetWheelColliderName(
        WheelCollider target
    )
    {
        return target != null
            ? target.gameObject.name
            : "없음";
    }
}

/*
 * ExtractElements가 자동으로 변경하거나 생성한 요소 표시용입니다.
 */
[DisallowMultipleComponent]
public class ExtractGeneratedMarker : MonoBehaviour
{
    [HideInInspector]
    public ExtractElements owner;

    [HideInInspector]
    public bool generatedGameObject;

    [HideInInspector]
    public bool generatedMeshCollider;

    [HideInInspector]
    public bool disabledRenderer;

    [HideInInspector]
    public bool generatedWheelPivot;
}
