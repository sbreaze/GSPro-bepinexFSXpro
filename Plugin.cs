using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using Foresight.ShotProcessing;
using FSXLiveClient.Models;
using Foresight.Flight;
using Foresight.SDK;
using FSXLiveClient;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Api;
using Newtonsoft.Json;
using System;
using UnityEngine;
using Foresight.UiManagment.Pages;
using Foresight.Components.UI;
using Foresight.UiManagment;
using System.Collections.Generic;
using Foresight.Licensing;

namespace bepinexFSXpro
{

	//TODO
	/*
	 * Canvas-InformationPopup false
	 * Canvas-PLMDeviceDisconnectPopup false
	 * Canvas-LicenseActivation false
	 * Canvas-MainMenu false
	 * Canvas-DefaultLayout true
	 * 
	 */


	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin
	{
		internal static new ManualLogSource Log;
		public static ConfigEntry<bool> waitForClubData;
		public static ConfigEntry<bool> autoConnectPLM;

		private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony("com.fs.interface"); // rename "author" and "project"
            harmony.PatchAll();
            Plugin.Log = base.Logger;
			Thread tcpSocketThread = new Thread(ModPerformanceShot.ConnectToGSP);
			tcpSocketThread.Start();
			//ModPerformanceShot.ConnectToGSP();

			waitForClubData = Config.Bind("General.Toggles",
											   "Wait for ClubData",
											   false,
											   "Enable/Disable ClubData, introduces slight delay");

			autoConnectPLM = Config.Bind("General.Toggles",
														   "AutoConnect BLP",
														   false,
														   "Automatcally connect to BLP when opening app.");
		}

		[HarmonyPatch]
		public class ModPerformanceShot
		{

			[HarmonyPostfix]
			[HarmonyPatch(typeof(FSXLiveLicensingComponent), nameof(FSXLiveLicensingComponent.checkInternetConnection))]

			public static void Postfix4(FSXLiveLicensingComponent __instance)
			{
				if (!Plugin.autoConnectPLM.Value) return;
				Plugin.Log.LogInfo("Connecting to PLM!");
				__instance.ConnectToPLMDevice();
			}

			/* constructor patch
			//Patch: Putting (no spin) TriggerOnShotTaken
			[HarmonyPostfix]
			[HarmonyPatch(typeof(PerformanceShot), "PerformanceShot")]
			[HarmonyPatch(MethodType.Constructor, new Type[] { typeof(IShot) })]
			public static void Postfix1(IShot __instance)
			*/

			[HarmonyPostfix]
			[HarmonyPatch(typeof(ShotProcessingSystem), nameof(ShotProcessingSystem.TriggerOnShotTaken))]
			public static void Postfix5(ref IShot shot)
			{
				if (Plugin.waitForClubData.Value) return;
				Plugin.Log.LogInfo("Shot Detected!");
				Plugin.Log.LogInfo($"SpinProcessed: {shot.SpinProcessed}");
				Plugin.Log.LogInfo($"BallSpeed: {shot.BallData.BallSpeed_MS * 2.23694f}");
				Plugin.Log.LogInfo($"VLA: {shot.BallData.Elevation_DEG}");
				Plugin.Log.LogInfo($"HLA: {shot.BallData.Azimuth_DEG}");
				Plugin.Log.LogInfo($"TotalSpin: {shot.BallData.TotalSpin_RPM}");
				Plugin.Log.LogInfo($"SpinAxis: {shot.SpinAxisDegrees}");

				if (shot.SpinProcessed == true || (shot.BallData.Elevation_DEG < 5 && shot.BallData.TotalSpin_RPM == 0 && shot.SpinAxisDegrees == 0))
				{
					GSPShotData gSPShotData = new GSPShotData();
					gSPShotData.DeviceID = "FSX Pro";
					gSPShotData.Units = "Yards";
					gSPShotData.ShotNumber = (int)shot.ShotDeviceNumber;
					gSPShotData.APIversion = "1";
					gSPShotData.BallData = new GSPBallData
					{
						Speed = shot.BallData.BallSpeed_MS * 2.23694f,
						SpinAxis = shot.SpinAxisDegrees,
						TotalSpin = shot.BallData.TotalSpin_RPM,
						BackSpin = 0f,
						SideSpin = 0f,
						HLA = shot.BallData.Azimuth_DEG,
						VLA = shot.BallData.Elevation_DEG,
						CarryDistance = 0f
					};
		
					gSPShotData.ShotDataOptions = new GSPShotDataOptions
					{
						ContainsBallData = true,
						ContainsClubData = false,
						LaunchMonitorIsReady = false,
						LaunchMonitorBallDetected = false,
						IsHeartBeat = false
					};
					try
					{
						SendToGSP(Newtonsoft.Json.JsonConvert.SerializeObject(gSPShotData));
					}
					catch (System.Exception ex)
					{
						Plugin.Log.LogInfo("Error: " + ex);
					}
				}
			}


