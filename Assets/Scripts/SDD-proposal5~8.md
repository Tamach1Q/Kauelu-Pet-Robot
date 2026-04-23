SDD: Rovy — Leading Pet Experience
Version 0.1 | Unity 6000.3.3f1 | URP | Depends on: SDD.md (base)

1. Overview

1.1 Purpose
既存Rovy(SDD.md)は感情・経路学習のロジックを実装済みだが、**プレイヤーが「Rovyに先導されている」と体感できるUX**が不足している。本SDDは、既存ロジックをプレイヤー体験に接続する4つの機能を追加する。

1.2 Goals
- Rovyが道を覚えてきたことをプレイヤーが実感できる
- Rovyが先導している時、物理的・視覚的にそれが伝わる
- お気に入りルート到達時に感情的なリアクションがある
- 学習の進行度が定量的に可視化される

1.3 Out of Scope
- MoodEngineのロジック変更
- RouteMemoryのスコアリング変更
- 新しいNavMeshエージェントタイプの追加

1.4 Dependencies on Existing Code
本SDDは以下の既存コンポーネントに依存する(改変は最小限):
- `RovyController.cs`: 状態(Idle/Following/Waiting/Exploring)を参照
- `RouteMemory.cs`: お気に入り判定、既知ノード数を参照
- `LeashSystem.cs`: リードの物理挙動を拡張
- `BehaviorExpressions.cs`: お気に入り到達時のWag連携

---

2. Feature Breakdown

本SDDは4つの独立した機能を含む。実装はこの順に並んでいるが、相互依存はないので並列実装可能。

Feature 1: Leading Notification (先導通知UI)
Feature 2: Leash Pull Feedback (リード引っ張り挙動)
Feature 3: Favorite Route Reaction (お気に入りルート到達リアクション)
Feature 4: Learning Progress UI (学習進行度の可視化)

---

3. Feature 1: Leading Notification

3.1 Concept
RovyがExplore状態に遷移した瞬間(=自律的に先導を始めた瞬間)に、プレイヤーに短いメッセージを画面上に表示する。例:「Rovyが道を知ってるみたい...」

3.2 Scripts
Assets/Scripts/UI/
└── LeadingNotificationUI.cs    // 新規

3.3 Component: LeadingNotificationUI
RovyController.CurrentStateを監視し、状態遷移を検出してUIにメッセージを出す。

**主要ロジック**:
```
- SerializeField:
    - rovyController: RovyController
    - notificationText: TextMeshProUGUI
    - canvasGroup: CanvasGroup (フェード用)
    - displayDuration: float (default 3.0f)
    - fadeDuration: float (default 0.5f)
    - leadingMessages: string[]
        default: {
            "Rovyが道を知ってるみたい...",
            "Rovyがリードしてくれる",
            "今日はRovyが先導する番"
        }

- Update():
    現在の RovyController.CurrentState を取得
    前回のフレームで保存した lastState と比較
    遷移パターンを検出:
        Following/Waiting/Idle → Exploring のとき:
            ShowRandomLeadingMessage()
    lastState を更新

- ShowRandomLeadingMessage():
    leadingMessages からランダムに1つ選ぶ
    Coroutine で表示 → displayDuration秒待機 → フェードアウト

- 連続発火防止:
    最後に通知を出した時刻を記録
    minimumIntervalSeconds (default 15.0f) 以内は再発火しない
```

3.4 Scene Setup
- Canvas配下に空のTextMeshProUGUIを1つ追加(例: "LeadingNotificationText")
- CanvasGroupをアタッチ(初期alpha=0)
- 同GameObjectにLeadingNotificationUIをアタッチ
- Inspectorで上記フィールドをアサイン

3.5 Acceptance Criteria
- RovyがExploreに入ると3秒メッセージが出てフェードアウトする
- 15秒以内の連続遷移では再表示されない
- Follow→Waiting→Exploreのような中間状態を経由する場合も、Explore突入時だけ発火する

---

4. Feature 2: Leash Pull Feedback

4.1 Concept
Rovyがプレイヤーより前方にいて、リード長に近づいた時、プレイヤーが「引っ張られている」感覚を持てるようにする。実装は以下の2層:

- **物理層**: Playerの CharacterController にわずかな引っ張り速度を与える
- **視覚層**: LineRendererのリードを視覚的に緊張させる(色変化 or 揺れ)

4.2 Scripts
Assets/Scripts/Rovy/
└── LeashSystem.cs            // 既存、拡張

Assets/Scripts/Player/
└── PlayerController.cs        // 既存、最小改変

4.3 LeashSystem.cs への追加

