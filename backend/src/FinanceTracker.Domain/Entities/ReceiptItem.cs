namespace FinanceTracker.Domain.Entities;

public class ReceiptItem
{
    public Guid Id { get; private set; }
    public Guid ReceiptId { get; private set; }
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal Sum { get; private set; }

    private ReceiptItem() { Name = string.Empty; }

    public ReceiptItem(Guid receiptId, string name, decimal price, decimal quantity, decimal sum)
    {
        Id = Guid.CreateVersion7();
        ReceiptId = receiptId;
        Name = name;
        Price = price;
        Quantity = quantity;
        Sum = sum;
    }

    public void Update(string name, decimal price, decimal quantity, decimal sum)
    {
        Name = name;
        Price = price;
        Quantity = quantity;
        Sum = sum;
    }
}
