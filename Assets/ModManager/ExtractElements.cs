using System;
using System.Collections.Generic;
using UnityEngine;

public class ExtractElements : MonoBehaviour
{
    [Header("새 모드들이 생성되는 부모")]
    [Tooltip("모드를 불러왔을 때 Environment 모델이 이 오브젝트의 자식으로 생성되도록 설정하세요.")]
    public Transform ModContainer;

    [Header("현재 검색할 모델")]
    public GameObject RootObject;

    [Tooltip("실행할 때 ModContainer의 가장 마지막 자식을 새 RootObject로 사용")]
    public bool autoFindNewestRoot = true;


    [Header("Colliders")]
    public List<GameObject> Colliders = new();

    [Header("Wheels")]
    public GameObject Wheel_FL;
    public GameObject Wheel_FR;
    public GameObject Wheel_RL;
    public GameObject Wheel_RR;

    [Header("Steering")]
    public GameObject SteeringWheel;

    [Header("Positions")]
    public GameObject FPSCamPoint;
    public GameObject Person_Position;

    [Header("Mirrors")]
    public GameObject MirrorLeft;
    public GameObject MirrorRight;
    public GameObject MirrorBack;

    [Header("Other")]
    public GameObject FuelPoint;

    public List<GameObject> Exhaust = new();
    public List<GameObject> Numberplate = new();

    [Header("Doors")]
    public GameObject Door_Left;
    public GameObject Door_Right;

    public GameObject Door_Fold;
    public GameObject Door_Slide;

    public GameObject BusDoor_Slide;
    public GameObject BusDoor_FoldRight;
    public GameObject BusDoor_FoldLeft;

    public GameObject Trunk;


    [Header("Lights")]
    public GameObject Light_Run;
    public GameObject Light_Brake;
    public GameObject Light_Head;
    public GameObject Light_Rev;
    public GameObject Light_Left;
    public GameObject Light_Right;

    [Header("COM")]
    public Transform COM;


    // =========================================================
    // Context Menu / 일반 실행
    // =========================================================

    [ContextMenu("Extract From Model")]
    public void ExtractFromModel()
    {
        GameObject newRoot = FindNewestRoot();

        ExtractFromModel(newRoot);
    }


    // =========================================================
    // 가장 권장하는 방법
    //
    // 새 모드를 불러온 직후:
    //
    // extractElements.ExtractFromModel(importedObject);
    //
    // 이렇게 직접 새 Root를 전달
    // =========================================================

    void Update()
    {
        ExtractFromModel(RootObject);
    }
    public void ExtractFromModel(GameObject newRoot)
    {
        // 중요:
        // 이전 검색 결과를 무조건 먼저 제거
        ClearAllData();

        if (newRoot == null)
        {
            RootObject = null;

            Debug.LogError(
                "새 Environment RootObject를 찾지 못했습니다.",
                this
            );

            return;
        }

        // 새 모드로 Root 교체
        RootObject = newRoot;


        Debug.Log(
            $"[ExtractElements] 새로운 Root 설정: {RootObject.name}",
            RootObject
        );


        // -------------------------------------------------
        // 1차 검색
        //
        // Wheel_FL 등의 정확한 이름을 먼저 찾음
        // -------------------------------------------------

        SearchRecursive(
            RootObject.transform,
            true
        );


        // -------------------------------------------------
        // 2차 검색
        //
        // 정확한 이름으로 못 찾은 경우
        //
        // Wheel_FL.001
        // Wheel_FL_Mesh
        // Door_Left_01
        // 등의 이름도 검색
        // -------------------------------------------------

        SearchRecursive(
            RootObject.transform,
            false
        );


        PrintResult();
    }


    // =========================================================
    // 새 Root 찾기
    // =========================================================

    private GameObject FindNewestRoot()
    {
        if (autoFindNewestRoot &&
            ModContainer != null)
        {
            // 새로 Instantiate된 오브젝트는 일반적으로
            // hierarchy의 마지막 자식으로 들어감

            for (int i = ModContainer.childCount - 1;
                 i >= 0;
                 i--)
            {
                Transform child =
                    ModContainer.GetChild(i);

                if (child == null)
                    continue;

                // ExtractElements 자신은 제외
                if (child == transform)
                    continue;

                return child.gameObject;
            }
        }


        // 자동 검색을 사용하지 않거나
        // Container에서 못 찾은 경우 현재 Root 사용

        return RootObject;
    }


