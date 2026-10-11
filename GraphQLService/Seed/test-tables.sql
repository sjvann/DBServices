-- 測試用資料表 (SQLite)

CREATE TABLE IF NOT EXISTS Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Email TEXT
);

CREATE TABLE IF NOT EXISTS Products (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Price REAL NOT NULL DEFAULT 0,
    Qty INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS Orders (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductName TEXT NOT NULL,
    Quantity INTEGER NOT NULL DEFAULT 1,
    OrderDate TEXT NOT NULL
);

INSERT INTO Users (Name, Email) VALUES
('王小明', 'ming@example.com'),
('李小華', 'hua@example.com'),
('陳大同', 'datong@example.com');

INSERT INTO Products (Name, Price, Qty) VALUES
('測試商品 A', 100.00, 50),
('測試商品 B', 250.50, 30),
('測試商品 C', 89.00, 120);

INSERT INTO Orders (ProductName, Quantity, OrderDate) VALUES
('測試商品 A', 2, '2026-05-18'),
('測試商品 B', 1, '2026-05-18'),
('測試商品 C', 5, '2026-05-17');
