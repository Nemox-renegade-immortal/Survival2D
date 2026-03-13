using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace SlayInspiredPrototype;

public enum PlayerUpgradeKind
{
    Damage,
    FireRate,
    Reload,
    Mobility,
    Vitality,
    Grenades,
    Turrets
}

public sealed class Player : Entity
{
    public const int ActiveWeaponSlots = 4;
    public const int ReserveWeaponSlots = 2;
    public const int TotalWeaponSlots = ActiveWeaponSlots + ReserveWeaponSlots;

    private readonly List<WeaponState> _arsenal = new();
    private int _selectedWeaponIndex;

    public string Callsign { get; }
    public Color Accent { get; }

    public float MoveSpeed = 260f;
    public float AimAngle;
    public int Kills;
    public int Armor;
    public int Credits;
    public int Scrap;
    public int BarricadeKits;
    public bool IsReloading;
    public float ReloadTimer;
    public float FireTimer;
    public float DashTimer;
    public float DashCooldownTimer;
    public float GrenadeCooldownTimer;
    public float TurretCooldownTimer;
    public float OverdriveTimer;
    public Vector2 DashDirection;
    public Vector2 LastMoveInput;
    public float MoveBlend;
    public float PickupAnimation;
    public float ShootAnimation;
    public float UseAnimation;
    public float ReloadAnimation;
    public int ShotSequence;
    public int GrenadeSequence;
    public int TurretSequence;
    public int BarricadeSequence;
    public int OverdriveSequence;
    public int PickupSequence;

    public float DamageMultiplier { get; private set; } = 1f;
    public float FireRateMultiplier { get; private set; } = 1f;
    public float ReloadSpeedMultiplier { get; private set; } = 1f;
    public int GrenadeDamage { get; private set; } = 76;
    public float GrenadeRadius { get; private set; } = 120f;
    public float GrenadeCooldownDuration { get; private set; } = 5.6f;

    public int TurretCharges { get; private set; } = 1;
    public int MaxTurretCharges { get; private set; } = 1;
    public float TurretCooldownDuration { get; private set; } = 18f;
    public int TurretDamageBonus { get; private set; }
    public float TurretLifetimeBonus { get; private set; }

    public float Adrenaline { get; private set; }
    public float MaxAdrenaline { get; private set; } = 100f;
    public float OverdriveDuration { get; private set; } = 6f;
    public float OverdriveChargeMultiplier { get; private set; } = 1f;

    public int DamageUpgradeRank { get; private set; }
    public int FireRateUpgradeRank { get; private set; }
    public int ReloadUpgradeRank { get; private set; }
    public int MobilityUpgradeRank { get; private set; }
    public int VitalityUpgradeRank { get; private set; }
    public int GrenadeUpgradeRank { get; private set; }
    public int TurretUpgradeRank { get; private set; }

    public IReadOnlyList<WeaponState> Arsenal => _arsenal;
    public int SelectedWeaponIndex => _selectedWeaponIndex;
    public WeaponState CurrentWeapon => _arsenal[Math.Clamp(_selectedWeaponIndex, 0, _arsenal.Count - 1)];
    public Weapon Weapon => CurrentWeapon.Definition;
    public bool IsDashing => DashTimer > 0f;
    public bool IsOverdriveActive => OverdriveTimer > 0f;

    public Player(Vector2 position, string callsign, Color accent)
    {
        Callsign = callsign;
        Accent = accent;
        Position = position;
        Radius = 18f;
        MaxHealth = 100;
        Health = 100;
        Armor = 25;
        BuildDefaultLoadout();
    }

