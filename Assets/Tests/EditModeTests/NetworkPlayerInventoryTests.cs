using NUnit.Framework;

public class NetworkPlayerInventoryTests
{
    [TestCase(1, WeaponSlot.Primary)]
    [TestCase(2, WeaponSlot.Secondary)]
    [TestCase(3, WeaponSlot.Melee)]
    [TestCase(4, WeaponSlot.Utility)]
    public void NumerosValidosSeleccionanSuRanura(
        int number,
        WeaponSlot expected)
    {
        Assert.That(NetworkPlayerInventory.TryGetSlotForNumber(
            number,
            out WeaponSlot actual), Is.True);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(0)]
    [TestCase(5)]
    [TestCase(99)]
    public void NumerosSinRanuraSonRechazados(int number)
    {
        Assert.That(NetworkPlayerInventory.TryGetSlotForNumber(
            number,
            out WeaponSlot actual), Is.False);
        Assert.That(actual, Is.EqualTo(WeaponSlot.None));
    }
}
