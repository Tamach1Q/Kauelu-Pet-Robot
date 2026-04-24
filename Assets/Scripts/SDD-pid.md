# SDD-pid: Rovy — Lead-Following PID Control
**Version 0.1 | Unity 6000.3.3f1 | URP | Depends on: SDD.md (base), SDD_LeadingPet.md**

---

## 1. Overview

### 1.1 Purpose
Rovyの「リード機能」（提案資料p.12）の制御ロジックをUnity上で再現・検証する。実機のセンサー構成（ジョイスティック・張力センサー・エンコーダー・LiDAR）を仮想センサー層として抽象化し、その上でPID制御アルゴリズムをチューニング・検証できる環境を構築する。

### 1.2 Goals
- **制御検証**: PIDパラメータ（Kp, Ki, Kd）をインスペクターで調整し、挙動をリアルタイム観察できる
- **再現性**: プレイヤー入力を記録・再生でき、同一シナリオで異なるパラメータを比較できる
- **説明性**: 各センサー値・制御出力の時系列をグラフとCSVで残し、事後分析できる
- **実機移行性**: 仮想センサー/実機センサーをインターフェースで抽象化し、ControllerNodeロジックを両環境で共通化

### 1.3 Out of Scope
- 実機センサー（ロードセル、ロータリーエンコーダー）との接続
- カメラによる人物検出（SDD_LeadingPet側で扱う）
- LiDARによる障害物回避ロジック（本SDDではデータ取得のみ）
- MoodEngineとの連携

### 1.4 Dependencies on Existing Code
本SDDは以下の既存コンポーネントに依存する：
- `RovyController.cs`: 状態管理の拡張（Leading状態を追加想定）
- `LeashSystem.cs`: 張力計算のデータソース
- `PlayerController.cs`: ユーザー入力（WASD）
- `SimulationLogger`: ログ出力の既存基盤を流用

### 1.5 Simulation Simplifications
実機との差分を明示する（検証用シミュの前提）：
- **Rovyのボディ**: 現行のカプセル型プリミティブを使用
- **駆動モデル**: 車輪の個別制御ではなく、Rigidbodyへの`AddForce`/`AddTorque`で簡略化
- **エンコーダー**: 車輪回転数ではなく、`Rigidbody.velocity`の積算で距離・速度を算出
- **張力センサー**: SpringJointの`currentForce`を直接読み取り

---

## 2. Architecture

### 2.1 Layered Design

```
┌─────────────────────────────────────────────────┐
│  Application Layer                              │
│  ┌─────────────────────────────────────────┐    │
│  │ LeadFollowController (MonoBehaviour)     │    │
│  │  - ISensorHub を参照                     │    │
│  │  - IMotorDriver を参照                   │    │
│  │  - PidController × 2 (速度, 旋回)        │    │
│  │  - PidConfig (ScriptableObject)          │    │
│  └─────────────────────────────────────────┘    │
├─────────────────────────────────────────────────┤
│  Abstraction Layer (Interfaces)                 │
│  ITensionSensor  IEncoder  IJoystickInput       │
│  ILiDAR          IMotorDriver                   │
├─────────────────────────────────────────────────┤
│  Implementation Layer                           │
│  ┌─────────────────────┐ ┌──────────────────┐   │
│  │ Simulated*          │ │ (Future) Serial* │   │
│  │  - Unity API を使用 │ │  - 実機接続用    │   │
│  └─────────────────────┘ └──────────────────┘   │
├─────────────────────────────────────────────────┤
│  Observation Layer                              │
│  PidLogger  PidGraphUI  ScenarioRecorder        │
└─────────────────────────────────────────────────┘
```

### 2.2 Data Flow