    public void BuildDefaultLoadout()
    {
        _arsenal.Clear();
        _arsenal.Add(new WeaponState(WeaponCatalog.Rifle, true));
        _arsenal.Add(new WeaponState(WeaponCatalog.Smg, false));
        _arsenal.Add(new WeaponState(WeaponCatalog.Shotgun, false));
        _arsenal.Add(new WeaponState(WeaponCatalog.Carbine, false));
        _arsenal.Add(new WeaponState(WeaponCatalog.Empty, false));
        _arsenal.Add(new WeaponState(WeaponCatalog.Empty, false));
        _selectedWeaponIndex = 0;
        IsReloading = false;
        ReloadTimer = 0f;
        FireTimer = 0f;
        DashTimer = 0f;
        DashCooldownTimer = 0f;
        GrenadeCooldownTimer = 0f;
        TurretCooldownTimer = 0f;
        OverdriveTimer = 0f;
        DamageMultiplier = 1f;
        FireRateMultiplier = 1f;
        ReloadSpeedMultiplier = 1f;
        GrenadeDamage = 76;
        GrenadeRadius = 120f;
        GrenadeCooldownDuration = 5.6f;
        MoveSpeed = 260f;
        TurretCharges = 0;
        MaxTurretCharges = 1;
        TurretCooldownDuration = 18f;
        TurretDamageBonus = 0;
        TurretLifetimeBonus = 0f;
        Adrenaline = 0f;
        MaxAdrenaline = 100f;
        OverdriveDuration = 6f;
        OverdriveChargeMultiplier = 1f;
        Credits = 0;
        Scrap = 0;
        BarricadeKits = 0;
        Kills = 0;
        DamageUpgradeRank = 0;
        FireRateUpgradeRank = 0;
        ReloadUpgradeRank = 0;
        MobilityUpgradeRank = 0;
        VitalityUpgradeRank = 0;
        GrenadeUpgradeRank = 0;
        TurretUpgradeRank = 0;
        LastMoveInput = Vector2.Zero;
        MoveBlend = 0f;
        PickupAnimation = 0f;
        ShootAnimation = 0f;
        UseAnimation = 0f;
        ReloadAnimation = 0f;
        ShotSequence = 0;
        GrenadeSequence = 0;
        TurretSequence = 0;
        BarricadeSequence = 0;
        OverdriveSequence = 0;
        PickupSequence = 0;
    }

    public void Tick(float dt)
    {
        FireTimer = MathF.Max(0f, FireTimer - dt);
        DashCooldownTimer = MathF.Max(0f, DashCooldownTimer - dt);
        DashTimer = MathF.Max(0f, DashTimer - dt);
        GrenadeCooldownTimer = MathF.Max(0f, GrenadeCooldownTimer - dt);
        TurretCooldownTimer = MathF.Max(0f, TurretCooldownTimer - dt);
        OverdriveTimer = MathF.Max(0f, OverdriveTimer - dt);
        PickupAnimation = MathF.Max(0f, PickupAnimation - dt);
        ShootAnimation = MathF.Max(0f, ShootAnimation - dt * 1.8f);
        UseAnimation = MathF.Max(0f, UseAnimation - dt * 1.55f);
        ReloadAnimation = MathF.Max(0f, ReloadAnimation - dt);
        MoveBlend = MathF.Max(0f, MoveBlend - dt * 1.35f);

        if (IsReloading)
        {
            ReloadTimer -= dt;
            ReloadAnimation = MathF.Max(ReloadAnimation, MathF.Min(0.55f, GetEffectiveReloadTime() * 0.4f));
            if (ReloadTimer <= 0f)
            {
                FinishReload();
            }
        }
    }

    public bool TrySelectWeapon(int slot)
    {
        if (!IsWeaponSlotUsable(slot))
        {
            return false;
        }

        _selectedWeaponIndex = slot;
        IsReloading = false;
        ReloadTimer = 0f;
        return true;
    }

    public bool IsWeaponSlotUsable(int slot)
    {
        if (slot < 0 || slot >= ActiveWeaponSlots || slot >= _arsenal.Count)
        {
            return false;
        }

        WeaponState state = _arsenal[slot];
        return state.Unlocked && !state.IsEmpty;
    }

    public bool SwapWeaponSlots(int from, int to)
    {
        if (from < 0 || from >= _arsenal.Count || to < 0 || to >= _arsenal.Count || from == to)
        {
            return false;
        }

        (_arsenal[from], _arsenal[to]) = (_arsenal[to], _arsenal[from]);

        if (_selectedWeaponIndex == from)
        {
            _selectedWeaponIndex = to;
        }
        else if (_selectedWeaponIndex == to)
        {
            _selectedWeaponIndex = from;
        }

        if (_selectedWeaponIndex >= ActiveWeaponSlots || !IsWeaponSlotUsable(_selectedWeaponIndex))
        {
            _selectedWeaponIndex = FindFirstUsableActiveWeaponSlot();
            IsReloading = false;
            ReloadTimer = 0f;
        }

        return true;
    }

    private int FindFirstUsableActiveWeaponSlot()
    {
        for (int i = 0; i < Math.Min(ActiveWeaponSlots, _arsenal.Count); i++)
        {
            if (IsWeaponSlotUsable(i))
            {
                return i;
            }
        }

        return 0;
    }