**新規フィールド**:
```
- SerializeField:
    - pullStartRatio: float (default 0.8f)    // リード長のこの割合以上でPullが働く
    - maxPullSpeed: float (default 0.4f)      // 最大引っ張り速度 [m/s]
    - tautColor: Color (default Color.gray)   // 緊張時の色
    - slackColor: Color (default new Color(0.7, 0.7, 0.7, 0.8))  // たるみ時の色
    - rovyController: RovyController (任意参照、Exploringの時だけPullを強くする)
```

**新規public API**:
```
public Vector3 CurrentPullVector { get; private set; }
  戻り値: プレイヤーが次フレームで受けるべき引っ張り量(world-space velocity)
  ゼロベクトルなら引っ張りなし
```

**計算ロジック (LateUpdateで毎フレーム)**:
```
- distance = Vector3.Distance(rovy, player)
- effectiveLeash = leashLength
- tautThreshold = effectiveLeash * pullStartRatio

- distance <= tautThreshold の場合:
    CurrentPullVector = Vector3.zero
    lineRenderer.color = slackColor
    return

- distance > tautThreshold の場合:
    // 0 (tautThreshold) ～ 1 (effectiveLeash以上) に正規化
    tautness = Clamp01((distance - tautThreshold) / (effectiveLeash - tautThreshold))
    
    // 方向: プレイヤー → Rovy の水平成分
    pullDir = (rovy.position - player.position)
    pullDir.y = 0
    pullDir.Normalize()
    
    // 強度: Rovyが Exploring 状態なら1.5倍
    multiplier = (rovyController != null && rovyController.CurrentState == Exploring) ? 1.5f : 1.0f
    
    CurrentPullVector = pullDir * maxPullSpeed * tautness * multiplier
    
    // 視覚的な色変化
    lineRenderer.color = Color.Lerp(slackColor, tautColor, tautness)
```

4.4 PlayerController.cs への最小改変

**追加**: `[SerializeField] private LeashSystem leashSystem;`

**Update() の move 計算に pullVelocity を加算**:
```
moveDirection = GetCameraRelativeMoveDirection(input)
Vector3 leashPull = leashSystem != null ? leashSystem.CurrentPullVector : Vector3.zero
Vector3 finalMove = moveDirection * moveSpeed + leashPull

characterController.Move(finalMove * Time.deltaTime)
```

**注意**: 既存のgravity処理は維持する。pullVelocityは水平成分のみで、垂直成分(重力)には干渉させない。

4.5 Acceptance Criteria
- Rovyがリード長の80%以上離れると、プレイヤーがゆっくりRovy方向に引かれる
- Rovy が Exploring 状態のときは引っ張りが強い (1.5倍)
- リードの色がたるみ時は薄く、緊張時は濃くなる
- プレイヤー操作を完全に奪わない(maxPullSpeed=0.4で、歩行速度1.2の1/3以下)

---

5. Feature 3: Favorite Route Reaction

5.1 Concept
RouteMemoryが「お気に入り」と判定しているwaypoint(7日以上前から6回以上訪問)にRovyが到達した時、以下を発火:
- Wag アニメーション (BehaviorExpressions 経由)
- UI通知: 「Rovyのお気に入りの場所」
- 発火位置を記録し、短時間の再発火は抑制

5.2 Scripts
Assets/Scripts/Rovy/
├── RouteMemory.cs                  // 既存、public API を追加
└── FavoriteRouteReaction.cs         // 新規

Assets/Scripts/UI/
└── LeadingNotificationUI.cs         // Feature 1 と共用(メッセージ種別を追加)

5.3 RouteMemory.cs への追加

**新規 public API**:
```
public bool TryGetNearestFavoriteWaypoint(Vector3 worldPos, float maxDistance, out Vector3 waypoint)
  引数:
    worldPos: 検索中心
    maxDistance: 近傍判定距離
  処理:
    knownNodes を走査し、IsFavoriteRoute(key, node) == true で
    worldPos から maxDistance 以内にある waypoint を探す
    複数あれば最も近いもの
  戻り値:
    見つかれば true, waypoint に座標をセット
    なければ false
```

既存の IsFavoriteRoute は private なので、**このメソッド経由でのみ外部から参照**できる。

5.4 FavoriteRouteReaction.cs (新規)

**責務**: Rovyの現在位置を監視し、お気に入りwaypointに近づいた瞬間を検出してリアクションを発火する。