```
[Player WASD]
     │
     ▼
[PlayerController] (既存)
     │ 位置変化
     ▼
[LeashSystem] (既存: SpringJoint or 手動バネ)
     │ currentForce
     ▼
┌──────────────────────────────────────────┐
│ SimulatedTensionSensor                    │── ISensorHub ──┐
│ SimulatedEncoder                          │                │
│ SimulatedJoystick (リード角度から推定)      │                │
│ SimulatedLiDAR                            │                │
└──────────────────────────────────────────┘                │
                                                             ▼
                                             ┌──────────────────────────┐
                                             │ LeadFollowController      │
                                             │  ├─ SpeedPid              │
                                             │  └─ SteerPid              │
                                             └──────────────────────────┘
                                                             │
                                             ┌──────────────────────────┐
                                             │ SimulatedMotorDriver      │── IMotorDriver
                                             │  Rigidbody.AddForce       │
                                             │  Rigidbody.AddTorque      │
                                             └──────────────────────────┘
                                                             │
                                                             ▼
                                                    [Rovy Rigidbody 挙動]
                                                             │
                                                             ▼
                                  ┌──────────────────────────────────────┐
                                  │ PidLogger → CSV                        │
                                  │ PidGraphUI → Runtime Graph             │
                                  │ ScenarioRecorder → .scenario ファイル   │
                                  └──────────────────────────────────────┘
```

### 2.3 Event Driven Updates
`LeadFollowController.FixedUpdate()` で制御ループが回る（物理演算と同期）。各tickで以下のイベントが発火：

- `OnSensorSampled(SensorSnapshot)` → PidLogger, PidGraphUI が購読
- `OnControlCommand(ControlCommand)` → PidLogger, PidGraphUI が購読

---

## 3. Scene Structure (Additions)

### 3.1 Hierarchy Additions

```
Scene: WalkSimulation
├── [Managers]
│   ├── ScenarioRecorder        // 新規
│   └── PidLogger               // 新規
│
├── [Characters]
│   └── Rovy
│       ├── (既存) RovyController, LeashSystem, ...
│       ├── LeadFollowController        // 新規
│       ├── SimulatedTensionSensor      // 新規
│       ├── SimulatedEncoder            // 新規
│       ├── SimulatedJoystick           // 新規
│       ├── SimulatedLiDAR              // 新規 (既存 LiDARScanner を利用 or 置換)
│       └── SimulatedMotorDriver        // 新規
│
└── [UI]
    └── PidDebugPanel           // 新規
        ├── PidGraphUI
        └── PidParameterEditor
```

### 3.2 Required Components on Rovy

| Component | 用途 |
|-----------|------|
| Rigidbody | 物理駆動（mass=5.0, drag=1.0, angularDrag=5.0 を推奨初期値） |
| SpringJoint (連結先=Player) | LeashSystem 経由の張力生成 |
| CapsuleCollider | 既存 |
| LeadFollowController | 制御本体 |
| Simulated* | 仮想センサー群 |

**注意**: 既存シーンでは NavMeshAgent を使って追従しているが、PID検証モード時は `NavMeshAgent.enabled = false` にして Rigidbody 駆動に切り替える。モード切替は `LeadFollowController.enabled` で連動させる。

---

## 4. Interface Definitions

### 4.1 Sensor Interfaces

```csharp
// Assets/Scripts/Rovy/Sensors/ISensors.cs
namespace Rovy.Sensors
{
    public interface ITensionSensor
    {
        // 前後方向の張力 [N]. 正=前方に引かれている
        float GetForwardTension();
        // 横方向の張力 [N]. 正=右に引かれている
        float GetLateralTension();
    }

    public interface IEncoder
    {
        // 累積走行距離 [m]
        float GetDistance();
        // 現在の前進速度 [m/s]. 後退時は負
        float GetForwardVelocity();
        // 現在の旋回角速度 [rad/s]
        float GetAngularVelocity();
    }

    public interface IJoystickInput
    {
        // -1.0 (左) ～ 1.0 (右)
        float GetHorizontal();
        // -1.0 (引き) ～ 1.0 (押し)
        float GetVertical();
    }

    public interface ILiDAR
    {
        // 360度分の距離配列 [m]. 未検出は float.PositiveInfinity
        float[] GetRanges();
        int RayCount { get; }
    }
}
```

