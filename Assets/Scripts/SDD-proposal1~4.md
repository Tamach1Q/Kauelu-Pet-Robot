SDD: Rovy — Pet Robot Walk Simulator
Version 0.1 | Unity 6000.3.3f1 | URP

1. Overview
1.1 Purpose
Rovyは高齢者の外出習慣を「関係性」によって支える屋外散歩型ペットロボットである。本SDDはそのUnityシミュレーターの設計を定義する。
1.2 Simulation Goal
購入日から1ヶ月間のRovyとユーザーの散歩体験を通しで再現する。ロボットの感情・学習・行動変容が正確に検証できることを最優先とする。
1.3 Out of Scope

スマートフォンアプリ連携
実機ロボットへのデプロイ
マルチプレイヤー

1.4 Development Phases
Phase内容目標P1基本移動・リード・カメラRovyがユーザーについて歩くP2感情システム・グラフUI4変数で感情が変化するP3LiDARライク学習・経路生成環境を認識して道を覚えるP4長期シミュレーション1ヶ月分の散歩を連続再生

2. Scene Structure
2.1 Hierarchy
Scene: WalkSimulation
│
├── [Managers]                    // 空のGameObject、管理系をまとめる
│   ├── GameManager
│   ├── TimeManager
│   ├── WeatherManager
│   └── SimulationLogger
│
├── [Environment]
│   ├── SkyboxController
│   ├── DirectionalLight
│   ├── RoadNetwork             // 道路メッシュ群
│   │   ├── Sidewalk_A
│   │   ├── Crosswalk_01
│   │   └── Park_Path
│   └── Props                   // ベンチ・電柱など
│
├── [Characters]
│   ├── Player                  // ユーザー（高齢者）
│   │   ├── PlayerController
│   │   └── PlayerInteraction   // 名前を呼ぶ入力など
│   └── Rovy                    // ペットロボット本体
│       ├── RovyController
│       ├── MoodEngine
│       ├── LeashSystem
│       ├── LiDARScanner
│       ├── RouteMemory
│       └── BehaviorExpressions
│
├── [Camera]
│   └── FollowCamera
│
└── [UI]
    ├── HUD                     // 散歩距離・時間
    ├── MoodPanel               // 感情グラフ
    └── SimulationPanel         // 日付・天気表示
2.2 使用するUnityコンポーネント一覧
オブジェクトコンポーネント用途RovyNavMeshAgent自律移動・障害物回避RovyAnimator歩く・止まる・振り返るアニメRovyRigidbody物理演算（坂道対応）RovyAudioSource鳴き声・足音PlayerCharacterControllerキー入力移動LeashSystemLineRendererリードの視覚表現LiDARScannerPhysics.RaycastAll周囲の環境スキャンRoadNetworkNavMeshSurface歩行可能領域の定義MoodPanelUI Toolkit / Graph感情グラフ描画WeatherManagerVolume (URP)天気による画面効果

3. Script Architecture
3.1 スクリプト一覧
Assets/Scripts/
│
├── Core/
│   ├── GameManager.cs          // ゲーム全体の状態管理
│   ├── TimeManager.cs          // シミュレーション時間（1日単位）
│   └── EventBus.cs             // スクリプト間イベント通信
│
├── Environment/
│   ├── WeatherManager.cs       // 天気・気温の生成
│   └── RoadNetworkBuilder.cs   // 道路の動的生成補助
│
├── Rovy/
│   ├── RovyController.cs       // 移動・状態マシン（メインクラス）
│   ├── MoodEngine.cs           // 感情パラメータ計算
│   ├── LeashSystem.cs          // リード物理・LineRenderer
│   ├── LiDARScanner.cs         // Raycastによる環境認識
│   ├── RouteMemory.cs          // 学習済み経路の記憶・選択
│   └── BehaviorExpressions.cs  // 愛着行動（立ち止まる等）
│
├── Player/
│   ├── PlayerController.cs     // キー入力・移動
│   └── PlayerInteraction.cs    // 名前呼びかけ入力
│
├── Simulation/
│   ├── SimulationLogger.cs     // 散歩履歴の記録
│   └── MonthSimulator.cs       // 1ヶ月分の自動再生
│
├── UI/
│   ├── MoodGraphUI.cs          // 感情グラフ描画
│   └── HUDController.cs        // 距離・時間表示
│
└── Data/
    ├── RovyProfile.cs          // ScriptableObject: 犬の性格定義
    ├── WalkRecord.cs           // ScriptableObject: 散歩記録
    └── MoodConfig.cs           // ScriptableObject: 感情パラメータ設定

4. Core Systems Detail
4.1 MoodEngine — 感情システム
概念図
[天気]──────────┐
[気温]──────────┤──→ [MoodEngine] ──→ energy / curiosity / comfort
[散歩履歴]──────┤              ↓
[呼びかけ回数]──┘         [MoodGraphUI]（リアルタイムグラフ）
                                ↓
                         [RouteGenerator]（経路選択に反映）
ScriptableObject: MoodConfig
// Assets/Data/MoodConfig.asset
// Inspectorから数値を調整可能にする

