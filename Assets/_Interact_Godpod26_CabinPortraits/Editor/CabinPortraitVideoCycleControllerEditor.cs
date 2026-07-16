using CabinPortraits.Video;
using UnityEditor;

namespace CabinPortraits.Editor
{
    [CustomEditor(typeof(CabinPortraitVideoCycleController))]
    [CanEditMultipleObjects]
    public sealed class CabinPortraitVideoCycleControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                using (new EditorGUI.DisabledScope(IsReadOnlyProperty(property)))
                {
                    EditorGUILayout.PropertyField(property, true);
                }

                enterChildren = false;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static bool IsReadOnlyProperty(SerializedProperty property)
        {
            return property.propertyPath == "m_Script" ||
                   property.propertyPath == "visibleStateSequence" ||
                   property.propertyPath == "currentState";
        }
    }
}