### 4.2 Actuator Interface

```csharp
namespace Rovy.Actuators
{
    public interface IMotorDriver
    {
        // 前進/後退の推力 [-1, 1]
        void SetThrottle(float throttle);
        // 旋回の指令 [-1, 1]. 正=右旋回
        void SetSteer(float steer);
        // 緊急停止
        void Stop();
    }
}
```

### 4.3 Sensor Hub (集約)

```csharp
namespace Rovy.Sensors
{
    public interface ISensorHub
    {
        ITensionSensor Tension { get; }
        IEncoder Encoder { get; }
        IJoystickInput Joystick { get; }
        ILiDAR LiDAR { get; }
    }

    // GameObjectにアタッチして、各Simulated*を集約
    public sealed class SensorHub : MonoBehaviour, ISensorHub
    {
        [SerializeField] private MonoBehaviour tensionSource;   // ITensionSensor実装
        [SerializeField] private MonoBehaviour encoderSource;   // IEncoder実装
        [SerializeField] private MonoBehaviour joystickSource;  // IJoystickInput実装
        [SerializeField] private MonoBehaviour lidarSource;     // ILiDAR実装

        public ITensionSensor Tension => (ITensionSensor)tensionSource;
        public IEncoder Encoder => (IEncoder)encoderSource;
        public IJoystickInput Joystick => (IJoystickInput)joystickSource;
        public ILiDAR LiDAR => (ILiDAR)lidarSource;
    }
}
```

---

## 5. Implementation Specs

### 5.1 SimulatedTensionSensor.cs

**役割**: SpringJointにかかる力をセンサー値に変換

**フィールド**:
```csharp
[SerializeField] private SpringJoint leashJoint;  // LeashSystemのジョイント
[SerializeField] private Transform rovyBody;       // 進行方向の基準
[SerializeField] private float noiseAmplitude = 0.05f; // 実機ノイズ模擬 [N]
```

**メソッド**:
```csharp
public float GetForwardTension()
{
    Vector3 force = leashJoint.currentForce; // ジョイントが受ける力
    float forward = Vector3.Dot(force, rovyBody.forward);
    return forward + Random.Range(-noiseAmplitude, noiseAmplitude);
}

public float GetLateralTension()
{
    Vector3 force = leashJoint.currentForce;
    float lateral = Vector3.Dot(force, rovyBody.right);
    return lateral + Random.Range(-noiseAmplitude, noiseAmplitude);
}
```

**注記**: `SpringJoint.currentForce` はUnityが提供する読み取り専用プロパティ。ここでは実機のロードセルのノイズを模擬するため微小なランダム項を加えている。ノイズをゼロにしたい場合は`noiseAmplitude=0`。

---

### 5.2 SimulatedEncoder.cs

**役割**: Rigidbodyの速度から距離・速度を計算（車輪回転の代替）

**フィールド**:
```csharp
[SerializeField] private Rigidbody rovyRigidbody;
[SerializeField] private Transform rovyBody;
private float accumulatedDistance;
private Vector3 lastPosition;
```

**メソッド**:
```csharp
private void Awake() { lastPosition = rovyRigidbody.position; }

private void FixedUpdate()
{
    float delta = Vector3.Distance(rovyRigidbody.position, lastPosition);
    accumulatedDistance += delta;
    lastPosition = rovyRigidbody.position;
}

public float GetDistance() => accumulatedDistance;

public float GetForwardVelocity()
{
    return Vector3.Dot(rovyRigidbody.linearVelocity, rovyBody.forward);
}

public float GetAngularVelocity()
{
    return rovyRigidbody.angularVelocity.y; // Y軸周りの回転のみ
}
```

