# ✈️ Aerial Combat AI Training Guide

Tai lieu huong dan train AI lai may bay chien dau su dung Unity ML-Agents.

---

## 🚀 1. Quick Start (Chay luon)

Moi khi bat dau lam viec, chi can mo Terminal va chay 2 dong lenh nay:

```bash
# 1. Di chuyen den thu muc du an (Thay doi duong dan neu can)
cd /Volumes/Database/Bill/TrainAI

# 2. Kich hoat moi truong ao (venv)
source venv/bin/activate

# 3. Kiem tra xem da dung python chua (Ket qua phai la .../TrainAI/venv/bin/python)
which python
```

### Lenh Train AI:
Sau khi kich hoat venv, chay lenh sau de bat dau train. 
*Luu y: `--force` se ghi de len model cu cung ten ID.*

```bash
# Train voi Curriculum (Stacked Learning)
mlagents-learn Assets/Script/aerial_combat_config.yaml --run-id=Aerial_Stacked_V1 --force
```

**Sau khi lenh hien thi logo Unity, bam nut ▶️ Play trong Unity Editor.**

---

## 🛠 2. Setup Moi Truong (Neu cai lai may hoac xoa venv)

Chi lam phan nay neu folder `venv` bi mat hoac loi.

### Yeu cau:
*   **Python 3.10** (Bat buoc, Python 3.11+ hien tai chua tuong thich tot voi mlagents).

### Cac buoc cai dat:

```bash
cd /Volumes/Database/Bill/TrainAI

# 1. Tao venv bang Python 3.10
python3.10 -m venv venv

# 2. Kich hoat venv
source venv/bin/activate

# 3. Cap nhat pip
pip install --upgrade pip

# 4. Cai dat ML-Agents (Phien ban on dinh nhat)
pip install mlagents==0.30.0
```

---

## 🎮 3. Unity Checklist (Truoc khi train)

Dam bao cac muc sau da duoc cau hinh dung trong Scene `Aerial_Env_Hub`:

1.  **Aerial_Env_Hub (Parent Object):**
    *   [ ] Script `AerialCombatEnvironment` da gan:
        *   `Spawn Area`: BoxCollider (IsTrigger = True).
        *   `Waypoint Prefab`: Qua cau do (Co `PoolMember`, ko Collider).
        *   `Is Training Mode`: **TICK CHON**.

2.  **Agent (AerialCombatAgentV2):**
    *   [ ] `Behavior Parameters`:
        *   Behavior Name: `AerialCombat` (Giong het file config).
        *   Vector Observation Space Size: `19`.
    *   [ ] `Ray Perception Sensor 3D`:
        *   Detectable Tags: `Agent`, `Wall`, `Ground`.
    *   [ ] `Decision Requester`: Period = 5.
    *   [ ] `Laser Prefab`: Da gan Prefab dan (Co `LaserProjectileV2` + `PoolMember`).

3.  **Config File (yaml):**
    *   [ ] Kiem tra file `Assets/Script/aerial_combat_config.yaml` da co du 4 phase (BasicFlight -> Dogfight).

---

## 📊 4. Theo doi huan luyen (TensorBoard)

De xem bieu do reward tang giam thoi gian thuc:

1.  Mo mot tab Terminal moi (Tab 2).
2.  Kich hoat venv nhu buoc 1.
3.  Chay lenh:

```bash
tensorboard --logdir results
```
4.  Mo trinh duyet va vao: `http://localhost:6006`

---

## 🐛 5. Troubleshooting (Cuu ho)

*   **Loi `numpy` khi cai dat:** Do phien ban Python qua moi. Hay chac chan dung Python 3.10.
*   **Loi `command not found: mlagents-learn`:** Ban chua chay `source venv/bin/activate`.
*   **Agent khong hoc (Reward khong tang):**
    *   Kiem tra xem `Is Training Mode` o Hub da tick chua.
    *   Kiem tra xem Tag `Wall`, `Ground` da gan cho tuong va san chua (Agent mu khong thay tuong).
    *   Kiem tra `Behavior Name` co khop voi file yaml khong.
