using System.Drawing;
using System.Numerics;

namespace SlayInspiredPrototype;

public sealed class RemotePlayerView
{
    public int PlayerId;
    public string Callsign = "NET";
    public Vector2 Position;
    public Vector2 TargetPosition;
    public float AimAngle;
    public int Health = 100;
    public int Armor;
    public bool IsAlive = true;
    public int WeaponLevel = 1;
    public string WeaponName = "Pistol";
    public int WeaponSlot;
    public int SelectedHotbarRawIndex;
    public Color Accent = Color.LightGray;
    public Vector2 MoveInput;
    public float MoveBlend;
    public bool IsReloading;
    public bool IsOverdriveActive;
    public float ShootAnimation;
    public float PickupAnimation;
    public float UseAnimation;
    public float ReloadAnimation;
    public int ShotSequence;
    public int GrenadeSequence;
    public int TurretSequence;
    public int BarricadeSequence;
    public int OverdriveSequence;
    public int PickupSequence;
}
