#if MOD_INGAME
using System.Collections.Generic;
using MobileModSystem;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Security.Cryptography;
using System.Text;
using Photon.Pun;

public class ModLoader : MonoBehaviour
{
    [Header("미리 여기에 컴포넌트 할당하기!")]
    public Text AuthorText;
    public GameObject ShowOnlyOnMain;
    public GameObject DisableOnLoadMod;
    public Transform PeopleContainer;

    public DoorAnimation DoorLeft;
    public DoorAnimation DoorRight;
    public DoorAnimation DoorLeftGul;
    public DoorAnimation DoorRightGul;
    public DoorAnimation Trunk;

    public BusSlidingDoor DoorSlideRight;
    public BusSlidingDoor BusDoorSlide;
    public CityBusFoldingDoor FoldingDoor;
    public CityBusInswingDoor InFoldingDoor;

    public List<GameObject> Exahusts = new();
    public List<GameObject> Plates = new();

    public RCC_DashboardObjects rCC_DashboardObjects;

    [Header("Digital Dashboard TMP")]
    public TMP_Text SpeedText;
    public TMP_Text RPMText;

    private const float DashboardTextBaseScale = 0.003f;
    private int lastSpeedTextValue = int.MinValue;
    private int lastRPMTextValue = int.MinValue;

    public RCC_LightEmission Head;
    public RCC_LightEmission Left;
    public RCC_LightEmission Right;
    public RCC_LightEmission Reverse;
    public RCC_LightEmission Brake;

    public Transform MirrorLeft;
    public Transform MirrorRight;
    public Transform MirrorBack;

    public Transform HoodCam;
    public Transform FuelPoint;

    private readonly List<GameObject> olds = new();

    public ExtractElements ex;

    private RCC_CarControllerV3 car;

    // 모드 로드 시에만 검색 및 설정 파싱. 매 프레임에는 회전만 적용합니다.
    private DashboardNeedle speedNeedle;
    private DashboardNeedle rpmNeedle;

    private struct DashboardNeedle
    {
        public Transform target;
        public Quaternion zeroRotation;
        public Vector3 axis;
        public float anglePerUnit;

        public void Apply(float value)
        {
            if (!target || float.IsNaN(value) || float.IsInfinity(value))
                return;

            float angle = value * anglePerUnit;
            if (float.IsNaN(angle) || float.IsInfinity(angle))
                return;

            target.localRotation = zeroRotation *
                Quaternion.AngleAxis(angle, axis);
        }

        public void Reset()
        {
            // 같은 모델을 다시 추출할 때 현재 각도가 새 0점이 되지 않도록 복원.
            if (target)
                target.localRotation = zeroRotation;

            this = default;
        }
    }

    public CarCustomizerComp cus;

    [HideInInspector]
    public string RecentModString = "last_loaded.sdgmod";


    private void Awake()
    {
        // 모드가 로드되기 전에는 디지털 계기판을 숨깁니다.
        if (SpeedText)
            SpeedText.gameObject.SetActive(false);
        if (RPMText)
            RPMText.gameObject.SetActive(false);
    }

    private void Start()
    {
        // ============================================================
        // 기본 초기화
        // ============================================================

        if (SceneManager.GetActiveScene().name == "main" &&
            transform.parent != null &&
            transform.parent.gameObject.name != "CarSpawnCarshop")
        {
            if (ShowOnlyOnMain)
                ShowOnlyOnMain.SetActive(true);
        }
        else
        {
            if (ShowOnlyOnMain)
                ShowOnlyOnMain.SetActive(false);
        }

        car = GetComponent<RCC_CarControllerV3>();

        if (cus == null)
            cus = GetComponent<CarCustomizerComp>();


        // ============================================================
        // 어떤 Mod slot을 사용 중인지 확인
        // ============================================================

        int carind = -1;

        if (cus != null)
            carind = cus.CarInd;


        // ============================================================
        // MULTIPLAYER
        //
        // 내 자동차:
        //      자기 로컬 mod를 직접 로드
        //
        // 상대 자동차:
        //      여기서는 로드하지 않음.
        //      Photon RPC를 받은 후 hash -> carID 변환하여 로드
        // ============================================================

        if (StateShare.online)
        {
            PhotonView pv = GetComponentInParent<PhotonView>();

            if (pv == null)
            {
                Debug.LogWarning(
                    "[ModLoader] Online vehicle has no PhotonView."
                );

                return;
            }

            // 자기 차량만 기존 저장된 mod를 직접 불러온다.
            if (pv.IsMine)
            {
                LoadModFromCarIndex(carind);
            }

            // Remote 차량은 RPC를 기다린다.
            return;
        }


        // ============================================================
        // SINGLE PLAYER
        // ============================================================

        LoadModFromCarIndex(carind);
    }