    public void CycleWeapon(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        int step = Math.Sign(direction);
        int next = Math.Clamp(_selectedWeaponIndex, 0, ActiveWeaponSlots - 1);
        for (int i = 0; i < ActiveWeaponSlots; i++)
        {
            next = (next + step + ActiveWeaponSlots) % ActiveWeaponSlots;
            if (IsWeaponSlotUsable(next))
            {
                TrySelectWeapon(next);
                return;
            }
        }
    }

    public WeaponState? GetWeaponState(string name)
    {
        return _arsenal.FirstOrDefault(w => string.Equals(w.Definition.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public bool UnlockWeapon(string name)
    {
        WeaponState? weaponState = GetWeaponState(name);
        if (weaponState is null || weaponState.Unlocked || weaponState.IsEmpty)
        {
            return false;
        }

        weaponState.Reset(true);

        if (!IsWeaponSlotUsable(_selectedWeaponIndex))
        {
            _selectedWeaponIndex = FindFirstUsableActiveWeaponSlot();
        }

        return true;
    }

    public bool TryPurchaseWeapon(string name)
    {
        WeaponState? weaponState = GetWeaponState(name);
        if (weaponState is null || weaponState.Unlocked || weaponState.IsEmpty)
        {
            return false;
        }

        int cost = Math.Max(0, weaponState.Definition.UnlockCost);
        if (Credits < cost)
        {
            return false;
        }

        Credits -= cost;
        return UnlockWeapon(name);
    }

    public void GiveAmmo(int amount)
    {
        CurrentWeapon.AmmoReserve += Math.Max(0, amount);
    }

    public void GiveAmmoForAllWeapons()
    {
        foreach (WeaponState weapon in _arsenal)
        {
            if (weapon.Unlocked && !weapon.IsEmpty)
            {
                weapon.AmmoReserve += weapon.Definition.AmmoPickupAmount;
                weapon.ClampAmmo();
            }
        }
    }

    public void GiveArmor(int amount)
    {
        Armor = Math.Min(100, Armor + amount);
    }

    public void GiveScrap(int amount)
    {
        Scrap += Math.Max(0, amount);
    }

    public bool TryCraftBarricadeKit(int scrapCost)
    {
        if (Scrap < scrapCost)
        {
            return false;
        }

        Scrap -= scrapCost;
        BarricadeKits++;
        return true;
    }

    public bool TryUseBarricadeKit()
    {
        if (BarricadeKits <= 0)
        {
            return false;
        }

        BarricadeKits--;
        return true;
    }

    public bool CanShoot() => !IsReloading && FireTimer <= 0f && CurrentWeapon.AmmoInClip > 0 && IsAlive;
    public bool CanReload() => !IsReloading && CurrentWeapon.AmmoInClip < GetCurrentClipSize() && CurrentWeapon.AmmoReserve > 0;


    public void SetMoveBlend(float amount, Vector2 moveInput)
    {
        MoveBlend = Math.Clamp(amount, 0f, 1f);
        LastMoveInput = moveInput;
    }

    public void TriggerPickupAnimation(float time = 0.32f)
    {
        PickupAnimation = MathF.Max(PickupAnimation, time);
        UseAnimation = MathF.Max(UseAnimation, 0.18f);
    }

    public void TriggerShootAnimation(float time = 0.14f)
    {
        ShootAnimation = MathF.Max(ShootAnimation, time);
    }

    public void TriggerUseAnimation(float time = 0.24f)
    {
        UseAnimation = MathF.Max(UseAnimation, time);
    }

    public void TriggerReloadAnimation(float time = 0.42f)
    {
        ReloadAnimation = MathF.Max(ReloadAnimation, time);
    }

    public void NotifyShot()
    {
        ShotSequence++;
        TriggerShootAnimation();
    }

    public void NotifyGrenade()
    {
        GrenadeSequence++;
        TriggerUseAnimation(0.3f);
    }

    public void NotifyTurret()
    {
        TurretSequence++;
        TriggerUseAnimation(0.34f);
    }

    public void NotifyBarricade()
    {
        BarricadeSequence++;
        TriggerUseAnimation(0.26f);
    }

    public void NotifyOverdrive()
    {
        OverdriveSequence++;
        TriggerUseAnimation(0.36f);
    }

    public void NotifyPickup()
    {
        PickupSequence++;
        TriggerPickupAnimation();
    }

    public void StartReload()
    {
        if (!CanReload())
        {
            return;
        }

        IsReloading = true;
        ReloadTimer = GetEffectiveReloadTime();
        TriggerReloadAnimation(MathF.Min(0.65f, ReloadTimer));
    }

    public void ConsumeShot()
    {
        CurrentWeapon.AmmoInClip = Math.Max(0, CurrentWeapon.AmmoInClip - 1);
        FireTimer = GetEffectiveFireInterval();

        if (CurrentWeapon.AmmoInClip <= 0 && CurrentWeapon.AmmoReserve > 0)
        {
            StartReload();
        }
    }

    public bool TryStartDash(Vector2 moveDirection)
    {
        if (DashCooldownTimer > 0f || moveDirection.LengthSquared() < 0.01f)
        {
            return false;
        }

        DashDirection = Vector2.Normalize(moveDirection);
        DashTimer = 0.14f;
        DashCooldownTimer = 1.2f;
        return true;
    }

    public bool CanThrowGrenade() => GrenadeCooldownTimer <= 0f && IsAlive;

    public void BeginGrenadeCooldown()
    {
        GrenadeCooldownTimer = GrenadeCooldownDuration;
    }

    public bool CanDeployTurret() => TurretCharges > 0 && TurretCooldownTimer <= 0f && IsAlive;

    public bool TryUseTurretCharge()
    {
        if (!CanDeployTurret())
        {
            return false;
        }

        TurretCharges--;
        TurretCooldownTimer = TurretCooldownDuration;
        return true;
    }

    public void RestoreTurretCharge(int amount)
    {
        TurretCharges = Math.Min(MaxTurretCharges, TurretCharges + amount);
    }

    public void ImproveTurrets()
    {
        MaxTurretCharges++;
        TurretCharges = MaxTurretCharges;
        TurretCooldownDuration = MathF.Max(8f, TurretCooldownDuration - 2.5f);
        TurretDamageBonus += 6;
        TurretLifetimeBonus += 2f;
    }

    public void AddAdrenaline(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        Adrenaline = MathF.Min(MaxAdrenaline, Adrenaline + amount * OverdriveChargeMultiplier);
    }

    public bool CanActivateOverdrive() => !IsOverdriveActive && Adrenaline >= MaxAdrenaline && IsAlive;

    public bool ActivateOverdrive()
    {
        if (!CanActivateOverdrive())
        {
            return false;
        }

        Adrenaline = 0f;
        OverdriveTimer = OverdriveDuration;
        TriggerUseAnimation(0.4f);
        return true;
    }

    public float GetCurrentMoveSpeedMultiplier()
    {
        float dashMult = IsDashing ? 3.8f : 1f;
        float overdriveMult = IsOverdriveActive ? 1.18f : 1f;
        return dashMult * overdriveMult;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || !IsAlive)
        {
            return;
        }

        if (IsDashing)
        {
            damage = Math.Max(1, damage / 3);
        }

        int absorbed = Math.Min(Armor, damage);
        Armor -= absorbed;
        damage -= absorbed;
        Health = Math.Max(0, Health - damage);
    }

    public void Heal(int amount)
    {
        Health = Math.Min(MaxHealth, Health + amount);
    }

    public void AddMaxHealth(int amount, bool healBonus)
    {
        MaxHealth += amount;
        if (healBonus)
        {
            Health = Math.Min(MaxHealth, Health + amount);
        }
    }

    public void ImproveDamage(float percent)
    {
        DamageMultiplier *= 1f + percent;
    }

    public void ImproveFireRate(float percent)
    {
        FireRateMultiplier *= 1f + percent;
    }

    public void ImproveReload(float percent)
    {
        ReloadSpeedMultiplier *= 1f + percent;
    }

    public void ImproveMoveSpeed(float bonus)
    {
        MoveSpeed += bonus;
    }

    public void ImproveGrenades(int damageBonus, float radiusBonus, float cooldownReduction)
    {
        GrenadeDamage += damageBonus;
        GrenadeRadius += radiusBonus;
        GrenadeCooldownDuration = MathF.Max(2.5f, GrenadeCooldownDuration - cooldownReduction);
        GrenadeCooldownTimer = MathF.Min(GrenadeCooldownTimer, GrenadeCooldownDuration);
    }

    public int GetShotDamage()
    {
        float overdriveMult = IsOverdriveActive ? 1.35f : 1f;
        return Math.Max(1, (int)MathF.Round(CurrentWeapon.EffectiveDamage * DamageMultiplier * overdriveMult));
    }

    public float GetEffectiveFireInterval()
    {
        float overdriveMult = IsOverdriveActive ? 1.25f : 1f;
        return CurrentWeapon.EffectiveFireInterval / MathF.Max(0.35f, FireRateMultiplier * overdriveMult);
    }

    public float GetEffectiveReloadTime()
    {
        float overdriveMult = IsOverdriveActive ? 1.12f : 1f;
        return CurrentWeapon.EffectiveReloadTime / MathF.Max(0.35f, ReloadSpeedMultiplier * overdriveMult);
    }

    public float GetCurrentSpreadRadians()
    {
        float overdriveMult = IsOverdriveActive ? 0.92f : 1f;
        return MathF.Max(0.005f, CurrentWeapon.EffectiveSpread * overdriveMult);
    }

    public float GetCurrentRecoil()
    {
        float overdriveMult = IsOverdriveActive ? 0.9f : 1f;
        return MathF.Max(0.2f, CurrentWeapon.EffectiveRecoil * overdriveMult);
    }

    public float GetZombieSlowSeconds()
    {
        float overdriveMult = IsOverdriveActive ? 1.08f : 1f;
        return CurrentWeapon.EffectiveZombieSlowSeconds * overdriveMult;
    }

    public float GetZombieSlowMultiplier()
    {
        float overdriveMult = IsOverdriveActive ? 0.96f : 1f;
        return Math.Clamp(CurrentWeapon.EffectiveZombieSlowMultiplier * overdriveMult, 0.35f, 1f);
    }

    public float GetZombieKnockback()
    {
        float overdriveMult = IsOverdriveActive ? 1.12f : 1f;
        return CurrentWeapon.EffectiveZombieKnockback * overdriveMult;
    }

    public int GetCurrentClipSize()
    {
        return CurrentWeapon.EffectiveClipSize;
    }

    public bool AwardWeaponExperience(string weaponName, int amount)
    {
        WeaponState? state = GetWeaponState(weaponName);
        return state is not null && state.AddExperience(amount);
    }

    public int GetUpgradeCost(PlayerUpgradeKind kind)
    {
        return kind switch
        {
            PlayerUpgradeKind.Damage => 45 + DamageUpgradeRank * 24,
            PlayerUpgradeKind.FireRate => 45 + FireRateUpgradeRank * 24,
            PlayerUpgradeKind.Reload => 40 + ReloadUpgradeRank * 20,
            PlayerUpgradeKind.Mobility => 36 + MobilityUpgradeRank * 18,
            PlayerUpgradeKind.Vitality => 55 + VitalityUpgradeRank * 28,
            PlayerUpgradeKind.Grenades => 62 + GrenadeUpgradeRank * 28,
            PlayerUpgradeKind.Turrets => 74 + TurretUpgradeRank * 34,
            _ => 999
        };
    }

    public bool TryBuyUpgrade(PlayerUpgradeKind kind)
    {
        int cost = GetUpgradeCost(kind);
        if (Credits < cost)
        {
            return false;
        }

        Credits -= cost;

        switch (kind)
        {
            case PlayerUpgradeKind.Damage:
                DamageUpgradeRank++;
                ImproveDamage(0.12f);
                return true;
            case PlayerUpgradeKind.FireRate:
                FireRateUpgradeRank++;
                ImproveFireRate(0.10f);
                return true;
            case PlayerUpgradeKind.Reload:
                ReloadUpgradeRank++;
                ImproveReload(0.10f);
                return true;
            case PlayerUpgradeKind.Mobility:
                MobilityUpgradeRank++;
                ImproveMoveSpeed(14f);
                return true;
            case PlayerUpgradeKind.Vitality:
                VitalityUpgradeRank++;
                AddMaxHealth(12, true);
                GiveArmor(18);
                return true;
            case PlayerUpgradeKind.Grenades:
                GrenadeUpgradeRank++;
                ImproveGrenades(16, 10f, 0.35f);
                return true;
            case PlayerUpgradeKind.Turrets:
                TurretUpgradeRank++;
                ImproveTurrets();
                return true;
            default:
                Credits += cost;
                return false;
        }
    }

    private void FinishReload()
    {
        IsReloading = false;
        int need = GetCurrentClipSize() - CurrentWeapon.AmmoInClip;
        int moved = Math.Min(need, CurrentWeapon.AmmoReserve);
        CurrentWeapon.AmmoReserve -= moved;
        CurrentWeapon.AmmoInClip += moved;
        CurrentWeapon.ClampAmmo();
    }
}
