using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class MultiplayerRoomService : MonoBehaviour, IMultiplayerRoomService
    {
        private const string GameIdPropertyName = "GameId";
        private const string GameId = "LastJumpCrew";
        private const string SessionType = "LastJumpCrew.Room";
        private static readonly TimeSpan ServiceReadyTimeout = TimeSpan.FromSeconds(10);

        [SerializeField] private NetworkManager networkManager;
        [SerializeField, Min(1)] private int roomQueryLimit = 100;

        private IReadOnlyList<RoomSessionInfo> rooms = Array.Empty<RoomSessionInfo>();
        private ISession activeSession;
        private int activeMaxPlayers = 8;
        private bool servicesReady;
        private bool operationInProgress;
        private bool sessionEndExpected;

        public event Action RoomsChanged;
        public event Action SessionJoined;
        public event Action UnexpectedSessionEnded;
        public event Action<string> OperationFailed;

        public IReadOnlyList<RoomSessionInfo> Rooms => rooms;
        public string SessionCode => activeSession == null ? string.Empty : activeSession.Code;
        public string SessionName => activeSession == null ? string.Empty : activeSession.Name;
        public bool IsHost => activeSession != null && activeSession.IsHost;
        public bool IsActive => activeSession != null;

        public async Task<bool> InitializeAsync()
        {
            if (servicesReady)
            {
                return true;
            }

            if (!TryBeginOperation("initialize"))
            {
                return false;
            }

            try
            {
                return await InitializeServicesAsync();
            }
            finally
            {
                operationInProgress = false;
            }
        }

        public async Task<bool> RefreshRoomsAsync()
        {
            if (!TryBeginOperation("refresh_rooms"))
            {
                return false;
            }

            try
            {
                if (!await InitializeServicesAsync())
                {
                    return false;
                }

                var options = new QuerySessionsOptions
                {
                    Count = Mathf.Clamp(roomQueryLimit, 1, 100),
                    FilterOptions = new List<FilterOption>
                    {
                        new(FilterField.StringIndex1, GameId, FilterOperation.Equal),
                    },
                };
                var results = await MultiplayerService.Instance.QuerySessionsAsync(options);
                var refreshedRooms = new List<RoomSessionInfo>(results.Sessions.Count);
                foreach (var session in results.Sessions)
                {
                    refreshedRooms.Add(new RoomSessionInfo(
                        session.Id,
                        session.Name,
                        session.MaxPlayers - session.AvailableSlots,
                        session.MaxPlayers,
                        session.HasPassword));
                }

                rooms = new ReadOnlyCollection<RoomSessionInfo>(refreshedRooms);
                RoomsChanged?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                ReportFailure("refresh_rooms", exception);
                return false;
            }
            finally
            {
                operationInProgress = false;
            }
        }

        public async Task<bool> CreateRoomAsync(string roomName, int maxPlayers, string password)
        {
            if (!TryBeginOperation("create_room"))
            {
                return false;
            }

            try
            {
                if (!await InitializeServicesAsync()
                    || !ValidateNetworkManager())
                {
                    return false;
                }

                roomName = ResolveRoomName(roomName);
                if (!ValidateCreateRequest(roomName, maxPlayers, password))
                {
                    return false;
                }

                activeMaxPlayers = maxPlayers;
                ConfigureConnectionApproval();

                var options = new SessionOptions
                {
                    Type = SessionType,
                    Name = roomName.Trim(),
                    MaxPlayers = maxPlayers,
                    IsPrivate = false,
                    Password = NormalizePassword(password),
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        {
                            GameIdPropertyName,
                            new SessionProperty(
                                GameId,
                                VisibilityPropertyOptions.Public,
                                PropertyIndex.String1)
                        },
                    },
                }.WithRelayNetwork();

                var session = await MultiplayerService.Instance.CreateSessionAsync(options);
                SetActiveSession(session);
                Debug.Log($"PHS_ROOM_CREATE_OK sessionId={session.Id} name={session.Name} players={session.PlayerCount}/{session.MaxPlayers} password={session.HasPassword}");
                SessionJoined?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                RemoveConnectionApproval();
                ReportFailure("create_room", exception);
                return false;
            }
            finally
            {
                operationInProgress = false;
            }
        }

        public async Task<bool> JoinRoomAsync(string sessionId, string password)
        {
            return await JoinRoomInternalAsync(sessionId, password, false);
        }

        public async Task<bool> JoinRoomByCodeAsync(string sessionCode, string password)
        {
            return await JoinRoomInternalAsync(sessionCode, password, true);
        }

        private async Task<bool> JoinRoomInternalAsync(string sessionIdentifier, string password, bool joinByCode)
        {
            if (!TryBeginOperation("join_room"))
            {
                return false;
            }

            try
            {
                if (activeSession != null)
                {
                    ReportFailure("join_room", "active_session_exists");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(sessionIdentifier))
                {
                    ReportFailure("join_room", joinByCode ? "session_code_empty" : "session_id_empty");
                    return false;
                }

                if (!ValidatePassword(password)
                    || !await InitializeServicesAsync()
                    || !ValidateNetworkManager())
                {
                    return false;
                }

                var options = new JoinSessionOptions
                {
                    Type = SessionType,
                    Password = NormalizePassword(password),
                };
                var session = joinByCode
                    ? await MultiplayerService.Instance.JoinSessionByCodeAsync(sessionIdentifier.Trim(), options)
                    : await MultiplayerService.Instance.JoinSessionByIdAsync(sessionIdentifier.Trim(), options);
                SetActiveSession(session);
                Debug.Log($"PHS_ROOM_JOIN_OK sessionId={session.Id} name={session.Name} players={session.PlayerCount}/{session.MaxPlayers}");
                SessionJoined?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                ReportFailure("join_room", exception);
                return false;
            }
            finally
            {
                operationInProgress = false;
            }
        }

        public async Task<bool> LeaveRoomAsync()
        {
            if (!TryBeginOperation("leave_room"))
            {
                return false;
            }

            try
            {
                if (activeSession == null)
                {
                    ReportFailure("leave_room", "active_session_missing");
                    return false;
                }

                var leavingSession = activeSession;
                sessionEndExpected = true;
                if (leavingSession.IsHost)
                {
                    await leavingSession.AsHost().DeleteAsync();
                }
                else
                {
                    await leavingSession.LeaveAsync();
                }

                ClearActiveSession(leavingSession);
                Debug.Log($"PHS_ROOM_LEAVE_OK sessionId={leavingSession.Id} wasHost={leavingSession.IsHost}");
                return true;
            }
            catch (Exception exception)
            {
                ReportFailure("leave_room", exception);
                return false;
            }
            finally
            {
                sessionEndExpected = false;
                operationInProgress = false;
            }
        }

        private async Task<bool> InitializeServicesAsync()
        {
            if (servicesReady)
            {
                return true;
            }

            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync(BuildInitializationOptions());
                }
                else if (UnityServices.State == ServicesInitializationState.Initializing)
                {
                    await WaitForServicesInitializationAsync();
                }

                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    ReportFailure("initialize", $"services_state_{UnityServices.State}");
                    return false;
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    try
                    {
                        await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    }
                    catch (AuthenticationException exception)
                        when (exception.ErrorCode == AuthenticationErrorCodes.ClientInvalidUserState)
                    {
                        if (!await WaitForConcurrentAuthenticationAsync())
                        {
                            throw;
                        }

                        Debug.Log("PHS_ROOM_AUTH_CONCURRENT_SIGN_IN_RECOVERED");
                    }
                }

                servicesReady = AuthenticationService.Instance.IsSignedIn;
                if (!servicesReady)
                {
                    ReportFailure("initialize", "authentication_not_signed_in");
                    return false;
                }

                Debug.Log($"PHS_ROOM_SERVICE_READY playerId={AuthenticationService.Instance.PlayerId}");
                return true;
            }
            catch (Exception exception)
            {
                ReportFailure("initialize", exception);
                return false;
            }
        }

        private static async Task WaitForServicesInitializationAsync()
        {
            var deadline = DateTime.UtcNow + ServiceReadyTimeout;
            while (UnityServices.State == ServicesInitializationState.Initializing
                   && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }
        }

        private static async Task<bool> WaitForConcurrentAuthenticationAsync()
        {
            var deadline = DateTime.UtcNow + ServiceReadyTimeout;
            while (!AuthenticationService.Instance.IsSignedIn
                   && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }

            return AuthenticationService.Instance.IsSignedIn;
        }

        private bool ValidateCreateRequest(string roomName, int maxPlayers, string password)
        {
            if (activeSession != null)
            {
                ReportFailure("create_room", "active_session_exists");
                return false;
            }

            if (string.IsNullOrWhiteSpace(roomName))
            {
                ReportFailure("create_room", "room_name_empty");
                return false;
            }

            if (maxPlayers < 1)
            {
                ReportFailure("create_room", $"invalid_max_players_{maxPlayers}");
                return false;
            }

            return ValidatePassword(password);
        }

        private static string ResolveRoomName(string roomName)
        {
            if (!string.IsNullOrWhiteSpace(roomName))
            {
                return roomName.Trim();
            }

            return $"Host-{UnityEngine.Random.Range(100000, 1000000)}";
        }

        private bool ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return true;
            }

            if (password.Length >= 8 && password.Length <= 12)
            {
                return true;
            }

            ReportFailure("validate_password", $"invalid_password_length_{password.Length}");
            return false;
        }

        private bool ValidateNetworkManager()
        {
            if (networkManager == null)
            {
                ReportFailure("network_setup", "network_manager_reference_missing");
                return false;
            }

            if (NetworkManager.Singleton != networkManager)
            {
                ReportFailure("network_setup", "network_manager_singleton_mismatch");
                return false;
            }

            if (networkManager.IsListening)
            {
                ReportFailure("network_setup", "network_manager_already_listening");
                return false;
            }

            return true;
        }

        private void ConfigureConnectionApproval()
        {
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback -= ApproveConnection;
            networkManager.ConnectionApprovalCallback += ApproveConnection;
        }

        private void RemoveConnectionApproval()
        {
            if (networkManager != null)
            {
                networkManager.ConnectionApprovalCallback -= ApproveConnection;
            }
        }

        private void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            var connectedCount = networkManager == null ? 0 : networkManager.ConnectedClientsIds.Count;
            response.Approved = connectedCount < activeMaxPlayers;
            response.CreatePlayerObject = response.Approved;
            response.Pending = false;
            response.Reason = response.Approved ? string.Empty : "Session is full.";
        }

        private void SetActiveSession(ISession session)
        {
            if (activeSession != null)
            {
                UnsubscribeSession(activeSession);
            }

            activeSession = session;
            activeSession.Deleted += HandleSessionEnded;
            activeSession.RemovedFromSession += HandleSessionEnded;
        }

        private void ClearActiveSession(ISession expectedSession)
        {
            if (activeSession != expectedSession)
            {
                return;
            }

            UnsubscribeSession(activeSession);
            activeSession = null;
            RemoveConnectionApproval();
        }

        private void UnsubscribeSession(ISession session)
        {
            session.Deleted -= HandleSessionEnded;
            session.RemovedFromSession -= HandleSessionEnded;
        }

        private void HandleSessionEnded()
        {
            if (activeSession == null)
            {
                return;
            }

            var endedSession = activeSession;
            ClearActiveSession(endedSession);
            if (sessionEndExpected)
            {
                return;
            }

            Debug.LogError($"PHS_ROOM_SESSION_ENDED sessionId={endedSession.Id}");
            UnexpectedSessionEnded?.Invoke();
            OperationFailed?.Invoke("session_ended");
        }

        private bool TryBeginOperation(string operation)
        {
            if (!operationInProgress)
            {
                operationInProgress = true;
                return true;
            }

            ReportFailure(operation, "operation_in_progress");
            return false;
        }

        private void ReportFailure(string operation, Exception exception)
        {
            var sessionError = exception is SessionException sessionException
                ? sessionException.Error.ToString()
                : "none";
            var message = $"{operation}_failed";
            Debug.LogError(
                $"PHS_ROOM_OPERATION_FAILED operation={operation} exception={exception.GetType().Name} sessionError={sessionError} message={exception.Message}");
            OperationFailed?.Invoke(message);
        }

        private void ReportFailure(string operation, string reason)
        {
            var message = $"{operation}_failed_{reason}";
            Debug.LogError($"PHS_ROOM_OPERATION_FAILED operation={operation} reason={reason}");
            OperationFailed?.Invoke(message);
        }

        private static string NormalizePassword(string password)
        {
            return string.IsNullOrEmpty(password) ? null : password;
        }

        private static InitializationOptions BuildInitializationOptions()
        {
            var options = new InitializationOptions();
            var profile = GetCommandLineValue(Environment.GetCommandLineArgs(), "-phsProfile");
            if (!string.IsNullOrWhiteSpace(profile))
            {
                options.SetProfile(profile);
                Debug.Log($"PHS_SERVICES_PROFILE profile={profile}");
            }

            return options;
        }

        private static string GetCommandLineValue(string[] args, string key)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }

        private void OnDestroy()
        {
            if (activeSession != null)
            {
                UnsubscribeSession(activeSession);
            }

            RemoveConnectionApproval();
        }
    }
}
