using UnityEngine;

public class WheelColliderShadow : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider wheelFL;
    public WheelCollider wheelFR;
    public WheelCollider wheelRL;
    public WheelCollider wheelRR;

    [Header("Shadow Quad")]
    [Tooltip("Unity default Quad used as the shadow.")]
    public Transform shadowQuad;

    [Header("Settings")]
    [Tooltip("Extra size around the wheels.")]
    public float sizePadding = 0.1f;

    [Tooltip("Lift the shadow slightly above the ground to avoid Z-fighting.")]
    public float groundOffset = 0.02f;

    [Tooltip("Update the shadow every frame.")]
    public bool updateContinuously = true;

    private WheelCollider[] wheels;

    private void Awake()
    {
        UpdateWheelArray();
    }

    private void Start()
    {
        UpdateShadow();
    }

    private void LateUpdate()
    {
        if (updateContinuously)
            UpdateShadow();
    }

    private void OnValidate()
    {
        UpdateWheelArray();
    }

    private void UpdateWheelArray()
    {
        wheels = new WheelCollider[]
        {
            wheelFL,
            wheelFR,
            wheelRL,
            wheelRR
        };
    }

    [ContextMenu("Update Shadow")]
    public void UpdateShadow()
    {
        if (shadowQuad == null)
            return;

        if (wheels == null || wheels.Length != 4)
            UpdateWheelArray();

        // Work in this object's local space.
        float minX = float.MaxValue;
        float maxX = float.MinValue;

        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        float groundY = float.MaxValue;

        int validWheels = 0;
        int groundedWheels = 0;

        foreach (WheelCollider wheel in wheels)
        {
            if (wheel == null)
                continue;

            validWheels++;

            // Actual WheelCollider center in world space
            Vector3 wheelCenterWorld =
                wheel.transform.TransformPoint(wheel.center);

            // Convert to this vehicle's local coordinate system
            Vector3 localCenter =
                transform.InverseTransformPoint(wheelCenterWorld);

            float radius = wheel.radius;

            // Include actual wheel radius in bounds
            minX = Mathf.Min(minX, localCenter.x - radius);
            maxX = Mathf.Max(maxX, localCenter.x + radius);

            minZ = Mathf.Min(minZ, localCenter.z - radius);
            maxZ = Mathf.Max(maxZ, localCenter.z + radius);

            // ----------------------------------------------------
            // Get actual ground contact
            // ----------------------------------------------------

            WheelHit hit;

            if (wheel.GetGroundHit(out hit))
            {
                Vector3 localGround =
                    transform.InverseTransformPoint(hit.point);

                groundY = Mathf.Min(groundY, localGround.y);
                groundedWheels++;
            }
        }

        if (validWheels == 0)
            return;

        // --------------------------------------------------------
        // If no wheel is touching the ground, calculate bottom
        // from the wheel radius.
        // --------------------------------------------------------

        if (groundedWheels == 0)
        {
            groundY = float.MaxValue;

            foreach (WheelCollider wheel in wheels)
            {
                if (wheel == null)
                    continue;

                Vector3 wheelCenterWorld =
                    wheel.transform.TransformPoint(wheel.center);

                Vector3 localCenter =
                    transform.InverseTransformPoint(wheelCenterWorld);

                float bottom =
                    localCenter.y - wheel.radius;

                groundY = Mathf.Min(groundY, bottom);
            }
        }

        // --------------------------------------------------------
        // Calculate shadow dimensions
        // --------------------------------------------------------

        float width =
            (maxX - minX) + sizePadding * 2f;

        float length =
            (maxZ - minZ) + sizePadding * 2f;

        float centerX =
            (minX + maxX) * 0.5f;

        float centerZ =
            (minZ + maxZ) * 0.5f;

        // --------------------------------------------------------
        // Place Quad
        // --------------------------------------------------------

        shadowQuad.localPosition = new Vector3(
            centerX,
            groundY + groundOffset,
            centerZ
        );

        // Unity default Quad lies on XY plane.
        // Rotate it so it lies horizontally on XZ plane.
        shadowQuad.localRotation =
            Quaternion.Euler(90f, 0f, 0f);

        // Unity default Quad is 1 x 1.
        //
        // After rotating 90 degrees:
        // local X = vehicle width
        // local Y = vehicle length
        shadowQuad.localScale = new Vector3(
            width,
            length,
            1f
        );
    }
}