using System.Collections.Generic;
using UnityEngine;
using VRM;

namespace Mate.Runtime.Face
{
    [DisallowMultipleComponent]
    public sealed class MateBlinkController : MonoBehaviour
    {
        [SerializeField] private bool enableBlink = true;
        [SerializeField] private Vector2 blinkInterval = new Vector2(2.5f, 6f);
        [SerializeField] private Vector2 closeDuration = new Vector2(0.06f, 0.1f);
        [SerializeField] private Vector2 openDuration = new Vector2(0.1f, 0.16f);
        [Range(0f, 1f)]
        [SerializeField] private float doubleBlinkChance = 0.12f;
        [SerializeField] private float doubleBlinkGap = 0.08f;

        private readonly List<BlendShapeTarget> _targets = new List<BlendShapeTarget>(4);
        private readonly BlendShapeKey _blinkKey = BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink);
        private VRMBlendShapeProxy _blendShapeProxy;
        private BlinkPhase _phase = BlinkPhase.Waiting;
        private float _phaseStartedAt;
        private float _phaseDuration;
        private float _nextBlinkAt;
        private bool _doubleBlinkPending;
        private bool _wasMissingTargetsLogged;

        private void Awake()
        {
            CacheBlendShapes();
            ScheduleNextBlink();
        }

        private void OnDisable()
        {
            SetBlinkWeight(0f);
        }

        private void Update()
        {
            if (!enableBlink)
            {
                SetBlinkWeight(0f);
                return;
            }

            if (!HasBlinkTarget())
            {
                if (!_wasMissingTargetsLogged && enableBlink)
                {
                    Debug.LogWarning("MateBlinkController could not find a blink blend shape on this VRM.");
                    _wasMissingTargetsLogged = true;
                }

                return;
            }

            if (_phase == BlinkPhase.Waiting)
            {
                SetBlinkWeight(0f);
                if (Time.time >= _nextBlinkAt)
                {
                    StartBlink();
                }

                return;
            }

            var t = Mathf.Clamp01((Time.time - _phaseStartedAt) / Mathf.Max(_phaseDuration, 0.001f));
            if (_phase == BlinkPhase.Closing)
            {
                SetBlinkWeight(Mathf.SmoothStep(0f, 100f, t));
                if (t >= 1f)
                {
                    BeginPhase(BlinkPhase.Closed, Random.Range(0.02f, 0.05f));
                }
            }
            else if (_phase == BlinkPhase.Closed)
            {
                SetBlinkWeight(100f);
                if (t >= 1f)
                {
                    BeginPhase(BlinkPhase.Opening, Random.Range(openDuration.x, openDuration.y));
                }
            }
            else if (_phase == BlinkPhase.Opening)
            {
                SetBlinkWeight(Mathf.SmoothStep(100f, 0f, t));
                if (t >= 1f)
                {
                    if (_doubleBlinkPending)
                    {
                        _doubleBlinkPending = false;
                        _nextBlinkAt = Time.time + doubleBlinkGap;
                        _phase = BlinkPhase.Waiting;
                    }
                    else
                    {
                        ScheduleNextBlink();
                    }
                }
            }
        }

        public void BlinkNow()
        {
            if (HasBlinkTarget())
            {
                StartBlink();
            }
        }

        private void CacheBlendShapes()
        {
            _targets.Clear();
            _blendShapeProxy = GetComponent<VRMBlendShapeProxy>();
            if (_blendShapeProxy == null)
            {
                _blendShapeProxy = GetComponentInChildren<VRMBlendShapeProxy>(true);
            }

            var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                var mesh = renderer.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                for (var i = 0; i < mesh.blendShapeCount; i++)
                {
                    var name = mesh.GetBlendShapeName(i);
                    if (IsBlinkName(name))
                    {
                        _targets.Add(new BlendShapeTarget(renderer, i));
                    }
                }
            }
        }

        private bool IsBlinkName(string blendShapeName)
        {
            if (string.IsNullOrEmpty(blendShapeName))
            {
                return false;
            }

            var lower = blendShapeName.ToLowerInvariant();
            return lower.Contains("blink") || blendShapeName.Contains("まばたき") || blendShapeName.Contains("瞬き");
        }

        private bool HasBlinkTarget()
        {
            return _blendShapeProxy != null || _targets.Count > 0;
        }

        private void StartBlink()
        {
            _doubleBlinkPending = Random.value < doubleBlinkChance;
            BeginPhase(BlinkPhase.Closing, Random.Range(closeDuration.x, closeDuration.y));
        }

        private void BeginPhase(BlinkPhase phase, float duration)
        {
            _phase = phase;
            _phaseStartedAt = Time.time;
            _phaseDuration = duration;
        }

        private void ScheduleNextBlink()
        {
            SetBlinkWeight(0f);
            _phase = BlinkPhase.Waiting;
            _nextBlinkAt = Time.time + Random.Range(blinkInterval.x, blinkInterval.y);
        }

        private void SetBlinkWeight(float weight)
        {
            if (_blendShapeProxy != null)
            {
                _blendShapeProxy.ImmediatelySetValue(_blinkKey, weight / 100f);
                return;
            }

            for (var i = 0; i < _targets.Count; i++)
            {
                _targets[i].Renderer.SetBlendShapeWeight(_targets[i].Index, weight);
            }
        }

        private readonly struct BlendShapeTarget
        {
            public readonly SkinnedMeshRenderer Renderer;
            public readonly int Index;

            public BlendShapeTarget(SkinnedMeshRenderer renderer, int index)
            {
                Renderer = renderer;
                Index = index;
            }
        }

        private enum BlinkPhase
        {
            Waiting,
            Closing,
            Closed,
            Opening
        }
    }
}
