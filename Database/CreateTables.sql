
USE OnlineFoodDB;
GO

CREATE TABLE Users
(
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(150) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    Phone NVARCHAR(20) NULL,
    Address NVARCHAR(255) NULL,
    Role NVARCHAR(20) NOT NULL DEFAULT 'Customer',
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CK_Users_Role
        CHECK (Role IN ('Customer', 'Admin'))
);
GO

CREATE TABLE Categories
(
    CategoryId INT IDENTITY(1,1) PRIMARY KEY,
    CategoryName NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(255) NULL
);
GO

CREATE TABLE Foods
(
    FoodId INT IDENTITY(1,1) PRIMARY KEY,
    CategoryId INT NOT NULL,
    FoodName NVARCHAR(150) NOT NULL,
    Description NVARCHAR(500) NULL,
    Price DECIMAL(10,2) NOT NULL,
    ImageUrl NVARCHAR(255) NULL,
    IsAvailable BIT NOT NULL DEFAULT 1,

    CONSTRAINT CK_Foods_Price CHECK (Price > 0),
    CONSTRAINT FK_Foods_Categories
        FOREIGN KEY (CategoryId)
        REFERENCES Categories(CategoryId)
);
GO

CREATE TABLE Orders
(
    OrderId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    OrderDate DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    TotalAmount DECIMAL(10,2) NOT NULL,
    Status NVARCHAR(30) NOT NULL DEFAULT 'Pending',
    DeliveryAddress NVARCHAR(255) NOT NULL,

    CONSTRAINT CK_Orders_TotalAmount
        CHECK (TotalAmount >= 0),
    CONSTRAINT CK_Orders_Status
        CHECK (Status IN (
            'Pending', 'Confirmed', 'Preparing',
            'Out for Delivery', 'Delivered', 'Cancelled'
        )),
    CONSTRAINT FK_Orders_Users
        FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
);
GO

CREATE TABLE OrderDetails
(
    OrderDetailId INT IDENTITY(1,1) PRIMARY KEY,
    OrderId INT NOT NULL,
    FoodId INT NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(10,2) NOT NULL,
    SubTotal DECIMAL(10,2) NOT NULL,

    CONSTRAINT CK_OrderDetails_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_OrderDetails_UnitPrice CHECK (UnitPrice > 0),
    CONSTRAINT CK_OrderDetails_SubTotal CHECK (SubTotal > 0),
    CONSTRAINT FK_OrderDetails_Orders
        FOREIGN KEY (OrderId)
        REFERENCES Orders(OrderId),
    CONSTRAINT FK_OrderDetails_Foods
        FOREIGN KEY (FoodId)
        REFERENCES Foods(FoodId)
);
GO