**注記**: Unity 6000以降は`Rigidbody.velocity`が`linearVelocity`にリネームされているので注意。

---

### 5.3 SimulatedJoystick.cs

**役割**: リードの角度から「ユーザーがどちらに引いているか」を推定し、実機ジョイスティックの左右入力を模擬

**フィールド**:
```csharp
[SerializeField] private Transform player;
[SerializeField] private Transform rovyBody;
[SerializeField] private float deadZoneAngleDeg = 5.0f;  // この角度以下は0扱い
[SerializeField] private float maxAngleDeg = 45.0f;       // これ以上は±1.0
```

**メソッド**:
```csharp
public float GetHorizontal()
{
    Vector3 toPlayer = (player.position - rovyBody.position);
    toPlayer.y = 0;
    // Rovyから見てPlayerが右にいれば正
    float signedAngle = Vector3.SignedAngle(rovyBody.forward, -toPlayer, Vector3.up);

    float abs = Mathf.Abs(signedAngle);
    if (abs < deadZoneAngleDeg) return 0f;

    float normalized = Mathf.Clamp((abs - deadZoneAngleDeg) / (maxAngleDeg - deadZoneAngleDeg), 0f, 1f);
    return Mathf.Sign(signedAngle) * normalized;
}

public float GetVertical()
{
    // 前後方向は張力センサーで取るので、ここでは常に0
    return 0f;
}
```

**設計判断**: 実機ではユーザーの手元にジョイスティックがあるが、シミュではWASD入力のみ。リードの角度からユーザーの意図を推定することで、実機と同じControllerNodeロジックをテスト可能にする。

---

### 5.4 SimulatedMotorDriver.cs

**役割**: Rigidbodyへの力/トルク印加

**フィールド**:
```csharp
[SerializeField] private Rigidbody rovyRigidbody;
[SerializeField] private Transform rovyBody;
[SerializeField] private float maxForce = 10.0f;       // [N]
[SerializeField] private float maxTorque = 3.0f;        // [N·m]
private float currentThrottle;
private float currentSteer;
```

**メソッド**:
```csharp
public void SetThrottle(float throttle)
{
    currentThrottle = Mathf.Clamp(throttle, -1f, 1f);
}

public void SetSteer(float steer)
{
    currentSteer = Mathf.Clamp(steer, -1f, 1f);
}

public void Stop()
{
    currentThrottle = 0f;
    currentSteer = 0f;
    rovyRigidbody.linearVelocity = Vector3.zero;
    rovyRigidbody.angularVelocity = Vector3.zero;
}

private void FixedUpdate()
{
    rovyRigidbody.AddForce(rovyBody.forward * currentThrottle * maxForce);
    rovyRigidbody.AddTorque(Vector3.up * currentSteer * maxTorque);
}
```

---

### 5.5 PidController.cs

**役割**: 汎用PID制御器

**フィールド**:
```csharp
private float integral;
private float previousError;
private bool hasPrevious;
```

**メソッド**:
```csharp
public float Update(float setpoint, float measured, float kp, float ki, float kd, float dt, float outputMin, float outputMax)
{
    float error = setpoint - measured;
    integral += error * dt;

    float derivative = hasPrevious ? (error - previousError) / dt : 0f;
    previousError = error;
    hasPrevious = true;

    float raw = kp * error + ki * integral + kd * derivative;
    float clamped = Mathf.Clamp(raw, outputMin, outputMax);

    // アンチワインドアップ: 出力が飽和してる時は積分を戻す
    if (clamped != raw)
    {
        integral -= error * dt;
    }

    return clamped;
}

public void Reset()
{
    integral = 0f;
    previousError = 0f;
    hasPrevious = false;
}
```

**設計判断**: PidControllerはただのロジッククラス（MonoBehaviourではない）。LeadFollowControllerがインスタンスを2個保持する（速度用・旋回用）。これにより単体テストが容易。

