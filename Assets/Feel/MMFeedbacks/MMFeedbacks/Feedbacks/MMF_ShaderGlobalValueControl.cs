using System.Collections;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace MoreMountains.Feedbacks
{
	/// <summary>
	/// This feedback animates Shader global values over time.
	/// </summary>
	[AddComponentMenu("")]
	[FeedbackHelp("This feedback animates Shader global values over time, using a curve for numeric values and a gradient for colors.")]
	[MovedFrom(false, null, "MoreMountains.Feedbacks")]
	[System.Serializable]
	[FeedbackPath("Renderer/Shader Global Value Control")]
	public class MMF_ShaderGlobalValueControl : MMF_Feedback
	{
		public static bool FeedbackTypeAuthorized = true;

		#if UNITY_EDITOR
		public override Color FeedbackColor => MMFeedbacksInspectorColors.RendererColor;
		public override bool EvaluateRequiresSetup() => string.IsNullOrEmpty(PropertyName);
		public override string RequiredTargetText => string.IsNullOrEmpty(PropertyName) ? "" : PropertyName;
		public override string RequiresSetupText => "This feedback requires a Shader global property name.";
		#endif

		public override bool CanForceInitialValue => true;

		public enum ShaderGlobalParameterTypes
		{
			Float,
			FloatSlider,
			Color,
			Vector,
			Int,
			Bool
		}

		[MMFInspectorGroup("Shader Global", true, 24, true)]
		[Tooltip("the Shader global parameter type to control")]
		public ShaderGlobalParameterTypes ParameterType = ShaderGlobalParameterTypes.Float;

		[Tooltip("the Shader global property name, for example _GlobalAmount")]
		public string PropertyName = "";

		[MMFInspectorGroup("Timing", true, 25)]
		[Tooltip("the delay, in seconds, before the value starts changing")]
		[Min(0f)]
		public float Delay = 0f;

		[Tooltip("the duration, in seconds, over which the value changes")]
		[MMFEnumCondition("ParameterType", (int)ShaderGlobalParameterTypes.Float, (int)ShaderGlobalParameterTypes.FloatSlider, (int)ShaderGlobalParameterTypes.Color, (int)ShaderGlobalParameterTypes.Vector, (int)ShaderGlobalParameterTypes.Int)]
		[Min(0f)]
		public float Duration = 1f;

		[MMFInspectorGroup("Float", true, 26)]
		[Tooltip("the curve used to evaluate float values over normalized time")]
		[MMFEnumCondition("ParameterType", (int)ShaderGlobalParameterTypes.Float, (int)ShaderGlobalParameterTypes.FloatSlider, (int)ShaderGlobalParameterTypes.Vector, (int)ShaderGlobalParameterTypes.Int)]
		public AnimationCurve FloatCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

		[Tooltip("the minimum value used by FloatSlider")]
		[MMFEnumCondition("ParameterType", (int)ShaderGlobalParameterTypes.FloatSlider)]
		public float SliderMin = 0f;

		[Tooltip("the maximum value used by FloatSlider")]
		[MMFEnumCondition("ParameterType", (int)ShaderGlobalParameterTypes.FloatSlider)]
		public float SliderMax = 1f;

		[MMFInspectorGroup("Color", true, 27)]
		[Tooltip("the gradient used to evaluate color values over normalized time")]
		[MMFEnumCondition("ParameterType", (int)ShaderGlobalParameterTypes.Color)]
		[GradientUsage(true)]
		public Gradient ColorGradient = CreateDefaultGradient();

		[MMFInspectorGroup("Vector", true, 28)]
		[Tooltip("the starting vector value")]
		[MMFEnumCondition("ParameterType", (int)ShaderGlobalParameterTypes.Vector)]
		public Vector4 VectorFrom = Vector4.zero;

		[Tooltip("the destination vector value")]
		[MMFEnumCondition("ParameterType", (int)ShaderGlobalParameterTypes.Vector)]
		public Vector4 VectorTo = Vector4.one;

		[MMFInspectorGroup("Int", true, 29)]
		[Tooltip("the starting int value")]
		[MMFEnumCondition("ParameterType", (int)ShaderGlobalParameterTypes.Int)]
		public int IntFrom = 0;

		[Tooltip("the destination int value")]
		[MMFEnumCondition("ParameterType", (int)ShaderGlobalParameterTypes.Int)]
		public int IntTo = 1;

		[MMFInspectorGroup("Bool", true, 30)]
		[Tooltip("the bool value to set. Shader globals store this as an int, 0 or 1.")]
		[MMFEnumCondition("ParameterType", (int)ShaderGlobalParameterTypes.Bool)]
		public bool BoolValue = true;

		public override float FeedbackDuration
		{
			get
			{
				float duration = ParameterType == ShaderGlobalParameterTypes.Bool ? Delay : Delay + Duration;
				return ApplyTimeMultiplier(duration);
			}
			set
			{
				Duration = Mathf.Max(0f, value - Delay);
			}
		}

		protected int _propertyID;
		protected Coroutine _coroutine;
		protected Color _initialColor;
		protected float _initialFloat;
		protected Vector4 _initialVector;
		protected int _initialInt;
		protected bool _initialValueCaptured;

		protected override void CustomInitialization(MMF_Player owner)
		{
			base.CustomInitialization(owner);

			if (!Active || !CanApplyParameter())
			{
				return;
			}

			CachePropertyID();
			CaptureInitialValue();
		}

		protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1.0f)
		{
			if (!Active || !FeedbackTypeAuthorized || !CanApplyParameter())
			{
				return;
			}

			CachePropertyID();

			if (!_initialValueCaptured)
			{
				CaptureInitialValue();
			}

			if (_coroutine != null)
			{
				Owner.StopCoroutine(_coroutine);
			}

			_coroutine = Owner.StartCoroutine(ControlSequence());
		}

		protected virtual IEnumerator ControlSequence()
		{
			IsPlaying = true;

			float currentDelay = ApplyTimeMultiplier(Mathf.Max(0f, Delay));
			float currentDuration = ApplyTimeMultiplier(Mathf.Max(0f, Duration));

			float delayJourney = 0f;
			while ((delayJourney < currentDelay) && (currentDelay > 0f))
			{
				delayJourney += FeedbackDeltaTime;
				yield return null;
			}

			if ((ParameterType == ShaderGlobalParameterTypes.Bool) || (currentDuration <= 0f))
			{
				ApplyFinalValue();
				StopSequence();
				yield break;
			}

			float journey = NormalPlayDirection ? 0f : currentDuration;
			while ((journey >= 0f) && (journey <= currentDuration) && (currentDuration > 0f))
			{
				float normalizedTime = Mathf.Clamp01(journey / currentDuration);
				ApplySampledValue(normalizedTime);

				journey += NormalPlayDirection ? FeedbackDeltaTime : -FeedbackDeltaTime;
				yield return null;
			}

			ApplySampledValue(FinalNormalizedTime);
			StopSequence();
		}

		protected virtual void ApplyFinalValue()
		{
			if (ParameterType == ShaderGlobalParameterTypes.Bool)
			{
				Shader.SetGlobalInt(_propertyID, BoolValue ? 1 : 0);
				return;
			}

			ApplySampledValue(FinalNormalizedTime);
		}

		protected virtual void ApplySampledValue(float normalizedTime)
		{
			float curveValue = EvaluateFloatCurve(normalizedTime);

			switch (ParameterType)
			{
				case ShaderGlobalParameterTypes.Float:
					Shader.SetGlobalFloat(_propertyID, curveValue);
					break;
				case ShaderGlobalParameterTypes.FloatSlider:
					Shader.SetGlobalFloat(_propertyID, Mathf.LerpUnclamped(SliderMin, SliderMax, curveValue));
					break;
				case ShaderGlobalParameterTypes.Color:
					Shader.SetGlobalColor(_propertyID, EvaluateColorGradient(normalizedTime));
					break;
				case ShaderGlobalParameterTypes.Vector:
					Shader.SetGlobalVector(_propertyID, Vector4.LerpUnclamped(VectorFrom, VectorTo, curveValue));
					break;
				case ShaderGlobalParameterTypes.Int:
					Shader.SetGlobalInt(_propertyID, Mathf.RoundToInt(Mathf.LerpUnclamped(IntFrom, IntTo, curveValue)));
					break;
			}
		}

		protected virtual float EvaluateFloatCurve(float normalizedTime)
		{
			return FloatCurve != null ? FloatCurve.Evaluate(normalizedTime) : normalizedTime;
		}

		protected virtual Color EvaluateColorGradient(float normalizedTime)
		{
			return ColorGradient != null ? ColorGradient.Evaluate(normalizedTime) : Color.white;
		}

		protected virtual bool CanApplyParameter()
		{
			if (!string.IsNullOrEmpty(PropertyName))
			{
				return true;
			}

			if (Owner != null)
			{
				Debug.LogWarning("[Shader Global Value Control Feedback] The feedback on " + Owner.name + " doesn't have a property name, it won't work. You need to specify one in its inspector.");
			}

			return false;
		}

		protected virtual void CachePropertyID()
		{
			_propertyID = Shader.PropertyToID(PropertyName);
		}

		protected virtual void CaptureInitialValue()
		{
			switch (ParameterType)
			{
				case ShaderGlobalParameterTypes.Float:
				case ShaderGlobalParameterTypes.FloatSlider:
					_initialFloat = Shader.GetGlobalFloat(_propertyID);
					break;
				case ShaderGlobalParameterTypes.Color:
					_initialColor = Shader.GetGlobalColor(_propertyID);
					break;
				case ShaderGlobalParameterTypes.Vector:
					_initialVector = Shader.GetGlobalVector(_propertyID);
					break;
				case ShaderGlobalParameterTypes.Int:
				case ShaderGlobalParameterTypes.Bool:
					_initialInt = Shader.GetGlobalInt(_propertyID);
					break;
			}

			_initialValueCaptured = true;
		}

		protected virtual void StopSequence()
		{
			_coroutine = null;
			IsPlaying = false;
		}

		protected override void CustomStopFeedback(Vector3 position, float feedbacksIntensity = 1.0f)
		{
			if (!Active || !FeedbackTypeAuthorized || (_coroutine == null))
			{
				return;
			}

			IsPlaying = false;
			Owner.StopCoroutine(_coroutine);
			_coroutine = null;
		}

		protected override void CustomRestoreInitialValues()
		{
			if (!Active || !FeedbackTypeAuthorized || !_initialValueCaptured)
			{
				return;
			}

			switch (ParameterType)
			{
				case ShaderGlobalParameterTypes.Float:
				case ShaderGlobalParameterTypes.FloatSlider:
					Shader.SetGlobalFloat(_propertyID, _initialFloat);
					break;
				case ShaderGlobalParameterTypes.Color:
					Shader.SetGlobalColor(_propertyID, _initialColor);
					break;
				case ShaderGlobalParameterTypes.Vector:
					Shader.SetGlobalVector(_propertyID, _initialVector);
					break;
				case ShaderGlobalParameterTypes.Int:
				case ShaderGlobalParameterTypes.Bool:
					Shader.SetGlobalInt(_propertyID, _initialInt);
					break;
			}
		}

		public override void OnDisable()
		{
			_coroutine = null;
		}

		public override void OnValidate()
		{
			base.OnValidate();

			Delay = Mathf.Max(0f, Delay);
			Duration = Mathf.Max(0f, Duration);

			if (SliderMax < SliderMin)
			{
				SliderMax = SliderMin;
			}

			if (FloatCurve == null)
			{
				FloatCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
			}

			if (ColorGradient == null)
			{
				ColorGradient = CreateDefaultGradient();
			}
		}

		protected static Gradient CreateDefaultGradient()
		{
			Gradient gradient = new Gradient();
			gradient.SetKeys(
				new[]
				{
					new GradientColorKey(Color.white, 0f),
					new GradientColorKey(Color.white, 1f)
				},
				new[]
				{
					new GradientAlphaKey(1f, 0f),
					new GradientAlphaKey(1f, 1f)
				});
			return gradient;
		}
	}
}
