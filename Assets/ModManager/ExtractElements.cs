using System;
using System.Collections.Generic;
using UnityEngine;
using MobileModSystem;

public class ExtractElements : MonoBehaviour
{
    [Header("자동차 색 제질")]
    public Material Paint;
    public Material Wheel;
    public Material Glass;
    public Material Custom;

    [Header("검색할 모델 루트")]
    public GameObject RootObject;

    [Header("충돌체")]
    public List<GameObject> Colliders = new();

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

    [Header("조명")]
    public GameObject Light_Run;
    public GameObject Light_Brake;
    public GameObject Light_Head;
    public GameObject Light_Rev;
    public GameObject Light_Left;
    public GameObject Light_Right;

    [Header("무게중심")]
    public Transform COM;


    public Rigidbody rb;
    private RuntimeModTextConfig rt;

    private const string GeneratedWheelColliderPrefix =
        "Generated_WheelCollider_";

    [ContextMenu("Extract From Model")]
    public void ExtractFromModel()
    {
        rt = RootObject.GetComponentInChildren<RuntimeModTextConfig>();
        if (RootObject == null)
        { 
            Debug.LogError(
                "RootObject가 설정되지 않았습니다.",
                this
            );

            return;
        }
        rb = RootObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        float mass = 0;
        if(rt.TryGetFloat("car.physics.mass",out mass)){
            rb.mass = mass;
        }else{
            rb.mass = 1200;
        }
        

        // 이전에 자동 생성했던 WheelCollider 삭제
        DeleteGeneratedWheelColliders();

        // 기존 참조 초기화
        ClearElements();

        // 모델 요소 검색
        ExtractElementRecursive(RootObject.transform);

        // 찾아낸 바퀴 메시를 기준으로 WheelCollider 생성
        GenerateWheelColliders();

        Debug.Log(
            $"모델 요소 추출 완료\n" +
            $"Colliders: {Colliders.Count}\n" +
            $"Wheel FL: {GetObjectName(Wheel_FL)}\n" +
            $"Wheel FR: {GetObjectName(Wheel_FR)}\n" +
            $"Wheel RL: {GetObjectName(Wheel_RL)}\n" +
            $"Wheel RR: {GetObjectName(Wheel_RR)}\n" +
            $"WheelCollider FL: {GetWheelColliderName(WheelCollider_FL)}\n" +
            $"WheelCollider FR: {GetWheelColliderName(WheelCollider_FR)}\n" +
            $"WheelCollider RL: {GetWheelColliderName(WheelCollider_RL)}\n" +
            $"WheelCollider RR: {GetWheelColliderName(WheelCollider_RR)}",
            this
        );
    }

