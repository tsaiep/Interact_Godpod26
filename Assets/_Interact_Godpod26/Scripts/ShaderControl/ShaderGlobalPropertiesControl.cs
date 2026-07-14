using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ShaderGlobalPropertiesControl : MonoBehaviour
{
    public enum ShaderGlobalParameterType
    {
        Float,
        FloatSlider,
        Color,
        Vector,
        Int,
        Bool
    }

    [Header("Target")]
    public ShaderGlobalParameterType parameterType = ShaderGlobalParameterType.Float;
    public string parameterName;

    [Header("Timing")]
    [Min(0f)] public float delay = 0f;
    [Min(0f)] public float duration = 1f;

    [Header("Float")]
    public AnimationCurve floatCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public float sliderMin = 0f;
    public float sliderMax = 1f;

    [Header("Color")]
    [GradientUsage(true)] public Gradient colorGradient = CreateDefaultGradient();

    [Header("Vector")]
    public Vector4 vectorFrom = Vector4.zero;
    public Vector4 vectorTo = Vector4.one;

    [Header("Int")]
    public int intFrom = 0;
    public int intTo = 1;

    [Header("Bool")]
    public bool boolValue;

    private bool _isControlling;
    private int _parameterId;
    private float _startTime;
    private float _currentDuration;

    public void Play()
    {
        StartControl();
    }

    public void StartControl()
    {
        if (!CanApplyParameter())
        {
            _isControlling = false;
            return;
        }

        _parameterId = Shader.PropertyToID(parameterName);
        float currentDelay = Mathf.Max(0f, delay);
        _startTime = Time.time + currentDelay;
        _currentDuration = Mathf.Max(0f, duration);

        if (currentDelay <= 0f && (_currentDuration <= 0f || parameterType == ShaderGlobalParameterType.Bool))
        {
            ApplyFinalValue();
            _isControlling = false;
            return;
        }

        _isControlling = true;

        if (currentDelay <= 0f && parameterType != ShaderGlobalParameterType.Bool)
        {
            ApplySampledValue(0f);
        }
    }

    private void Update()
    {
        if (!_isControlling)
        {
            return;
        }

        float elapsedTime = Time.time - _startTime;

        if (elapsedTime < 0f)
        {
            return;
        }

        if (parameterType == ShaderGlobalParameterType.Bool || elapsedTime >= _currentDuration)
        {
            ApplyFinalValue();
            _isControlling = false;
            return;
        }

        float normalizedTime = Mathf.Clamp01(elapsedTime / _currentDuration);
        ApplySampledValue(normalizedTime);
    }

    private bool CanApplyParameter()
    {
        if (!string.IsNullOrEmpty(parameterName))
        {
            return true;
        }

        Debug.LogWarning($"{nameof(ShaderGlobalPropertiesControl)} on {name}: Parameter Name is empty.", this);
        return false;
    }

    private void ApplyFinalValue()
    {
        if (parameterType == ShaderGlobalParameterType.Bool)
        {
            Shader.SetGlobalInt(_parameterId, boolValue ? 1 : 0);
            return;
        }

        ApplySampledValue(1f);
    }

    private void ApplySampledValue(float normalizedTime)
    {
        float curveValue = EvaluateFloatCurve(normalizedTime);

        switch (parameterType)
        {
            case ShaderGlobalParameterType.Float:
                Shader.SetGlobalFloat(_parameterId, curveValue);
                break;
            case ShaderGlobalParameterType.FloatSlider:
                Shader.SetGlobalFloat(_parameterId, Mathf.LerpUnclamped(sliderMin, sliderMax, curveValue));
                break;
            case ShaderGlobalParameterType.Color:
                Shader.SetGlobalColor(_parameterId, EvaluateColorGradient(normalizedTime));
                break;
            case ShaderGlobalParameterType.Vector:
                Shader.SetGlobalVector(_parameterId, Vector4.LerpUnclamped(vectorFrom, vectorTo, curveValue));
                break;
            case ShaderGlobalParameterType.Int:
                Shader.SetGlobalInt(_parameterId, Mathf.RoundToInt(Mathf.LerpUnclamped(intFrom, intTo, curveValue)));
                break;
        }
    }

    private float EvaluateFloatCurve(float normalizedTime)
    {
        return floatCurve != null ? floatCurve.Evaluate(normalizedTime) : normalizedTime;
    }

    private Color EvaluateColorGradient(float normalizedTime)
    {
        return colorGradient != null ? colorGradient.Evaluate(normalizedTime) : Color.white;
    }

    private void OnValidate()
    {
        delay = Mathf.Max(0f, delay);
        duration = Mathf.Max(0f, duration);

        if (sliderMax < sliderMin)
        {
            sliderMax = sliderMin;
        }

        if (floatCurve == null)
        {
            floatCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        if (colorGradient == null)
        {
            colorGradient = CreateDefaultGradient();
        }
    }

    private static Gradient CreateDefaultGradient()
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

#if UNITY_EDITOR
[CustomEditor(typeof(ShaderGlobalPropertiesControl))]
public class ShaderGlobalPropertiesControlEditor : Editor
{
    private SerializedProperty _parameterType;
    private SerializedProperty _parameterName;
    private SerializedProperty _delay;
    private SerializedProperty _duration;
    private SerializedProperty _floatCurve;
    private SerializedProperty _sliderMin;
    private SerializedProperty _sliderMax;
    private SerializedProperty _colorGradient;
    private SerializedProperty _vectorFrom;
    private SerializedProperty _vectorTo;
    private SerializedProperty _intFrom;
    private SerializedProperty _intTo;
    private SerializedProperty _boolValue;

    private void OnEnable()
    {
        _parameterType = serializedObject.FindProperty(nameof(ShaderGlobalPropertiesControl.parameterType));
        _parameterName = serializedObject.FindProperty(nameof(ShaderGlobalPropertiesControl.parameterName));
        _delay = serializedObject.FindProperty(nameof(ShaderGlobalPropertiesControl.delay));
        _duration = serializedObject.FindProperty(nameof(ShaderGlobalPropertiesControl.duration));
        _floatCurve = serializedObject.FindProperty(nameof(ShaderGlobalPropertiesControl.floatCurve));
        _sliderMin = serializedObject.FindProperty(nameof(ShaderGlobalPropertiesControl.sliderMin));
        _sliderMax = serializedObject.FindProperty(nameof(ShaderGlobalPropertiesControl.sliderMax));
        _colorGradient = serializedObject.FindProperty(nameof(ShaderGlobalPropertiesControl.colorGradient));
        _vectorFrom = serializedObject.FindProperty(nameof(ShaderGlobalPropertiesControl.vectorFrom));
        _vectorTo = serializedObject.FindProperty(nameof(ShaderGlobalPropertiesControl.vectorTo));
        _intFrom = serializedObject.FindProperty(nameof(ShaderGlobalPropertiesControl.intFrom));
        _intTo = serializedObject.FindProperty(nameof(ShaderGlobalPropertiesControl.intTo));
        _boolValue = serializedObject.FindProperty(nameof(ShaderGlobalPropertiesControl.boolValue));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_parameterType, new GUIContent("Parameter Type"));
        EditorGUILayout.PropertyField(_parameterName, new GUIContent("Parameter Name"));
        EditorGUILayout.PropertyField(_delay, new GUIContent("Delay"));
        EditorGUILayout.PropertyField(_duration, new GUIContent("Duration"));

        EditorGUILayout.Space();

        ShaderGlobalPropertiesControl.ShaderGlobalParameterType parameterType =
            (ShaderGlobalPropertiesControl.ShaderGlobalParameterType)_parameterType.enumValueIndex;

        switch (parameterType)
        {
            case ShaderGlobalPropertiesControl.ShaderGlobalParameterType.Float:
                EditorGUILayout.PropertyField(_floatCurve, new GUIContent("Curve"));
                break;
            case ShaderGlobalPropertiesControl.ShaderGlobalParameterType.FloatSlider:
                EditorGUILayout.PropertyField(_floatCurve, new GUIContent("Curve"));
                EditorGUILayout.PropertyField(_sliderMin, new GUIContent("Slider Min"));
                EditorGUILayout.PropertyField(_sliderMax, new GUIContent("Slider Max"));
                break;
            case ShaderGlobalPropertiesControl.ShaderGlobalParameterType.Color:
                EditorGUILayout.PropertyField(_colorGradient, new GUIContent("Gradient"));
                break;
            case ShaderGlobalPropertiesControl.ShaderGlobalParameterType.Vector:
                EditorGUILayout.PropertyField(_floatCurve, new GUIContent("Curve"));
                EditorGUILayout.PropertyField(_vectorFrom, new GUIContent("From"));
                EditorGUILayout.PropertyField(_vectorTo, new GUIContent("To"));
                break;
            case ShaderGlobalPropertiesControl.ShaderGlobalParameterType.Int:
                EditorGUILayout.PropertyField(_floatCurve, new GUIContent("Curve"));
                EditorGUILayout.PropertyField(_intFrom, new GUIContent("From"));
                EditorGUILayout.PropertyField(_intTo, new GUIContent("To"));
                break;
            case ShaderGlobalPropertiesControl.ShaderGlobalParameterType.Bool:
                EditorGUILayout.PropertyField(_boolValue, new GUIContent("Toggle"));
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
