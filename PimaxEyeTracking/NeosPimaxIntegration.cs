﻿using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using BaseX;
using FrooxEngine.CommonAvatar;
using System;

namespace NeosPimaxIntegration
{
	public class NeosPimaxIntegration : NeosMod
	{
		public override string Name => "PimaxEyeTracking";
		public override string Author => "dfgHiatus";
		public override string Version => "1.0.1";
		public override string Link => "https://github.com/dfgHiatus/NeosPimaxEyeTracking/";
		public override void OnEngineInit()
		{
			// Harmony.DEBUG = true;
			Harmony harmony = new Harmony("net.dfg.PimaxEyeTracking");
			harmony.PatchAll();
		}

		// Fix Issue 3440 (Can't mix and match the Eye Tracker with SRAnipal Lip)
		[HarmonyPatch(typeof(AvatarEyeDataSourceAssigner), "OnEquip")]
		public class AvatarEyeDataSourceAssignerPatch
        {
			public static void Postfix(AvatarEyeDataSourceAssigner __instance, AvatarObjectSlot slot)
			{
				if (__instance.TargetReference.Target == null)
					return;
				AvatarEyeTrackingInfo rawEyeData = null;
				slot.Slot.ActiveUserRoot.ForeachRegisteredComponent<AvatarEyeTrackingInfo>(val => {
					if (val.EyeDataSource.Target?.IsEyeTrackingActive ?? false)
						rawEyeData = val; } );
				__instance.TargetReference.Target.Target = rawEyeData?.EyeDataSource.Target;
			}
		}

		[HarmonyPatch(typeof(InputInterface), MethodType.Constructor)]
		[HarmonyPatch(new[] { typeof(Engine)})]
		public class InputInterfaceCtorPatch
		{
			public static void Postfix(InputInterface __instance)
			{
				try
				{
					PimaxEyeInputDevice pi = new PimaxEyeInputDevice();
					__instance.RegisterInputDriver(pi);
					Debug("Pimax Module Registered: " + pi.ToString());
				}
				catch (Exception e)
				{
					Warn("PimaxEyeTracking failed to initiallize.");
					Warn(e.ToString());
				}
			}
		}
	}

	class PimaxEyeInputDevice : IInputDriver
	{
		public Eyes eyes;
		public Pimax.EyeTracking.EyeTracker eyeTracker = new Pimax.EyeTracking.EyeTracker();
		public int UpdateOrder => 100;

		// Both of these will need tweaking depending on user eye swing
		public float Alpha = 2f;
		public float Beta = 2f;

		public void CollectDeviceInfos(BaseX.DataTreeList list)
        {
			DataTreeDictionary dataTreeDictionary = new DataTreeDictionary();
			dataTreeDictionary.Add("Name", "Pimax Eye Tracking");
			dataTreeDictionary.Add("Type", "Eye Tracking");
			dataTreeDictionary.Add("Model", "Droolon Pi1");
			list.Add(dataTreeDictionary);
		}

		public void RegisterInputs(InputInterface inputInterface)
		{
			if (!eyeTracker.Active)
            {
				eyeTracker.Start();
			}
			eyes = new Eyes(inputInterface, "Pimax Eye Tracking");
		}

