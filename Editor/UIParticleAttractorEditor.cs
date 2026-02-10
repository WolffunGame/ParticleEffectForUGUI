using UnityEngine;
using UnityEditor;

namespace Coffee.UIExtensions
{
    [CustomEditor(typeof(UIParticleAttractor))]
    public class UIParticleAttractorEditor : Editor
    {
        SerializedProperty m_Settings;
        SerializedProperty m_ParticleSystems;
        SerializedProperty m_OnAttracted;

        void OnEnable()
        {
            m_Settings = serializedObject.FindProperty("settings");
            m_ParticleSystems = serializedObject.FindProperty("particleSystems");
            m_OnAttracted = serializedObject.FindProperty("onAttracted");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_ParticleSystems);

            if (m_Settings != null)
            {
                var destinationRadius = m_Settings.FindPropertyRelative("destinationRadius");
                var delayRate = m_Settings.FindPropertyRelative("delayRate");
                var maxSpeed = m_Settings.FindPropertyRelative("maxSpeed");
                var movement = m_Settings.FindPropertyRelative("movement");
                var acceleration = m_Settings.FindPropertyRelative("acceleration");
                var accelerationCurve = m_Settings.FindPropertyRelative("accelerationCurve");
                var updateMode = m_Settings.FindPropertyRelative("updateMode");

                EditorGUILayout.PropertyField(destinationRadius);
                EditorGUILayout.PropertyField(delayRate);
                EditorGUILayout.PropertyField(maxSpeed);
                EditorGUILayout.PropertyField(movement);

                if (movement != null && movement.enumValueIndex == (int)UIParticleAttractor.Movement.VelocityCurve)
                {
                    EditorGUILayout.PropertyField(acceleration);
                    EditorGUILayout.PropertyField(accelerationCurve);
                }

                EditorGUILayout.PropertyField(updateMode);
            }

            EditorGUILayout.PropertyField(m_OnAttracted);

            serializedObject.ApplyModifiedProperties();
        }
    }
}