using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class EditingConvini : MonoBehaviour
{
    public Button ExportModButton;
    public ExtractElements ex;

    public Toggle DisableCol;
    public bool isOnCol = true;

    public GameObject noWheel;
    public GameObject noCol;

    public Toggle PlateTog;
    public GameObject[] Plate;

    public Toggle EXTog;
    public GameObject[] Ex;

    public Toggle HumanTog;
    public GameObject Human;

    [Header("Digital Dashboard Preview")]
    public TMP_Text SpeedText;
    public TMP_Text RPMText;

    public int PreviewSpeed = 100;
    public int PreviewRPM = 3000;

    private const float DashboardBaseScale = 0.003f;

    private int lastSpeed = int.MinValue;
    private int lastRPM = int.MinValue;

    void Start()
    {
        ExportModButton.interactable = false;

        DisableCol.onValueChanged.AddListener(OnCollisionSet);
        PlateTog.onValueChanged.AddListener(ShowPlates);
        EXTog.onValueChanged.AddListener(ShowEx);
        HumanTog.onValueChanged.AddListener(ShowHuman);

        if (SpeedText)
            SpeedText.gameObject.SetActive(false);

        if (RPMText)
            RPMText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!ex)
            return;

        bool hasWheels =
            ex.Wheel_FL &&
            ex.Wheel_FR &&
            ex.Wheel_RL &&
            ex.Wheel_RR;

        bool hasColliders = ex.Colliders.Count > 0;

        noWheel.SetActive(!hasWheels);
        noCol.SetActive(!hasColliders);

        ExportModButton.interactable =
            hasWheels && hasColliders;
    }

    void LateUpdate()
    {
        UpdateDashboardPreview();
    }

    // =========================================================
    // AUTOMATIC DIGITAL DASHBOARD PREVIEW
    // =========================================================

    void UpdateDashboardPreview()
    {
        UpdateTextPreview(
            SpeedText,
            ex ? ex.SpeedTextPos : null,
            PreviewSpeed,
            ref lastSpeed
        );

        UpdateTextPreview(
            RPMText,
            ex ? ex.RPMTextPos : null,
            PreviewRPM,
            ref lastRPM
        );
    }

    void UpdateTextPreview(
        TMP_Text text,
        GameObject marker,
        int value,
        ref int lastValue)
    {
        if (!text)
            return;

        // Automatically hide text when the marker is missing.
        if (!marker)
        {
            if (text.gameObject.activeSelf)
                text.gameObject.SetActive(false);

            lastValue = int.MinValue;
            return;
        }

        Transform source = marker.transform;
        Transform target = text.transform;

        // Copy world position and rotation.
        target.SetPositionAndRotation(
            source.position,
            source.rotation
        );

        // Multiply marker scale by the default TMP scale.
        Vector3 scale =
            source.lossyScale * DashboardBaseScale;

        // Compensate for inherited parent scaling.
        Transform parent = target.parent;

        if (parent != null)
        {
            Quaternion localRotation = target.localRotation;

            scale.x = DivideScale(
                scale.x,
                parent.TransformVector(
                    localRotation * Vector3.right
                ).magnitude
            );

            scale.y = DivideScale(
                scale.y,
                parent.TransformVector(
                    localRotation * Vector3.up
                ).magnitude
            );

            scale.z = DivideScale(
                scale.z,
                parent.TransformVector(
                    localRotation * Vector3.forward
                ).magnitude
            );
        }

        target.localScale = scale;

        // Update text only when the value changes.
        if (lastValue != value)
        {
            text.SetText("{0:0}", (float)value);
            lastValue = value;
        }

        // Automatically show text when the marker exists.
        if (!text.gameObject.activeSelf)
            text.gameObject.SetActive(true);
    }

    private static float DivideScale(
        float value,
        float parentScale)
    {
        return parentScale > 0.000001f
            ? value / parentScale
            : 0f;
    }

    // =========================================================
    // COLLIDER PREVIEW
    // =========================================================

    void OnCollisionSet(bool ha)
    {
        isOnCol = !isOnCol;

        List<GameObject> objectsWithCollider =
            FindObjectsOfType<GameObject>(true)
            .Where(go =>
                go.name.ToLower().Contains("collider"))
            .ToList();

        foreach (GameObject obj in objectsWithCollider)
        {
            obj.SetActive(isOnCol);
        }
    }

    // =========================================================
    // NUMBER PLATE PREVIEW
    // =========================================================

    void ShowPlates(bool b)
    {
        for (int i = 0; i < Plate.Length; i++)
        {
            if (!Plate[i])
                continue;

            bool available =
                b &&
                ex &&
                i < ex.Numberplate.Count &&
                ex.Numberplate[i];

            Plate[i].SetActive(available);

            if (available)
            {
                Plate[i].transform.SetPositionAndRotation(
                    ex.Numberplate[i].transform.position,
                    ex.Numberplate[i].transform.rotation
                );
            }
        }
    }

    // =========================================================
    // EXHAUST PREVIEW
    // =========================================================

    void ShowEx(bool b)
    {
        for (int i = 0; i < Ex.Length; i++)
        {
            if (!Ex[i])
                continue;

            bool available =
                b &&
                ex &&
                i < ex.Exhaust.Count &&
                ex.Exhaust[i];

            Ex[i].SetActive(available);

            if (available)
            {
                Ex[i].transform.position =
                    ex.Exhaust[i].transform.position;
            }
        }
    }

    // =========================================================
    // HUMAN PREVIEW
    // =========================================================

    void ShowHuman(bool b)
    {
        if (!Human)
            return;

        bool available =
            b && ex && ex.Person_Position;

        Human.SetActive(available);

        if (available)
        {
            Human.transform.position =
                ex.Person_Position.transform.position;
        }
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    void OnDestroy()
    {
        if (DisableCol)
            DisableCol.onValueChanged.RemoveListener(OnCollisionSet);

        if (PlateTog)
            PlateTog.onValueChanged.RemoveListener(ShowPlates);

        if (EXTog)
            EXTog.onValueChanged.RemoveListener(ShowEx);

        if (HumanTog)
            HumanTog.onValueChanged.RemoveListener(ShowHuman);
    }
}