    // ================================================================
    // Mod slot을 이용하여 실제 sdgmod 불러오기
    //
    // Photon 상대방 차량도 이 함수를 사용한다.
    // ================================================================

    public void LoadModFromCarIndex(int carind)
    {
        if (MobileModController.Instance == null)
        {
            Debug.LogWarning(
                "[ModLoader] MobileModController.Instance is null."
            );

            return;
        }

        if (ex == null)
        {
            Debug.LogWarning(
                "[ModLoader] ExtractElements is null."
            );

            return;
        }

        RecentModString = GetModFileName(carind);

        Debug.Log(
            "[ModLoader] Loading Mod. CarIndex = " +
            carind +
            ", File = " +
            RecentModString
        );

        MobileModController.Instance.recentModFileName =
            RecentModString;

        MobileModController.Instance.LoadRecentModPackage(
            importedRoot =>
            {
                if (ex != null)
                {
                    ex.ExtractFromModel();
                }
            }
        );
    }


    // ================================================================
    // 기존 filename 결정 로직
    // ================================================================

    private string GetModFileName(int carind)
    {
        if (carind == -1)
        {
            return "no.sdgmod";
        }

        string result =
            "last_loaded" + carind + ".sdgmod";


        // 기존 CarID == 87 특수 슬롯 처리 유지
        if (PlayerDataManager.instance != null)
        {
            for (
                int i = 0;
                i <
                PlayerDataManager.instance
                    .DGroup.playerData.carData.Count;
                i++
            )
            {
                if (
                    PlayerDataManager.instance
                        .DGroup.playerData.carData[i]
                        .CarID == 87
                )
                {
                    if (i == carind)
                    {
                        result = "last_loaded.sdgmod";
                    }

                    break;
                }
            }
        }

        return result;
    }


    private void Update()
    {
        if (car == null || car.rigid == null)
            return;

        // First let Unity calculate the natural center of mass
        car.rigid.automaticCenterOfMass = true;

        Vector3 autoCOM =
            car.rigid.centerOfMass;

        // Switch to manual COM
        car.rigid.automaticCenterOfMass = false;

        // Force COM to the exact left/right center,
        // while keeping automatic front/rear position.
        car.rigid.centerOfMass =
            new Vector3(
                0f,
                autoCOM.y - 0.4f,
                autoCOM.z
            );
    }


    private void LateUpdate()
    {
        if (car == null)
            return;

        UpdateDashboardNeedles();
        UpdateDashboardTexts();
    }

    private void InitializeDashboardTexts()
    {
        lastSpeedTextValue = int.MinValue;
        lastRPMTextValue = int.MinValue;

        ConfigureDashboardText(SpeedText, ex != null ? ex.SpeedTextPos : null);
        ConfigureDashboardText(RPMText, ex != null ? ex.RPMTextPos : null);

        if (car != null)
            UpdateDashboardTexts();
    }

    private static void ConfigureDashboardText(TMP_Text text, GameObject marker)
    {
        if (!text)
            return;

        if (!marker)
        {
            text.gameObject.SetActive(false);
            return;
        }

        Transform target = text.transform;
        Transform source = marker.transform;

        // Keep the original TMP parent so it survives imported model replacement.
        target.SetPositionAndRotation(source.position, source.rotation);

        // Base scale is always 0.003, never the previously applied TMP scale.
        // Compensate for the TMP parent's scale along the text's local axes.
        Vector3 scale = source.lossyScale * DashboardTextBaseScale;
        Transform parent = target.parent;
        if (parent != null)
        {
            Quaternion localRotation = target.localRotation;
            scale.x = DivideDashboardScale(scale.x,
                parent.TransformVector(localRotation * Vector3.right).magnitude);
            scale.y = DivideDashboardScale(scale.y,
                parent.TransformVector(localRotation * Vector3.up).magnitude);
            scale.z = DivideDashboardScale(scale.z,
                parent.TransformVector(localRotation * Vector3.forward).magnitude);
        }

        target.localScale = scale;
        text.gameObject.SetActive(true);
    }

    private static float DivideDashboardScale(float value, float parentScale)
    {
        return parentScale > 0.000001f ? value / parentScale : 0f;
    }