    // =========================================================
    // 재귀 검색
    // =========================================================

    private void SearchRecursive(
        Transform target,
        bool exactOnly
    )
    {
        if (target == null)
            return;


        string objectName = target.name;


        // =====================================================
        // Collider
        // =====================================================

        if (!exactOnly)
        {
            if (ContainsName(
                    objectName,
                    "Collider",
                    "Collision"
                ))
            {
                AddUnique(
                    Colliders,
                    target.gameObject
                );
            }
        }


        // =====================================================
        // COM
        // =====================================================

        if (COM == null &&
            Matches(
                objectName,
                exactOnly,
                "COM"
            ))
        {
            COM = target;
        }


        // =====================================================
        // Wheels
        // =====================================================

        if (Wheel_FL == null &&
            Matches(
                objectName,
                exactOnly,
                "Wheel_FL",
                "WheelFL",
                "Wheel_F_L",
                "FrontLeftWheel",
                "Front_Left_Wheel"
            ))
        {
            Wheel_FL = target.gameObject;
        }


        if (Wheel_FR == null &&
            Matches(
                objectName,
                exactOnly,
                "Wheel_FR",
                "WheelFR",
                "Wheel_F_R",
                "FrontRightWheel",
                "Front_Right_Wheel"
            ))
        {
            Wheel_FR = target.gameObject;
        }


        if (Wheel_RL == null &&
            Matches(
                objectName,
                exactOnly,
                "Wheel_RL",
                "WheelRL",
                "Wheel_R_L",
                "RearLeftWheel",
                "Rear_Left_Wheel"
            ))
        {
            Wheel_RL = target.gameObject;
        }


        if (Wheel_RR == null &&
            Matches(
                objectName,
                exactOnly,
                "Wheel_RR",
                "WheelRR",
                "Wheel_R_R",
                "RearRightWheel",
                "Rear_Right_Wheel"
            ))
        {
            Wheel_RR = target.gameObject;
        }


        // =====================================================
        // Steering
        // =====================================================

        if (SteeringWheel == null &&
            Matches(
                objectName,
                exactOnly,
                "SteeringWheel",
                "Steering_Wheel",
                "SteerWheel"
            ))
        {
            SteeringWheel = target.gameObject;
        }


        // =====================================================
        // First person camera
        // =====================================================

        if (FPSCamPoint == null &&
            Matches(
                objectName,
                exactOnly,
                "Cam_FirstPerson",
                "CamFirstPerson",
                "FirstPersonCamera",
                "FPSCamPoint"
            ))
        {
            FPSCamPoint = target.gameObject;
        }


        // =====================================================
        // Person
        // =====================================================

        if (Person_Position == null &&
            Matches(
                objectName,
                exactOnly,
                "Person_Position",
                "PersonPosition"
            ))
        {
            Person_Position = target.gameObject;
        }


        // =====================================================
        // Mirrors
        // =====================================================

        if (MirrorLeft == null &&
            Matches(
                objectName,
                exactOnly,
                "Mirror_Left",
                "MirrorLeft"
            ))
        {
            MirrorLeft = target.gameObject;
        }


        if (MirrorRight == null &&
            Matches(
                objectName,
                exactOnly,
                "Mirror_Right",
                "MirrorRight"
            ))
        {
            MirrorRight = target.gameObject;
        }


        if (MirrorBack == null &&
            Matches(
                objectName,
                exactOnly,
                "Mirror_Back",
                "MirrorBack",
                "RearViewMirrorPoint"
            ))
        {
            MirrorBack = target.gameObject;
        }


        // =====================================================
        // Fuel
        // =====================================================

        if (FuelPoint == null &&
            Matches(
                objectName,
                exactOnly,
                "FuelPoint",
                "Fuel_Point",
                "FuelPort"
            ))
        {
            FuelPoint = target.gameObject;
        }


        // =====================================================
        // Doors
        // =====================================================

        if (Door_Left == null &&
            Matches(
                objectName,
                exactOnly,
                "Door_Left",
                "DoorLeft"
            ))
        {
            Door_Left = target.gameObject;
        }


        if (Door_Right == null &&
            Matches(
                objectName,
                exactOnly,
                "Door_Right",
                "DoorRight"
            ))
        {
            Door_Right = target.gameObject;
        }


        if (Door_Fold == null &&
            Matches(
                objectName,
                exactOnly,
                "Door_Fold",
                "DoorFold",
                "FoldingDoor"
            ))
        {
            Door_Fold = target.gameObject;
        }


        if (Door_Slide == null &&
            Matches(
                objectName,
                exactOnly,
                "Door_Slide",
                "DoorSlide",
                "SlidingDoor"
            ))
        {
            Door_Slide = target.gameObject;
        }


        if (BusDoor_Slide == null &&
            Matches(
                objectName,
                exactOnly,
                "BusDoor_Slide",
                "BusDoorSlide"
            ))
        {
            BusDoor_Slide = target.gameObject;
        }


        if (BusDoor_FoldRight == null &&
            Matches(
                objectName,
                exactOnly,
                "BusDoor_FoldRight",
                "BusDoorFoldRight"
            ))
        {
            BusDoor_FoldRight =
                target.gameObject;
        }


        if (BusDoor_FoldLeft == null &&
            Matches(
                objectName,
                exactOnly,
                "BusDoor_FoldLeft",
                "BusDoorFoldLeft"
            ))
        {
            BusDoor_FoldLeft =
                target.gameObject;
        }


        if (Trunk == null &&
            Matches(
                objectName,
                exactOnly,
                "Trunk",
                "Door_Trunk",
                "DoorTrunk",
                "Boot"
            ))
        {
            Trunk = target.gameObject;
        }


        // =====================================================
        // Exhaust
        // NumberPlate
        //
        // 여러 개 검색 가능
        // =====================================================

        if (MatchesListObject(
                objectName,
                exactOnly,
                "Exhaust"
            ))
        {
            AddUnique(
                Exhaust,
                target.gameObject
            );
        }


        if (MatchesListObject(
                objectName,
                exactOnly,
                "NumberPlate",
                "Number_Plate"
            ))
        {
            AddUnique(
                Numberplate,
                target.gameObject
            );
        }


        // =====================================================
        // Lights
        // =====================================================

        if (Light_Run == null &&
            Matches(
                objectName,
                exactOnly,
                "Light_Run",
                "LightRun",
                "RunningLight",
                "DayLight",
                "DRL"
            ))
        {
            Light_Run = target.gameObject;
        }


        if (Light_Brake == null &&
            Matches(
                objectName,
                exactOnly,
                "Light_Brake",
                "LightBrake",
                "BrakeLight",
                "StopLight"
            ))
        {
            Light_Brake = target.gameObject;
        }


        if (Light_Head == null &&
            Matches(
                objectName,
                exactOnly,
                "Light_Head",
                "LightHead",
                "HeadLight",
                "Headlamp"
            ))
        {
            Light_Head = target.gameObject;
        }


        if (Light_Rev == null &&
            Matches(
                objectName,
                exactOnly,
                "Light_Rev",
                "LightRev",
                "ReverseLight",
                "BackupLight"
            ))
        {
            Light_Rev = target.gameObject;
        }


        if (Light_Left == null &&
            Matches(
                objectName,
                exactOnly,
                "Light_Left",
                "LightLeft",
                "LeftIndicator",
                "LeftTurnSignal",
                "Indicator_L"
            ))
        {
            Light_Left = target.gameObject;
        }


        if (Light_Right == null &&
            Matches(
                objectName,
                exactOnly,
                "Light_Right",
                "LightRight",
                "RightIndicator",
                "RightTurnSignal",
                "Indicator_R"
            ))
        {
            Light_Right = target.gameObject;
        }


        // =====================================================
        // Children
        // =====================================================

        for (int i = 0;
             i < target.childCount;
             i++)
        {
            SearchRecursive(
                target.GetChild(i),
                exactOnly
            );
        }
    }


