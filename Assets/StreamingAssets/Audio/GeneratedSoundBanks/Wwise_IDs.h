/////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Audiokinetic Wwise generated include file. Do not edit.
//
/////////////////////////////////////////////////////////////////////////////////////////////////////

#ifndef __WWISE_IDS_H__
#define __WWISE_IDS_H__

#include <AK/SoundEngine/Common/AkTypes.h>

namespace AK
{
    namespace EVENTS
    {
        static const AkUniqueID PLAY_AMBIENCE_CAVE = 204269822U;
        static const AkUniqueID PLAY_AMBIENCE_OUTDOORS = 790324878U;
        static const AkUniqueID PLAY_AMBIENCE_RAIN = 2378670719U;
        static const AkUniqueID PLAY_AMBIENCE_WIND = 1913801107U;
        static const AkUniqueID PLAY_AUDIOINPUT = 4015255420U;
        static const AkUniqueID PLAY_FOOTSTEPS = 3854155799U;
        static const AkUniqueID PLAY_SCULPTING = 1233863437U;
        static const AkUniqueID PLAY_SNAP = 3858275350U;
        static const AkUniqueID PLAY_SPLASH = 3948925255U;
        static const AkUniqueID STOP_AUDIOINPUT = 985077522U;
        static const AkUniqueID STOP_SCULPTING = 780683743U;
    } // namespace EVENTS

    namespace STATES
    {
        namespace SCULPTMATERIAL
        {
            static const AkUniqueID GROUP = 2082818283U;

            namespace STATE
            {
                static const AkUniqueID DIRT = 2195636714U;
                static const AkUniqueID NONE = 748895195U;
                static const AkUniqueID STONE = 1216965916U;
            } // namespace STATE
        } // namespace SCULPTMATERIAL

    } // namespace STATES

    namespace SWITCHES
    {
        namespace GROUNDMATERIAL
        {
            static const AkUniqueID GROUP = 3072116243U;

            namespace SWITCH
            {
                static const AkUniqueID DIRT = 2195636714U;
                static const AkUniqueID GRASS = 4248645337U;
                static const AkUniqueID SAND = 803837735U;
                static const AkUniqueID SNOW = 787898836U;
                static const AkUniqueID STONE = 1216965916U;
            } // namespace SWITCH
        } // namespace GROUNDMATERIAL

    } // namespace SWITCHES

    namespace GAME_PARAMETERS
    {
        static const AkUniqueID BUBBLEABSORPTION = 3725968720U;
        static const AkUniqueID BUBBLEAVERAGE = 596440120U;
        static const AkUniqueID BUBBLEHEIGHT = 744577782U;
        static const AkUniqueID BUBBLEWIDTH = 2967072453U;
        static const AkUniqueID PLAYERSUBMERSION = 363247939U;
        static const AkUniqueID RAIN = 2043403999U;
        static const AkUniqueID TIMEOFDAY = 3729505769U;
        static const AkUniqueID WINDSTRENGTH = 3158975812U;
    } // namespace GAME_PARAMETERS

    namespace BANKS
    {
        static const AkUniqueID INIT = 1355168291U;
        static const AkUniqueID MAIN = 3161908922U;
    } // namespace BANKS

    namespace BUSSES
    {
        static const AkUniqueID AMBISONICS = 2903397179U;
        static const AkUniqueID MASTER_AUDIO_BUS = 3803692087U;
    } // namespace BUSSES

    namespace AUX_BUSSES
    {
        static const AkUniqueID REVERB = 348963605U;
    } // namespace AUX_BUSSES

    namespace AUDIO_DEVICES
    {
        static const AkUniqueID NO_OUTPUT = 2317455096U;
        static const AkUniqueID SYSTEM = 3859886410U;
    } // namespace AUDIO_DEVICES

}// namespace AK

#endif // __WWISE_IDS_H__
