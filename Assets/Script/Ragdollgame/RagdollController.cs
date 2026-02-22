using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RagdollGame
{
    [RequireComponent(typeof(Rigidbody))]
    public class RagdollController : MonoBehaviour
    {
        #region Data Structures

        [System.Serializable]
        public class RagdollPose
        {
            public string poseName;
            public List<JointRotation> jointRotations = new List<JointRotation>();
        }

        [System.Serializable]
        public struct JointRotation
        {
            public string boneName;
            public Quaternion localRotation;
        }

        public enum RagdollState { Active, KnockedOut, Recovering }

        #endregion

        #region Config

        [TitleGroup("References")]
        [Required, ChildGameObjectsOnly]
        public Rigidbody hips;
        [ChildGameObjectsOnly]
        public Animator animator;

        [TitleGroup("Roly-Poly Stabilizer")]
        [Range(1000f, 20000f)] public float uprightTorque = 10000f;
        [Range(10f, 500f)] public float uprightDamper = 100f;

        [TitleGroup("Levitation")]
        [Range(0.5f, 3f)] public float hoverHeight = 1.2f;
        public float hoverSpring = 2000f;
        public float hoverDamper = 150f;
        public LayerMask groundLayer = 1;

        [TitleGroup("Muscle System")]
        [Tooltip("Tự động điều chỉnh lực dựa trên khối lượng từng bộ phận. Giúp tay chân mềm mại nhưng lưng vẫn cứng cáp.")]
        public bool useMassScaling = true;
        [Range(0f, 2000f)] public float muscleStiffness = 100f; // Giảm mạnh xuống vì đã nhân với Mass
        [Tooltip("Giảm cái này xuống thấp (0-5) để bớt cảm giác 'nhão' hay 'đi trong nước'.")]
        public float muscleDamping = 0.5f; // Giảm nữa cho linh hoạt
        [Tooltip("Giới hạn lực tối đa cơ bắp có thể dùng. Giảm xuống để nhân vật 'mềm' hơn khi va chạm mạnh.")]
        public float maxMuscleForce = 1000f;

        [TitleGroup("Constraints")]
        [Tooltip("Nên để Free để Lò xo tự giữ form. Limited sẽ làm khớp bị khựng khi đạt giới hạn.")]
        public ConfigurableJointMotion activeAngularMotion = ConfigurableJointMotion.Free;

        [TitleGroup("Combat")]
        public float knockoutThreshold = 2000f;
        [InfoBox("Lực bắn test mặc định.")]
        public float defaultImpactForce = 500f;

        [TitleGroup("Recovery")]
        public float recoverDelay = 3f;
        public float standUpDuration = 1.0f;

        [TitleGroup("Pose System")]
        [SerializeField] private List<RagdollPose> savedPoses = new List<RagdollPose>();

        [TitleGroup("State")]
        [ShowInInspector, ReadOnly, EnumToggleButtons]
        private RagdollState _state = RagdollState.Active;

        #endregion

        #region Internal

        private ConfigurableJoint _stabilizer;

        // Class lưu trữ trạng thái khớp ban đầu để tính toán rotation
        private class Muscle
        {
            public ConfigurableJoint joint;
            public Quaternion initialRotation;
            public Transform transform;
        }

        private List<Muscle> _muscles = new List<Muscle>();
        [ShowInInspector, ReadOnly, FoldoutGroup("Debug Info")]
        private List<ConfigurableJoint> _joints = new List<ConfigurableJoint>(); // Giữ lại để hiển thị Inspector nếu cần
        private Dictionary<string, ConfigurableJoint> _jointMap = new Dictionary<string, ConfigurableJoint>();

        private Coroutine _recoverRoutine;
        private float _currentMuscleMlp = 1f;

        #endregion

        #region Unity

        private void Start()
        {
            Init();
        }

        private void FixedUpdate()
        {
            if (_state == RagdollState.Active)
            {
                ApplyHoverForce();
                if (_stabilizer != null)
                {
                    _stabilizer.targetRotation = Quaternion.identity;
                }
                // Cực kỳ quan trọng: Cập nhật Target Rotation theo Animation hiện tại
                // Nếu không có dòng này, Ragdoll sẽ cố quay về T-Pose ban đầu -> Gây cứng ngắt
                SyncJointsToAnimation();
            }
        }

        private void OnValidate()
        {
            // Cập nhật ngay khi kéo thanh trượt trong Inspector
            if (Application.isPlaying && _state != RagdollState.KnockedOut)
            {
                UpdateMuscles();
            }
        }

        #endregion

        #region Core Logic

        private void Init()
        {
            if (hips == null) hips = GetComponent<Rigidbody>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            hips.transform.parent = null;
            hips.useGravity = true;
            hips.isKinematic = false;

            FindJoints();
            CreateStabilizer();
            UpdateMuscles();
            SetJointAngularMotion(activeAngularMotion);
        }

        private void FindJoints()
        {
            // Tìm tất cả Joint con (trừ cái ở Hips nếu là Stabilizer)
            _joints = GetComponentsInChildren<ConfigurableJoint>()
                .Where(j => j.gameObject != hips.gameObject && j.connectedBody != null)
                .ToList();

            _jointMap.Clear();
            _muscles.Clear();

            foreach (var j in _joints)
            {
                _jointMap[j.name] = j;

                // Lưu lại trạng thái ban đầu của khớp để làm chuẩn
                _muscles.Add(new Muscle
                {
                    joint = j,
                    transform = j.transform,
                    initialRotation = j.transform.localRotation
                });
            }
        }

        private void SyncJointsToAnimation()
        {
            // CÔNG THỨC CHUẨN CHO ACTIVE RAGDOLL:
            // TargetRotation là góc xoay CẦN THIẾT để đưa khớp từ trạng thái Start về trạng thái Animation hiện tại.
            // Công thức: Target = Inverse(AnimRotation) * InitialRotation

            foreach (var m in _muscles)
            {
                // 1. Lấy rotation mà Animation đang muốn (Animator cập nhật transform.localRotation mỗi frame)
                Quaternion currentAnimRot = m.transform.localRotation;

                // 2. Tính toán TargetRotation cho Joint
                // Giải thích: ConfigurableJoint.targetRotation hoạt động ngược (Inverse).
                // Nếu gán Identity, nó giữ nguyên InitialRotation.
                // Nếu gán Inverse(Animation) * Initial, nó sẽ lái xương về Animation.
                m.joint.targetRotation = Quaternion.Inverse(currentAnimRot) * m.initialRotation;
            }
        }

        private void SetJointAngularMotion(ConfigurableJointMotion motion)
        {
            foreach (var j in _joints)
            {
                j.angularXMotion = motion;
                j.angularYMotion = motion;
                j.angularZMotion = motion;
            }
        }

        private void CreateStabilizer()
        {
            if (_stabilizer != null) Destroy(_stabilizer);

            _stabilizer = hips.gameObject.AddComponent<ConfigurableJoint>();
            _stabilizer.connectedBody = null;
            _stabilizer.autoConfigureConnectedAnchor = true;

            _stabilizer.xMotion = ConfigurableJointMotion.Free;
            _stabilizer.zMotion = ConfigurableJointMotion.Free;
            _stabilizer.yMotion = ConfigurableJointMotion.Free;
            _stabilizer.angularXMotion = ConfigurableJointMotion.Free;
            _stabilizer.angularYMotion = ConfigurableJointMotion.Free;
            _stabilizer.angularZMotion = ConfigurableJointMotion.Free;

            var uprightDrive = new JointDrive
            {
                positionSpring = uprightTorque,
                positionDamper = uprightDamper,
                maximumForce = float.MaxValue
            };

            _stabilizer.rotationDriveMode = RotationDriveMode.XYAndZ;
            _stabilizer.angularXDrive = uprightDrive;
            _stabilizer.angularYZDrive = uprightDrive;
            _stabilizer.targetRotation = Quaternion.identity;
        }

        private void ApplyHoverForce()
        {
            Ray ray = new Ray(hips.position, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, hoverHeight * 2f, groundLayer))
            {
                float groundDist = hit.distance;
                float delta = hoverHeight - groundDist;
                float velY = hips.linearVelocity.y;
                float force = (delta * hoverSpring) - (velY * hoverDamper);

                hips.AddForce(Vector3.up * force);
            }
        }

        private void UpdateMuscles()
        {
            foreach (var j in _joints)
            {
                var rb = j.GetComponent<Rigidbody>();
                float massFactor = (useMassScaling && rb != null) ? rb.mass : 1f;

                float str = muscleStiffness * massFactor * _currentMuscleMlp;
                float dmp = muscleDamping * massFactor;
                float maxForce = maxMuscleForce;

                // QUAN TRỌNG: Khi Knockout (_currentMuscleMlp = 0), lực lò xo phải bằng 0 tuyệt đối
                if (_currentMuscleMlp <= 0.001f)
                {
                    str = 0f;
                    dmp = 0.1f * massFactor; // Giữ lại chút ma sát rất nhỏ để không bị rung
                    maxForce = 0f;
                }

                var drive = new JointDrive
                {
                    positionSpring = str,
                    positionDamper = dmp,
                    maximumForce = (maxForce > 0 && _currentMuscleMlp > 0) ? (maxForce * massFactor) : float.MaxValue
                };

                // Chỉ set drive tương ứng với Mode để tối ưu physics
                if (j.rotationDriveMode == RotationDriveMode.Slerp)
                {
                    j.slerpDrive = drive;
                }
                else
                {
                    j.angularXDrive = drive;
                    j.angularYZDrive = drive;
                }
            }
        }

        #endregion

        #region Combat System

        public void ApplyImpulse(Vector3 forceVector, Vector3 hitPoint, Rigidbody hitPart)
        {
            if (hitPart == null) return;

            // Áp dụng lực vật lý
            hitPart.AddForceAtPosition(forceVector, hitPoint, ForceMode.Impulse);

            if (_state == RagdollState.Active && forceVector.magnitude > knockoutThreshold)
            {
                Knockout();
            }
        }

        [Button("TEST SHOT (Force Knockout)"), GUIColor(1f, 0f, 0f), FoldoutGroup("Debug Actions")]
        public void SimulateHeavyShot()
        {
            if (_joints.Count == 0) FindJoints();

            // Chọn ngẫu nhiên bộ phận
            Rigidbody targetRb = (Random.value > 0.5f) ? hips : _joints[Random.Range(0, _joints.Count)].GetComponent<Rigidbody>();

            // Hướng ngẫu nhiên
            Vector3 randomDir = Random.onUnitSphere;
            randomDir.y = Mathf.Abs(randomDir.y) + 0.3f; // Hướng lên trên một chút để nhân vật bay lên
            randomDir.Normalize();

            // Lực cực mạnh để đảm bảo ngã
            float testForce = 3500f;

            Debug.DrawLine(targetRb.position, targetRb.position + randomDir * 2f, Color.red, 2f);
            ApplyImpulse(randomDir * testForce, targetRb.position, targetRb);
        }

        #endregion

        #region Pose System

        [Button("Capture Current Pose"), GUIColor(0.5f, 0.5f, 1f), FoldoutGroup("Pose System")]
        public void CapturePose(string newPoseName = "New Pose")
        {
            var pose = new RagdollPose { poseName = newPoseName };
            foreach (var j in _joints)
            {
                pose.jointRotations.Add(new JointRotation
                {
                    boneName = j.name,
                    localRotation = j.transform.localRotation
                });
            }
            savedPoses.Add(pose);
        }

        [Button("Apply Pose"), FoldoutGroup("Pose System")]
        public void ApplyPose(string poseName)
        {
            var pose = savedPoses.FirstOrDefault(p => p.poseName == poseName);
            if (pose != null && _state != RagdollState.KnockedOut)
            {
                foreach (var rotData in pose.jointRotations)
                {
                    if (_jointMap.TryGetValue(rotData.boneName, out ConfigurableJoint joint))
                    {
                        joint.targetRotation = rotData.localRotation;
                    }
                }
            }
        }

        #endregion

        #region State Machine

        [Button("Knockout (Collapse)"), GUIColor(1f, 0.2f, 0.2f), FoldoutGroup("Debug Actions")]
        public void Knockout()
        {
            if (_state == RagdollState.KnockedOut) return;
            if (_recoverRoutine != null) StopCoroutine(_recoverRoutine);

            _state = RagdollState.KnockedOut;

            // 1. Hủy Stabilizer (Lật đật)
            if (_stabilizer != null) Destroy(_stabilizer);

            // 2. Tắt Animator
            if (animator != null) animator.enabled = false;

            // 3. Set cơ bắp về 0 (nhão hoàn toàn)
            _currentMuscleMlp = 0f;
            UpdateMuscles();

            // 4. MỞ KHÓA KHỚP (Quan trọng: Để ragdoll đổ sập tự nhiên)
            SetJointAngularMotion(ConfigurableJointMotion.Free);

            hips.WakeUp();
            StartCoroutine(AutoRecoverSequence());
        }

        private IEnumerator AutoRecoverSequence()
        {
            yield return new WaitForSeconds(recoverDelay);
            StandUp();
        }

        [Button("Stand Up"), GUIColor(0.2f, 1f, 0.2f), FoldoutGroup("Debug Actions")]
        public void StandUp()
        {
            if (_state == RagdollState.Recovering || _state == RagdollState.Active) return;
            _recoverRoutine = StartCoroutine(StandUpRoutine());
        }

        private IEnumerator StandUpRoutine()
        {
            _state = RagdollState.Recovering;

            Vector3 fwd = Vector3.ProjectOnPlane(hips.transform.forward, Vector3.up).normalized;
            if (fwd == Vector3.zero) fwd = Vector3.forward;

            GameObject ghost = new GameObject("Ghost_Anchor");
            ghost.transform.position = hips.position;
            ghost.transform.rotation = hips.rotation;
            Rigidbody ghostRb = ghost.AddComponent<Rigidbody>();
            ghostRb.isKinematic = true;

            ConfigurableJoint liftJoint = hips.gameObject.AddComponent<ConfigurableJoint>();
            liftJoint.connectedBody = ghostRb;
            liftJoint.autoConfigureConnectedAnchor = false;
            liftJoint.anchor = Vector3.zero;
            liftJoint.connectedAnchor = Vector3.zero;

            liftJoint.xMotion = ConfigurableJointMotion.Locked;
            liftJoint.yMotion = ConfigurableJointMotion.Locked;
            liftJoint.zMotion = ConfigurableJointMotion.Locked;
            liftJoint.angularXMotion = ConfigurableJointMotion.Free;
            liftJoint.angularYMotion = ConfigurableJointMotion.Free;
            liftJoint.angularZMotion = ConfigurableJointMotion.Free;

            var snapDrive = new JointDrive { positionSpring = 10000f, positionDamper = 200f, maximumForce = float.MaxValue };
            liftJoint.angularXDrive = snapDrive;
            liftJoint.angularYZDrive = snapDrive;

            Vector3 startPos = ghost.transform.position;
            Quaternion startRot = ghost.transform.rotation;
            Vector3 targetPos = startPos;

            if (Physics.Raycast(startPos + Vector3.up * 5, Vector3.down, out RaycastHit hit, 10f, groundLayer))
                targetPos = hit.point + Vector3.up * hoverHeight;
            else
                targetPos += Vector3.up * hoverHeight;

            Quaternion targetRot = Quaternion.LookRotation(fwd, Vector3.up);

            float t = 0;
            while (t < 1f)
            {
                t += Time.fixedDeltaTime / standUpDuration;
                float curve = t * t * (3f - 2f * t);

                ghost.transform.position = Vector3.Lerp(startPos, targetPos, curve);
                ghost.transform.rotation = Quaternion.Slerp(startRot, targetRot, curve);

                if (t > 0.4f)
                {
                    _currentMuscleMlp = Mathf.Lerp(0f, 1f, (t - 0.4f) * 1.6f);
                    UpdateMuscles();
                }

                yield return new WaitForFixedUpdate();
            }

            Destroy(liftJoint);
            Destroy(ghost);

            if (animator != null)
            {
                animator.Play("Idle", 0, 0);
                animator.enabled = true;
            }

            CreateStabilizer();
            SetJointAngularMotion(activeAngularMotion);

            _state = RagdollState.Active;
        }

        #endregion

        #region Editor Utils

        [Button("Auto Setup Joints"), GUIColor(0, 0.8f, 1f), FoldoutGroup("Setup")]
        private void SetupRagdoll()
        {
            if (hips == null) hips = GetComponentInChildren<Rigidbody>();

            // Clean các joint cũ bị lỗi
            var existingJoints = GetComponentsInChildren<ConfigurableJoint>();
            foreach (var j in existingJoints)
            {
                if (j.connectedBody == null && j.GetComponent<Rigidbody>() == hips) DestroyImmediate(j);
            }

            var rbs = GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs)
            {
                if (rb == hips) continue;
                var j = rb.GetComponent<ConfigurableJoint>();
                if (j == null) j = rb.gameObject.AddComponent<ConfigurableJoint>();

                // Tăng độ đầm cho vật lý (Giảm Drag xuống để bớt cảm giác đi trong bùn)
                rb.linearDamping = 0.1f;
                rb.angularDamping = 0.05f; // Quan trọng: Giảm số này để khớp xoay mượt hơn

                // Setup chuẩn cho Muscle Ragdoll
                j.rotationDriveMode = RotationDriveMode.Slerp;
                j.projectionMode = JointProjectionMode.None;

                // Khóa Position
                j.xMotion = ConfigurableJointMotion.Locked;
                j.yMotion = ConfigurableJointMotion.Locked;
                j.zMotion = ConfigurableJointMotion.Locked;

                // Thiết lập giới hạn mềm (Soft Limits) mặc định rộng hơn
                SoftJointLimit softLimit = new SoftJointLimit { limit = 30f, bounciness = 0.5f, contactDistance = 0.1f }; // Mặc định 30 độ
                SoftJointLimitSpring limitSpring = new SoftJointLimitSpring { spring = 50f, damper = 5f };

                // Cài đặt Limit cho các trục
                j.lowAngularXLimit = new SoftJointLimit { limit = -30f };
                j.highAngularXLimit = new SoftJointLimit { limit = 30f };
                j.angularYLimit = new SoftJointLimit { limit = 30f };
                j.angularZLimit = new SoftJointLimit { limit = 30f };

                // Mặc định cho phép xoay Limited
                j.angularXMotion = ConfigurableJointMotion.Limited;
                j.angularYMotion = ConfigurableJointMotion.Limited;
                j.angularZMotion = ConfigurableJointMotion.Limited;

                j.linearLimitSpring = limitSpring;
                j.angularXLimitSpring = limitSpring;
                j.angularYZLimitSpring = limitSpring;

                // Tăng độ chính xác vật lý để tránh rung lắc
                rb.solverIterations = 10;
                rb.solverVelocityIterations = 10;

                // 4. CHỐNG VA CHẠM NỘI BỘ (SELF-COLLISION)
                // Bỏ qua va chạm giữa bộ phận này và bộ phận cha (Connected Body)
                if (j.connectedBody != null)
                {
                    Collider colA = j.GetComponent<Collider>();
                    Collider colB = j.connectedBody.GetComponent<Collider>();
                    if (colA != null && colB != null)
                    {
                        Physics.IgnoreCollision(colA, colB);
                    }
                }
            }

            FindJoints();
            UpdateMuscles();
            Debug.Log("Ragdoll Setup Complete. Hãy nhớ chỉnh sửa Angular Limits trong Editor cho từng khớp!");
        }

        #endregion
    }
}