			//Patch shots with club data
			[HarmonyPostfix]
			[HarmonyPatch(typeof(ShotProcessingSystem), nameof(ShotProcessingSystem.TriggerOnClubProcessed))]
			public static void Postfix2(ref IShot shot)
			{
				if (!Plugin.waitForClubData.Value) return;
				Plugin.Log.LogInfo("Shot Detected!");
				Plugin.Log.LogInfo($"SpinProcessed: {shot.SpinProcessed}");
				Plugin.Log.LogInfo($"BallSpeed: {shot.BallData.BallSpeed_MS * 2.23694f}");
				Plugin.Log.LogInfo($"VLA: {shot.BallData.Elevation_DEG}");
				Plugin.Log.LogInfo($"HLA: {shot.BallData.Azimuth_DEG}");
				Plugin.Log.LogInfo($"TotalSpin: {shot.BallData.TotalSpin_RPM}");
				Plugin.Log.LogInfo($"SpinAxis: {shot.SpinAxisDegrees}");
				Plugin.Log.LogInfo($"Carry: {shot.BallData.FlightCarryDistance_M * 1.09361f}");

				Plugin.Log.LogInfo("ClubProcessed: " + shot.ClubProcessed);
				if (shot.ClubProcessed)
				{
					Plugin.Log.LogInfo($"ClubHeadSpeed: {shot.ClubData.ClubSpeed_MS * 2.23694f}");
					Plugin.Log.LogInfo($"AoA: {shot.ClubData.AngleOfAttack_DEG}");
					Plugin.Log.LogInfo($"ClubPath: {shot.ClubData.ClubPath_DEG}");
					Plugin.Log.LogInfo($"FaceToPath: {shot.FaceToPath}");
				}

				if (shot.SpinProcessed == true || (shot.BallData.Elevation_DEG < 5 && shot.BallData.TotalSpin_RPM == 0 && shot.SpinAxisDegrees == 0))
				{
					GSPShotData gSPShotData = new GSPShotData();
					gSPShotData.DeviceID = "FSX Pro";
					gSPShotData.Units = "Yards";
					gSPShotData.ShotNumber = (int)shot.ShotDeviceNumber;
					gSPShotData.APIversion = "1";
					gSPShotData.BallData = new GSPBallData
					{
						Speed = shot.BallData.BallSpeed_MS * 2.23694f,
						SpinAxis = shot.SpinAxisDegrees,
						TotalSpin = shot.BallData.TotalSpin_RPM,
						BackSpin = 0f,
						SideSpin = 0f,
						HLA = shot.BallData.Azimuth_DEG,
						VLA = shot.BallData.Elevation_DEG,
						CarryDistance = shot.BallData.FlightCarryDistance_M * 1.09361f
					};
					if (shot.ClubProcessed)
						{
						gSPShotData.ClubData = new GSPClubData
						{
								Speed = shot.ClubData.ClubSpeed_MS * 2.23694f,
								AngleOfAttack = shot.ClubData.AngleOfAttack_DEG,
								Path = shot.ClubData.ClubPath_DEG
						};
						}
					gSPShotData.ShotDataOptions = new GSPShotDataOptions
					{
						ContainsBallData = true,
						ContainsClubData = (!Double.IsInfinity(shot.ClubData.ClubSpeed_MS) && shot.ClubData.AngleOfAttack_DEG < 360 && shot.ClubData.ClubPath_DEG < 360),
						LaunchMonitorIsReady = false,
						LaunchMonitorBallDetected = false,
						IsHeartBeat = false
					};
					try
                    {
						SendToGSP(Newtonsoft.Json.JsonConvert.SerializeObject(gSPShotData));
					}
					catch (System.Exception ex)
					{
						Plugin.Log.LogInfo("Error: " + ex);
					}
			}
			}