weatherWeight      : float   // 天気の影響度
temperatureWeight  : float   // 気温の影響度
walkHistoryWeight  : float   // 散歩履歴の影響度
nameCallWeight     : float   // 呼びかけ回数の影響度
walkHistoryThreshold: int    // 何回以上でマイナスになるか
計算ロジック（MoodEngine.cs）
energy    = f(weather, temperature)
curiosity = f(nameCallCount, walkHistory)
comfort   = f(temperature, walkHistory)

// 各weightはMoodConfigから読み込み
// → weightを変えるだけで挙動が変わる設計
グラフUI（MoodGraphUI.cs）

UI ToolkitのVisualElementでリアルタイム折れ線グラフを描画
4変数それぞれの貢献度を色分けで表示
Inspector上でも確認できるよう[SerializeField]で公開


4.2 LiDARScanner — 環境学習システム
概念
本物のLiDARと同様に、Rovyを中心に複数方向へRayを飛ばし、ヒット情報から周囲の地形を把握する。走行しながらデータを蓄積し、安全な経路を学習する。
// 水平360度 × 垂直複数段
// 1フレームにN本のRayを発射（負荷調整可能）

foreach ray in scanPattern:
    if hit:
        hitMap[position] = obstacleType  // 壁・段差・道路を分類
        RouteMemory.UpdateMap(hit)
使用するUnity機能

Physics.RaycastAll — 複数Rayの同時処理
LayerMask — 道路・障害物・段差を別レイヤーで管理
Gizmos.DrawLine — Editorでスキャン範囲を可視化（デバッグ用）


4.3 RouteMemory — 経路記憶・選択
概念
LiDARスキャンで得た情報 + MoodEngineの感情を組み合わせて経路を選択する。
// WaypointノードにMoodスコアを付与
// energyが高い  → 遠いノードを好む
// comfortが低い → 平坦・短いノードを好む
// curiosityが高い → 未探索ノードを優先

score = (energy × distance) 
      - (1 - comfort) × difficulty
      + curiosity × novelty
学習の仕組み

初日：全経路が未探索、短いルートのみ
1週間後：よく歩いたルートのスコアが上昇
1ヶ月後：天気・気分に応じた「お気に入りルート」が形成される


4.4 LeashSystem — リード
使用コンポーネント

LineRenderer — リードの視覚表現（曲線で描画）
SpringJoint — 物理的な引っ張り合いの表現

ロジック
distance = Vector3.Distance(rovy, player)

if distance > leashLength:
    // Rovyがプレイヤー方向へ引き戻す
    // または NavMeshAgentの速度を調整してついてくる

LineRenderer.SetPositions([rovyPos, midPoint, playerPos])
// midPointをサイン波で動かすとリードのたるみが表現できる

4.5 道路環境の再現
手法：ProBuilder + NavMeshSurface
[推奨ワークフロー]
1. ProBuilderで歩道・横断歩道・公園路を手作り
2. 各メッシュにNavMeshSurfaceをアタッチ
3. Bakeで歩行可能領域を定義
4. LayerMaskで道路種別を分類
   └── Layer: Sidewalk / Crosswalk / ParkPath / Obstacle

[将来的な拡張]
OpenStreetMapデータをインポートして
実際の神山町の道路を再現することも可能
地面の質感（URP）

アスファルト・砂利・土のマテリアルを用意
Rovyの足音をSurface別に切り替え


4.6 TimeManager — シミュレーション時間
SimulationTime:
  currentDay    : int      // 0〜30日
  currentHour   : float    // 0.0〜24.0
  timeScale     : float    // 1.0 = リアルタイム, 10.0 = 10倍速

// 1日の流れ
Morning(6-9)   → energy高め、curiosity高め
Afternoon(12-15) → comfort低下（暑さ）
Evening(17-19) → 散歩のゴールデンタイム

5. Data Flow
WeatherManager
    │ weather, temperature
    ▼
MoodEngine ◄── PlayerInteraction (nameCallCount)
    │              ◄── SimulationLogger (walkHistory)
    │ energy, curiosity, comfort
    ├──► RouteMemory (経路スコア計算)
    ├──► BehaviorExpressions (行動選択)
    └──► MoodGraphUI (グラフ描画)

RouteMemory ◄── LiDARScanner (環境マップ更新)
    │
    ▼
NavMeshAgent.SetDestination()

LeashSystem
    │ player位置 + rovy位置
    ▼
LineRenderer + SpringJoint

6. ScriptableObject 設計
Codexへの指示を明確にするため、データとロジックを分離する。
ファイル内容変更タイミングRovyProfile.asset性格（活発/おとなしい）、基本速度、リード長犬の個体差を試したいときMoodConfig.asset4変数のweight、閾値感情モデルを調整したいときWalkRecord.asset過去7日分の散歩距離・時間シミュレーションが記録

7. Phase実装順序（Codexへの指示順）
Phase 1（今すぐ）
  └── PlayerController.cs
  └── RovyController.cs（NavMeshAgent基本追従）
  └── LeashSystem.cs（LineRenderer）
  └── FollowCamera

Phase 2
  └── MoodConfig.asset（ScriptableObject）
  └── MoodEngine.cs
  └── WeatherManager.cs
  └── MoodGraphUI.cs

Phase 3
  └── LiDARScanner.cs
  └── RouteMemory.cs
  └── BehaviorExpressions.cs

Phase 4
  └── TimeManager.cs
  └── SimulationLogger.cs
  └── MonthSimulator.cs