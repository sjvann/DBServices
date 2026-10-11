-- 簡易庫存管理 (PostgreSQL / 資料庫: inventory)

CREATE TABLE IF NOT EXISTS categories (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT
);

CREATE TABLE IF NOT EXISTS products (
    id SERIAL PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    sku VARCHAR(50),
    category_id INTEGER REFERENCES categories(id),
    unit_price NUMERIC(12, 2) NOT NULL DEFAULT 0,
    stock_qty INTEGER NOT NULL DEFAULT 0,
    unit VARCHAR(20) DEFAULT '件'
);

CREATE TABLE IF NOT EXISTS suppliers (
    id SERIAL PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    contact VARCHAR(100),
    phone VARCHAR(50),
    address TEXT
);

CREATE TABLE IF NOT EXISTS customers (
    id SERIAL PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    contact VARCHAR(100),
    phone VARCHAR(50),
    address TEXT
);

CREATE TABLE IF NOT EXISTS purchase_orders (
    id SERIAL PRIMARY KEY,
    supplier_id INTEGER NOT NULL REFERENCES suppliers(id),
    order_date DATE NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT '已完成',
    total_amount NUMERIC(14, 2) NOT NULL DEFAULT 0,
    remark TEXT
);

CREATE TABLE IF NOT EXISTS purchase_order_items (
    id SERIAL PRIMARY KEY,
    purchase_order_id INTEGER NOT NULL REFERENCES purchase_orders(id),
    product_id INTEGER NOT NULL REFERENCES products(id),
    quantity INTEGER NOT NULL,
    unit_price NUMERIC(12, 2) NOT NULL
);

CREATE TABLE IF NOT EXISTS sales_orders (
    id SERIAL PRIMARY KEY,
    customer_id INTEGER NOT NULL REFERENCES customers(id),
    order_date DATE NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT '已完成',
    total_amount NUMERIC(14, 2) NOT NULL DEFAULT 0,
    remark TEXT
);

CREATE TABLE IF NOT EXISTS sales_order_items (
    id SERIAL PRIMARY KEY,
    sales_order_id INTEGER NOT NULL REFERENCES sales_orders(id),
    product_id INTEGER NOT NULL REFERENCES products(id),
    quantity INTEGER NOT NULL,
    unit_price NUMERIC(12, 2) NOT NULL
);

INSERT INTO categories (name, description) VALUES
('文具', '辦公文具用品'),
('電子', '3C 與周邊'),
('食品', '零食飲料')
;

INSERT INTO suppliers (name, contact, phone, address) VALUES
('文具批發商', '王經理', '02-12345678', '台北市中山區'),
('科技供應', '李小姐', '02-87654321', '新北市板橋區')
;

INSERT INTO customers (name, contact, phone, address) VALUES
('小明商店', '陳小明', '0912-111222', '台中市西區'),
('大同公司', '林採購', '04-5556666', '台中市北屯區')
;

INSERT INTO products (name, sku, category_id, unit_price, stock_qty, unit) VALUES
('A4 影印紙', 'PAP-A4-500', 1, 120.00, 200, '包'),
('原子筆 (藍)', 'PEN-BLU-01', 1, 15.00, 500, '支'),
('無線滑鼠', 'MOU-WL-01', 2, 399.00, 80, '個'),
('USB-C 傳輸線', 'CBL-USBC-1M', 2, 199.00, 150, '條'),
('礦泉水 600ml', 'DRK-WATER', 3, 20.00, 300, '瓶')
;

INSERT INTO purchase_orders (supplier_id, order_date, status, total_amount, remark) VALUES
(1, '2026-05-01', '已完成', 24000.00, '月初補貨'),
(2, '2026-05-10', '已完成', 31920.00, '滑鼠與傳輸線')
;

INSERT INTO purchase_order_items (purchase_order_id, product_id, quantity, unit_price) VALUES
(1, 1, 200, 120.00),
(2, 3, 80, 399.00)
;

INSERT INTO sales_orders (customer_id, order_date, status, total_amount, remark) VALUES
(1, '2026-05-12', '已完成', 1350.00, '門市零售'),
(2, '2026-05-15', '已完成', 5980.00, '公司採購')
;

INSERT INTO sales_order_items (sales_order_id, product_id, quantity, unit_price) VALUES
(1, 2, 50, 15.00),
(1, 5, 30, 20.00),
(2, 3, 10, 399.00),
(2, 4, 10, 199.00);
