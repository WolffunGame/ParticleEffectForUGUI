using UnityEngine;
using UnityEditor;

namespace Coffee.UIExtensions
{
    [CustomEditor(typeof(UIParticleAttractor))]
    public class UIParticleAttractorEditor : Editor
    {
        SerializedProperty m_ParticleSystem;
        SerializedProperty m_ParticleSystems;
        SerializedProperty m_DestinationRadius;
        SerializedProperty m_DelayRate;
        SerializedProperty m_MaxSpeed;
        SerializedProperty m_Acceleration;
        SerializedProperty m_AccelerationCurve;
        SerializedProperty m_Movement;
        SerializedProperty m_UpdateMode;
        SerializedProperty m_OnAttracted;

        void OnEnable()
        {
            m_ParticleSystem = serializedObject.FindProperty("m_ParticleSystem");
            m_ParticleSystems = serializedObject.FindProperty("m_ParticleSystems");
            m_DestinationRadius = serializedObject.FindProperty("m_DestinationRadius");
            m_DelayRate = serializedObject.FindProperty("m_DelayRate");
            m_MaxSpeed = serializedObject.FindProperty("m_MaxSpeed");
            m_Acceleration = serializedObject.FindProperty("m_Acceleration");
            m_AccelerationCurve = serializedObject.FindProperty("m_AccelerationCurve");
            m_Movement = serializedObject.FindProperty("m_Movement");
            m_UpdateMode = serializedObject.FindProperty("m_UpdateMode");
            m_OnAttracted = serializedObject.FindProperty("m_OnAttracted");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_ParticleSystems);
            EditorGUILayout.PropertyField(m_DestinationRadius);
            EditorGUILayout.PropertyField(m_DelayRate);
            EditorGUILayout.PropertyField(m_MaxSpeed);
            EditorGUILayout.PropertyField(m_Movement);

            // Chỉ hiện acceleration và curve khi Movement là VelocityCurve
            if (m_Movement.enumValueIndex == (int)UIParticleAttractor.Movement.VelocityCurve)
            {
                EditorGUILayout.PropertyField(m_Acceleration);
                EditorGUILayout.PropertyField(m_AccelerationCurve);
            }

            EditorGUILayout.PropertyField(m_UpdateMode);
            EditorGUILayout.PropertyField(m_OnAttracted);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