    private void UpdateDashboardTexts()
    {
        UpdateDashboardText(SpeedText, Mathf.Abs(car.speed), ref lastSpeedTextValue);
        UpdateDashboardText(RPMText, Mathf.Max(0f, car.engineRPM), ref lastRPMTextValue);
    }

    private static void UpdateDashboardText(TMP_Text text, float value, ref int lastValue)
    {
        if (!text || !text.gameObject.activeSelf ||
            float.IsNaN(value) || float.IsInfinity(value))
            return;

        int rounded = Mathf.RoundToInt(value);
        if (rounded == lastValue)
            return;

        lastValue = rounded;
        // TMP numeric formatting avoids creating a new string each frame.
        text.SetText("{0:0}", (float)rounded);
    }

    private void UpdateDashboardNeedles()
    {
        // RCC의 speed는 km/h, engineRPM은 rpm 단위입니다.
        // 후진 시에도 속도계는 양수 속력을 표시합니다.
        speedNeedle.Apply(Mathf.Abs(car.speed));
        rpmNeedle.Apply(Mathf.Max(0f, car.engineRPM));
    }

    private void InitializeDashboardNeedles()
    {
        speedNeedle.Reset();
        rpmNeedle.Reset();

        if (ex == null || ex.rt == null)
            return;

        speedNeedle = CreateDashboardNeedle(
            ex.Needle_Speed,
            "car.dash.speedometerAngle100",
            "car.dash.speedometerAxis",
            170f,
            100f
        );

        // 설정 키의 이름은 RPMAngle100이지만, 기준 RPM은 1000입니다.
        rpmNeedle = CreateDashboardNeedle(
            ex.Needle_RPM,
            "car.dash.RPMAngle100",
            "car.dash.RPMAxis",
            30f,
            1000f
        );

        if (car != null)
            UpdateDashboardNeedles();
    }

    private DashboardNeedle CreateDashboardNeedle(
        GameObject needle,
        string angleKey,
        string axisKey,
        float defaultAngle,
        float referenceValue)
    {
        if (!needle)
            return default;

        float angle;
        if (!ex.rt.TryGetFloat(angleKey, out angle) ||
            float.IsNaN(angle) || float.IsInfinity(angle))
        {
            angle = defaultAngle;
        }

        Vector3 axis;
        if (!ex.rt.TryGetVector3(axisKey, out axis) ||
            float.IsNaN(axis.sqrMagnitude) ||
            float.IsInfinity(axis.sqrMagnitude) ||
            axis.sqrMagnitude < 0.000001f)
        {
            axis = Vector3.up;
        }

        return new DashboardNeedle
        {
            target = needle.transform,
            zeroRotation = needle.transform.localRotation,
            axis = axis.normalized,
            anglePerUnit = angle / referenceValue
        };
    }


    // ================================================================
    // Mod 추출 완료 후 호출
    // ================================================================

