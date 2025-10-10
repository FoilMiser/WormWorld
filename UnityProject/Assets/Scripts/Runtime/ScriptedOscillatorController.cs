using System;
using UnityEngine;
using WormWorld.Core;

namespace WormWorld.Runtime
{
    /// <summary>
    /// Deterministic controller that drives muscles with sinusoidal activations.
    /// </summary>
    [RequireComponent(typeof(CreatureRuntime))]
    public sealed class ScriptedOscillatorController : MonoBehaviour
    {
        [Tooltip("Oscillation frequency in Hertz.")]
        [Min(0f)]
        public float frequency = 1f;

        [Tooltip("Amplitude for the sinusoidal activation (clamped to [-1,1]).")]
        [Range(0f, 1f)]
        public float amplitude = 0.6f;

        private CreatureRuntime _runtime;
        private RngService _rng;
        private float[] _buffer = Array.Empty<float>();
        private float[] _phases = Array.Empty<float>();
        private float _time;

        private void Awake()
        {
            _runtime = GetComponent<CreatureRuntime>();
        }

        private void Start()
        {
            if (_runtime == null || _runtime.ActuatorCount == 0)
            {
                enabled = false;
                return;
            }

            var seed = unchecked((int)(_runtime.ActiveGenome?.Seed ?? 0UL));
            _rng = new RngService(seed);
            _buffer = new float[_runtime.ActuatorCount];
            _phases = new float[_runtime.ActuatorCount];

            for (var i = 0; i < _phases.Length; i++)
            {
                _phases[i] = _rng.NextFloat01() * Mathf.PI * 2f;
            }
        }

        private void FixedUpdate()
        {
            if (_runtime == null || _runtime.ActuatorCount == 0)
            {
                return;
            }

            _time += Time.fixedDeltaTime;
            var omega = Mathf.PI * 2f * frequency;
            for (var i = 0; i < _buffer.Length; i++)
            {
                var activation = amplitude * Mathf.Sin(omega * _time + _phases[i]);
                _buffer[i] = Mathf.Clamp(activation, -1f, 1f);
            }

            _runtime.Actuate(_buffer, Time.fixedDeltaTime);
        }
    }
}
