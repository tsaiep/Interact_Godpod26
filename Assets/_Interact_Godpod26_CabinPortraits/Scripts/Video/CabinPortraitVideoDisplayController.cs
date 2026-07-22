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
        [Header("Initial Display")]
        [SerializeField, Tooltip("Renderer for the static initial image Quad shown while no video is playing.")]
        private Renderer initialDisplayRenderer;

        [SerializeField, Tooltip("Optional texture to assign to the static initial image Quad.")]
        private Texture initialTexture;

        [Header("Manual Shared Display")]
        [SerializeField, Tooltip("Optional single renderer for manual videos. The active manual player's RenderTexture is assigned to this renderer.")]
        private Renderer sharedDisplayRenderer;

        [Header("Manual Separate Displays")]
        [SerializeField, Tooltip("Optional renderer dedicated to manual player A.")]
        private Renderer playerADisplayRenderer;

        [SerializeField, Tooltip("Optional renderer dedicated to manual player B.")]
        private Renderer playerBDisplayRenderer;

        [Header("Manual Fallback Textures")]
        [SerializeField, Tooltip("Texture assigned to manual player A, usually a RenderTexture.")]
        private Texture playerATexture;

        [SerializeField, Tooltip("Texture assigned to manual player B, usually a RenderTexture.")]
        private Texture playerBTexture;

        [Header("Timer Shared Display")]
        [SerializeField, Tooltip("Optional single renderer for timer videos. Leave empty to fall back to the manual display renderers.")]
        private Renderer timerSharedDisplayRenderer;

        [Header("Timer Separate Displays")]
        [SerializeField, Tooltip("Optional renderer dedicated to timer player A.")]
        private Renderer timerPlayerADisplayRenderer;

        [SerializeField, Tooltip("Optional renderer dedicated to timer player B.")]
        private Renderer timerPlayerBDisplayRenderer;

        [Header("Timer Fallback Textures")]
        [SerializeField, Tooltip("Texture assigned to timer player A, usually a RenderTexture.")]
        private Texture timerPlayerATexture;

        [SerializeField, Tooltip("Texture assigned to timer player B, usually a RenderTexture.")]
        private Texture timerPlayerBTexture;

        [Header("Material Texture Properties")]
        [SerializeField, Tooltip("Primary shader texture property. URP Lit usually uses _BaseMap.")]
        private string texturePropertyName = "_BaseMap";

        [SerializeField, Tooltip("Also writes _MainTex for Built-in/Unlit compatible materials.")]
        private bool alsoSetMainTex = true;

        [SerializeField, Tooltip("When enabled, display texture assignments are logged to Console.")]
        private bool verboseDisplayLogs = true;

        private MaterialPropertyBlock initialPropertyBlock;
        private MaterialPropertyBlock sharedPropertyBlock;
        private MaterialPropertyBlock playerAPropertyBlock;
        private MaterialPropertyBlock playerBPropertyBlock;
        private MaterialPropertyBlock timerSharedPropertyBlock;
        private MaterialPropertyBlock timerPlayerAPropertyBlock;
        private MaterialPropertyBlock timerPlayerBPropertyBlock;

        private Texture initialAssignedTexture;
        private Texture sharedAssignedTexture;
        private Texture playerAAssignedTexture;
        private Texture playerBAssignedTexture;
        private Texture timerSharedAssignedTexture;
        private Texture timerPlayerAAssignedTexture;
        private Texture timerPlayerBAssignedTexture;

        private int texturePropertyId;
        private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");

        private void Awake()
        {
            InitializePropertyIds();
            ApplyConfiguredTextures();

            if (verboseDisplayLogs)
            {
                Debug.Log(
                    $"[CabinPortraits.Display] Initialized. Initial={DescribeRenderer(initialDisplayRenderer)}, " +
                    $"InitialTexture={DescribeTexture(initialTexture)}, ManualShared={DescribeRenderer(sharedDisplayRenderer)}, " +
                    $"ManualA={DescribeRenderer(playerADisplayRenderer)}, ManualB={DescribeRenderer(playerBDisplayRenderer)}, " +
                    $"ManualFallbackA={DescribeTexture(playerATexture)}, ManualFallbackB={DescribeTexture(playerBTexture)}, " +
                    $"TimerShared={DescribeRenderer(timerSharedDisplayRenderer)}, TimerA={DescribeRenderer(timerPlayerADisplayRenderer)}, " +
                    $"TimerB={DescribeRenderer(timerPlayerBDisplayRenderer)}, TimerFallbackA={DescribeTexture(timerPlayerATexture)}, " +
                    $"TimerFallbackB={DescribeTexture(timerPlayerBTexture)}.",
                    this);
            }
        }

        private void OnValidate()
        {
            InitializePropertyIds();
        }

        public void ShowInitial()
        {
            Renderer preservedRenderer = null;

            if (initialDisplayRenderer != null)
            {
                if (initialTexture != null)
                {
                    SetRendererTexture(initialDisplayRenderer, initialTexture, ref initialPropertyBlock, ref initialAssignedTexture);
                }

                SetVisible(initialDisplayRenderer, true);
                preservedRenderer = initialDisplayRenderer;
            }
            else if (sharedDisplayRenderer != null && initialTexture != null)
            {
                SetRendererTexture(sharedDisplayRenderer, initialTexture, ref sharedPropertyBlock, ref sharedAssignedTexture);
                SetVisible(sharedDisplayRenderer, true);
                preservedRenderer = sharedDisplayRenderer;
            }

            HideManualVideoRenderers(preservedRenderer);
            HideTimerVideoRenderers(preservedRenderer);

            if (verboseDisplayLogs)
            {
                Debug.Log(
                    $"[CabinPortraits.Display] ShowInitial. Initial={DescribeRenderer(initialDisplayRenderer)}, " +
                    $"Texture={DescribeTexture(initialTexture)}, Preserved={DescribeRenderer(preservedRenderer)}.",
                    this);
            }
        }

        public void ShowSlot(CabinPortraitVideoSlot slot, Texture runtimeTexture)
        {
            ShowSlot(CabinPortraitVideoSequenceKind.ManualInput, slot, runtimeTexture);
        }

        public void ShowSlot(CabinPortraitVideoSequenceKind sequenceKind, CabinPortraitVideoSlot slot, Texture runtimeTexture)
        {
            bool useTimerDisplay = sequenceKind == CabinPortraitVideoSequenceKind.Timer && HasTimerDisplayRenderer();
            bool usingFallback = runtimeTexture == null;
            Texture texture = runtimeTexture != null ? runtimeTexture : GetConfiguredTexture(sequenceKind, slot, useTimerDisplay);

            if (useTimerDisplay)
            {
                ShowTimerSlot(slot, texture);
            }
            else
            {
                ShowManualSlot(slot, texture);
            }

            if (verboseDisplayLogs)
            {
                string displayMode = useTimerDisplay
                    ? (timerSharedDisplayRenderer != null ? "TimerShared" : "TimerSeparate")
                    : (sequenceKind == CabinPortraitVideoSequenceKind.Timer ? "ManualFallback" : (sharedDisplayRenderer != null ? "ManualShared" : "ManualSeparate"));

                Debug.Log(
                    $"[CabinPortraits.Display] ShowSlot {sequenceKind}/{slot}. Texture={DescribeTexture(texture)}, " +
                    $"Source={(usingFallback ? "fallback" : "runtime")}, DisplayMode={displayMode}.",
                    this);
            }
        }

        public void ShowPlayerA()
        {
            ShowSlot(CabinPortraitVideoSequenceKind.ManualInput, CabinPortraitVideoSlot.A, playerATexture);
        }

        public void ShowPlayerB()
        {
            ShowSlot(CabinPortraitVideoSequenceKind.ManualInput, CabinPortraitVideoSlot.B, playerBTexture);
        }

        public void HideAll()
        {
            SetVisible(initialDisplayRenderer, false);
            SetVisible(sharedDisplayRenderer, false);
            SetVisible(playerADisplayRenderer, false);
            SetVisible(playerBDisplayRenderer, false);
            SetVisible(timerSharedDisplayRenderer, false);
            SetVisible(timerPlayerADisplayRenderer, false);
            SetVisible(timerPlayerBDisplayRenderer, false);
        }

        private void ShowManualSlot(CabinPortraitVideoSlot slot, Texture texture)
        {
            HideTimerVideoRenderers(null);

            if (sharedDisplayRenderer != null)
            {
                SetRendererTexture(sharedDisplayRenderer, texture, ref sharedPropertyBlock, ref sharedAssignedTexture);
                SetVisible(sharedDisplayRenderer, true);
                SetVisibleIfNotRenderer(playerADisplayRenderer, sharedDisplayRenderer, false);
                SetVisibleIfNotRenderer(playerBDisplayRenderer, sharedDisplayRenderer, false);
                HideInitialIfNotRenderer(sharedDisplayRenderer);
                return;
            }

            Renderer selectedRenderer = slot == CabinPortraitVideoSlot.A ? playerADisplayRenderer : playerBDisplayRenderer;

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
            HideInitialIfNotRenderer(selectedRenderer);
        }

        private void ShowTimerSlot(CabinPortraitVideoSlot slot, Texture texture)
        {
            HideManualVideoRenderers(null);

            if (timerSharedDisplayRenderer != null)
            {
                SetRendererTexture(timerSharedDisplayRenderer, texture, ref timerSharedPropertyBlock, ref timerSharedAssignedTexture);
                SetVisible(timerSharedDisplayRenderer, true);
                SetVisibleIfNotRenderer(timerPlayerADisplayRenderer, timerSharedDisplayRenderer, false);
                SetVisibleIfNotRenderer(timerPlayerBDisplayRenderer, timerSharedDisplayRenderer, false);
                HideInitialIfNotRenderer(timerSharedDisplayRenderer);
                return;
            }

            Renderer selectedRenderer = GetTimerSlotRenderer(slot);
            if (selectedRenderer == null)
            {
                selectedRenderer = GetAnyTimerSeparateRenderer();
            }

            if (selectedRenderer == timerPlayerADisplayRenderer)
            {
                SetRendererTexture(timerPlayerADisplayRenderer, texture, ref timerPlayerAPropertyBlock, ref timerPlayerAAssignedTexture);
            }
            else if (selectedRenderer == timerPlayerBDisplayRenderer)
            {
                SetRendererTexture(timerPlayerBDisplayRenderer, texture, ref timerPlayerBPropertyBlock, ref timerPlayerBAssignedTexture);
            }

            SetVisible(timerPlayerADisplayRenderer, timerPlayerADisplayRenderer == selectedRenderer);
            SetVisible(timerPlayerBDisplayRenderer, timerPlayerBDisplayRenderer == selectedRenderer);
            HideInitialIfNotRenderer(selectedRenderer);
        }

        private void ApplyConfiguredTextures()
        {
            if (initialTexture != null)
            {
                SetRendererTexture(initialDisplayRenderer, initialTexture, ref initialPropertyBlock, ref initialAssignedTexture);
            }

            if (sharedDisplayRenderer != null)
            {
                SetRendererTexture(sharedDisplayRenderer, playerATexture, ref sharedPropertyBlock, ref sharedAssignedTexture);
            }
            else
            {
                SetRendererTexture(playerADisplayRenderer, playerATexture, ref playerAPropertyBlock, ref playerAAssignedTexture);
                SetRendererTexture(playerBDisplayRenderer, playerBTexture, ref playerBPropertyBlock, ref playerBAssignedTexture);
            }

            if (timerSharedDisplayRenderer != null)
            {
                SetRendererTexture(timerSharedDisplayRenderer, timerPlayerATexture, ref timerSharedPropertyBlock, ref timerSharedAssignedTexture);
            }
            else
            {
                SetRendererTexture(timerPlayerADisplayRenderer, timerPlayerATexture, ref timerPlayerAPropertyBlock, ref timerPlayerAAssignedTexture);
                SetRendererTexture(timerPlayerBDisplayRenderer, timerPlayerBTexture, ref timerPlayerBPropertyBlock, ref timerPlayerBAssignedTexture);
            }
        }

        private Texture GetConfiguredTexture(CabinPortraitVideoSequenceKind sequenceKind, CabinPortraitVideoSlot slot, bool useTimerDisplay)
        {
            if (sequenceKind == CabinPortraitVideoSequenceKind.Timer && useTimerDisplay)
            {
                return slot == CabinPortraitVideoSlot.A ? timerPlayerATexture : timerPlayerBTexture;
            }

            return slot == CabinPortraitVideoSlot.A ? playerATexture : playerBTexture;
        }

        private bool HasTimerDisplayRenderer()
        {
            return timerSharedDisplayRenderer != null ||
                   timerPlayerADisplayRenderer != null ||
                   timerPlayerBDisplayRenderer != null;
        }

        private Renderer GetTimerSlotRenderer(CabinPortraitVideoSlot slot)
        {
            return slot == CabinPortraitVideoSlot.A ? timerPlayerADisplayRenderer : timerPlayerBDisplayRenderer;
        }

        private Renderer GetAnyTimerSeparateRenderer()
        {
            return timerPlayerADisplayRenderer != null ? timerPlayerADisplayRenderer : timerPlayerBDisplayRenderer;
        }

        private void HideManualVideoRenderers(Renderer preservedRenderer)
        {
            SetVisibleIfNotRenderer(sharedDisplayRenderer, preservedRenderer, false);
            SetVisibleIfNotRenderer(playerADisplayRenderer, preservedRenderer, false);
            SetVisibleIfNotRenderer(playerBDisplayRenderer, preservedRenderer, false);
        }

        private void HideTimerVideoRenderers(Renderer preservedRenderer)
        {
            SetVisibleIfNotRenderer(timerSharedDisplayRenderer, preservedRenderer, false);
            SetVisibleIfNotRenderer(timerPlayerADisplayRenderer, preservedRenderer, false);
            SetVisibleIfNotRenderer(timerPlayerBDisplayRenderer, preservedRenderer, false);
        }

        private void HideInitialIfNotRenderer(Renderer activeRenderer)
        {
            SetVisibleIfNotRenderer(initialDisplayRenderer, activeRenderer, false);
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

        private static void SetVisibleIfNotRenderer(Renderer targetRenderer, Renderer preservedRenderer, bool visible)
        {
            if (targetRenderer == null || targetRenderer == preservedRenderer)
            {
                return;
            }

            targetRenderer.enabled = visible;
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