---

### 5.6 PidConfig.cs (ScriptableObject)

**役割**: PIDパラメータをアセットとして外部化、エディタで調整

```csharp
[CreateAssetMenu(fileName = "PidConfig", menuName = "Rovy/PidConfig")]
public sealed class PidConfig : ScriptableObject
{
    [Header("Speed PID (前進速度)")]
    [Range(0f, 5f)] public float speedKp = 1.0f;
    [Range(0f, 2f)] public float speedKi = 0.1f;
    [Range(0f, 2f)] public float speedKd = 0.05f;

    [Header("Steer PID (旋回)")]
    [Range(0f, 5f)] public float steerKp = 1.5f;
    [Range(0f, 2f)] public float steerKi = 0.0f;
    [Range(0f, 2f)] public float steerKd = 0.2f;

    [Header("目標値生成")]
    [Tooltip("張力→目標速度の変換係数. 張力1Nあたりの速度指令")]
    public float tensionToSpeedGain = 0.3f;

    [Tooltip("目標速度の上限 [m/s]")]
    public float maxTargetSpeed = 1.0f;

    [Tooltip("ジョイスティック→目標旋回の変換係数")]
    public float joystickToSteerGain = 1.0f;
}
```

**運用**: `Assets/Data/PidConfig_Default.asset` を作成し、`LeadFollowController`にアサイン。Playモード中の変更は保存されないので、調整後にコピペして保存。

---

### 5.7 LeadFollowController.cs

**役割**: 制御本体。仮想RaspberryPiに相当。

**フィールド**:
```csharp
[SerializeField] private SensorHub sensorHub;
[SerializeField] private SimulatedMotorDriver motorDriver; // IMotorDriverにcastして使用
[SerializeField] private PidConfig config;

private PidController speedPid;
private PidController steerPid;

public event Action<SensorSnapshot> OnSensorSampled;
public event Action<ControlCommand> OnControlCommand;
```

**メソッド**:
```csharp
private void Awake()
{
    speedPid = new PidController();
    steerPid = new PidController();
}

private void FixedUpdate()
{
    // 1. センサー読み取り
    var snapshot = new SensorSnapshot
    {
        time = Time.time,
        forwardTension = sensorHub.Tension.GetForwardTension(),
        lateralTension = sensorHub.Tension.GetLateralTension(),
        forwardVelocity = sensorHub.Encoder.GetForwardVelocity(),
        angularVelocity = sensorHub.Encoder.GetAngularVelocity(),
        joystickHorizontal = sensorHub.Joystick.GetHorizontal(),
    };
    OnSensorSampled?.Invoke(snapshot);

    // 2. 目標値生成
    float targetSpeed = Mathf.Clamp(
        snapshot.forwardTension * config.tensionToSpeedGain,
        -config.maxTargetSpeed,
        config.maxTargetSpeed);
    float targetSteer = snapshot.joystickHorizontal * config.joystickToSteerGain;

    // 3. PID計算
    float throttleCmd = speedPid.Update(
        targetSpeed, snapshot.forwardVelocity,
        config.speedKp, config.speedKi, config.speedKd,
        Time.fixedDeltaTime, -1f, 1f);

    float steerCmd = steerPid.Update(
        targetSteer, snapshot.angularVelocity,
        config.steerKp, config.steerKi, config.steerKd,
        Time.fixedDeltaTime, -1f, 1f);

    // 4. モーター指令
    motorDriver.SetThrottle(throttleCmd);
    motorDriver.SetSteer(steerCmd);

    OnControlCommand?.Invoke(new ControlCommand
    {
        time = Time.time,
        targetSpeed = targetSpeed,
        targetSteer = targetSteer,
        throttle = throttleCmd,
        steer = steerCmd,
    });
}

private void OnDisable()
{
    motorDriver.Stop();
    speedPid.Reset();
    steerPid.Reset();
}
```