			//Patch shots with spin
			[HarmonyPostfix]
			[HarmonyPatch(typeof(ShotProcessingSystem), nameof(ShotProcessingSystem.TriggerOnTotalDistanceProcessed))]
			public static void Postfix3(ref IShot shot)
			{
				if (Plugin.waitForClubData.Value) return;
				
						Plugin.Log.LogInfo("Shot Detected!");
						Plugin.Log.LogInfo($"SpinProcessed: {shot.SpinProcessed}");
						Plugin.Log.LogInfo($"BallSpeed: {shot.BallData.BallSpeed_MS * 2.23694f}");
						Plugin.Log.LogInfo($"VLA: {shot.BallData.Elevation_DEG}");
						Plugin.Log.LogInfo($"HLA: {shot.BallData.Azimuth_DEG}");
						Plugin.Log.LogInfo($"TotalSpin: {shot.BallData.TotalSpin_RPM}");
						Plugin.Log.LogInfo($"SpinAxis: {shot.SpinAxisDegrees}");
						Plugin.Log.LogInfo($"Carry: {shot.BallData.FlightCarryDistance_M * 1.09361f}");

				if (shot.SpinProcessed == true || (shot.BallData.Elevation_DEG < 5 && shot.BallData.TotalSpin_RPM == 0 && shot.SpinAxisDegrees == 0))
					{
						GSPShotData gSPShotData = new GSPShotData();
						gSPShotData.DeviceID = "FSX Pro";
						gSPShotData.Units = "Yards";
						gSPShotData.ShotNumber = (int)shot.ShotDeviceNumber;
						gSPShotData.APIversion = "1";
						gSPShotData.BallData = new GSPBallData
						{
							Speed = shot.BallData.BallSpeed_MS * 2.23694f,
							SpinAxis = shot.SpinAxisDegrees,
							TotalSpin = shot.BallData.TotalSpin_RPM,
							BackSpin = 0f,
							SideSpin = 0f,
							HLA = shot.BallData.Azimuth_DEG,
							VLA = shot.BallData.Elevation_DEG,
							CarryDistance = shot.BallData.FlightCarryDistance_M * 1.09361f
						};
			
						gSPShotData.ShotDataOptions = new GSPShotDataOptions
						{
							ContainsBallData = true,
							ContainsClubData = false,
							LaunchMonitorIsReady = false,
							LaunchMonitorBallDetected = false,
							IsHeartBeat = false
						};
						try
						{
							SendToGSP(Newtonsoft.Json.JsonConvert.SerializeObject(gSPShotData));
						}
						catch (System.Exception ex)
						{
							Plugin.Log.LogInfo("Error: " + ex);
						}
					}
				
			}

			public static void SendToGSP(string data)
			{
				byte[] bytes = Encoding.ASCII.GetBytes(data);
				Plugin.Log.LogInfo(_GSPSocket.Send(bytes) + " Bytes Sent to GSPro OpenAPI");
			}
			public static void ConnectToGSP()
			{
		
				IPAddress iPAddress = IPAddress.Parse("127.0.0.1");
				IPEndPoint remoteEP = new IPEndPoint(iPAddress, 921);
				_GSPSocket = new Socket(iPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				while (!connected)
					try
					{
						_GSPSocket.Connect(remoteEP);
						connected = true;
						//GSProStatusLabel.Text = "Connected";
						_GSPSocket.BeginReceive(_GSPReadBuffer, 0, 1024, SocketFlags.None, new AsyncCallback(GSPReadCallback), null);
						GSPShotData gSPShotData = new GSPShotData();
						gSPShotData.DeviceID = "FSX Pro";
						gSPShotData.Units = "Yards";
						gSPShotData.ShotNumber = 0;
						gSPShotData.APIversion = "1";
						gSPShotData.ShotDataOptions = new GSPShotDataOptions
					{
							ContainsBallData = false,
							ContainsClubData = false,
							//LaunchMonitorIsReady = (_BallStatus.Status == StatusData.BallStatus.LockedOnBall),
							//LaunchMonitorBallDetected = (_BallStatus.Status == StatusData.BallStatus.LockedOnBall),
							IsHeartBeat = true
					};
						SendToGSP(Newtonsoft.Json.JsonConvert.SerializeObject(gSPShotData));
						Plugin.Log.LogInfo("Connected to GSPro OpenAPI");
					}
					catch (System.Exception ex)
					{
						Plugin.Log.LogInfo("Connecting to GSPro OpenAPI:  " + ex.Message);
						Plugin.Log.LogInfo("Connect Retry:  " + (numberOfTimes));
						System.Threading.Thread.Sleep(5000);
						if (numberOfTimes == 120)
						{
							_GSPSocket = null;
							Plugin.Log.LogInfo("Failed to connect to GSPro OpenAPI:  " + ex.Message);
							break;
						}
						else
						{
							numberOfTimes++;
							continue;
						}
					}
			}

			public static void GSPReadCallback(IAsyncResult ar)
			{
				try
				{
					_ = (GSPReceiveState)ar.AsyncState;
					int num = _GSPSocket.EndReceive(ar);
					if (num <= 0)
					{
						return;
					}
					_GSPResponse = Encoding.ASCII.GetString(_GSPReadBuffer, 0, num);
					if (_GSPResponse.Length >= 1)
					{
						Plugin.Log.LogInfo("GSPro OpenAPI Message Received:" + _GSPResponse);
					}
				}
				catch (System.Exception ex)
				{
					Plugin.Log.LogInfo("Error: " + ex);
				}
				try
				{
					_GSPSocket.BeginReceive(_GSPReadBuffer, 0, 1024, SocketFlags.None, new AsyncCallback(GSPReadCallback), null);
				}
				catch (System.Exception ex)
				{
					Plugin.Log.LogInfo("Error: " + ex);
				}
			}


			public BackgroundQueue _GSPSendQueue = new BackgroundQueue();

			public const int _OpenAPIPort = 921;

			public static Socket _GSPSocket;

			public static byte[] _GSPReadBuffer = new byte[1024];

			public static string _GSPResponse = "";

            private static bool connected;

            private static int numberOfTimes;
        }




    }
}
