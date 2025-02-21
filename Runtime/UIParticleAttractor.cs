using System;
using System.Buffers;
using System.Collections.Generic;
using Coffee.UIParticleInternal;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Pool;

namespace Coffee.UIExtensions
{
    [ExecuteAlways]
    public class UIParticleAttractor : MonoBehaviour, ISerializationCallbackReceiver
    {
        public enum Movement { Linear, Smooth, Sphere, VelocityCurve }
        public enum UpdateMode { Normal, UnscaledTime }

        [Serializable]
        public class AttractorSettings
        {
            [Range(0.1f, 10f)] public float destinationRadius = 1f;
            [Range(0f, 0.95f)] public float delayRate = .4f;
            [Range(0.001f, 100f)] public float maxSpeed = 8f;
            public float acceleration = 1f;
            public AnimationCurve accelerationCurve = AnimationCurve.Linear(0, 0, 1, 1);
            public Movement movement;
            public UpdateMode updateMode;
        }

        [SerializeField] private AttractorSettings settings = new AttractorSettings();
        [SerializeField] private List<ParticleSystem> particleSystems = new List<ParticleSystem>();
        [SerializeField] private UnityEvent onAttracted;
        
        private List<UIParticle> uiParticles = new List<UIParticle>();
        private Transform cachedTransform;
        private Vector3 cachedPosition;
        private float cachedDeltaTime;
        private float cachedUnscaledDeltaTime;

        private void Awake()
        {
            cachedTransform = transform;
        }

        private void OnEnable() => UIParticleUpdater.Register(this);
        private void OnDisable() => UIParticleUpdater.Unregister(this);

        public void AddParticleSystem(ParticleSystem ps)
        {
            if (!particleSystems.Contains(ps))
            {
                particleSystems.Add(ps);
                uiParticles.Clear();
            }
        }

        public void RemoveParticleSystem(ParticleSystem ps)
        {
            int index = particleSystems.IndexOf(ps);
            if (index >= 0)
            {
                particleSystems.RemoveAt(index);
                uiParticles.Clear();
            }
        }

        internal void Attract()
        {
            CollectUIParticlesIfNeeded();
            UpdateCachedValues();

            for (int i = 0; i < particleSystems.Count; i++)
            {
                var ps = particleSystems[i];
                if (!IsValidParticleSystem(ps)) continue;

                ProcessParticleSystem(ps, uiParticles[i]);
            }
        }

        private void UpdateCachedValues()
        {
            cachedPosition = cachedTransform.position;
            cachedDeltaTime = Time.deltaTime;
            cachedUnscaledDeltaTime = Time.unscaledDeltaTime;
        }

        private bool IsValidParticleSystem(ParticleSystem ps)
        {
            return ps != null && ps.gameObject.activeInHierarchy && ps.particleCount > 0;
        }

        private void ProcessParticleSystem(ParticleSystem ps, UIParticle uiParticle)
        {
            int particleCount = ps.particleCount;
            var particles = ArrayPool<ParticleSystem.Particle>.Shared.Rent(particleCount);

            try
            {
                ps.GetParticles(particles, particleCount);
                var destination = GetDestinationPosition(uiParticle, ps);

                for (int i = 0; i < particleCount; i++)
                {
                    UpdateParticle(ref particles[i], destination);
                }

                ps.SetParticles(particles, particleCount);
            }
            finally
            {
                ArrayPool<ParticleSystem.Particle>.Shared.Return(particles);
            }
        }

        private Vector3 GetDestinationPosition(UIParticle uiParticle, ParticleSystem particleSystem)
        {
            var isUI = uiParticle && uiParticle.enabled;
            var psPos = particleSystem.transform.position;
            var dstPos = cachedPosition;
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

        private void UpdateParticle(ref ParticleSystem.Particle particle, Vector3 destination)
        {
            if (IsParticleAtDestination(particle, destination))
            {
                HandleParticleArrival(ref particle);
                return;
            }

            var delayTime = particle.startLifetime * settings.delayRate;
            var duration = particle.startLifetime - delayTime;
            var time = Mathf.Max(0, particle.startLifetime - particle.remainingLifetime - delayTime);

            if (time <= 0) return;

            if (settings.movement == Movement.VelocityCurve)
                UpdateParticleVelocity(ref particle, destination, time, duration);
            else
                UpdateParticlePosition(ref particle, destination, time, duration);
        }

        private bool IsParticleAtDestination(ParticleSystem.Particle particle, Vector3 destination)
        {
            return particle.remainingLifetime > 0 && 
                   Vector3.Distance(particle.position, destination) < settings.destinationRadius;
        }

        private void HandleParticleArrival(ref ParticleSystem.Particle particle)
        {
            particle.remainingLifetime = 0f;
            onAttracted?.Invoke();
        }

        private void UpdateParticleVelocity(ref ParticleSystem.Particle particle, Vector3 destination, float time, float duration)
        {
            float normalizedTime = time / duration;
            float acceleration = settings.accelerationCurve.Evaluate(normalizedTime) * settings.acceleration;
            
            Vector3 direction = (destination - particle.position).normalized;
            particle.velocity += direction * acceleration;
            particle.velocity = Vector3.ClampMagnitude(particle.velocity, settings.maxSpeed);
        }

        private void UpdateParticlePosition(ref ParticleSystem.Particle particle, Vector3 destination, float time, float duration)
        {
            particle.position = GetAttractedPosition(particle.position, destination, duration, time);
            particle.velocity *= 0.5f;
        }

        private Vector3 GetAttractedPosition(Vector3 current, Vector3 target, float duration, float time)
        {
            float normalizedTime = time / duration;
            float currentSpeed = settings.maxSpeed;

            switch (settings.updateMode)
            {
                case UpdateMode.Normal:
                    currentSpeed *= 60 * cachedDeltaTime;
                    break;
                case UpdateMode.UnscaledTime:
                    currentSpeed *= 60 * cachedUnscaledDeltaTime;
                    break;
            }

            switch (settings.movement)
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
            if (particleSystems.Count == 0 || uiParticles.Count != 0) return;

            foreach (var ps in particleSystems)
            {
                if (ps == null)
                {
                    uiParticles.Add(null);
                    continue;
                }

                var uiParticle = ps.GetComponentInParent<UIParticle>(true);
                uiParticles.Add(uiParticle?.particles.Contains(ps) == true ? uiParticle : null);
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }
        void ISerializationCallbackReceiver.OnAfterDeserialize() { }
    }
}