**データ構造**:
```csharp
public struct SensorSnapshot
{
    public float time;
    public float forwardTension;
    public float lateralTension;
    public float forwardVelocity;
    public float angularVelocity;
    public float joystickHorizontal;
}

public struct ControlCommand
{
    public float time;
    public float targetSpeed;
    public float targetSteer;
    public float throttle;
    public float steer;
}
```

---

### 5.8 PidLogger.cs

**役割**: センサー・制御出力をCSVに保存

**フィールド**:
```csharp
[SerializeField] private LeadFollowController controller;
[SerializeField] private string outputDirectory = "Logs";
private StreamWriter writer;
```

**メソッド**:
```csharp
private void OnEnable()
{
    string dir = Path.Combine(Application.dataPath, "..", outputDirectory);
    Directory.CreateDirectory(dir);
    string path = Path.Combine(dir, $"pid_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
    writer = new StreamWriter(path);
    writer.WriteLine("time,fwd_tension,lat_tension,fwd_vel,ang_vel,joy_h,tgt_speed,tgt_steer,throttle,steer");

    controller.OnSensorSampled += HandleSensor;
    controller.OnControlCommand += HandleCommand;
}

private SensorSnapshot latestSensor;
private void HandleSensor(SensorSnapshot s) { latestSensor = s; }

private void HandleCommand(ControlCommand c)
{
    writer.WriteLine(
        $"{c.time:F3},{latestSensor.forwardTension:F3},{latestSensor.lateralTension:F3}," +
        $"{latestSensor.forwardVelocity:F3},{latestSensor.angularVelocity:F3},{latestSensor.joystickHorizontal:F3}," +
        $"{c.targetSpeed:F3},{c.targetSteer:F3},{c.throttle:F3},{c.steer:F3}");
}

private void OnDisable()
{
    writer?.Flush();
    writer?.Close();
    if (controller != null)
    {
        controller.OnSensorSampled -= HandleSensor;
        controller.OnControlCommand -= HandleCommand;
    }
}
```

**出力例** (`Logs/pid_20260424_153012.csv`):
```
time,fwd_tension,lat_tension,fwd_vel,ang_vel,joy_h,tgt_speed,tgt_steer,throttle,steer
0.020,0.000,0.000,0.000,0.000,0.000,0.000,0.000,0.000,0.000
0.040,0.123,0.011,0.002,0.001,0.050,0.037,0.050,0.038,0.073
...
```

---

### 5.9 PidGraphUI.cs

**役割**: ランタイムでPID内部状態を折れ線グラフ表示

**使用方式**: 既存MoodGraphUIと同じスタイル（UI Toolkit + 自前描画）でもよいし、**簡易実装ならGraphy（既存アセット）を流用**してもよい。本SDDではUI Toolkitベースで自前描画とする。

**フィールド**:
```csharp
[SerializeField] private LeadFollowController controller;
[SerializeField] private UIDocument document;
[SerializeField] private int maxSamples = 300; // 約5秒分（60Hz）

private readonly Queue<float> targetSpeedHistory = new();
private readonly Queue<float> actualSpeedHistory = new();
private readonly Queue<float> tensionHistory = new();
```

**描画内容**: 3段グラフ
1. **目標速度 vs 実速度**（PIDが追従しているかの可視化）
2. **張力**（入力の時系列）
3. **スロットル出力**（制御指令）

**メソッド骨子**:
```csharp
private void OnEnable()
{
    controller.OnSensorSampled += HandleSensor;
    controller.OnControlCommand += HandleCommand;
}

private void HandleSensor(SensorSnapshot s)
{
    Enqueue(actualSpeedHistory, s.forwardVelocity);
    Enqueue(tensionHistory, s.forwardTension);
}

private void HandleCommand(ControlCommand c)
{
    Enqueue(targetSpeedHistory, c.targetSpeed);
    // 描画は GenerateVisualContent コールバックで毎フレーム
    document.rootVisualElement.Q<VisualElement>("graph-area").MarkDirtyRepaint();
}

private void Enqueue(Queue<float> q, float v)
{
    q.Enqueue(v);
    while (q.Count > maxSamples) q.Dequeue();
}
```