**主要ロジック**:
```
- SerializeField:
    - routeMemory: RouteMemory
    - behaviorExpressions: BehaviorExpressions
    - notificationUI: LeadingNotificationUI (任意)
    - detectionRadius: float (default 1.5f)
    - reactionCooldown: float (default 30.0f)
    - favoriteMessages: string[]
        default: {
            "Rovyのお気に入りの場所みたい",
            "ここ、よく来るね",
            "Rovyが嬉しそう"
        }

- 状態:
    lastReactedWaypoint: Vector3? (null可能)
    nextReactionTime: float

- Update():
    Time.time < nextReactionTime ならreturn
    
    routeMemory.TryGetNearestFavoriteWaypoint(transform.position, detectionRadius, out waypoint) がtrueなら:
        lastReactedWaypoint と同じなら return (同じ場所で連続発火しない)
        
        TriggerReaction(waypoint)
        lastReactedWaypoint = waypoint
        nextReactionTime = Time.time + reactionCooldown

- TriggerReaction(waypoint):
    behaviorExpressions.NotifyNameCalled()  // Wagをトリガ(既存API再利用)
    notificationUI にメッセージ表示を依頼(favoriteMessagesからランダム)
```

5.5 LeadingNotificationUI への追加
Feature 1 で作ったUIに、外部から任意メッセージを表示するpublic APIを追加:
```
public void ShowMessage(string message)
  引数のメッセージをShowRandomLeadingMessageと同じフェード挙動で表示
  cooldown制御は適用する
```

5.6 Scene Setup
- Rovy GameObject に FavoriteRouteReaction をアタッチ
- Inspector で routeMemory, behaviorExpressions, notificationUI をアサイン

5.7 Acceptance Criteria
- お気に入りwaypointから1.5m以内にRovyが入るとWagが発火する
- UIに「Rovyのお気に入りの場所みたい」等が表示される
- 同じwaypointでの連続発火は30秒間抑制される
- 別のお気に入りwaypointには即座に反応できる
- お気に入り条件(7日以上かつ6回以上)を満たしていないwaypointには反応しない

---

6. Feature 4: Learning Progress UI

6.1 Concept
Rovyがどれだけマップを「学習」したかを、常時表示の UI バーで可視化する。シミュレーションの進行に伴ってバーが伸びていくことで、プレイヤーは長期的な成長を実感できる。

6.2 計算方法
学習度は以下の2指標の合成とする:

```
exploreBreadth   = min(1.0, knownNodeCount / targetNodeCount)
                   範囲の広さ。default targetNodeCount = 200
routeMastery     = favoriteRouteCount / max(1, knownNodeCount)
                   お気に入り率 = 習熟度
learningProgress = 0.7 * exploreBreadth + 0.3 * routeMastery
```

意図:
- 最初はknownNodeCount = 0 なので progress = 0
- 歩き回るとknownNodeCountが増え、exploreBreadthが上がる (最大0.7に到達)
- 7日以上通い続けるとお気に入りが増え、routeMasteryが上がる (最大0.3が加算)

6.3 Scripts
Assets/Scripts/Rovy/
└── RouteMemory.cs                // 既存、public API追加

Assets/Scripts/UI/
└── LearningProgressUI.cs         // 新規

6.4 RouteMemory.cs への追加

**新規 public API**:
```
public int FavoriteRouteCount { get; }
  処理:
    knownNodes を走査し、IsFavoriteRoute(key, node) == true の個数を返す
    実装上は内部キャッシュ可(OnValidateやVisitCount更新時に再計算)

既存の KnownNodeCount は流用する。
```

6.5 LearningProgressUI.cs (新規)

**責務**: RouteMemoryの2指標を集計して進捗バーと数値をUIに表示する。

**主要ロジック**:
```
- SerializeField:
    - routeMemory: RouteMemory
    - progressBar: Slider (または Image fillAmount)
    - progressText: TextMeshProUGUI (例: "学習: 42%")
    - updateInterval: float (default 1.0f)
    - targetNodeCount: int (default 200)
    - breadthWeight: float (default 0.7f)
    - masteryWeight: float (default 0.3f)
    - smoothSpeed: float (default 2.0f)  // バーのイージング

- 状態:
    currentDisplayedProgress: float
    nextUpdateTime: float

- Update():
    Time.time >= nextUpdateTime のとき:
        RecalculateTargetProgress()
        nextUpdateTime = Time.time + updateInterval
    
    currentDisplayedProgress を targetProgress に向けて smoothSpeed で Lerp
    progressBar.value と progressText を更新

- RecalculateTargetProgress():
    int known = routeMemory.KnownNodeCount
    int favorites = routeMemory.FavoriteRouteCount
    
    float breadth = Mathf.Clamp01((float)known / targetNodeCount)
    float mastery = known > 0 ? (float)favorites / known : 0.0f
    mastery = Mathf.Clamp01(mastery)
    
    targetProgress = breadth * breadthWeight + mastery * masteryWeight

- Display format:
    progressText.text = $"学習: {Mathf.RoundToInt(currentDisplayedProgress * 100)}%"
```

