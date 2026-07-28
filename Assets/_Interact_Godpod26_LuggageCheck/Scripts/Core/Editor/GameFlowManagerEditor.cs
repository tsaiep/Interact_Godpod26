using RFIDBaggage.Core;
using UnityEditor;

namespace RFIDBaggage.EditorTools
{
    [CustomEditor(typeof(GameFlowManager))]
    public sealed class GameFlowManagerEditor : Editor
    {
        private const string VisibleStateSequenceProperty = "visibleStateSequence";
        private const string CurrentStateProperty = "currentState";
        private const string GameplayElapsedSecondsProperty = "gameplayElapsedSeconds";
        private const string LastGameplayDurationSecondsProperty = "lastGameplayDurationSeconds";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                bool isScriptReference = property.propertyPath == "m_Script";
                bool isRuntimeDebugField =
                    property.propertyPath == VisibleStateSequenceProperty ||
                    property.propertyPath == CurrentStateProperty ||
                    property.propertyPath == GameplayElapsedSecondsProperty ||
                    property.propertyPath == LastGameplayDurationSecondsProperty;

                using (new EditorGUI.DisabledScope(isScriptReference || isRuntimeDebugField))
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