    public void Initialize()
    {
        if (car == null)
            car = GetComponent<RCC_CarControllerV3>();

        if (cus == null)
            cus = GetComponent<CarCustomizerComp>();


        InitializeDashboardNeedles();
        InitializeDashboardTexts();

        // ============================================================
        // MOD NAME / AUTHOR
        // ============================================================

        if (AuthorText)
        {
            string fileName;

            if (
                ex.rt.TryGetString(
                    "mod.name",
                    out fileName
                )
            )
            {
                AuthorText.text = fileName;
            }
            else
            {
                AuthorText.text = "";
            }


            if (
                ex.rt.TryGetString(
                    "mod.author",
                    out fileName
                )
            )
            {
                AuthorText.text +=
                    "\n" + fileName;
            }
        }


        // ============================================================
        // DRIVER POSITION
        // ============================================================

        if (ex.Person_Position)
        {
            PeopleContainer.transform.position =
                ex.Person_Position.transform.position;

            PeopleContainer.gameObject.SetActive(true);
        }
        else
        {
            PeopleContainer.gameObject.SetActive(false);
        }


        if (DisableOnLoadMod)
            DisableOnLoadMod.SetActive(false);


        // ============================================================
        // OLD WHEELS
        // ============================================================

        DeleteOldWheelModels();


        // ============================================================
        // TRUNK
        // ============================================================

        if (ex.Trunk)
        {
            Trunk.door =
                ex.Trunk.transform;

            Trunk.gameObject.SetActive(true);

            car.anim.Features1.Add(
                new(
                    "Trunk",
                    false,
                    null,
                    null
                )
            );
        }


        // ============================================================
        // LEFT DOOR
        // ============================================================

        if (ex.Door_Left)
        {
            DoorLeft.door =
                ex.Door_Left.transform;

            DoorLeft.gameObject.SetActive(true);

            car.anim.Features1.Add(
                new(
                    "LeftDoor",
                    false,
                    null,
                    null
                )
            );
        }


        // ============================================================
        // BUS FOLDING DOOR
        // ============================================================

        if (ex.BusDoor_FoldRight && ex.BusDoor_FoldLeft)
        {
            FoldingDoor.leftDoor = ex.BusDoor_FoldLeft.transform;

            FoldingDoor.rightDoor = ex.BusDoor_FoldRight.transform;

            FoldingDoor.gameObject.SetActive(true);

            car.anim.Features1.Add(
                new(
                    "DoorFront",
                    false,
                    null,
                    null,
                    null
                )
            );
        }
        // ============================================================
        // BUS IN FOLDING DOOR
        // ============================================================
        //if (ex.BusDoor_InFoldRight && ex.BusDoor_InFoldLeft)
        if (ex.BusDoor_FoldRight && ex.BusDoor_FoldLeft)
        {
            InFoldingDoor.leftDoor = ex.BusDoor_FoldLeft.transform;

            InFoldingDoor.rightDoor = ex.BusDoor_FoldRight.transform;

            InFoldingDoor.gameObject.SetActive(true);

            car.anim.Features1.Add(
                new(
                    "DoorBack",
                    false,
                    null,
                    null,
                    null
                )
            );
        }


        // ============================================================
        // RIGHT DOOR
        // ============================================================

        if (ex.Door_Right)
        {
            DoorRight.door =
                ex.Door_Right.transform;

            DoorRight.gameObject.SetActive(true);

            car.anim.Features1.Add(
                new(
                    "RightDoor",
                    false,
                    null,
                    null
                )
            );
        }


        // ============================================================
        // GULLWING LEFT
        // ============================================================

        if (ex.Door_Left_Gul)
        {
            DoorLeftGul.door =
                ex.Door_Left_Gul.transform;

            DoorLeftGul.gameObject.SetActive(true);

            car.anim.Features1.Add(
                new(
                    "LeftDoor",
                    false,
                    null,
                    null
                )
            );
        }


        // ============================================================
        // GULLWING RIGHT
        // ============================================================

        if (ex.Door_Right_Gul)
        {
            DoorRightGul.door =
                ex.Door_Right_Gul.transform;

            DoorRightGul.gameObject.SetActive(true);

            car.anim.Features1.Add(
                new(
                    "RightDoor",
                    false,
                    null,
                    null
                )
            );
        }


        // ============================================================
        // SLIDING DOOR
        // ============================================================

        if (ex.Door_Slide)
        {
            DoorSlideRight.door =
                ex.Door_Slide.transform;

            DoorSlideRight.gameObject.SetActive(true);

            car.anim.Features1.Add(
                new(
                    "DoorBack",
                    false,
                    null,
                    null
                )
            );
        }


        if (ex.BusDoor_Slide)
        {
            BusDoorSlide.door =
                ex.BusDoor_Slide.transform;

            BusDoorSlide.gameObject.SetActive(true);

            car.anim.Features1.Add(
                new(
                    "DoorBack",
                    false,
                    null,
                    null
                )
            );
        }


        // ============================================================
        // BOOSTER
        // ============================================================

        bool mooster = false;

        if (
            ex.rt.TryGetBool(
                "car.engine.booster",
                out mooster
            )
        )
        {
            if (mooster)
            {
                UniversialAnim universalAnim =
                    car.anim
                        .GetComponentInParent<UniversialAnim>();

                if (
                    universalAnim != null &&
                    universalAnim.GetFeatureIndex("Boost") == -1
                )
                {
                    car.anim.Features1.Add(
                        new UniversialAnim.Component(
                            "Boost"
                        )
                    );
                }
            }
        }


        // ============================================================
        // EXHAUST
        // ============================================================

        for (int i = 0; i < ex.Exhaust.Count; i++)
        {
            if (i > 3)
                break;

            if (i >= Exahusts.Count)
                break;

            bool booster = false;

            if (
                ex.rt.TryGetBool(
                    "car.engine.booster",
                    out booster
                )
            )
            {
                if (booster)
                {
                    BoosterScript boosterScript =
                        Exahusts[i]
                            .transform
                            .GetComponentInChildren<BoosterScript>();

                    if (boosterScript != null)
                    {
                        boosterScript
                            .gameObject
                            .SetActive(true);
                    }
                }
            }

            Exahusts[i].SetActive(true);

            Exahusts[i]
                .transform.position =
                ex.Exhaust[i]
                    .transform.position;
        }


        // ============================================================
        // NUMBER PLATES
        // ============================================================

        if (Plates.Count > 0 && Plates[0])
            Plates[0].SetActive(false);

        if (Plates.Count > 1 && Plates[1])
            Plates[1].SetActive(false);


        for (
            int i = 0;
            i < ex.Numberplate.Count;
            i++
        )
        {
            if (i > 1)
                break;

            if (i >= Plates.Count)
                break;

            if (!Plates[i])
                continue;

            Plates[i].SetActive(true);

            Plates[i].transform.position =
                ex.Numberplate[i]
                    .transform.position;

            Plates[i].transform.rotation =
                ex.Numberplate[i]
                    .transform.rotation;
        }


        // ============================================================
        // CAMERA / FUEL POINT
        // ============================================================

        if (ex.FPSCamPoint && HoodCam)
        {
            HoodCam.position =
                ex.FPSCamPoint
                    .transform.position;
        }


        if (ex.FuelPoint && FuelPoint)
        {
            FuelPoint.position =
                ex.FuelPoint
                    .transform.position;
        }


        // ============================================================
        // MIRRORS
        // ============================================================

        if (ex.MirrorBack && MirrorBack)
        {
            MirrorBack.position =
                ex.MirrorBack.transform.position;
        }


        if (ex.MirrorRight && MirrorRight)
        {
            MirrorRight.position =
                ex.MirrorRight.transform.position;
        }


        if (ex.MirrorLeft && MirrorLeft)
        {
            MirrorLeft.position =
                ex.MirrorLeft.transform.position;
        }


        // ============================================================
        // LIGHTS
        // ============================================================

        if (rCC_DashboardObjects)
        {
            rCC_DashboardObjects.Onobjects =
                ex.Light_Run;
        }


        if (Head)
        {
            if (ex.Light_Head)
            {
                Head.lightFlare =
                    ex.Light_Head;

                Head.enabled = true;
            }
            else
            {
                Head.enabled = false;
            }
        }


        if (Left)
        {
            if (ex.Light_Left)
            {
                Left.lightFlare =
                    ex.Light_Left;

                Left.enabled = true;
            }
            else
            {
                Left.enabled = false;
            }
        }


        if (Right)
        {
            if (ex.Light_Right)
            {
                Right.lightFlare =
                    ex.Light_Right;

                Right.enabled = true;
            }
            else
            {
                Right.enabled = false;
            }
        }


        if (Reverse)
        {
            if (ex.Light_Rev)
            {
                Reverse.lightFlare =
                    ex.Light_Rev;

                Reverse.enabled = true;
            }
            else
            {
                Reverse.enabled = false;
            }
        }


        if (Brake)
        {
            Brake.lightFlare =
                ex.Light_Brake;
        }


        // ============================================================
        // STEERING WHEEL
        // ============================================================

        if (ex.SteeringWheel)
        {
            car.SteeringWheel =
                ex.SteeringWheel.transform;
        }


        Vector3 SteeringAngle =
            Vector3.zero;


        if (
            ex.rt.TryGetVector3(
                "car.wheel.steeringWheelRotation",
                out SteeringAngle
            )
        )
        {
            if (SteeringAngle.x != 0)
            {
                car.steeringWheelRotateAround =
                    RCC_CarControllerV3
                        .SteeringWheelRotateAround
                        .XAxis;

                car.steeringWheelAngleMultiplier =
                    SteeringAngle.x;
            }


            if (SteeringAngle.y != 0)
            {
                car.steeringWheelRotateAround =
                    RCC_CarControllerV3
                        .SteeringWheelRotateAround
                        .YAxis;

                car.steeringWheelAngleMultiplier =
                    SteeringAngle.y;
            }


            if (SteeringAngle.z != 0)
            {
                car.steeringWheelRotateAround =
                    RCC_CarControllerV3
                        .SteeringWheelRotateAround
                        .ZAxis;

                car.steeringWheelAngleMultiplier =
                    SteeringAngle.z;
            }
        }


        // ============================================================
        // CUSTOM MATERIALS
        // ============================================================

        cus.CustomizableMat.Clear();
        cus.TempMat.Clear();
        cus.CusTempMat.Clear();
        cus.CustomTextureList.Clear();


        for (
            int i = 0;
            i < ex.PaintMat.Count;
            i++
        )
        {
            cus.CustomizableMat.Add(
                ex.PaintMat[i]
            );

            cus.TempMat.Add(
                ex.PaintMat[i]
            );
        }


        for (
            int i = 0;
            i < ex.CustomMat.Count;
            i++
        )
        {
            cus.CusTempMat.Add(
                ex.CustomMat[i]
            );

            cus.CustomTextureList.Add(
                new CustomTexture(
                    "커스텀 도색",
                    ex.CustomMat[i],
                    ex.CustomMat[i]
                        .mainTexture as Texture2D
                )
            );
        }


        // ============================================================
        // WHEEL COLLIDERS
        // ============================================================

        try
        {
            CopyValuesFrom(
                ex.WheelCollider_FL,
                car.FrontLeftWheelCollider
                    .wheelCollider
            );

            if (ex.WheelCollider_FL)
                Destroy(
                    ex.WheelCollider_FL.gameObject
                );


            CopyValuesFrom(
                ex.WheelCollider_FR,
                car.FrontRightWheelCollider
                    .wheelCollider
            );

            if (ex.WheelCollider_FR)
                Destroy(
                    ex.WheelCollider_FR.gameObject
                );


            CopyValuesFrom(
                ex.WheelCollider_RL,
                car.RearLeftWheelCollider
                    .wheelCollider
            );

            if (ex.WheelCollider_RL)
                Destroy(
                    ex.WheelCollider_RL.gameObject
                );


            CopyValuesFrom(
                ex.WheelCollider_RR,
                car.RearRightWheelCollider
                    .wheelCollider
            );

            if (ex.WheelCollider_RR)
                Destroy(
                    ex.WheelCollider_RR.gameObject
                );
        }
        catch
        {
        }


        // ============================================================
        // REGISTER LOADED WHEELS
        // ============================================================

        RegisterLoadedWheel(ex.Wheel_FL);
        RegisterLoadedWheel(ex.Wheel_FR);
        RegisterLoadedWheel(ex.Wheel_RL);
        RegisterLoadedWheel(ex.Wheel_RR);


        // ============================================================
        // FRONT LEFT WHEEL MODEL
        // ============================================================

        try
        {
            if (
                ex.Wheel_FL != null &&
                car.FrontLeftWheelTransform != null &&
                car.FrontLeftWheelTransform.parent != null
            )
            {
                ex.Wheel_FL.transform.parent =
                    car.FrontLeftWheelTransform.parent;

                ex.Wheel_FL.transform.localPosition =
                    Vector3.zero;
            }
        }
        catch
        {
        }


        // ============================================================
        // FRONT RIGHT WHEEL MODEL
        // ============================================================

        try
        {
            if (
                ex.Wheel_FR != null &&
                car.FrontRightWheelTransform != null &&
                car.FrontRightWheelTransform.parent != null
            )
            {
                ex.Wheel_FR.transform.parent =
                    car.FrontRightWheelTransform.parent;

                ex.Wheel_FR.transform.localPosition =
                    Vector3.zero;
            }
        }
        catch
        {
        }


        // ============================================================
        // REAR LEFT WHEEL MODEL
        // ============================================================

        try
        {
            if (
                ex.Wheel_RL != null &&
                car.RearLeftWheelTransform != null &&
                car.RearLeftWheelTransform.parent != null
            )
            {
                ex.Wheel_RL.transform.parent =
                    car.RearLeftWheelTransform.parent;

                ex.Wheel_RL.transform.localPosition =
                    Vector3.zero;
            }
        }
        catch
        {
        }


        // ============================================================
        // REAR RIGHT WHEEL MODEL
        // ============================================================

        try
        {
            if (
                ex.Wheel_RR != null &&
                car.RearRightWheelTransform != null &&
                car.RearRightWheelTransform.parent != null
            )
            {
                ex.Wheel_RR.transform.parent =
                    car.RearRightWheelTransform.parent;

                ex.Wheel_RR.transform.localPosition =
                    Vector3.zero;
            }
        }
        catch
        {
        }


        if (ex.SteeringWheel)
        {
            car.SteeringWheel =
                ex.SteeringWheel.transform;
        }


        cus.enabled = true;


        if (PlayerCarSelect.instance)
        {
            PlayerCarSelect.instance
                .ResetColoring();
        }


        // ============================================================
        // NEW:
        // MOD HASH 저장 및 Photon 전송
        // ============================================================

        RegisterAndBroadcastModHash();
    }


