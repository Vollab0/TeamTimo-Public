using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Inventory/Weapon")]
public class WeaponItem : ItemData // Hérite de ItemData !
{
    public int damage;
}