using System;
using System.Collections.Generic;
using Coffee.UIParticleInternal;
using UnityEngine;
using UnityEngine.Events;

namespace Coffee.UIExtensions
{
    [ExecuteAlways]
    public class UIParticleAttractor : MonoBehaviour, ISerializationCallbackReceiver
    {
        public enum Movement
        {
            Linear,
            Smooth,
            Sphere,
            VelocityCurve
        }

        public enum UpdateMode
        {
            Normal,
            UnscaledTime
        }

        [SerializeField]
        [HideInInspector]
        private ParticleSystem m_ParticleSystem;

        [SerializeField]
        private List<ParticleSystem> m_ParticleSystems = new List<ParticleSystem>();

        [Range(0.1f, 10f)]
        [SerializeField]
        private float m_DestinationRadius = 1;

        [Range(0f, 0.95f)]
        [SerializeField]
        private float m_DelayRate;

        [Range(0.001f, 100f)]
        [SerializeField]
        private float m_MaxSpeed = 1;

        [SerializeField]
        private float m_Acceleration = 1f;

        [SerializeField]
        private AnimationCurve m_AccelerationCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [SerializeField]
        private Movement m_Movement;

        [SerializeField]
        private UpdateMode m_UpdateMode;

        [SerializeField]
        private UnityEvent m_OnAttracted;

        private List<UIParticle> _uiParticles = new List<UIParticle>();

        public float destinationRadius
        {
            get => m_DestinationRadius;
            set => m_DestinationRadius = Mathf.Clamp(value, 0.1f, 10f);
        }

        public float delay
        {
            get => m_DelayRate;
            set => m_DelayRate = value;
        }

        public float maxSpeed
        {
            get => m_MaxSpeed;
            set => m_MaxSpeed = value;
        }

        public float acceleration
        {
            get => m_Acceleration;
            set => m_Acceleration = value;
        }

        public AnimationCurve accelerationCurve
        {
            get => m_AccelerationCurve;
            set => m_AccelerationCurve = value;
        }

        public Movement movement
        {
            get => m_Movement;
            set => m_Movement = value;
        }

        public UpdateMode updateMode
        {
            get => m_UpdateMode;
            set => m_UpdateMode = value;
        }

        public UnityEvent onAttracted
        {
            get => m_OnAttracted;
            set => m_OnAttracted = value;
        }

        public IReadOnlyList<ParticleSystem> particleSystems => m_ParticleSystems;

        public void AddParticleSystem(ParticleSystem ps)
        {
            if (m_ParticleSystems == null)
            {
                m_ParticleSystems = new List<ParticleSystem>();
            }

            var i = m_ParticleSystems.IndexOf(ps);
            if (0 <= i) return;

            m_ParticleSystems.Add(ps);
            _uiParticles.Clear();
        }

        public void RemoveParticleSystem(ParticleSystem ps)
        {
            if (m_ParticleSystems == null)
            {
                return;
            }

            var i = m_ParticleSystems.IndexOf(ps);
            if (i < 0) return;

            m_ParticleSystems.RemoveAt(i);
            _uiParticles.Clear();
        }

        private void Awake()
        {
            UpgradeIfNeeded();
        }

        private void OnEnable()
        {
            UIParticleUpdater.Register(this);
        }

        private void OnDisable()
        {
            UIParticleUpdater.Unregister(this);
        }

        private void OnDestroy()
        {
            _uiParticles = null;
            m_ParticleSystems = null;
        }

