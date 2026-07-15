using UnityEngine;

namespace CabinPortraits.Video
{
    public enum CabinPortraitVideoSlot
    {
        A,
        B
    }

    public sealed class CabinPortraitVideoDisplayController : MonoBehaviour
    {
        [Header("Shared Display")]
        [SerializeField, Tooltip("Optional single renderer. The active player's RenderTexture is assigned to this renderer.")]
        private Renderer sharedDisplayRenderer;

        [Header("Separate Displays")]
        [SerializeField, Tooltip("Optional renderer dedicated to player A.")]
        private Renderer playerADisplayRenderer;

        [SerializeField, Tooltip("Optional renderer dedicated to player B.")]
        private Renderer playerBDisplayRenderer;

        [Header("Fallback Textures")]
        [SerializeField, Tooltip("Texture assigned to player A, usually a RenderTexture.")]
        private Texture playerATexture;

        [SerializeField, Tooltip("Texture assigned to player B, usually a RenderTexture.")]
        private Texture playerBTexture;

        [Header("Material Texture Properties")]
        [SerializeField, Tooltip("Primary shader texture property. URP Lit usually uses _BaseMap.")]
        private string texturePropertyName = "_BaseMap";

        [SerializeField, Tooltip("Also writes _MainTex for Built-in/Unlit compatible materials.")]
        private bool alsoSetMainTex = true;

        [SerializeField, Tooltip("When enabled, display texture assignments are logged to Console.")]
        private bool verboseDisplayLogs = true;

        private MaterialPropertyBlock sharedPropertyBlock;
        private MaterialPropertyBlock playerAPropertyBlock;
        private MaterialPropertyBlock playerBPropertyBlock;
        private Texture sharedAssignedTexture;
        private Texture playerAAssignedTexture;
        private Texture playerBAssignedTexture;

        private int texturePropertyId;
        private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");

        private void Awake()
        {
            InitializePropertyIds();
            ApplyConfiguredTextures();

            if (verboseDisplayLogs)
            {
                Debug.Log(
                    $"[CabinPortraits.Display] Initialized. Shared={DescribeRenderer(sharedDisplayRenderer)}, " +
                    $"PlayerA={DescribeRenderer(playerADisplayRenderer)}, PlayerB={DescribeRenderer(playerBDisplayRenderer)}, " +
                    $"FallbackA={DescribeTexture(playerATexture)}, FallbackB={DescribeTexture(playerBTexture)}.",
                    this);
            }
        }

        private void OnValidate()
        {
            InitializePropertyIds();
        }

        public void ShowSlot(CabinPortraitVideoSlot slot, Texture runtimeTexture)
        {
            Texture texture = runtimeTexture != null ? runtimeTexture : GetConfiguredTexture(slot);
            bool usingFallback = runtimeTexture == null;

            if (sharedDisplayRenderer != null)
            {
                SetRendererTexture(sharedDisplayRenderer, texture, ref sharedPropertyBlock, ref sharedAssignedTexture);
                SetVisible(sharedDisplayRenderer, true);
                SetVisibleIfNotShared(playerADisplayRenderer, false);
                SetVisibleIfNotShared(playerBDisplayRenderer, false);
            }
            else
            {
                if (slot == CabinPortraitVideoSlot.A)
                {
                    SetRendererTexture(playerADisplayRenderer, texture, ref playerAPropertyBlock, ref playerAAssignedTexture);
                }
                else
                {
                    SetRendererTexture(playerBDisplayRenderer, texture, ref playerBPropertyBlock, ref playerBAssignedTexture);
                }

                SetVisible(playerADisplayRenderer, slot == CabinPortraitVideoSlot.A);
                SetVisible(playerBDisplayRenderer, slot == CabinPortraitVideoSlot.B);
            }

            if (verboseDisplayLogs)
            {
                Debug.Log(
                    $"[CabinPortraits.Display] ShowSlot {slot}. Texture={DescribeTexture(texture)}, " +
                    $"Source={(usingFallback ? "fallback" : "runtime")}, Shared={DescribeRenderer(sharedDisplayRenderer)}, " +
                    $"PlayerA={DescribeRenderer(playerADisplayRenderer)}, PlayerB={DescribeRenderer(playerBDisplayRenderer)}, " +
                    $"Mode={(sharedDisplayRenderer != null ? "Shared" : "Separate")}.",
                    this);
            }
        }

        public void ShowPlayerA()
        {
            ShowSlot(CabinPortraitVideoSlot.A, playerATexture);
        }

        public void ShowPlayerB()
        {
            ShowSlot(CabinPortraitVideoSlot.B, playerBTexture);
        }

        public void HideAll()
        {
            SetVisible(sharedDisplayRenderer, false);
            SetVisible(playerADisplayRenderer, false);
            SetVisible(playerBDisplayRenderer, false);
        }

        private void ApplyConfiguredTextures()
        {
            if (sharedDisplayRenderer != null)
            {
                SetRendererTexture(sharedDisplayRenderer, playerATexture, ref sharedPropertyBlock, ref sharedAssignedTexture);
                return;
            }

            SetRendererTexture(playerADisplayRenderer, playerATexture, ref playerAPropertyBlock, ref playerAAssignedTexture);
            SetRendererTexture(playerBDisplayRenderer, playerBTexture, ref playerBPropertyBlock, ref playerBAssignedTexture);
        }

        private Texture GetConfiguredTexture(CabinPortraitVideoSlot slot)
        {
            return slot == CabinPortraitVideoSlot.A ? playerATexture : playerBTexture;
        }

        private void SetRendererTexture(Renderer targetRenderer, Texture texture, ref MaterialPropertyBlock propertyBlock, ref Texture assignedTexture)
        {
            if (targetRenderer == null)
            {
                return;
            }

            if (texture == null)
            {
                if (verboseDisplayLogs)
                {
                    Debug.LogWarning($"[CabinPortraits.Display] Cannot assign null texture to {targetRenderer.name}.", this);
                }

                return;
            }

            if (assignedTexture == texture)
            {
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
            assignedTexture = texture;

            if (verboseDisplayLogs)
            {
                Debug.Log($"[CabinPortraits.Display] Assigned {DescribeTexture(texture)} to {targetRenderer.name}.", this);
            }
        }

        private static void SetVisible(Renderer targetRenderer, bool visible)
        {
            if (targetRenderer == null)
            {
                return;
            }

            targetRenderer.enabled = visible;
        }

        private void SetVisibleIfNotShared(Renderer targetRenderer, bool visible)
        {
            if (targetRenderer == sharedDisplayRenderer)
            {
                return;
            }

            SetVisible(targetRenderer, visible);
        }

        private void InitializePropertyIds()
        {
            if (string.IsNullOrWhiteSpace(texturePropertyName))
            {
                texturePropertyName = "_BaseMap";
            }

            texturePropertyId = Shader.PropertyToID(texturePropertyName);
        }

        private static string DescribeRenderer(Renderer targetRenderer)
        {
            return targetRenderer != null ? targetRenderer.name : "<none>";
        }

        private static string DescribeTexture(Texture texture)
        {
            return texture != null ? $"{texture.name} ({texture.width}x{texture.height})" : "<null>";
        }
    }
}