6.6 Scene Setup
- Canvas配下に UI要素を新規作成:
    - 背景 Image (暗めの半透明 bar)
    - 前景 Image または Slider (progressBar)
    - TextMeshProUGUI (progressText)
- HUD (既存のCanvas階層) の隅に配置、HUDControllerと並ぶように
- LearningProgressUI をどこかにアタッチ(Canvas直下が自然)
- Inspectorでフィールドをアサイン

6.7 Acceptance Criteria
- Play開始直後は 0% 表示
- Rovyが歩き回るとプログレスが徐々に増える
- お気に入りルートができるとさらに伸びる
- 値変化は滑らか(瞬時ジャンプしない)
- 1秒に1回の再計算で十分軽量

---

7. Data Flow

```
RovyController.CurrentState ─────► LeadingNotificationUI (Feature 1)
                                          ▲
                                          │ public ShowMessage(string)
                                          │
Rovy Transform ─► FavoriteRouteReaction ──┘
                     │
                     ├─► BehaviorExpressions.NotifyNameCalled() [Wag]
                     │
                     └─► RouteMemory.TryGetNearestFavoriteWaypoint()

Rovy Transform + Player Transform ─► LeashSystem
                                          │
                                          │ CurrentPullVector
                                          ▼
                                     PlayerController.Move

RouteMemory.KnownNodeCount       ─┐
RouteMemory.FavoriteRouteCount   ─┴─► LearningProgressUI
```

---

8. Testing & Verification

8.1 Feature 1 確認手順
- Play開始
- Playerを動かさずRovyのEnergyを高く設定(MoodConfigのweatherWeight等を一時的に上げる)
- RovyがExploreに入った瞬間にメッセージが表示されるか確認
- 連続してExploreに入っても15秒以内は再表示されないことを確認

8.2 Feature 2 確認手順
- RovyをPlayerから遠い位置に強制配置
- Playerを動かさない
- Playerがゆっくり引っ張られる方向に動くことを確認
- リードの色が変化することを確認
- 距離を縮めると引っ張りが止まることを確認

8.3 Feature 3 確認手順(現実的な方法)
- RouteMemoryをContextMenuから手動で編集し、testウェイポイントを以下に設定:
    - visitCount = 10
    - firstSeenDay = currentSimulationDay - 8
    - 位置をRovyの近くに
- これでお気に入り判定がtrueになる
- Rovyがその位置に近づくとWag+通知が発火するか確認
- 同じ場所で再発火しないことを確認

8.4 Feature 4 確認手順
- Play開始直後: 0%を確認
- マップを歩き回って KnownNodeCount が増えることを確認
- UIバーが徐々に伸びることを確認
- MonthSimulatorでfast-forwardして、数日後にmasteryが上がることを確認

---

9. Implementation Order (Codex指示順)

Phase A (並列実装可能):
  └── LeadingNotificationUI.cs (Feature 1)
  └── LearningProgressUI.cs (Feature 4)
      ※ RouteMemory.FavoriteRouteCountの追加も含む

Phase B (Phase Aの完了後):
  └── LeashSystem.cs 拡張 + PlayerController.cs 最小改変 (Feature 2)

Phase C (Phase A完了後、Bとは並列可能):
  └── RouteMemory.TryGetNearestFavoriteWaypoint 追加
  └── FavoriteRouteReaction.cs (Feature 3)
      ※ LeadingNotificationUI.ShowMessage の追加も含む

---

10. File Summary

新規作成:
  Assets/Scripts/UI/LeadingNotificationUI.cs
  Assets/Scripts/UI/LearningProgressUI.cs
  Assets/Scripts/Rovy/FavoriteRouteReaction.cs

既存改変:
  Assets/Scripts/Rovy/LeashSystem.cs            (pull計算の追加)
  Assets/Scripts/Player/PlayerController.cs      (pullVelocityの加算)
  Assets/Scripts/Rovy/RouteMemory.cs             (public API 2件追加)

未改変で参照するのみ:
  Assets/Scripts/Rovy/RovyController.cs          (CurrentStateを読む)
  Assets/Scripts/Rovy/BehaviorExpressions.cs     (NotifyNameCalledを呼ぶ)
  Assets/Scripts/Data/MoodConfig.cs
  Assets/Scripts/Rovy/MoodEngine.cs