**UXML構造** (`PidGraphUI.uxml`):
```xml
<ui:UXML>
  <ui:VisualElement name="pid-panel" style="position:absolute; right:10px; top:10px; width:400px; height:300px; background-color:rgba(0,0,0,0.7);">
    <ui:VisualElement name="graph-area" style="flex-grow:1;" />
    <ui:Label name="pid-label" text="PID Debug" />
  </ui:VisualElement>
</ui:UXML>
```

---

### 5.10 ScenarioRecorder.cs

**役割**: プレイヤー入力とタイムスタンプを記録・再生

**データフォーマット** (`.scenario` JSON):
```json
{
  "version": 1,
  "startedAt": "2026-04-24T15:30:12",
  "samples": [
    {"t": 0.000, "pos": [0, 0, 0], "rot_y": 0.0},
    {"t": 0.033, "pos": [0, 0, 0.01], "rot_y": 0.5},
    ...
  ]
}
```

**モード**:
```csharp
public enum Mode { Idle, Recording, Replaying }

[SerializeField] private PlayerController player;
[SerializeField] private string scenarioFile = "scenario_01.json";
[SerializeField] private Mode mode = Mode.Idle;
```

**Recording**:
```csharp
private void FixedUpdate()
{
    if (mode == Mode.Recording)
    {
        samples.Add(new Sample {
            t = Time.time - startTime,
            pos = player.transform.position,
            rotY = player.transform.eulerAngles.y,
        });
    }
    else if (mode == Mode.Replaying)
    {
        float t = Time.time - startTime;
        var (a, b, alpha) = FindInterpolationPair(t);
        player.transform.position = Vector3.Lerp(a.pos, b.pos, alpha);
        player.transform.eulerAngles = new Vector3(0, Mathf.LerpAngle(a.rotY, b.rotY, alpha), 0);
    }
}
```

**Replay時の注意**: `PlayerController`の入力処理を無効化する必要がある。`PlayerController.enabled = false` にする、もしくはScenarioRecorderが`PlayerController`を排他制御する。

**保存先**: `Application.persistentDataPath/Scenarios/*.json`

---

### 5.11 PidDebugPanel (UI)

**役割**: インスペクターに頼らず実行時にPIDパラメータを触れるようにする

**構成** (UIDocument):
- Kp/Ki/Kd × (Speed, Steer) のスライダー（6個）
- tensionToSpeedGain, joystickToSteerGain のスライダー（2個）
- 「PIDリセット」ボタン
- 「ログ記録開始/停止」ボタン
- 「シナリオ記録開始/停止」「再生」ボタン

**実装方針**: `PidConfig` ScriptableObjectを直接バインドする。スライダーのvalueChangedで`config`のフィールドを書き換える。

---

## 6. Execution Flow

### 6.1 Typical Tuning Session

```
1. Playモード開始
2. PidDebugPanelで Kp/Ki/Kd を適当な初期値に設定
3. ScenarioRecorderで「記録」を押し、WASD でユーザー役を動かす
   → Rovy が LeadFollowController のPID制御で追従する
4. 「記録停止」→ scenario_XX.json が保存される
5. Kp を変更 → 「再生」を押す → 同じユーザー動作に対する Rovy の挙動を観察
6. PidGraphUI で目標速度 vs 実速度 の追従具合を見る
7. Logs/pid_*.csv を pandas で開いて詳細分析
```

### 6.2 Mode Switching

シーンに `NavMesh追従モード` と `PID制御モード` の2つを共存させる：