    // ================================================================
    // 내 차량이 로드한 mod를 dictionary에 저장하고
    // multiplayer인 경우 상대방에게 hash 전송
    //
    // Remote 차량에서 Initialize가 실행되어도
    // 절대 자기 CarDataManager에 저장하지 않는다.
    // ================================================================

    private void RegisterAndBroadcastModHash()
    {
        // Multiplayer라면 반드시 내 차량인지 확인.
        if (StateShare.online)
        {
            PhotonView pv =
                GetComponentInParent<PhotonView>();

            if (pv == null)
            {
                Debug.LogWarning(
                    "[ModLoader] Cannot register mod hash: PhotonView missing."
                );

                return;
            }


            // Remote vehicle가 로드 완료한 경우
            // 여기서 바로 종료.
            if (!pv.IsMine)
            {
                return;
            }
        }


        if (cus == null)
            cus = GetComponent<CarCustomizerComp>();


        if (cus == null)
        {
            Debug.LogWarning(
                "[ModLoader] CarCustomizerComp missing."
            );

            return;
        }


        int carID = cus.CarInd;


        // -1 = mod 없음
        if (carID < 0)
        {
            return;
        }


        string hash = GetModHash();


        if (string.IsNullOrEmpty(hash))
        {
            Debug.LogWarning(
                "[ModLoader] Mod hash is empty."
            );

            return;
        }


        // ============================================================
        // SAVE HASH -> CAR SLOT
        // ============================================================

        if (PlayerCarDataManager.instance != null)
        {
            PlayerCarDataManager.instance
                .AddModCarHashtoDictionary(
                    carID,
                    hash
                );

            PlayerCarDataManager.instance
                .SaveModCarDictionary();


            Debug.Log(
                "[ModLoader] Registered mod hash. " +
                "CarID = " +
                carID +
                ", Hash = " +
                hash
            );
        }
        else
        {
            Debug.LogWarning(
                "[ModLoader] CarDataManager.instance is null."
            );
        }


        // ============================================================
        // MULTIPLAYER HASH BROADCAST
        // ============================================================

        if (StateShare.online)
        {
            PhotonModCarSync sync =
                GetComponentInParent<PhotonModCarSync>();


            if (sync != null)
            {
                sync.BroadcastCurrentMod();
            }
            else
            {
                Debug.LogWarning(
                    "[ModLoader] PhotonModCarSync was not found " +
                    "on Photon vehicle hierarchy."
                );
            }
        }
    }


