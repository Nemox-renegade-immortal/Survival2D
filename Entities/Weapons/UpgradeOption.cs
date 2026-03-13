namespace SlayInspiredPrototype;

public sealed class UpgradeOption
{
    public string KeyText { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int BaseCost { get; init; }
    public int PurchaseCount { get; private set; }

    public int CurrentCost => BaseCost + PurchaseCount * System.Math.Max(12, BaseCost / 2);

    public void MarkPurchased()
    {
        PurchaseCount++;
    }
}
