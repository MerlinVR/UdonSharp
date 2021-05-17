
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonSharp.Examples.Utilities
{
    /// <summary>
    /// Sets the default voice and avatar audio settings for players when they enter the world
    /// See https://docs.vrchat.com/docs/player-audio for more detailed documentation 
    /// </summary>
    [AddComponentMenu("Udon Sharp/Utilities/World Audio Settings")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class WorldAudioSettings : UdonSharpBehaviour 
    {
        [Header("Player voice")]
        [Tooltip("Adjusts the player volume")]
        [Range(0f, 24f)]
        public float voiceGain = 15f;

        [Tooltip("The end of the range for hearing a user's voice")]
        public float voiceFar = 25f;

        [Tooltip("The near radius in meters where player audio starts to fall off, it is recommended to keep this at 0")]
        public float voiceNear = 0f;

        [Tooltip("The volumetric radius for the player voice, this should be left at 0 unless you know what you're doing")]
        public float voiceVolumetricRadius = 0f;

        [Tooltip("Disables the low-pass filter when players are far away")]
        public bool voiceDisableLowpass = false;

        [Header("Avatar audio")]
        [Tooltip("The maximum gain allowed on avatar audio sources")]
        [Range(0f, 10f)]
        public float avatarMaxAudioGain = 10f;

        [Tooltip("The maximum end of avatar audio range, a value of 0 will effectively mute avatar audio")]
        public float avatarMaxFarRadius = 40f;

        // I don't think the docs are accurate for this one, they say it's for the maximum radius where you can start to hear an audio source
        [Tooltip("The maximum for the radius where avatar audio starts to fall off")]
        public float avatarMaxNearRadius = 40f;

        [Tooltip("The max volumetric radius for avatar audio sources")]
        public float avatarMaxVolumetricRadius = 40f;

        [Tooltip("Forces avatars to have spatialized audio")]
        public bool avatarForceSpacialization = false;

        [Tooltip("Disables custom curves on avatar audio sources")]
        public bool avatarDisableCustomCurve = false;

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (!player.isLocal)
            {
                // Player voice
                player.SetVoiceGain(voiceGain);
                player.SetVoiceDistanceFar(voiceFar);
                player.SetVoiceDistanceNear(voiceNear);
                player.SetVoiceVolumetricRadius(voiceVolumetricRadius);
                player.SetVoiceLowpass(!voiceDisableLowpass);

                // Avatar audio
                player.SetAvatarAudioGain(avatarMaxAudioGain);
                player.SetAvatarAudioFarRadius(avatarMaxFarRadius);
                player.SetAvatarAudioNearRadius(avatarMaxNearRadius);
                player.SetAvatarAudioVolumetricRadius(avatarMaxVolumetricRadius);
                player.SetAvatarAudioForceSpatial(avatarForceSpacialization);
                player.SetAvatarAudioCustomCurve(!avatarDisableCustomCurve);
            }
        }
    }
}