    // ================================================================
    // Removes wheel models attached by the previous mod.
    // ================================================================

    public void DeleteOldWheelModels()
    {
        HashSet<GameObject> objectsToDelete =
            new HashSet<GameObject>();


        // Legacy/runtime references.
        for (int i = 0; i < olds.Count; i++)
        {
            if (olds[i])
            {
                GameObject oldWheel =
                    olds[i];

                if (
                    oldWheel != null &&
                    !IsCurrentExtractedWheel(oldWheel)
                )
                {
                    objectsToDelete.Add(
                        oldWheel
                    );
                }
            }
        }


        olds.Clear();


        // Search four RCC wheel mounting hierarchies.
        CollectOldWheelModels(
            car != null &&
            car.FrontLeftWheelTransform != null

                ? car.FrontLeftWheelTransform.parent
                : null,

            objectsToDelete
        );


        CollectOldWheelModels(
            car != null &&
            car.FrontRightWheelTransform != null

                ? car.FrontRightWheelTransform.parent
                : null,

            objectsToDelete
        );


        CollectOldWheelModels(
            car != null &&
            car.RearLeftWheelTransform != null

                ? car.RearLeftWheelTransform.parent
                : null,

            objectsToDelete
        );


        CollectOldWheelModels(
            car != null &&
            car.RearRightWheelTransform != null

                ? car.RearRightWheelTransform.parent
                : null,

            objectsToDelete
        );


        foreach (
            GameObject oldWheel
            in objectsToDelete
        )
        {
            if (
                oldWheel == null ||
                IsCurrentExtractedWheel(oldWheel)
            )
            {
                continue;
            }


            oldWheel.SetActive(false);

            Destroy(oldWheel);
        }
    }


