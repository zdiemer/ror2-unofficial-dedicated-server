using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using RoR2;
using RoR2.Networking;
using UnityEngine;
using UnityEngine.Networking;

namespace Ror2UnofficialDedicatedServer
{
    [BepInPlugin("com.zdiemer.ror2.unofficialdedicatedserver", "RoR2 Unofficial Dedicated Server", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private Harmony harmony;
        private ConfigEntry<int> port;
        private ConfigEntry<int> maxPlayers;
        private ConfigEntry<float> gameOverReturnDelay;
        private float nextStatusTime;
        private float nextDisconnectCheck;
        private Coroutine returnToLobby;

        internal static Plugin Instance { get; private set; }

        private void Awake()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-server") < 0)
            {
                Logger.LogInfo("Dedicated server mode is inactive; add -server to enable it.");
                return;
            }

            Instance = this;
            port = Config.Bind("Server", "Port", 7777, "UDP listen port (1-65535).");
            maxPlayers = Config.Bind("Server", "MaxPlayers", 4, "Maximum remote players.");
            gameOverReturnDelay = Config.Bind("Server", "GameOverReturnDelaySeconds", 20f, "Delay before returning players to the lobby after game over.");
            if (port.Value < 1 || port.Value > 65535 || maxPlayers.Value < 1 || maxPlayers.Value > RoR2Application.hardMaxPlayers || gameOverReturnDelay.Value < 0f)
            {
                Logger.LogError("Invalid Port or MaxPlayers; server mode was not started.");
                Instance = null;
                return;
            }

            harmony = new Harmony("com.zdiemer.ror2.unofficialdedicatedserver");
            harmony.PatchAll(typeof(Plugin).Assembly);
            RoR2Application.onLoadFinished += StartDedicatedServer;
            Run.onServerGameOver += OnServerGameOver;
            Logger.LogInfo("Dedicated server mode armed.");
        }

        private void OnDestroy()
        {
            RoR2Application.onLoadFinished -= StartDedicatedServer;
            Run.onServerGameOver -= OnServerGameOver;
            if (returnToLobby != null) StopCoroutine(returnToLobby);
            harmony?.UnpatchSelf();
            Instance = null;
        }

        private void Update()
        {
            if (Instance != this) return;
            if (Time.unscaledTime >= nextDisconnectCheck)
            {
                nextDisconnectCheck = Time.unscaledTime + 5f;
                if (NetworkServer.active && Run.instance && NetworkUser.readOnlyInstancesList.Count == 0 && returnToLobby == null)
                {
                    Logger.LogInfo("No players remain in the run; returning to lobby.");
                    returnToLobby = StartCoroutine(ReturnToLobby(Run.instance, 0f));
                }
            }
            if (Time.unscaledTime < nextStatusTime) return;
            nextStatusTime = Time.unscaledTime + 15f;
            var manager = PlatformSystems.networkManager;
            Logger.LogInfo($"Status: loaded={RoR2Application.loadFinished}, initialized={SystemInitializerAttribute.hasExecuted}, manager={((bool)manager)}, server={NetworkServer.active}, localUser={LocalUserManager.isAnyUserSignedIn}");
        }

        private void OnServerGameOver(Run run, GameEndingDef ending)
        {
            if (Instance != this || !NetworkServer.active || returnToLobby != null) return;
            Logger.LogInfo($"Game over ({ending.cachedName}); returning to lobby in {gameOverReturnDelay.Value} seconds.");
            returnToLobby = StartCoroutine(ReturnToLobby(run, gameOverReturnDelay.Value));
        }

        private IEnumerator ReturnToLobby(Run run, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (NetworkServer.active && Run.instance == run && PlatformSystems.networkManager)
            {
                PlatformSystems.networkManager.ServerChangeScene("lobby");
            }
            returnToLobby = null;
        }

        private void StartDedicatedServer()
        {
            if (Instance != this) return;
            var manager = PlatformSystems.networkManager;
            if (!manager)
            {
                Logger.LogError("Network manager was not initialized.");
                return;
            }
            if (NetworkServer.active || LocalUserManager.isAnyUserSignedIn)
            {
                Logger.LogError("Expected an inactive server with no signed-in local users.");
                return;
            }

            manager.networkPort = port.Value;
            manager.maxConnections = maxPlayers.Value;
            NetworkServer.dontListen = false;
            manager.desiredHost = new HostDescription(new HostDescription.HostingParameters
            {
                listen = true,
                maxPlayers = maxPlayers.Value
            });
            Logger.LogInfo($"Requested dedicated UDP server on port {port.Value} for {maxPlayers.Value} players.");
        }

        [HarmonyPatch(typeof(PlatformSystems), nameof(PlatformSystems.Init))]
        private static class PlatformInitPatch
        {
            private static void Postfix()
            {
                if (Instance == null) return;
                // The game uses this delegate's result to decide whether to show a fatal Steam dialog.
                // A dedicated IP server does not need a desktop Steam client.
                RoR2Application.loadSteamworksClient = () => true;
            }
        }

        [HarmonyPatch(typeof(NetworkManagerSystemSteam), nameof(NetworkManagerSystemSteam.InitPlatformServer))]
        private static class SteamServerPatch
        {
            private static bool Prefix()
            {
                // UNet still starts and binds the UDP socket in NetworkManager.StartServer.
                // The Steam game-server API and its ticket validation require Steam services.
                return Instance == null;
            }
        }

        [HarmonyPatch(typeof(BlockObjectOnLoad), "Start")]
        private static class BlockObjectOnLoadPatch
        {
            private static bool Prefix()
            {
                // This UI helper registers a callback against an object destroyed during
                // headless loading; its callback aborts the game's startup coroutine.
                return Instance == null;
            }
        }
    }
}