		public void UpdateInputs(float deltaTime)
        {
			eyes.IsEyeTrackingActive     = eyeTracker.Active & Engine.Current.InputInterface.VR_Active;

			// Direction uses some cheeky plane to sphere projection
			eyes.LeftEye.Direction       = new float3(MathX.Tan(Alpha * eyeTracker.LeftEye.PupilCenter.X),
													  MathX.Tan(Beta  * (-1f) * eyeTracker.LeftEye.PupilCenter.Y), 
													  1f).Normalized;
			eyes.LeftEye.RawPosition = new float3((eyeTracker.LeftEye.PupilCenter.Y * (-1f)),
												  eyeTracker.LeftEye.PupilCenter.X,
												  0f);
			eyes.LeftEye.Openness        = eyeTracker.LeftEye.Openness;
			eyes.LeftEye.PupilDiameter   = eyeTracker.LeftEye.PupilMajorUnitDiameter;
			eyes.LeftEye.IsTracking      = eyeTracker.Active;
			eyes.LeftEye.IsDeviceActive  = eyeTracker.Active;
			eyes.LeftEye.Widen           = MathX.Clamp01(eyeTracker.LeftEye.PupilCenter.Y);
			eyes.LeftEye.Squeeze         = MathX.Remap(MathX.Clamp(eyeTracker.LeftEye.PupilCenter.Y, -1f, 0f), -1f, 0f, 0f, 1f);

			eyes.RightEye.Direction      = new float3(MathX.Tan(Alpha * eyeTracker.RightEye.PupilCenter.X),
													  MathX.Tan(Beta  * (-1f) * eyeTracker.RightEye.PupilCenter.Y), 
													  1f).Normalized;
			eyes.RightEye.RawPosition    = new float3((eyeTracker.RightEye.PupilCenter.Y * (-1f)), 
												      eyeTracker.RightEye.PupilCenter.X, 
													  0f);
			eyes.RightEye.Openness       = eyeTracker.RightEye.Openness;
			eyes.RightEye.PupilDiameter  = eyeTracker.RightEye.PupilMajorUnitDiameter;
			eyes.RightEye.IsTracking     = eyeTracker.Active;
			eyes.RightEye.IsDeviceActive = eyeTracker.Active;
			eyes.RightEye.Widen          = MathX.Clamp01(eyeTracker.RightEye.PupilCenter.Y);
			eyes.RightEye.Squeeze        = MathX.Remap(MathX.Clamp(eyeTracker.RightEye.PupilCenter.Y, -1f, 0f), -1f, 0f, 0f, 1f);

			eyes.CombinedEye.Direction      = new float3(MathX.Average(MathX.Tan(Alpha * eyeTracker.LeftEye.PupilCenter.Y), MathX.Tan(Alpha * eyeTracker.RightEye.PupilCenter.Y)),
														 MathX.Average(MathX.Tan(Alpha * eyeTracker.LeftEye.PupilCenter.X), MathX.Tan(Alpha * eyeTracker.RightEye.PupilCenter.X)), 
														 1f).Normalized;
			eyes.CombinedEye.RawPosition    = new float3(MathX.Average(eyeTracker.LeftEye.PupilCenter.X + eyeTracker.RightEye.PupilCenter.X),
													  MathX.Average(eyeTracker.LeftEye.PupilCenter.Y + eyeTracker.RightEye.PupilCenter.X),
													  0f);
			eyes.CombinedEye.Openness       = MathX.Average(eyeTracker.LeftEye.Openness, eyeTracker.RightEye.Openness);
			eyes.CombinedEye.PupilDiameter  = MathX.Average(eyeTracker.LeftEye.PupilMajorUnitDiameter + eyeTracker.RightEye.PupilMajorUnitDiameter);
			eyes.CombinedEye.IsTracking     = eyeTracker.Active;
			eyes.CombinedEye.IsDeviceActive = eyeTracker.Active;
			eyes.CombinedEye.Widen          = MathX.Average(MathX.Clamp01(eyeTracker.LeftEye.PupilCenter.X), MathX.Clamp01(eyeTracker.LeftEye.PupilCenter.Y));
			eyes.CombinedEye.Squeeze        = MathX.Average(MathX.Remap(MathX.Clamp(eyeTracker.LeftEye.PupilCenter.Y, -1f, 0f), -1f, 0f, 0f, 1f),
															MathX.Remap(MathX.Clamp(eyeTracker.RightEye.PupilCenter.Y, -1f, 0f), -1f, 0f, 0f, 1f));

			// Vive Pro Eye Style
			eyes.Timestamp = (double) eyeTracker.Timestamp * 0.001;
		}

	}
}