using UnityEngine;

public class QuadFitDisplay : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private float _distance = 10f;

    private void Update()
    {
        float height = 2.0f * _distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * _camera.aspect;

        transform.localScale = new Vector3(width, height, 0);

        Matrix4x4 viewMatInv = _camera.worldToCameraMatrix.inverse;
        Vector4 viewPos = new Vector4(0, 0, -_distance, 1);
        Vector4 worldPos = viewMatInv * viewPos;

        transform.position = worldPos;
        transform.rotation = _camera.transform.rotation;
    }
}