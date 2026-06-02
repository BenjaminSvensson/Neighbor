using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class PlanarMirror : MonoBehaviour
    {
        [SerializeField] private Renderer mirrorRenderer;
        [SerializeField] private Transform mirrorPlane;
        [SerializeField, Min(64)] private int textureSize = 768;
        [SerializeField, Min(0.01f)] private float clipPlaneOffset = 0.04f;
        [SerializeField] private LayerMask reflectionMask = ~0;

        private Camera mirrorCamera;
        private Camera sourceCamera;
        private RenderTexture renderTexture;
        private Material mirrorMaterial;
        private int lastTextureSize;
        private float nextSourceCameraRefreshTime;
        private static bool isRenderingMirror;
        private const float SourceCameraRefreshInterval = 0.5f;

        private void Awake()
        {
            if (mirrorPlane == null)
            {
                mirrorPlane = transform;
            }

            if (mirrorRenderer == null)
            {
                mirrorRenderer = GetComponentInChildren<Renderer>();
            }

            EnsureResources();
        }

        private void OnDisable()
        {
            ReleaseResources();
        }

        private void LateUpdate()
        {
            RenderReflection();
        }

        private void RenderReflection()
        {
            if (isRenderingMirror || mirrorRenderer == null)
            {
                return;
            }

            if (!mirrorRenderer.isVisible)
            {
                return;
            }

            sourceCamera = ResolveSourceCamera();
            if (sourceCamera == null)
            {
                return;
            }

            EnsureResources();
            if (mirrorCamera == null || renderTexture == null)
            {
                return;
            }

            bool previousRendererState = mirrorRenderer.enabled;
            bool previousInvertCulling = GL.invertCulling;
            isRenderingMirror = true;

            try
            {
                Vector3 planePosition = mirrorPlane.position;
                Vector3 planeNormal = mirrorPlane.forward.normalized;
                float planeDistance = -Vector3.Dot(planeNormal, planePosition) - clipPlaneOffset;
                Vector4 reflectionPlane = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, planeDistance);

                Matrix4x4 reflectionMatrix = Matrix4x4.zero;
                CalculateReflectionMatrix(ref reflectionMatrix, reflectionPlane);

                Vector3 reflectedPosition = reflectionMatrix.MultiplyPoint(sourceCamera.transform.position);
                Vector3 reflectedForward = reflectionMatrix.MultiplyVector(sourceCamera.transform.forward);
                Vector3 reflectedUp = reflectionMatrix.MultiplyVector(sourceCamera.transform.up);

                mirrorCamera.CopyFrom(sourceCamera);
                mirrorCamera.enabled = false;
                mirrorCamera.cullingMask = reflectionMask;
                mirrorCamera.targetTexture = renderTexture;
                mirrorCamera.transform.SetPositionAndRotation(reflectedPosition, Quaternion.LookRotation(reflectedForward, reflectedUp));
                mirrorCamera.worldToCameraMatrix = sourceCamera.worldToCameraMatrix * reflectionMatrix;

                Vector4 cameraSpacePlane = CameraSpacePlane(mirrorCamera, planePosition, planeNormal, 1f);
                mirrorCamera.projectionMatrix = sourceCamera.CalculateObliqueMatrix(cameraSpacePlane);

                mirrorRenderer.enabled = false;
                GL.invertCulling = !previousInvertCulling;
                mirrorCamera.Render();
            }
            finally
            {
                GL.invertCulling = previousInvertCulling;
                mirrorRenderer.enabled = previousRendererState;
                isRenderingMirror = false;
            }
        }

        private void EnsureResources()
        {
            int size = Mathf.Max(64, textureSize);
            if (renderTexture == null || lastTextureSize != size)
            {
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    Destroy(renderTexture);
                }

                renderTexture = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32)
                {
                    name = $"{name}_MirrorReflection",
                    antiAliasing = 2,
                    useMipMap = false
                };
                renderTexture.Create();
                lastTextureSize = size;
            }

            if (mirrorCamera == null)
            {
                GameObject cameraObject = new GameObject($"{name}_ReflectionCamera");
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                mirrorCamera = cameraObject.AddComponent<Camera>();
                mirrorCamera.enabled = false;
            }

            if (mirrorRenderer != null && mirrorMaterial == null)
            {
                mirrorMaterial = mirrorRenderer.material;
            }

            if (mirrorMaterial != null)
            {
                mirrorMaterial.mainTexture = renderTexture;
                mirrorMaterial.SetTexture("_BaseMap", renderTexture);
                mirrorMaterial.SetTexture("_MainTex", renderTexture);
            }
        }

        private Camera ResolveSourceCamera()
        {
            if (Time.time < nextSourceCameraRefreshTime && IsUsableSourceCamera(sourceCamera))
            {
                return sourceCamera;
            }

            nextSourceCameraRefreshTime = Time.time + SourceCameraRefreshInterval;
            Camera taggedCamera = Camera.main;
            if (IsUsableSourceCamera(taggedCamera))
            {
                return taggedCamera;
            }

            Camera bestCamera = null;
            float bestDepth = float.NegativeInfinity;
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (!IsUsableSourceCamera(candidate) || candidate.depth < bestDepth)
                {
                    continue;
                }

                bestCamera = candidate;
                bestDepth = candidate.depth;
            }

            return bestCamera;
        }

        private bool IsUsableSourceCamera(Camera candidate)
        {
            return candidate != null
                && candidate != mirrorCamera
                && candidate.isActiveAndEnabled
                && candidate.targetTexture != renderTexture
                && candidate.cameraType == CameraType.Game;
        }

        private void ReleaseResources()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
                renderTexture = null;
            }

            if (mirrorCamera != null)
            {
                Destroy(mirrorCamera.gameObject);
                mirrorCamera = null;
            }

            if (mirrorMaterial != null)
            {
                Destroy(mirrorMaterial);
                mirrorMaterial = null;
            }
        }

        private static Vector4 CameraSpacePlane(Camera camera, Vector3 position, Vector3 normal, float sideSign)
        {
            Vector3 offsetPosition = position + normal * 0.04f;
            Matrix4x4 worldToCamera = camera.worldToCameraMatrix;
            Vector3 cameraPosition = worldToCamera.MultiplyPoint(offsetPosition);
            Vector3 cameraNormal = worldToCamera.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPosition, cameraNormal));
        }

        private static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMatrix, Vector4 plane)
        {
            reflectionMatrix.m00 = 1f - 2f * plane[0] * plane[0];
            reflectionMatrix.m01 = -2f * plane[0] * plane[1];
            reflectionMatrix.m02 = -2f * plane[0] * plane[2];
            reflectionMatrix.m03 = -2f * plane[3] * plane[0];

            reflectionMatrix.m10 = -2f * plane[1] * plane[0];
            reflectionMatrix.m11 = 1f - 2f * plane[1] * plane[1];
            reflectionMatrix.m12 = -2f * plane[1] * plane[2];
            reflectionMatrix.m13 = -2f * plane[3] * plane[1];

            reflectionMatrix.m20 = -2f * plane[2] * plane[0];
            reflectionMatrix.m21 = -2f * plane[2] * plane[1];
            reflectionMatrix.m22 = 1f - 2f * plane[2] * plane[2];
            reflectionMatrix.m23 = -2f * plane[3] * plane[2];

            reflectionMatrix.m30 = 0f;
            reflectionMatrix.m31 = 0f;
            reflectionMatrix.m32 = 0f;
            reflectionMatrix.m33 = 1f;
        }

        private void OnValidate()
        {
            textureSize = Mathf.Max(64, textureSize);
            clipPlaneOffset = Mathf.Max(0.01f, clipPlaneOffset);
        }
    }
}
