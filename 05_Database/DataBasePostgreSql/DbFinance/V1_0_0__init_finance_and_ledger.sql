-- =========================================================================
-- MIGRATION : V1_0_0__init_finance_and_ledger.sql
-- CIBLE     : finance_db (Bulle Haute Sécurité)
-- OPTI      : 100% BIGINT + ENUMs pour des index ultra-performants en RAM et BI
-- =========================================================================

-- Création des types énumérés exclusifs au domaine Finance
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'transaction_status') THEN
        CREATE TYPE transaction_status AS ENUM ('PENDING', 'COMPLETED', 'REFUNDED');
    END IF;
    
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'ledger_account_type') THEN
        CREATE TYPE ledger_account_type AS ENUM ('SYSTEM_REVENUE', 'SELLER_PAYOUT', 'BUYER_HOLD');
    END IF;

    -- AJOUT : Type énuméré pour les devises (Norme ISO 4217)
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'currency_type') THEN
        CREATE TYPE currency_type AS ENUM ('EUR', 'USD', 'GBP', 'CHF');
    END IF;
END $$;

-- 1. Table des transactions de trading (Financier)
CREATE TABLE IF NOT EXISTS financial_transactions (
    transaction_id BIGSERIAL PRIMARY KEY,
    buyer_id BIGINT NOT NULL,  -- Logique users.id (DbUsers)
    seller_id BIGINT NOT NULL, -- Logique users.id (DbUsers)
    item_id BIGINT NOT NULL,   -- Logique items.id (DbCollector)
    
    gross_amount NUMERIC(12, 2) NOT NULL,
    commission_amount NUMERIC(12, 2) NOT NULL,
    
    -- Utilisation de l'ENUM pour la devise (Zéro allocation de chaîne de caractères)
    currency currency_type DEFAULT 'EUR' NOT NULL,
    status transaction_status DEFAULT 'PENDING' NOT NULL,
    
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_transactions_status ON financial_transactions(status);
CREATE INDEX IF NOT EXISTS idx_transactions_buyer ON financial_transactions(buyer_id);
CREATE INDEX IF NOT EXISTS idx_transactions_seller ON financial_transactions(seller_id);
CREATE INDEX IF NOT EXISTS idx_transactions_item ON financial_transactions(item_id);

-- 2. Grand Livre Comptable Immuable (Ledger)
CREATE TABLE IF NOT EXISTS financial_ledger (
    ledger_id BIGSERIAL PRIMARY KEY,
    transaction_id BIGINT REFERENCES financial_transactions(transaction_id) ON DELETE RESTRICT,
    account_type ledger_account_type NOT NULL,
    debit_amount NUMERIC(12, 2) DEFAULT 0.00 NOT NULL,
    credit_amount NUMERIC(12, 2) DEFAULT 0.00 NOT NULL,
    description VARCHAR(255) NOT NULL,
    recorded_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_ledger_recorded_at ON financial_ledger(recorded_at);
CREATE INDEX IF NOT EXISTS idx_ledger_transaction ON financial_ledger(transaction_id);