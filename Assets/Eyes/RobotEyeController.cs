using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RobotAI.Animation
{
    public enum RobotEmotion
    {
        Neutral,
        Happy,
        Angry,
        Sad,
        Surprised,
        Sleepy,
        Suspicious
    }

    [Serializable]
    public struct EmotionConfig
    {
        public RobotEmotion emotionType;
        [Range(0.01f, 0.5f)] public float blinkDuration;
        [Range(0f, 1f)] public float eyeSquintAmount;
        public Vector2 blinkIntervalRange;
        public bool doubleBlinkChance;
    }

    public class RobotEyeController : MonoBehaviour
    {
        [Header("Bone References")]
        [SerializeField] private Transform upperEyelidBone;
        [SerializeField] private Transform lowerEyelidBone;

        [Header("Rotation Settings")]
        [SerializeField] private Vector3 rotationAxis = Vector3.right;

        [Header("Angle Constraints (Degrees)")]
        [SerializeField] private float upperLidOpenAngle = -45f;
        [SerializeField] private float upperLidClosedAngle = 0f;
        [SerializeField] private float lowerLidOpenAngle = 45f;
        [SerializeField] private float lowerLidClosedAngle = 0f;

        [Header("Configuration")]
        [SerializeField] private RobotEmotion currentEmotion = RobotEmotion.Neutral;
        [SerializeField] private List<EmotionConfig> emotionPresets;

        private Quaternion _initialUpperRotation;
        private Quaternion _initialLowerRotation;
        private Coroutine _blinkCoroutine;
        private Coroutine _squintCoroutine;
        private EmotionConfig _activeConfig;
        private float _currentSquintFactor;
        private bool _isBlinking;

        public bool IsVisionClear => !_isBlinking && _currentSquintFactor < 0.8f;

        private void Awake()
        {
            ValidateComponents();
            CacheInitialRotations();
            InitializeDefaultPresets();
            ApplyEmotionConfig(currentEmotion);
        }

        private void OnEnable()
        {
            StartBlinkRoutine();
        }

        private void OnDisable()
        {
            StopBlinkRoutine();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            if (emotionPresets == null || emotionPresets.Count == 0) return;
            ApplyEmotionConfig(currentEmotion);
            UpdateSquintState();
        }

        public void SetEmotion(RobotEmotion newEmotion)
        {
            if (currentEmotion == newEmotion) return;
            currentEmotion = newEmotion;
            ApplyEmotionConfig(newEmotion);
            UpdateSquintState();
        }

        public void ForceBlink()
        {
            if (_blinkCoroutine != null) StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = StartCoroutine(PerformBlinkSequence());
        }

        private void ValidateComponents()
        {
            if (upperEyelidBone == null || lowerEyelidBone == null)
            {
                enabled = false;
                throw new MissingReferenceException("Eyelid bones are not assigned.");
            }
        }

        private void CacheInitialRotations()
        {
            _initialUpperRotation = upperEyelidBone.localRotation;
            _initialLowerRotation = lowerEyelidBone.localRotation;
        }

        private void InitializeDefaultPresets()
        {
            if (emotionPresets != null && emotionPresets.Count > 0) return;
            emotionPresets = new List<EmotionConfig>
            {
                new EmotionConfig { emotionType = RobotEmotion.Neutral, blinkDuration = 0.15f, blinkIntervalRange = new Vector2(2f, 5f), eyeSquintAmount = 0f, doubleBlinkChance = false },
                new EmotionConfig { emotionType = RobotEmotion.Happy, blinkDuration = 0.2f, blinkIntervalRange = new Vector2(1f, 3f), eyeSquintAmount = 0.6f, doubleBlinkChance = true },
                new EmotionConfig { emotionType = RobotEmotion.Angry, blinkDuration = 0.1f, blinkIntervalRange = new Vector2(3f, 6f), eyeSquintAmount = 0.4f, doubleBlinkChance = false },
                new EmotionConfig { emotionType = RobotEmotion.Surprised, blinkDuration = 0.1f, blinkIntervalRange = new Vector2(4f, 8f), eyeSquintAmount = -0.2f, doubleBlinkChance = false },
                new EmotionConfig { emotionType = RobotEmotion.Sleepy, blinkDuration = 0.4f, blinkIntervalRange = new Vector2(0.5f, 2f), eyeSquintAmount = 0.5f, doubleBlinkChance = false },
                new EmotionConfig { emotionType = RobotEmotion.Suspicious, blinkDuration = 0.15f, blinkIntervalRange = new Vector2(2f, 4f), eyeSquintAmount = 0.7f, doubleBlinkChance = false }
            };
        }

        private void ApplyEmotionConfig(RobotEmotion emotion)
        {
            _activeConfig = emotionPresets.Find(x => x.emotionType == emotion);
            if (_activeConfig.Equals(default(EmotionConfig)) && emotionPresets.Count > 0)
            {
                _activeConfig = emotionPresets[0];
            }
        }

        private void StartBlinkRoutine()
        {
            StopBlinkRoutine();
            _blinkCoroutine = StartCoroutine(AutoBlinkLoop());
        }

        private void StopBlinkRoutine()
        {
            if (_blinkCoroutine != null)
            {
                StopCoroutine(_blinkCoroutine);
                _blinkCoroutine = null;
            }
        }

        private IEnumerator AutoBlinkLoop()
        {
            while (true)
            {
                float waitTime = UnityEngine.Random.Range(_activeConfig.blinkIntervalRange.x, _activeConfig.blinkIntervalRange.y);
                yield return new WaitForSeconds(waitTime);
                yield return PerformBlinkSequence();
                if (_activeConfig.doubleBlinkChance && UnityEngine.Random.value > 0.7f)
                {
                    yield return new WaitForSeconds(0.1f);
                    yield return PerformBlinkSequence();
                }
            }
        }

        private IEnumerator PerformBlinkSequence()
        {
            _isBlinking = true;
            float duration = _activeConfig.blinkDuration;
            float halfDuration = duration * 0.5f;
            float timer = 0f;

            while (timer < halfDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, timer / halfDuration);
                UpdateEyelidTransforms(t);
                yield return null;
            }

            UpdateEyelidTransforms(1f);

            timer = 0f;
            while (timer < halfDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(1f, 0f, timer / halfDuration);
                UpdateEyelidTransforms(t);
                yield return null;
            }
            _isBlinking = false;
            UpdateSquintState();
        }

        private void UpdateSquintState()
        {
            if (_squintCoroutine != null) StopCoroutine(_squintCoroutine);
            _squintCoroutine = StartCoroutine(TransitionToSquint(_activeConfig.eyeSquintAmount));
        }

        private IEnumerator TransitionToSquint(float targetSquint)
        {
            float timer = 0f;
            float duration = 0.3f;
            float startSquint = _currentSquintFactor;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                _currentSquintFactor = Mathf.Lerp(startSquint, targetSquint, timer / duration);
                UpdateEyelidTransforms(0f);
                yield return null;
            }

            _currentSquintFactor = targetSquint;
            UpdateEyelidTransforms(0f);
        }

        private void UpdateEyelidTransforms(float blinkWeight)
        {
            float currentClosedness = Mathf.Clamp01(blinkWeight + _currentSquintFactor);
            float currentUpperAngle = Mathf.Lerp(upperLidOpenAngle, upperLidClosedAngle, currentClosedness);
            float currentLowerAngle = Mathf.Lerp(lowerLidOpenAngle, lowerLidClosedAngle, currentClosedness);
            upperEyelidBone.localRotation = _initialUpperRotation * Quaternion.AngleAxis(currentUpperAngle, rotationAxis);
            lowerEyelidBone.localRotation = _initialLowerRotation * Quaternion.AngleAxis(currentLowerAngle, rotationAxis);
        }
    }
}