    // =========================================================
    // 이름 비교
    // =========================================================

    private bool Matches(
        string source,
        bool exactOnly,
        params string[] names
    )
    {
        string normalizedSource =
            Normalize(source);


        for (int i = 0;
             i < names.Length;
             i++)
        {
            string target =
                Normalize(names[i]);


            // 정확히 일치
            if (string.Equals(
                    normalizedSource,
                    target,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return true;
            }


            // 2차 검색에서는 suffix 허용
            if (!exactOnly &&
                normalizedSource.StartsWith(
                    target,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return true;
            }
        }


        return false;
    }


    private bool MatchesListObject(
        string source,
        bool exactOnly,
        params string[] baseNames
    )
    {
        string normalizedSource =
            Normalize(source);


        for (int i = 0;
             i < baseNames.Length;
             i++)
        {
            string baseName =
                Normalize(baseNames[i]);


            if (string.Equals(
                    normalizedSource,
                    baseName,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return true;
            }


            if (!exactOnly &&
                normalizedSource.StartsWith(
                    baseName,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return true;
            }
        }


        return false;
    }


    private bool ContainsName(
        string source,
        params string[] names
    )
    {
        string normalizedSource =
            Normalize(source);


        for (int i = 0;
             i < names.Length;
             i++)
        {
            if (normalizedSource.IndexOf(
                    Normalize(names[i]),
                    StringComparison.OrdinalIgnoreCase
                ) >= 0)
            {
                return true;
            }
        }


        return false;
    }


    // =========================================================
    // 이름 Normalize
    // =========================================================

    private string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;


        string result =
            value.Trim();


        // Runtime 생성 이름 제거
        result = result.Replace(
            "(Clone)",
            ""
        );

        result = result.Replace(
            "(Instance)",
            ""
        );


        // GLTF / FBX 등에서 생길 수 있는 구분자 무시
        result = result
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .Replace(".", "");


        return result.Trim();
    }


    // =========================================================
    // List 중복 방지
    // =========================================================

    private void AddUnique(
        List<GameObject> list,
        GameObject obj
    )
    {
        if (list == null ||
            obj == null)
        {
            return;
        }


        if (!list.Contains(obj))
        {
            list.Add(obj);
        }
    }


    // =========================================================
    // 이전 데이터 완전 제거
    // =========================================================

    private void ClearAllData()
    {
        Colliders.Clear();

        Wheel_FL = null;
        Wheel_FR = null;
        Wheel_RL = null;
        Wheel_RR = null;

        SteeringWheel = null;

        FPSCamPoint = null;
        Person_Position = null;

        MirrorLeft = null;
        MirrorRight = null;
        MirrorBack = null;

        FuelPoint = null;

        Exhaust.Clear();
        Numberplate.Clear();

        Door_Left = null;
        Door_Right = null;

        Door_Fold = null;
        Door_Slide = null;

        BusDoor_Slide = null;
        BusDoor_FoldRight = null;
        BusDoor_FoldLeft = null;

        Trunk = null;

        Light_Run = null;
        Light_Brake = null;
        Light_Head = null;
        Light_Rev = null;
        Light_Left = null;
        Light_Right = null;

        COM = null;

        // RootObject도 반드시 초기화
        RootObject = null;
    }


    // =========================================================
    // Debug
    // =========================================================

    private void PrintResult()
    {
        Debug.Log(
            "======= ExtractElements =======\n" +

            $"Root = {GetName(RootObject)}\n" +

            $"Wheel_FL = {GetName(Wheel_FL)}\n" +
            $"Wheel_FR = {GetName(Wheel_FR)}\n" +
            $"Wheel_RL = {GetName(Wheel_RL)}\n" +
            $"Wheel_RR = {GetName(Wheel_RR)}\n" +

            $"SteeringWheel = {GetName(SteeringWheel)}\n" +

            $"FPSCamPoint = {GetName(FPSCamPoint)}\n" +
            $"Person = {GetName(Person_Position)}\n" +

            $"MirrorLeft = {GetName(MirrorLeft)}\n" +
            $"MirrorRight = {GetName(MirrorRight)}\n" +
            $"MirrorBack = {GetName(MirrorBack)}\n" +

            $"FuelPoint = {GetName(FuelPoint)}\n" +

            $"Exhaust = {Exhaust.Count}\n" +
            $"NumberPlate = {Numberplate.Count}\n" +

            $"DoorLeft = {GetName(Door_Left)}\n" +
            $"DoorRight = {GetName(Door_Right)}\n" +

            $"LightHead = {GetName(Light_Head)}\n" +
            $"LightBrake = {GetName(Light_Brake)}\n" +

            "===============================",
            this
        );
    }


    private string GetName(
        GameObject obj
    )
    {
        return obj != null
            ? obj.name
            : "NULL";
    }
}