    private void CollectOldWheelModels(
        Transform wheelMount,
        HashSet<GameObject> objectsToDelete
    )
    {
        if (
            wheelMount == null ||
            objectsToDelete == null
        )
        {
            return;
        }


        ModLoadedWheelModelMarker[] markers =
            wheelMount
                .GetComponentsInChildren
                <ModLoadedWheelModelMarker>(
                    true
                );


        for (
            int i = 0;
            i < markers.Length;
            i++
        )
        {
            ModLoadedWheelModelMarker marker =
                markers[i];


            if (
                marker == null ||
                marker.owner != this
            )
            {
                continue;
            }


            GameObject wheelModel =
                marker.gameObject;


            if (
                wheelModel == null ||
                IsCurrentExtractedWheel(wheelModel)
            )
            {
                continue;
            }


            objectsToDelete.Add(
                wheelModel
            );
        }
    }


    // ================================================================
    // Keeps reference to generated pivot and places marker on
    // actual imported wheel model.
    // ================================================================

    private void RegisterLoadedWheel(
        GameObject wheelObject
    )
    {
        if (wheelObject == null)
            return;


        if (!olds.Contains(wheelObject))
        {
            olds.Add(wheelObject);
        }


        Transform modelRoot =
            GetActualWheelModelRoot(
                wheelObject.transform
            );


        if (modelRoot == null)
            return;


        ModLoadedWheelModelMarker marker =
            modelRoot
                .GetComponent
                <ModLoadedWheelModelMarker>();


        if (marker == null)
        {
            marker =
                modelRoot.gameObject
                    .AddComponent
                    <ModLoadedWheelModelMarker>();
        }


        marker.owner = this;
    }


