/* =========================================================
   0) CREATE DATABASE
========================================================= */
IF DB_ID('SagaCommerce') IS NULL
BEGIN
    CREATE DATABASE SagaCommerce;
END
GO

USE SagaCommerce;
GO

/* =========================================================
   1) CREATE SCHEMAS
========================================================= */
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'cart') EXEC('CREATE SCHEMA cart');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'ord')  EXEC('CREATE SCHEMA ord');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'pay')  EXEC('CREATE SCHEMA pay');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'inv')  EXEC('CREATE SCHEMA inv');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'ship') EXEC('CREATE SCHEMA ship');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'msg')  EXEC('CREATE SCHEMA msg');
GO

/* =========================================================
   2) CART
========================================================= */
IF OBJECT_ID('cart.Carts', 'U') IS NULL
BEGIN
    CREATE TABLE cart.Carts (
        CartId           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        UserId           UNIQUEIDENTIFIER NOT NULL,
        Status           VARCHAR(20) NOT NULL
                         CHECK (Status IN ('ACTIVE','CHECKED_OUT','ABANDONED')),
        CreatedAt        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF OBJECT_ID('cart.CartItems', 'U') IS NULL
BEGIN
    CREATE TABLE cart.CartItems (
        CartItemId       UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        CartId           UNIQUEIDENTIFIER NOT NULL,
        ProductId        UNIQUEIDENTIFIER NOT NULL,
        Quantity         INT NOT NULL CHECK (Quantity > 0),
        UnitPrice        DECIMAL(18,2) NOT NULL CHECK (UnitPrice >= 0),
        CreatedAt        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CartItems_Carts FOREIGN KEY (CartId) REFERENCES cart.Carts(CartId)
    );
    CREATE INDEX IX_CartItems_CartId ON cart.CartItems(CartId);
END
GO

/* =========================================================
   3) ORDER
========================================================= */
IF OBJECT_ID('ord.Orders', 'U') IS NULL
BEGIN
    CREATE TABLE ord.Orders (
        OrderId          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        CartId           UNIQUEIDENTIFIER NULL,
        UserId           UNIQUEIDENTIFIER NOT NULL,
        TotalAmount      DECIMAL(18,2) NOT NULL CHECK (TotalAmount >= 0),
        Status           VARCHAR(20) NOT NULL
                         CHECK (Status IN ('PENDING','PAID','COMPLETED','CANCELLED')),
        CancelReason     NVARCHAR(200) NULL,
        CreatedAt        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_Orders_UserId ON ord.Orders(UserId);
    CREATE INDEX IX_Orders_Status ON ord.Orders(Status);
END
GO

IF OBJECT_ID('ord.OrderItems', 'U') IS NULL
BEGIN
    CREATE TABLE ord.OrderItems (
        OrderItemId      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        OrderId          UNIQUEIDENTIFIER NOT NULL,
        ProductId        UNIQUEIDENTIFIER NOT NULL,
        Quantity         INT NOT NULL CHECK (Quantity > 0),
        UnitPrice        DECIMAL(18,2) NOT NULL CHECK (UnitPrice >= 0),
        LineTotal AS (Quantity * UnitPrice) PERSISTED,
        CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES ord.Orders(OrderId)
    );
    CREATE INDEX IX_OrderItems_OrderId ON ord.OrderItems(OrderId);
    CREATE INDEX IX_OrderItems_ProductId ON ord.OrderItems(ProductId);
END
GO

/* =========================================================
   4) PAYMENT
========================================================= */
IF OBJECT_ID('pay.Payments', 'U') IS NULL
BEGIN
    CREATE TABLE pay.Payments (
        PaymentId        UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        OrderId          UNIQUEIDENTIFIER NOT NULL,
        Amount           DECIMAL(18,2) NOT NULL CHECK (Amount >= 0),
        Status           VARCHAR(20) NOT NULL
                         CHECK (Status IN ('SUCCESS','FAILED','REFUNDED')),
        Provider         NVARCHAR(50) NULL,
        TransactionRef   NVARCHAR(100) NULL,
        FailureReason    NVARCHAR(200) NULL,
        CreatedAt        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Payments_OrderId UNIQUE (OrderId)
    );
    CREATE INDEX IX_Payments_Status ON pay.Payments(Status);
END
GO

IF OBJECT_ID('pay.PaymentRefunds', 'U') IS NULL
BEGIN
    CREATE TABLE pay.PaymentRefunds (
        RefundId         UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        PaymentId        UNIQUEIDENTIFIER NOT NULL,
        OrderId          UNIQUEIDENTIFIER NOT NULL,
        Amount           DECIMAL(18,2) NOT NULL CHECK (Amount >= 0),
        Reason           NVARCHAR(200) NULL,
        CreatedAt        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_PaymentRefunds_Payments FOREIGN KEY (PaymentId) REFERENCES pay.Payments(PaymentId)
    );
    CREATE INDEX IX_PaymentRefunds_OrderId ON pay.PaymentRefunds(OrderId);
END
GO

/* =========================================================
   5) INVENTORY
========================================================= */
IF OBJECT_ID('inv.Products', 'U') IS NULL
BEGIN
    CREATE TABLE inv.Products (
        ProductId        UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Sku              NVARCHAR(50) NOT NULL UNIQUE,
        ProductName      NVARCHAR(200) NOT NULL
    );
END
GO

IF OBJECT_ID('inv.InventoryStocks', 'U') IS NULL
BEGIN
    CREATE TABLE inv.InventoryStocks (
        ProductId        UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        OnHandQty        INT NOT NULL CHECK (OnHandQty >= 0),
        ReservedQty      INT NOT NULL DEFAULT 0 CHECK (ReservedQty >= 0),
        UpdatedAt        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_InventoryStocks_Products FOREIGN KEY (ProductId) REFERENCES inv.Products(ProductId)
    );
END
GO

IF OBJECT_ID('inv.InventoryReservations', 'U') IS NULL
BEGIN
    CREATE TABLE inv.InventoryReservations (
        ReservationId    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        OrderId          UNIQUEIDENTIFIER NOT NULL,
        ProductId        UNIQUEIDENTIFIER NOT NULL,
        Quantity         INT NOT NULL CHECK (Quantity > 0),
        Status           VARCHAR(20) NOT NULL
                         CHECK (Status IN ('RESERVED','FAILED','RELEASED')),
        Reason           NVARCHAR(200) NULL,
        CreatedAt        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_InventoryReservations_OrderId ON inv.InventoryReservations(OrderId);
    CREATE INDEX IX_InventoryReservations_ProductId ON inv.InventoryReservations(ProductId);
END
GO

/* =========================================================
   6) SHIPPING (optional)
========================================================= */
IF OBJECT_ID('ship.Shipments', 'U') IS NULL
BEGIN
    CREATE TABLE ship.Shipments (
        ShipmentId       UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        OrderId          UNIQUEIDENTIFIER NOT NULL UNIQUE,
        Status           VARCHAR(20) NOT NULL
                         CHECK (Status IN ('CREATED','IN_TRANSIT','DELIVERED','FAILED')),
        Carrier          NVARCHAR(100) NULL,
        TrackingNumber   NVARCHAR(100) NULL,
        CreatedAt        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt        DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

/* =========================================================
   7) EVENT TABLES (Outbox / Inbox for reliable choreography)
========================================================= */
IF OBJECT_ID('msg.OutboxEvents', 'U') IS NULL
BEGIN
    CREATE TABLE msg.OutboxEvents (
        EventId          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        AggregateType    NVARCHAR(50) NOT NULL,     -- Order, Payment, Inventory...
        AggregateId      UNIQUEIDENTIFIER NOT NULL, -- orderId, paymentId...
        EventType        NVARCHAR(100) NOT NULL,    -- OrderCreated, PaymentSucceeded...
        PayloadJson      NVARCHAR(MAX) NOT NULL,
        OccurredAt       DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        PublishedAt      DATETIME2(3) NULL,
        PublishStatus    VARCHAR(20) NOT NULL DEFAULT 'PENDING'
                         CHECK (PublishStatus IN ('PENDING','PUBLISHED','FAILED')),
        RetryCount       INT NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_Outbox_PublishStatus_OccurredAt ON msg.OutboxEvents(PublishStatus, OccurredAt);
END
GO

IF OBJECT_ID('msg.InboxEvents', 'U') IS NULL
BEGIN
    CREATE TABLE msg.InboxEvents (
        EventId          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        ConsumerName     NVARCHAR(100) NOT NULL,    -- OrderService, PaymentService...
        EventType        NVARCHAR(100) NOT NULL,
        PayloadJson      NVARCHAR(MAX) NOT NULL,
        ReceivedAt       DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        ProcessedAt      DATETIME2(3) NULL,
        ProcessStatus    VARCHAR(20) NOT NULL DEFAULT 'RECEIVED'
                         CHECK (ProcessStatus IN ('RECEIVED','PROCESSED','FAILED')),
        ErrorMessage     NVARCHAR(500) NULL
    );
    CREATE INDEX IX_Inbox_Consumer_ProcessStatus ON msg.InboxEvents(ConsumerName, ProcessStatus);
END
GO