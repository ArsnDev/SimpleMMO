using UnityEngine;
using SimpleMMO.Network;
using SimpleMMO.Game;
using CppMMO.Protocol;
using SimpleMMO.Protocol.Extensions;
using System.Collections.Generic;

namespace SimpleMMO.Managers
{
    public class WorldSyncManager : MonoBehaviour
    {
        [Header("Sync Settings")]
        [SerializeField] private float interpolationSpeed = 10f;
        [SerializeField] private bool enablePositionInterpolation = true;
        [SerializeField] private float teleportThreshold = 10f;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        public static WorldSyncManager Instance { get; private set; }
        
        private PlayerController localPlayer;
        
        private ulong lastTickNumber = 0;
        private float lastSyncTime = 0f;
        
        // Coroutine management
        private Coroutine currentInterpolation;
        
        // Subscription tracking
        private bool isSubscribed = false;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                Debug.Log("WorldSyncManager: Initialized");
            }
            else
            {
                Debug.LogWarning("WorldSyncManager: Multiple instances detected, destroying duplicate");
                Destroy(gameObject);
            }
        }

        void Start()
        {
            SubscribeToEvents();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                UnsubscribeFromEvents();
            }
        }

        private void SubscribeToEvents()
        {
            var gameClient = GameServerClient.Instance;
            if (gameClient != null && !isSubscribed)
            {
                gameClient.OnWorldSnapshot += OnWorldSnapshot;
                isSubscribed = true;
                LogDebug("Subscribed to WorldSnapshot events");
            }
            else if (gameClient == null)
            {
                Debug.LogWarning("WorldSyncManager: GameServerClient not available");
            }
        }

        private void UnsubscribeFromEvents()
        {
            var gameClient = GameServerClient.Instance;
            if (gameClient != null && isSubscribed)
            {
                gameClient.OnWorldSnapshot -= OnWorldSnapshot;
                isSubscribed = false;
                LogDebug("Unsubscribed from WorldSnapshot events");
            }
        }

        public void SetLocalPlayer(PlayerController player)
        {
            localPlayer = player;
            LogDebug($"Local player set: {player?.PlayerName}");
        }

        private void OnWorldSnapshot(S_WorldSnapshot snapshot)
        {
            if (snapshot.TickNumber <= lastTickNumber)
            {
                return;
            }

            lastTickNumber = snapshot.TickNumber;
            lastSyncTime = Time.time;

            LogDebug($"WorldSnapshot received: Tick {snapshot.TickNumber}, {snapshot.PlayerStatesLength} players");

            SyncPlayerStates(snapshot);

            ProcessGameEvents(snapshot);
        }

        private void SyncPlayerStates(S_WorldSnapshot snapshot)
        {
            if (localPlayer == null)
            {
                LogDebug("Local player not set, skipping sync");
                return;
            }

            for (int i = 0; i < snapshot.PlayerStatesLength; i++)
            {
                var playerState = snapshot.PlayerStates(i);
                if (playerState.HasValue)
                {
                    SyncPlayerState(playerState.Value);
                }
            }
        }

        private void SyncPlayerState(PlayerState playerState)
        {
            if (localPlayer != null && playerState.PlayerId == localPlayer.PlayerId)
            {
                SyncLocalPlayer(playerState);
            }
            else
            {
                // Sync other players (TODO: Move to MultiplayerManager)
                SyncRemotePlayer(playerState);
            }
        }

        private void SyncLocalPlayer(PlayerState playerState)
        {
            Vector3 serverPosition = playerState.Position?.ToUnityVector3() ?? Vector3.zero;
            Vector3 serverVelocity = playerState.Velocity?.ToUnityVector3() ?? Vector3.zero;

            if (enablePositionInterpolation)
            {
                // Stop any previous interpolation to prevent conflicts
                if (currentInterpolation != null)
                {
                    StopCoroutine(currentInterpolation);
                }
                
                // Start smooth position interpolation
                currentInterpolation = StartCoroutine(InterpolateToPosition(localPlayer, serverPosition));
            }
            else
            {
                localPlayer.UpdatePosition(serverPosition);
            }

            localPlayer.UpdateVelocity(serverVelocity);

            LogDebug($"Local player synced: {serverPosition}, velocity: {serverVelocity}");
        }

        private void SyncRemotePlayer(PlayerState playerState)
        {
            // TODO: Integrate with MultiplayerManager to sync other players
            LogDebug($"Remote player {playerState.PlayerId} at {playerState.Position?.ToUnityVector3()}");
        }

        private System.Collections.IEnumerator InterpolateToPosition(PlayerController player, Vector3 targetPosition)
        {
            Vector3 startPosition = player.transform.position;
            float journey = 0f;
            float distance = Vector3.Distance(startPosition, targetPosition);

            if (distance > teleportThreshold)
            {
                player.UpdatePosition(targetPosition);
                currentInterpolation = null; // Clear reference
                yield break;
            }

            while (journey <= 1f)
            {
                journey += Time.deltaTime * interpolationSpeed;
                Vector3 currentPosition = Vector3.Lerp(startPosition, targetPosition, journey);
                player.UpdatePosition(currentPosition);
                yield return null;
            }

            // Ensure final position accuracy
            player.UpdatePosition(targetPosition);
            currentInterpolation = null; // Clear reference when completed
        }

        private void ProcessGameEvents(S_WorldSnapshot snapshot)
        {
            if (snapshot.EventsLength > 0)
            {
                LogDebug($"Processing {snapshot.EventsLength} game events");

                for (int i = 0; i < snapshot.EventsLength; i++)
                {
                    var gameEvent = snapshot.Events(i);
                    if (gameEvent.HasValue)
                    {
                        ProcessGameEvent(gameEvent.Value);
                    }
                }
            }
        }

        private void ProcessGameEvent(GameEvent gameEvent)
        {
            switch (gameEvent.EventType)
            {
                case CppMMO.Protocol.EventType.PLAYER_DAMAGE:
                    LogDebug($"Player damage event: Source={gameEvent.SourcePlayerId}, Target={gameEvent.TargetPlayerId}, Value={gameEvent.Value}");
                    // TODO: Process damage effects
                    break;

                case CppMMO.Protocol.EventType.PLAYER_HEAL:
                    LogDebug($"Player heal event: Source={gameEvent.SourcePlayerId}, Target={gameEvent.TargetPlayerId}, Value={gameEvent.Value}");
                    // TODO: Process heal effects
                    break;

                case CppMMO.Protocol.EventType.PLAYER_DEATH:
                    LogDebug($"Player death event: Source={gameEvent.SourcePlayerId}, Target={gameEvent.TargetPlayerId}");
                    // TODO: Process death effects
                    break;

                case CppMMO.Protocol.EventType.PLAYER_RESPAWN:
                    LogDebug($"Player respawn event: PlayerId={gameEvent.SourcePlayerId}, Position={gameEvent.Position?.ToUnityVector3()}");
                    // TODO: Process respawn effects
                    break;

                case CppMMO.Protocol.EventType.NONE:
                default:
                    LogDebug($"Event type: {gameEvent.EventType}, Source={gameEvent.SourcePlayerId}, Target={gameEvent.TargetPlayerId}");
                    break;
            }
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[WorldSyncManager] {message}");
            }
        }

        #region Public API

        public bool IsReceivingUpdates => Time.time - lastSyncTime < 1f; // Check if updates received within 1 second
        public ulong LastTickNumber => lastTickNumber;
        public float LastSyncTime => lastSyncTime;

        public void EnableDebugLogs(bool enable)
        {
            enableDebugLogs = enable;
        }

        public void SetInterpolationSpeed(float speed)
        {
            interpolationSpeed = Mathf.Clamp(speed, 1f, 50f);
        }

        /// <summary>
        /// Retry subscription to GameServerClient events if it becomes available after Start()
        /// </summary>
        public void RetrySubscription()
        {
            if (GameServerClient.Instance != null && !isSubscribed)
            {
                SubscribeToEvents();
            }
        }

        #endregion
    }
}