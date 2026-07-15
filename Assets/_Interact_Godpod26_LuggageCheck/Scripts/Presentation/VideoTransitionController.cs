using UnityEngine;
using UnityEngine.Serialization;

namespace RFIDBaggage.Presentation
{
    public sealed class VideoTransitionController : MonoBehaviour
    {
        [Header("3D Surfaces")]
        [SerializeField, Tooltip("Renderer displaying the idle video RenderTexture.")]
        private Renderer idleVideoRenderer;

        [SerializeField, Tooltip("Texture assigned to the idle video renderer, usually RT_IdleVideo.")]
        private Texture idleVideoTexture;

        [SerializeField, Tooltip("Renderer displaying intro/result video RenderTexture.")]
        private Renderer contentVideoRenderer;

        [FormerlySerializedAs("contentVideoTexture")]
        [SerializeField, Tooltip("Texture assigned to ContentVideoPlayerA, usually RT_ContentA.")]
        private Texture contentVideoTextureA;

        [SerializeField, Tooltip("Texture assigned to ContentVideoPlayerB, usually RT_ContentB.")]
        private Texture contentVideoTextureB;

        [SerializeField, Tooltip("Renderer displaying the final-frame static background texture.")]
        private Renderer staticBackgroundRenderer;

        [Header("Material Texture Properties")]
        [SerializeField, Tooltip("Primary shader texture property. URP Lit usually uses _BaseMap.")]
        private string texturePropertyName = "_BaseMap";

        [SerializeField, Tooltip("Also writes _MainTex for Built-in/Unlit compatible materials.")]
        private bool alsoSetMainTex = true;

        [SerializeField, Tooltip("Optional loading or error overlay object.")]
        private GameObject loadingOverlay;

        private MaterialPropertyBlock idlePropertyBlock;
        private MaterialPropertyBlock contentPropertyBlock;
        private MaterialPropertyBlock staticPropertyBlock;

        private int texturePropertyId;
        private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");

        private void Awake()
        {
            InitializePropertyIds();
            ApplyConfiguredVideoTextures();
        }

        private void OnValidate()
        {
            InitializePropertyIds();
        }

        public void ShowIdleVideo()
        {
            SetRendererTexture(idleVideoRenderer, idleVideoTexture, ref idlePropertyBlock);
            SetVisible(idleVideoRenderer, true);
            SetVisible(contentVideoRenderer, false);
            SetVisible(staticBackgroundRenderer, false);
            Debug.Log("[Transition] Current -> IdleVideoSurface", this);
        }

        public void ShowContentVideo()
        {
            ShowContentVideo(contentVideoTextureA);
        }

        public void ShowContentVideoB()
        {
            ShowContentVideo(contentVideoTextureB);
        }

        public void ShowContentVideo(Texture contentTexture)
        {
            SetRendererTexture(contentVideoRenderer, contentTexture, ref contentPropertyBlock);
            SetVisible(contentVideoRenderer, true);
            SetVisible(idleVideoRenderer, false);
            SetVisible(staticBackgroundRenderer, false);
            Debug.Log("[Transition] Current -> ContentVideoSurface", this);
        }

        public void ShowStaticBackground(Texture texture)
        {
            SetRendererTexture(staticBackgroundRenderer, texture, ref staticPropertyBlock);
            SetVisible(staticBackgroundRenderer, true);
            SetVisible(contentVideoRenderer, false);
            SetVisible(idleVideoRenderer, false);
            Debug.Log("[Transition] ContentVideoSurface -> StaticBackgroundSurface", this);
        }

        public void ShowLoadingOverlay()
        {
            if (loadingOverlay != null)
            {
                loadingOverlay.SetActive(true);
            }
        }

        public void HideLoadingOverlay()
        {
            if (loadingOverlay != null)
            {
                loadingOverlay.SetActive(false);
            }
        }

        public void HideAll()
        {
            SetVisible(idleVideoRenderer, false);
            SetVisible(contentVideoRenderer, false);
            SetVisible(staticBackgroundRenderer, false);
            HideLoadingOverlay();
        }

        public void ClearStaticBackground()
        {
            SetVisible(staticBackgroundRenderer, false);
        }

        private void ApplyConfiguredVideoTextures()
        {
            SetRendererTexture(idleVideoRenderer, idleVideoTexture, ref idlePropertyBlock);
            SetRendererTexture(contentVideoRenderer, contentVideoTextureA, ref contentPropertyBlock);
        }

        private void SetRendererTexture(Renderer targetRenderer, Texture texture, ref MaterialPropertyBlock propertyBlock)
        {
            if (targetRenderer == null)
            {
                return;
            }

            if (texture == null)
            {
                Debug.LogWarning($"[Transition] Missing texture for renderer {targetRenderer.name}.", this);
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetTexture(texturePropertyId, texture);

            if (alsoSetMainTex)
            {
                propertyBlock.SetTexture(MainTexPropertyId, texture);
            }

            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private static void SetVisible(Renderer targetRenderer, bool visible)
        {
            if (targetRenderer == null)
            {
                return;
            }

            targetRenderer.enabled = visible;
            targetRenderer.gameObject.SetActive(visible);
        }

        private void InitializePropertyIds()
        {
            if (string.IsNullOrWhiteSpace(texturePropertyName))
            {
                texturePropertyName = "_BaseMap";
            }

            texturePropertyId = Shader.PropertyToID(texturePropertyName);
        }
    }
}
