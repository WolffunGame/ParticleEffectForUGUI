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
        private const float TIME_MULTIPLIER = 60f;

        public enum Movement
        {
            Linear,
            Smooth,
            Sphere
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
        private ParticleSystem[] m_ParticleSystems = Array.Empty<ParticleSystem>();

        [Range(0.1f, 10f)]
        [SerializeField]
        private float m_DestinationRadius = 1;

        [Range(0f, 0.95f)]
        [SerializeField]
        private float m_DelayRate;

        [SerializeField]
        private AnimationCurve m_SpeedCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [SerializeField]
        private Movement m_Movement;

        [SerializeField]
        private UpdateMode m_UpdateMode;

        [SerializeField]
        private UnityEvent m_OnAttracted;

        // Cache components
        private Transform _transform;
        private readonly List<UIParticle> _uiParticles = new List<UIParticle>();
        private static readonly ParticleSystem.Particle[] ParticlePool = new ParticleSystem.Particle[1000];
        
        // Cache calculations
        private readonly Vector3 _tempVector = new Vector3();
        private Vector3 _cachedInverseScale;
        private Vector3 _cachedScale3D;

        public AnimationCurve speedCurve
        {
            get => m_SpeedCurve;
            set => m_SpeedCurve = value;
        }

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
            if (ps == null) return;
            
            var list = new List<ParticleSystem>(m_ParticleSystems);
            if (!list.Contains(ps))
            {
                list.Add(ps);
                m_ParticleSystems = list.ToArray();
                _uiParticles.Clear();
            }
        }

        public void RemoveParticleSystem(ParticleSystem ps)
        {
            if (ps == null) return;
            
            var list = new List<ParticleSystem>(m_ParticleSystems);
            if (list.Remove(ps))
            {
                m_ParticleSystems = list.ToArray();
                _uiParticles.Clear();
            }
        }

        private void Awake()
        {
            _transform = transform;
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
            _uiParticles.Clear();
            m_ParticleSystems = null;
        }

        internal void Attract()
        {
            CollectUIParticlesIfNeeded();

            float deltaTime = (m_UpdateMode == UpdateMode.Normal) ? Time.deltaTime : Time.unscaledDeltaTime;
            
            for (var particleIndex = 0; particleIndex < m_ParticleSystems.Length; particleIndex++)
            {
                var particleSystem = m_ParticleSystems[particleIndex];
                if (particleSystem == null || !particleSystem.gameObject.activeInHierarchy) continue;

                var count = particleSystem.particleCount;
                if (count == 0) continue;

                particleSystem.GetParticles(ParticlePool, count);

                var uiParticle = _uiParticles[particleIndex];
                var dstPos = GetDestinationPosition(uiParticle, particleSystem);
                
                for (var i = 0; i < count; i++)
                {
                    var p = ParticlePool[i];
                    if (0f < p.remainingLifetime && Vector3.Distance(p.position, dstPos) < m_DestinationRadius)
                    {
                        p.remainingLifetime = 0f;
                        ParticlePool[i] = p;

                        m_OnAttracted?.Invoke();
                        continue;
                    }

                    var delayTime = p.startLifetime * m_DelayRate;
                    var duration = p.startLifetime - delayTime;
                    var time = Mathf.Max(0, p.startLifetime - p.remainingLifetime - delayTime);

                    if (time <= 0) continue;

                    p.position = GetAttractedPosition(p.position, dstPos, duration, time, deltaTime, p.velocity);
                    ParticlePool[i] = p;
                }

                particleSystem.SetParticles(ParticlePool, count);
            }
        }

        private Vector3 GetDestinationPosition(UIParticle uiParticle, ParticleSystem particleSystem)
        {
            var isUI = uiParticle && uiParticle.enabled;
            var psPos = particleSystem.transform.position;
            var attractorPos = _transform.position;
            _tempVector.Set(attractorPos.x, attractorPos.y, attractorPos.z);
            var dstPos = _tempVector;
            var isLocalSpace = particleSystem.IsLocalSpace();

            if (isLocalSpace)
            {
                dstPos = particleSystem.transform.InverseTransformPoint(dstPos);
            }

            if (isUI)
            {
                _cachedInverseScale = uiParticle.parentScale.Inverse();
                _cachedScale3D = uiParticle.scale3DForCalc;
                
                dstPos = dstPos.GetScaled(_cachedInverseScale, _cachedScale3D.Inverse());

                if (uiParticle.positionMode == UIParticle.PositionMode.Relative)
                {
                    var diff = uiParticle.transform.position - psPos;
                    diff.Scale(_cachedScale3D - _cachedInverseScale);
                    diff.Scale(_cachedScale3D.Inverse());
                    dstPos += diff;
                }

#if UNITY_EDITOR
                if (!Application.isPlaying && !isLocalSpace)
                {
                    dstPos += psPos - psPos.GetScaled(_cachedInverseScale, _cachedScale3D.Inverse());
                }
#endif
            }

            return dstPos;
        }

        private Vector3 GetAttractedPosition(Vector3 current, Vector3 target, float duration, float time, float deltaTime, Vector3 velocity)
        {
            float normalizedTime = time / duration;
            float initialSpeed = velocity.magnitude;
            float currentSpeed = initialSpeed * m_SpeedCurve.Evaluate(normalizedTime);

            currentSpeed *= TIME_MULTIPLIER * deltaTime;

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
            if (m_ParticleSystems.Length == 0 || _uiParticles.Count != 0) return;

            _uiParticles.Capacity = Math.Max(_uiParticles.Capacity, m_ParticleSystems.Length);

            for (var i = 0; i < m_ParticleSystems.Length; i++)
            {
                var ps = m_ParticleSystems[i];
                if (ps == null)
                {
                    _uiParticles.Add(null);
                    continue;
                }

                var uiParticle = ps.GetComponentInParent<UIParticle>(true);
                _uiParticles.Add(uiParticle != null && uiParticle.particles.Contains(ps) ? uiParticle : null);
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
                var list = new List<ParticleSystem>(m_ParticleSystems);
                if (!list.Contains(m_ParticleSystem))
                {
                    list.Add(m_ParticleSystem);
                    m_ParticleSystems = list.ToArray();
                }

                m_ParticleSystem = null;
                Debug.Log($"Upgraded!");
            }
        }
    }
}
