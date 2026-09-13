using System.Collections.Generic;

public static class SoundKeys
{
    const string SFX_ROOT = "Common/Sound/SFX/";
    const string MUSIC_ROOT = "Common/Sound/Music/";

    public const string CHOP = "chop";
    public const string DELIVERY_FAIL = "delivery_fail";
    public const string DELIVERY_SUCCESS = "delivery_success";
    public const string FOOTSTEP = "footstep";
    public const string OBJECT_DROP = "object_drop";
    public const string OBJECT_PICKUP = "object_pickup";
    public const string PAN_SIZZLE_LOOP = "pan_sizzle_loop";
    public const string TRASH = "trash";
    public const string WARNING = "warning";

    public const string MUSIC_ADDRESS = MUSIC_ROOT + "Music";

    public static readonly SoundAddressData[] EffectSounds =
    {
        new SoundAddressData(
            CHOP,
            SFX_ROOT + "SFX_chop01",
            SFX_ROOT + "SFX_chop02",
            SFX_ROOT + "SFX_chop03"),
        new SoundAddressData(
            DELIVERY_FAIL,
            SFX_ROOT + "SFX_delivery_fail01",
            SFX_ROOT + "SFX_delivery_fail02"),
        new SoundAddressData(
            DELIVERY_SUCCESS,
            SFX_ROOT + "SFX_delivery_success01",
            SFX_ROOT + "SFX_delivery_success02"),
        new SoundAddressData(
            FOOTSTEP,
            SFX_ROOT + "SFX_footstep01_01",
            SFX_ROOT + "SFX_footstep01_02",
            SFX_ROOT + "SFX_footstep02_01",
            SFX_ROOT + "SFX_footstep02_02"),
        new SoundAddressData(
            OBJECT_DROP,
            SFX_ROOT + "SFX_object_drop01",
            SFX_ROOT + "SFX_object_drop02",
            SFX_ROOT + "SFX_object_drop03"),
        new SoundAddressData(
            OBJECT_PICKUP,
            SFX_ROOT + "SFX_object_pickup01",
            SFX_ROOT + "SFX_object_pickup02",
            SFX_ROOT + "SFX_object_pickup03"),
        new SoundAddressData(
            PAN_SIZZLE_LOOP,
            SFX_ROOT + "SFX_pan_sizzle_loop"),
        new SoundAddressData(
            TRASH,
            SFX_ROOT + "SFX_trash01",
            SFX_ROOT + "SFX_trash02"),
        new SoundAddressData(
            WARNING,
            SFX_ROOT + "SFX_warning01"),
    };

    public static void CollectEffectAddresses(List<string> results)
    {
        if (results == null)
        {
            return;
        }

        for (var i = 0; i < EffectSounds.Length; i++)
        {
            EffectSounds[i].CollectAddresses(results);
        }
    }
}