    private void ExtractElementRecursive(Transform target)
    {
        MeshRenderer temp = target.GetComponent<MeshRenderer>();
        if(temp != null){
            string matname = "";
            if(Paint == null && rt.TryGetString("car.custom.bodyPaintMaterail",out matname)){
                Material[] mats = temp.sharedMaterials;
                for(int i=0; i<mats.Length;i++){
                    if(mats[i].name == matname){
                        Paint = mats[i];
                        break;
                    }
                }
            }


            matname = "";
            if(Wheel == null && rt.TryGetString("car.custom.wheelPaintMaterial",out matname)){
                Material[] mats = temp.sharedMaterials;
                for(int i=0; i<mats.Length;i++){
                    if(mats[i].name == matname){
                        Wheel = mats[i];
                        break;
                    }
                }
            }


            matname = "";
            if(Glass == null && rt.TryGetString("car.custom.glassPaintMaterial",out matname)){
                Material[] mats = temp.sharedMaterials;
                for(int i=0; i<mats.Length;i++){
                    if(mats[i].name == matname){
                        Glass = mats[i];
                        break;
                    }
                }
            }


            matname = "";
            if(Custom == null && rt.TryGetString("car.custom.customTextureMaterial",out matname)){
                Material[] mats = temp.sharedMaterials;
                for(int i=0; i<mats.Length;i++){
                    if(mats[i].name == matname){
                        Custom = mats[i];
                        break;
                    }
                }
            }
        }

        // 자동 생성된 WheelCollider는 모델 검색 대상에서 제외
        if (target.GetComponent<WheelCollider>() != null)
            return;

        string objectName = target.name;

        /*
         * 일반 충돌체
         */
        if (ContainsAny(
                objectName,
                "Collider",
                "Collision"
            ))
        {
            MeshRenderer meshRenderer =
                target.GetComponent<MeshRenderer>();

            if (meshRenderer != null)
                meshRenderer.enabled = false;

            MeshCollider meshCollider =
                target.GetComponent<MeshCollider>();

            if (meshCollider == null)
            {
                meshCollider =
                    target.gameObject.AddComponent<MeshCollider>();
            }

            meshCollider.convex = true;

            if (!Colliders.Contains(target.gameObject))
                Colliders.Add(target.gameObject);
        }

        /*
         * 무게중심
         */
        if (COM == null &&
            ContainsAny(objectName, "COM"))
        {
            COM = target;
        }

        /*
         * 바퀴
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
                "SteerWheel",
                "Handle"
            ))
        {
            SteeringWheel = target.gameObject;
        }

        /*
         * 조명
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
         * 모든 자식 검색
         */
        for (int i = 0; i < target.childCount; i++)
        {
            ExtractElementRecursive(target.GetChild(i));
        }
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

        if (!TryGetLocalRendererBounds(
                wheelMesh.transform,
                out Bounds wheelBounds
            ))
        {
            Debug.LogWarning(
                $"{wheelMesh.name}에서 Renderer를 찾지 못했습니다.",
                wheelMesh
            );

            return null;
        }

        GameObject wheelColliderObject =
            new GameObject(
                GeneratedWheelColliderPrefix + wheelName
            );

        wheelColliderObject.layer = wheelMesh.layer;

        Transform wheelTransform = wheelMesh.transform;
        Transform wheelParent = wheelTransform.parent;

        if (wheelParent == null)
            wheelParent = RootObject.transform;

        /*
         * 바퀴 메시의 자식이 아닌 같은 부모의 별도 오브젝트로 생성합니다.
         *
         * 이렇게 해야 바퀴 메시가 회전해도 WheelCollider 오브젝트가
         * 메시의 회전에 직접 끌려가지 않습니다.
         */
        wheelColliderObject.transform.SetParent(
            wheelParent,
            false
        );

        wheelColliderObject.transform.localPosition =
            wheelTransform.localPosition;

       // wheelColliderObject.transform.localRotation =
          //  wheelTransform.localRotation;

      //  wheelColliderObject.transform.localScale =
         //   wheelTransform.localScale;

        WheelCollider wheelCollider =
            wheelColliderObject.AddComponent<WheelCollider>();

        /*
         * Renderer Bounds의 중심이 바퀴 오브젝트의 원점과 다를 경우
         * WheelCollider.center로 보정합니다.
         */
        wheelCollider.center = wheelBounds.center;

        /*
         * 일반적인 자동차 바퀴는 로컬 X축이 바퀴 축이고,
         * Y/Z 크기가 바퀴의 지름입니다.
         */
        float radiusFromY = wheelBounds.extents.y;
        float radiusFromZ = wheelBounds.extents.z;

        wheelCollider.radius =
            Mathf.Max(radiusFromY, radiusFromZ) *
            WheelRadiusMultiplier;

        wheelCollider.mass = WheelMass;
        wheelCollider.suspensionDistance =
            SuspensionDistance;

        Debug.Log(
            $"{wheelName} WheelCollider 생성\n" +
            $"Mesh: {wheelMesh.name}\n" +
            $"Radius: {wheelCollider.radius:F3}\n" +
            $"Center: {wheelCollider.center}",
            wheelColliderObject
        );

        return wheelCollider;
    }

    private bool TryGetLocalRendererBounds(
        Transform wheelTransform,
        out Bounds localBounds
    )
    {
        Renderer[] renderers =
            wheelTransform.GetComponentsInChildren<Renderer>(
                true
            );

        localBounds = new Bounds();

        if (renderers.Length == 0)
            return false;

        bool hasBounds = false;

        foreach (Renderer rendererComponent in renderers)
        {
            if (rendererComponent == null)
                continue;

            Bounds worldBounds = rendererComponent.bounds;

            Vector3 center = worldBounds.center;
            Vector3 extents = worldBounds.extents;

            /*
             * 월드 Bounds의 8개 꼭짓점을 바퀴의 로컬 좌표로 변환합니다.
             */
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

                        Vector3 localPoint =
                            wheelTransform.InverseTransformPoint(
                                worldPoint
                            );

                        if (!hasBounds)
                        {
                            localBounds =
                                new Bounds(
                                    localPoint,
                                    Vector3.zero
                                );

                            hasBounds = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(localPoint);
                        }
                    }
                }
            }
        }

        return hasBounds;
    }

    private void DeleteGeneratedWheelColliders()
    {
        WheelCollider[] existingWheelColliders =
            RootObject.GetComponentsInChildren<WheelCollider>(
                true
            );

        foreach (WheelCollider existing in existingWheelColliders)
        {
            if (existing == null)
                continue;

            if (!existing.gameObject.name.StartsWith(
                    GeneratedWheelColliderPrefix,
                    StringComparison.Ordinal
                ))
            {
                continue;
            }

            GameObject targetObject = existing.gameObject;

            if (Application.isPlaying)
            {
                targetObject.SetActive(false);
                Destroy(targetObject);
            }
            else
            {
                DestroyImmediate(targetObject);
            }
        }
    }

    private bool ContainsAny(
        string source,
        params string[] keywords
    )
    {
        if (string.IsNullOrEmpty(source))
            return false;

        foreach (string keyword in keywords)
        {
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

    private void ClearElements()
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

        Light_Run = null;
        Light_Brake = null;
        Light_Head = null;
        Light_Rev = null;
        Light_Left = null;
        Light_Right = null;

        COM = null;
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