    private Transform GetActualWheelModelRoot(
        Transform wheelObject
    )
    {
        if (wheelObject == null)
            return null;


        if (
            wheelObject.name.StartsWith(
                "Generated_WheelPivot_"
            ) &&
            wheelObject.childCount > 0
        )
        {
            return wheelObject.GetChild(0);
        }


        return wheelObject;
    }


    private bool IsCurrentExtractedWheel(
        GameObject target
    )
    {
        if (
            target == null ||
            ex == null
        )
        {
            return false;
        }


        if (
            target == ex.Wheel_FL ||
            target == ex.Wheel_FR ||
            target == ex.Wheel_RL ||
            target == ex.Wheel_RR
        )
        {
            return true;
        }


        return
            IsActualModelOfCurrentWheel(
                target,
                ex.Wheel_FL
            ) ||

            IsActualModelOfCurrentWheel(
                target,
                ex.Wheel_FR
            ) ||

            IsActualModelOfCurrentWheel(
                target,
                ex.Wheel_RL
            ) ||

            IsActualModelOfCurrentWheel(
                target,
                ex.Wheel_RR
            );
    }


    private bool IsActualModelOfCurrentWheel(
        GameObject target,
        GameObject currentWheel
    )
    {
        if (
            target == null ||
            currentWheel == null
        )
        {
            return false;
        }


        Transform actualRoot =
            GetActualWheelModelRoot(
                currentWheel.transform
            );


        return
            actualRoot != null &&
            actualRoot.gameObject == target;
    }


    // ================================================================
    // WHEEL COLLIDER COPY
    // ================================================================

    public void CopyValuesFrom(
        WheelCollider source,
        WheelCollider target
    )
    {
        if (
            source == null ||
            target == null
        )
        {
            return;
        }


        target.transform.position =
            source.transform.position;


        target.radius =
            source.radius;


        target.suspensionDistance =
            source.suspensionDistance;
    }


    // ================================================================
    // MOD HASH
    //
    // mod.name
    // +
    // mod.author
    //
    // -> SHA256
    // ================================================================

    public string GetModHash()
    {
        if (
            ex == null ||
            ex.rt == null
        )
        {
            return "";
        }


        string modName = "";
        string modAuthor = "";


        ex.rt.TryGetString(
            "mod.name",
            out modName
        );


        ex.rt.TryGetString(
            "mod.author",
            out modAuthor
        );


        modName =
            modName ?? "";

        modAuthor =
            modAuthor ?? "";


        // Separator prevents:
        //
        // AB + C
        //
        // and
        //
        // A + BC
        //
        // producing identical input.

        string input =
            modName +
            "\n" +
            modAuthor;


        using (
            SHA256 sha256 =
                SHA256.Create()
        )
        {
            byte[] bytes =
                Encoding.UTF8.GetBytes(
                    input
                );


            byte[] hash =
                sha256.ComputeHash(
                    bytes
                );


            StringBuilder builder =
                new StringBuilder(
                    hash.Length * 2
                );


            foreach (byte b in hash)
            {
                builder.Append(
                    b.ToString("x2")
                );
            }


            return builder.ToString();
        }
    }
}


// ====================================================================
// Marker placed on the actual imported wheel model root.
//
// It survives ExtractElements generated-pivot cleanup.
// ====================================================================

[DisallowMultipleComponent]
public class ModLoadedWheelModelMarker
    : MonoBehaviour
{
    [HideInInspector]
    public ModLoader owner;
}
#endif