```csharp
// 例: RovyController 側に mode スイッチを追加
public enum FollowMode { NavMesh, Pid }
[SerializeField] private FollowMode mode = FollowMode.Pid;

private void OnEnable()
{
    navMeshAgent.enabled = (mode == FollowMode.NavMesh);
    leadFollowController.enabled = (mode == FollowMode.Pid);
}
```

---

## 7. Acceptance Criteria

### 7.1 機能要件
- [ ] Playerが前進するとRovyが張力に応じて追従し、距離がほぼ一定に保たれる
- [ ] Playerが急停止するとRovyもPID制御で減速して止まる
- [ ] Playerが横に動くと、リードの角度からジョイスティック入力が生成され、Rovyが旋回する
- [ ] PidConfigのKp/Ki/Kdを変えると挙動が変わる

### 7.2 説明性
- [ ] PidGraphUIで目標速度と実速度の時系列が見える
- [ ] CSVログに全センサー値・制御出力・タイムスタンプが残る
- [ ] CSVの時間刻みはFixedUpdateの周期（デフォルト0.02s）と一致

### 7.3 再現性
- [ ] 同じscenarioファイルを2回再生すると、Rovyの軌跡がフレーム単位で一致する（誤差±0.01m以内）
  - ※物理演算の非決定性に注意。Time.fixedDeltaTime固定、Physics.autoSimulation=true で検証
- [ ] 異なるPidConfigで同じscenarioを再生し、挙動の違いを定性的に比較できる

### 7.4 テスト
- [ ] PidControllerの単体テスト（EditMode）: 定常偏差がKiで消えること、Kdが大きいと振動が減ることを確認
- [ ] SimulatedTensionSensorの単体テスト: 既知の力を印加して期待値が返ること

---

## 8. Open Questions / Deferred

以下は本SDDの範囲外だが、今後の課題として明示：

1. **LiDARを使った障害物回避の統合**: 現状 ILiDAR は定義したが、LeadFollowControllerで未使用。次フェーズで追加。
2. **リード長の動的変化**: 現状はSpringJointの`maxDistance`固定。実機ではリードのたるみ/緊張がある。
3. **実機との差分検証**: SerialTensionSensor等を実装した後、同じControllerNodeで動かし、シミュと実機の挙動を比較する方法論の確立。
4. **ノイズモデル**: 現在はガウス風の一様乱数。実機ロードセルの実測ノイズ特性を取り込むか検討。
5. **PIDの自動チューニング**: Ziegler-Nichols法やベイズ最適化の導入。

---

## 9. File Manifest

新規作成ファイル：
```
Assets/Scripts/Rovy/Sensors/
├── ISensors.cs                 // 全センサーインターフェース
├── SensorHub.cs
├── SimulatedTensionSensor.cs
├── SimulatedEncoder.cs
├── SimulatedJoystick.cs
└── SimulatedLiDAR.cs           // 既存 LiDARScanner からリファクタ

Assets/Scripts/Rovy/Actuators/
├── IMotorDriver.cs
└── SimulatedMotorDriver.cs

Assets/Scripts/Rovy/Control/
├── PidController.cs            // Plain C# class
├── LeadFollowController.cs
└── PidConfig.cs                // ScriptableObject

Assets/Scripts/Rovy/Debug/
├── PidLogger.cs
├── PidGraphUI.cs
├── PidDebugPanel.cs
└── ScenarioRecorder.cs

Assets/UI/
├── PidGraphUI.uxml
├── PidGraphUI.uss
├── PidDebugPanel.uxml
└── PidDebugPanel.uss

Assets/Data/
└── PidConfig_Default.asset     // ScriptableObject インスタンス

Assets/Tests/EditMode/
├── PidControllerTests.cs
└── SimulatedTensionSensorTests.cs
```

改変ファイル：
```
Assets/Scripts/Rovy/RovyController.cs    // FollowMode enum追加、切替ロジック
```