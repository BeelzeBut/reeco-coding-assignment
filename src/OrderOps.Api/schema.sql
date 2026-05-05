-- OrderOps canonical schema. Applied by OrderOps.Importer before COPY.
-- DROP-and-CREATE: CSVs are the only data source, so re-imports always
-- pick up schema changes (no migration framework needed for a take-home).

DROP TABLE IF EXISTS jobs       CASCADE;
DROP TABLE IF EXISTS orders     CASCADE;
DROP TABLE IF EXISTS products   CASCADE;
DROP TABLE IF EXISTS suppliers  CASCADE;
DROP TABLE IF EXISTS categories CASCADE;

CREATE TABLE categories (
  id          varchar(16) PRIMARY KEY,
  name        text NOT NULL,
  parent_id   varchar(16) NULL
    REFERENCES categories(id) DEFERRABLE INITIALLY DEFERRED
);

CREATE TABLE suppliers (
  id          varchar(16) PRIMARY KEY,
  name        text NOT NULL,
  email       text,
  rating      numeric(3,2),
  country     varchar(8),
  active      boolean NOT NULL,
  created_at  timestamptz NOT NULL
);

CREATE TABLE products (
  id          varchar(16) PRIMARY KEY,
  name        text NOT NULL,
  category_id varchar(16) REFERENCES categories(id),
  sku         text,
  price       numeric(12,2) NOT NULL
);

CREATE TABLE orders (
  id           varchar(16) PRIMARY KEY,
  supplier_id  varchar(16) NOT NULL REFERENCES suppliers(id),
  product_id   varchar(16) NOT NULL REFERENCES products(id),
  quantity     integer NOT NULL,
  unit_price   numeric(12,2) NOT NULL,
  total_price  numeric(14,2) NOT NULL,
  status       varchar(16) NOT NULL,
  priority     varchar(16) NOT NULL,
  created_at   timestamptz NOT NULL,
  updated_at   timestamptz NOT NULL,
  warehouse    varchar(32) NULL,
  notes        text,
  version      integer NOT NULL DEFAULT 1
);

CREATE TABLE jobs (
  id          varchar(32) PRIMARY KEY,
  status      varchar(16) NOT NULL,
  total       integer NOT NULL,
  completed   integer NOT NULL DEFAULT 0,
  failed      integer NOT NULL DEFAULT 0,
  action      varchar(16) NOT NULL,
  created_at  timestamptz NOT NULL DEFAULT now(),
  finished_at timestamptz NULL
);

CREATE INDEX idx_orders_status      ON orders(status);
CREATE INDEX idx_orders_priority    ON orders(priority);
CREATE INDEX idx_orders_supplier    ON orders(supplier_id);
CREATE INDEX idx_orders_warehouse   ON orders(warehouse);
CREATE INDEX idx_orders_created_at  ON orders(created_at);
CREATE INDEX idx_orders_total_price ON orders(total_price);

CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE INDEX idx_products_name_trgm ON products USING gin (name gin_trgm_ops);