        internal void Attract()
        {
            CollectUIParticlesIfNeeded();

            for (var particleIndex = 0; particleIndex < m_ParticleSystems.Count; particleIndex++)
            {
                var particleSystem = m_ParticleSystems[particleIndex];

                if (particleSystem == null || !particleSystem.gameObject.activeInHierarchy) continue;

                var count = particleSystem.particleCount;
                if (count == 0) continue;

                var particles = ParticleSystemExtensions.GetParticleArray(count);
                particleSystem.GetParticles(particles, count);

                var uiParticle = _uiParticles[particleIndex];
                var dstPos = GetDestinationPosition(uiParticle, particleSystem);
                
                for (var i = 0; i < count; i++)
                {
                    var p = particles[i];
                    if (0f < p.remainingLifetime && Vector3.Distance(p.position, dstPos) < m_DestinationRadius)
                    {
                        p.remainingLifetime = 0f;
                        particles[i] = p;

                        if (m_OnAttracted != null)
                        {
                            try
                            {
                                m_OnAttracted.Invoke();
                            }
                            catch (Exception e)
                            {
                                Debug.LogException(e);
                            }
                        }
                        continue;
                    }

                    var delayTime = p.startLifetime * m_DelayRate;
                    var duration = p.startLifetime - delayTime;
                    var time = Mathf.Max(0, p.startLifetime - p.remainingLifetime - delayTime);

                    if (time <= 0) continue;

                    if (m_Movement == Movement.VelocityCurve)
                    {
                        float normalizedTime = time / duration;
                        float acceleration = m_AccelerationCurve.Evaluate(normalizedTime) * m_Acceleration;
                        
                        Vector3 direction = (dstPos - p.position).normalized;
                        p.velocity += direction * acceleration;
                        p.velocity = Vector3.ClampMagnitude(p.velocity, m_MaxSpeed);
                    }
                    else 
                    {
                        p.position = GetAttractedPosition(p.position, dstPos, duration, time);
                        p.velocity *= 0.5f;
                    }
                    
                    particles[i] = p;
                }

                particleSystem.SetParticles(particles, count);
            }
        }

        private Vector3 GetDestinationPosition(UIParticle uiParticle, ParticleSystem particleSystem)
        {
            var isUI = uiParticle && uiParticle.enabled;
            var psPos = particleSystem.transform.position;
            var attractorPos = transform.position;
            var dstPos = attractorPos;
            var isLocalSpace = particleSystem.IsLocalSpace();

            if (isLocalSpace)
            {
                dstPos = particleSystem.transform.InverseTransformPoint(dstPos);
            }

            if (isUI)
            {
                var inverseScale = uiParticle.parentScale.Inverse();
                var scale3d = uiParticle.scale3DForCalc;
                dstPos = dstPos.GetScaled(inverseScale, scale3d.Inverse());

                if (uiParticle.positionMode == UIParticle.PositionMode.Relative)
                {
                    var diff = uiParticle.transform.position - psPos;
                    diff.Scale(scale3d - inverseScale);
                    diff.Scale(scale3d.Inverse());
                    dstPos += diff;
                }

#if UNITY_EDITOR
                if (!Application.isPlaying && !isLocalSpace)
                {
                    dstPos += psPos - psPos.GetScaled(inverseScale, scale3d.Inverse());
                }
#endif
            }

            return dstPos;
        }

        private Vector3 GetAttractedPosition(Vector3 current, Vector3 target, float duration, float time)
        {
            float normalizedTime = time / duration;
            float currentSpeed = m_MaxSpeed;

            switch (m_UpdateMode)
            {
                case UpdateMode.Normal:
                    currentSpeed *= 60 * Time.deltaTime;
                    break;
                case UpdateMode.UnscaledTime:
                    currentSpeed *= 60 * Time.unscaledDeltaTime;
                    break;
            }

            switch (m_Movement)
            {
                case Movement.Linear:
                    currentSpeed /= duration;
                    break;
                case Movement.Smooth:
                    target = Vector3.Lerp(current, target, normalizedTime);
                    break;
                case Movement.Sphere:
                    target = Vector3.Slerp(current, target, normalizedTime);
                    break;
            }

            return Vector3.MoveTowards(current, target, currentSpeed);
        }

        private void CollectUIParticlesIfNeeded()
        {
            if (m_ParticleSystems.Count == 0 || _uiParticles.Count != 0) return;

            if (_uiParticles.Capacity < m_ParticleSystems.Capacity)
            {
                _uiParticles.Capacity = m_ParticleSystems.Capacity;
            }

            for (var i = 0; i < m_ParticleSystems.Count; i++)
            {
                var ps = m_ParticleSystems[i];
                if (ps == null)
                {
                    _uiParticles.Add(null);
                    continue;
                }

                var uiParticle = ps.GetComponentInParent<UIParticle>(true);
                _uiParticles.Add(uiParticle.particles.Contains(ps) ? uiParticle : null);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _uiParticles.Clear();
        }
#endif

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            UpgradeIfNeeded();
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
        }

        private void UpgradeIfNeeded()
        {
            if (m_ParticleSystem != null)
            {
                if (!m_ParticleSystems.Contains(m_ParticleSystem))
                {
                    m_ParticleSystems.Add(m_ParticleSystem);
                }

                m_ParticleSystem = null;
                Debug.Log($"Upgraded!");
            }
        }
    }
}
