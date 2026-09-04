namespace BlocksPlant.Api.Models;

public enum UserRole
{
    Owner = 0,
    Cashier = 1,
    Operator = 2
}

public enum FulfillmentType
{
    Collect = 0,
    Deliver = 1
}

public enum DeliveryStatus
{
    None = 0,
    Pending = 1,
    Delivered = 2,
    Cancelled = 3
}

public enum PaymentMethod
{
    Cash = 0,
    Transfer = 1,
    Other = 2
}

public enum MaterialTxnType
{
    Receive = 0,
    Adjust = 1,
    ProductionConsume = 2
}
